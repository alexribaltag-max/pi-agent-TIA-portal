using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace TiaLocalBridge.Services
{
    [DataContract]
    internal sealed class SourceLocation
    {
        [DataMember] public string section { get; set; }
        [DataMember] public string xmlUid { get; set; }
        [DataMember] public int viewLine { get; set; }
        internal int offset;
        internal bool safeBoundary;
    }

    internal sealed class StructuredTextView
    {
        public string Text;
        public int UnitCount;
        public List<SourceLocation> SourceMap;
        public List<int> SafeBoundaryLines;
    }

    // Conservative subset of Siemens StructuredText/v4 and Interface/v5; never strip XML tags
    // or flatten nested accesses. Unknown executable/declaration nodes fail this adapter.
    internal static class StructuredTextReader
    {
        public static StructuredTextView Read(string path, string culture)
        {
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new FormatException("XML reader limit: 8 MiB.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 8 * 1024 * 1024, MaxCharactersFromEntities = 0 };
            XDocument document;
            using (var reader = XmlReader.Create(path, settings)) document = XDocument.Load(reader);
            if (document.Root == null || document.Root.Name != "Document") throw new FormatException("Unexpected XML root.");
            var blocks = document.Root.Elements().Where(e => e.Name.LocalName.StartsWith("SW.Blocks.", StringComparison.Ordinal)).ToList();
            if (blocks.Count != 1) throw new FormatException("Expected exactly one PLC block.");
            var block = blocks[0];
            var attrs = block.Element("AttributeList") ?? throw new FormatException("Block attributes missing.");
            if ((string)attrs.Element("ProgrammingLanguage") != "SCL") throw new FormatException("This adapter only supports SCL XML.");
            // Sections is namespace qualified in Siemens exports.
            var interfaces = attrs.Element("Interface")?.Elements().SingleOrDefault(e => e.Name.LocalName == "Sections" && (e.Name.NamespaceName == "http://www.siemens.com/automation/Openness/SW/Interface/v5" || e.Name.NamespaceName == "http://www.siemens.com/automation/Openness/SW/Interface/v6" || e.Name.NamespaceName == "http://www.siemens.com/automation/Openness/SW/Interface/v7"));
            if (interfaces == null) throw new FormatException("Unsupported or missing interface schema.");
            var InterfaceNs = interfaces.Name.NamespaceName;
            var builder = new StringBuilder("// Reconstructed analysis view; NOT importable source\n");
            var locations = new List<SourceLocation>();
            foreach (var section in interfaces.Elements())
            {
                if (section.Name != XName.Get("Section", InterfaceNs)) throw new FormatException("Unknown interface section node: " + section.Name);
                var sectionName = (string)section.Attribute("Name");
                if (string.IsNullOrWhiteSpace(sectionName)) throw new FormatException("Interface section lacks name.");
                locations.Add(new SourceLocation { section = "interface:" + sectionName, offset = builder.Length, safeBoundary = true });
                builder.Append("INTERFACE ").Append(sectionName);
                foreach (var attribute in section.Attributes().Where(a => a.Name.LocalName != "Name"))
                    builder.Append(" [").Append(attribute.Name.LocalName).Append('=').Append(attribute.Value).Append(']');
                builder.Append('\n');
                foreach (var member in section.Elements())
                {
                    if (member.Name != XName.Get("Member", InterfaceNs)) throw new FormatException("Unknown interface member node: " + member.Name);
                    var name = (string)member.Attribute("Name");
                    var type = (string)member.Attribute("Datatype");
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type)) throw new FormatException("Incomplete member declaration.");
                    locations.Add(new SourceLocation { section = "interface:" + sectionName + "/" + name, offset = builder.Length });
                    builder.Append("  ").Append(name).Append(" : ").Append(type);
                    foreach (var attribute in member.Attributes().Where(a => a.Name.LocalName != "Name" && a.Name.LocalName != "Datatype"))
                        builder.Append(" [").Append(attribute.Name.LocalName).Append('=').Append(attribute.Value).Append(']');
                    foreach (var child in member.Elements())
                    {
                        if (child.Name == XName.Get("Comment", InterfaceNs))
                        {
                            var comments = child.Elements(XName.Get("MultiLanguageText", InterfaceNs)).ToList();
                            if (child.Attributes().Any() || comments.Count != child.Elements().Count() ||
                                comments.Any(c => c.HasElements || c.Attributes().Any(a => a.Name.LocalName != "Lang") || (string)c.Attribute("Lang") == null))
                                throw new FormatException("Unsupported interface comment.");
                            var selected = comments.FirstOrDefault(c => (string)c.Attribute("Lang") == culture);
                            if (comments.Count != 0 && selected == null) throw new FormatException("Interface comment culture unavailable.");
                            if (selected != null) builder.Append(" // ").Append(selected.Value.Replace("\n", "\\n"));
                        }
                        else if (child.Name == XName.Get("StartValue", InterfaceNs))
                        {
                            if (child.HasElements || child.Attributes().Any()) throw new FormatException("Unsupported start value.");
                            builder.Append(" := ").Append(child.Value);
                        }
                        else throw new FormatException("Unknown member construct: " + child.Name.LocalName);
                    }
                    builder.Append('\n');
                }
            }
            var units = block.Descendants().Where(e => e.Name.LocalName == "SW.Blocks.CompileUnit").ToList();
            if (units.Count == 0) throw new FormatException("No compile units in SCL block.");
            foreach (var unit in units)
            {
                if (unit.Attributes().Any(a => a.Name.LocalName != "ID" && a.Name.LocalName != "CompositionName"))
                    throw new FormatException("Unsupported compile unit attribute.");
                var networks = unit.Descendants().Where(e => e.Name.LocalName == "NetworkSource").ToList();
                if (networks.Count != 1 || networks[0].Elements().Count() != 1) throw new FormatException("Unexpected SCL compile unit network.");
                var structured = networks[0].Elements().Single();
                if (structured.Name.LocalName != "StructuredText" || !(structured.Name.NamespaceName == "http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v4" || structured.Name.NamespaceName == "http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v5" || structured.Name.NamespaceName == "http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v6") ||
                    networks[0].Attributes().Any() || structured.Attributes().Any(a => !a.IsNamespaceDeclaration))
                    throw new FormatException("Unsupported StructuredText schema or attributes.");
                var NetworkNs = structured.Name.NamespaceName;
                var unitId = (string)unit.Attribute("ID") ?? "unknown";
                locations.Add(new SourceLocation { section = "unit:" + unitId, offset = builder.Length + 1, safeBoundary = true });
                builder.Append("\n// CompileUnit ID=").Append(unitId).Append('\n');
                foreach (var node in structured.Elements()) Render(node, builder, 0, locations, unitId, NetworkNs);
                builder.Append('\n');
            }
            var line = 1;
            var position = 0;
            foreach (var entry in locations)
            {
                while (position < entry.offset && position < builder.Length)
                    if (builder[position++] == '\n') line++;
                entry.viewLine = line;
            }
            var boundaries = locations.Where(e => e.safeBoundary)
                .Select(e => e.viewLine).Distinct().OrderBy(n => n).ToList();
            return new StructuredTextView { Text = builder.ToString(), UnitCount = units.Count, SourceMap = locations, SafeBoundaryLines = boundaries };
        }

        private static void Render(XElement node, StringBuilder output, int depth, List<SourceLocation> locations, string unitId, string NetworkNs)
        {
            if (depth > 40 || node.Name.NamespaceName != NetworkNs) throw new FormatException("Unknown or overly deep SCL node: " + node.Name);
            var uid = (string)node.Attribute("UId");
            Record(locations, unitId, uid, output.Length);
            var name = node.Name.LocalName;
            foreach (var attribute in node.Attributes())
            {
                var allowed = attribute.Name.LocalName == "UId" ||
                    (attribute.Name.LocalName == "Num" && (name == "Blank" || name == "NewLine")) ||
                    (attribute.Name.LocalName == "Text" && name == "Token") ||
                    (attribute.Name.LocalName == "Scope" && name == "Access") ||
                    (attribute.Name.LocalName == "Name" && (name == "Component" || name == "Instruction" || name == "Parameter"));
                if (!allowed) throw new FormatException("Unsupported SCL attribute: " + name + "." + attribute.Name.LocalName);
            }
            switch (name)
            {
                case "Blank": output.Append(' ', Count(node)); break;
                case "NewLine": output.Append('\n', Count(node)); break;
                case "Token": output.Append(Required(node, "Text")); break;
                case "Text":
                    if (node.HasElements) throw new FormatException("Nested text node.");
                    output.Append(node.Value); break;
                case "LineComment":
                    output.Append("//");
                    RenderChildren(node, output, depth, locations, unitId, NetworkNs);
                    break;
                case "Access":
                    var scope = Required(node, "Scope");
                    if (scope == "LocalVariable" || scope == "GlobalVariable")
                    {
                        var children = node.Elements().ToList();
                        if (children.Count != 1 || children[0].Name.LocalName != "Symbol") throw new FormatException("Unsupported symbol access.");
                        if (scope == "LocalVariable") output.Append('#');
                        if (scope == "GlobalVariable")
                        {
                            var symbol = children[0];
                            var first = symbol.Elements().FirstOrDefault();
                            if (first == null || first.Name.LocalName != "Component") throw new FormatException("Global access lacks component.");
                            if (symbol.Attributes().Any(a => a.Name.LocalName != "UId") ||
                                first.Attributes().Any(a => a.Name.LocalName != "Name" && a.Name.LocalName != "UId"))
                                throw new FormatException("Unsupported global access attribute.");
                            Record(locations, unitId, (string)symbol.Attribute("UId"), output.Length);
                            Record(locations, unitId, (string)first.Attribute("UId"), output.Length);
                            output.Append('"').Append(Required(first, "Name").Replace("\"", "\"\"")).Append('"');
                            RenderChildren(first, output, depth + 2, locations, unitId, NetworkNs);
                            foreach (var next in first.ElementsAfterSelf()) Render(next, output, depth + 2, locations, unitId, NetworkNs);
                        }
                        else Render(children[0], output, depth + 1, locations, unitId, NetworkNs);
                    }
                    else if (scope == "Call" || scope == "LiteralConstant" || scope == "TypedConstant")
                    {
                        var expected = scope == "Call" ? "Instruction" : "Constant";
                        var children = node.Elements().ToList();
                        if (children.Count != 1 || children[0].Name.LocalName != expected) throw new FormatException("Unsupported " + scope + " access.");
                        Render(children[0], output, depth + 1, locations, unitId, NetworkNs);
                    }
                    else throw new FormatException("Unsupported SCL access scope: " + scope);
                    break;
                case "Component":
                    output.Append(SimpleIdentifier(node, "Name"));
                    RenderChildren(node, output, depth, locations, unitId, NetworkNs);
                    break;
                case "Instruction": output.Append(SimpleIdentifier(node, "Name")); RenderChildren(node, output, depth, locations, unitId, NetworkNs); break;
                case "Parameter": output.Append(SimpleIdentifier(node, "Name")); RenderChildren(node, output, depth, locations, unitId, NetworkNs); break;
                case "Symbol": case "Constant": RenderChildren(node, output, depth, locations, unitId, NetworkNs); break;
                case "ConstantType":
                    if (node.HasElements) throw new FormatException("Nested constant type.");
                    output.Append(node.Value).Append('#'); break;
                case "ConstantValue":
                    if (node.HasElements) throw new FormatException("Nested constant value.");
                    output.Append(node.Value); break;
                case "StringConstant":
                    if (node.HasElements) throw new FormatException("Nested string constant.");
                    output.Append('\'').Append(node.Value.Replace("'", "''")).Append('\''); break;
                default: throw new FormatException("Unsupported SCL semantic node: " + name);
            }
        }
        private static void Record(List<SourceLocation> locations, string unitId, string uid, int offset)
        {
            if (uid == null) return;
            if (locations.Count >= 100000) throw new FormatException("SCL source map exceeds node limit.");
            locations.Add(new SourceLocation { section = "unit:" + unitId, xmlUid = uid, offset = offset });
        }
        private static void RenderChildren(XElement node, StringBuilder output, int depth, List<SourceLocation> locations, string unitId, string NetworkNs)
        {
            foreach (var child in node.Elements()) Render(child, output, depth + 1, locations, unitId, NetworkNs);
        }
        private static int Count(XElement node)
        {
            var raw = (string)node.Attribute("Num");
            if (raw == null) return 1;
            if (!int.TryParse(raw, out var count) || count < 1 || count > 100) throw new FormatException("Invalid whitespace multiplicity.");
            return count;
        }
        private static string SimpleIdentifier(XElement node, string attribute)
        {
            var value = Required(node, attribute);
            if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_') ||
                value.Skip(1).Any(c => !(char.IsLetterOrDigit(c) || c == '_')) ||
                new[] { "IF", "THEN", "ELSE", "ELSIF", "END_IF", "FOR", "TO", "DO", "END_FOR", "CASE", "END_CASE" }
                    .Contains(value, StringComparer.OrdinalIgnoreCase))
                throw new FormatException("Quoted or ambiguous identifier requires a verified adapter: " + node.Name.LocalName);
            return value;
        }
        private static string Required(XElement node, string attribute)
        {
            var value = (string)node.Attribute(attribute);
            if (value == null) throw new FormatException("Missing " + attribute + " on " + node.Name.LocalName);
            return value;
        }
    }
}
