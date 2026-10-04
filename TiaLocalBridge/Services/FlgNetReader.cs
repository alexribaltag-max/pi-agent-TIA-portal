using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TiaLocalBridge.Services
{
    internal sealed class FlgNetView
    {
        public string Text;
        public int NetworkCount;
        public bool IsPartial;
    }

    internal static class FlgNetReader
    {
        public static FlgNetView Read(string path, string culture)
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
            var lang = (string)attrs.Element("ProgrammingLanguage");
            if (lang != "LAD" && lang != "FBD") throw new FormatException("This adapter only supports LAD/FBD XML.");

            var builder = new StringBuilder($"// Reconstructed {lang} view (fallback)\n");
            var units = block.Descendants().Where(e => e.Name.LocalName == "SW.Blocks.CompileUnit").ToList();
            
            bool partial = false;
            foreach (var unit in units)
            {
                var unitId = (string)unit.Attribute("ID") ?? "unknown";
                builder.Append($"\nNETWORK ID={unitId}\n");

                var objList = unit.Elements().FirstOrDefault(e => e.Name.LocalName == "ObjectList");
                if (objList != null)
                {
                    var titleObj = objList.Elements().FirstOrDefault(e => e.Name.LocalName == "MultilingualText" && (string)e.Attribute("CompositionName") == "Title");
                    if (titleObj != null)
                    {
                        var item = titleObj.Descendants().FirstOrDefault(e => e.Name.LocalName == "MultilingualTextItem" && (string)e.Elements().FirstOrDefault(a => a.Name.LocalName == "AttributeList")?.Elements().FirstOrDefault(c => c.Name.LocalName == "Culture")?.Value == culture);
                        var text = item?.Elements().FirstOrDefault(a => a.Name.LocalName == "AttributeList")?.Elements().FirstOrDefault(t => t.Name.LocalName == "Text")?.Value;
                        if (!string.IsNullOrEmpty(text)) builder.Append($"// TITLE: {text}\n");
                    }
                    var commentObj = objList.Elements().FirstOrDefault(e => e.Name.LocalName == "MultilingualText" && (string)e.Attribute("CompositionName") == "Comment");
                    if (commentObj != null)
                    {
                        var item = commentObj.Descendants().FirstOrDefault(e => e.Name.LocalName == "MultilingualTextItem" && (string)e.Elements().FirstOrDefault(a => a.Name.LocalName == "AttributeList")?.Elements().FirstOrDefault(c => c.Name.LocalName == "Culture")?.Value == culture);
                        var text = item?.Elements().FirstOrDefault(a => a.Name.LocalName == "AttributeList")?.Elements().FirstOrDefault(t => t.Name.LocalName == "Text")?.Value;
                        if (!string.IsNullOrEmpty(text)) builder.Append($"// COMMENT: {text.Replace("\n", "\\n")}\n");
                    }
                }
                
                var networks = unit.Descendants().Where(e => e.Name.LocalName == "NetworkSource").ToList();
                if (networks.Count == 1)
                {
                    var flgnet = networks[0].Elements().FirstOrDefault(e => e.Name.LocalName == "FlgNet");
                    if (flgnet != null)
                    {
                        var parts = flgnet.Elements().Where(e => e.Name.LocalName == "Parts").FirstOrDefault();
                        var wires = flgnet.Elements().Where(e => e.Name.LocalName == "Wires").FirstOrDefault();
                        
                        if (parts != null)
                        {
                            foreach (var part in parts.Elements())
                            {
                                var type = part.Name.LocalName;
                                var uid = (string)part.Attribute("UId");
                                var name = (string)part.Attribute("Name");
                                if (type == "Access")
                                {
                                    var scope = (string)part.Attribute("Scope");
                                    var symbols = string.Join(".", part.Descendants().Where(d => d.Name.LocalName == "Component").Select(c => (string)c.Attribute("Name")));
                                    builder.Append($"    Access({uid}) Scope={scope} Symbol={symbols}\n");
                                }
                                else if (type == "Call")
                                {
                                    var callInfo = part.Elements().FirstOrDefault(e => e.Name.LocalName == "CallInfo");
                                    var blockName = (string)callInfo?.Attribute("Name");
                                    var blockType = (string)callInfo?.Attribute("BlockType");
                                    builder.Append($"    Call({uid}) {blockType} {blockName}\n");
                                }
                                else
                                {
                                    var extras = string.Join(" ", part.Attributes().Where(a => a.Name.LocalName != "Name" && a.Name.LocalName != "UId").Select(a => $"{a.Name.LocalName}={a.Value}"));
                                    var children = string.Join(" ", part.Elements().Select(e => e.Name.LocalName));
                                    builder.Append($"    Part({uid}) {type} Name={name} {extras} {(children.Length > 0 ? $"Children=[{children}]" : "")}\n".TrimEnd() + "\n");
                                }
                            }
                        }
                        if (wires != null)
                        {
                            foreach (var wire in wires.Elements())
                            {
                                var uid = (string)wire.Attribute("UId");
                                var connections = new List<string>();
                                foreach (var con in wire.Elements())
                                {
                                    if (con.Name.LocalName == "Powerrail") connections.Add("Powerrail");
                                    else if (con.Name.LocalName == "NameCon") connections.Add($"NameCon({(string)con.Attribute("UId")},{(string)con.Attribute("Name")})");
                                    else if (con.Name.LocalName == "IdentCon") connections.Add($"IdentCon({(string)con.Attribute("UId")})");
                                    else if (con.Name.LocalName == "OpenCon") connections.Add($"OpenCon({(string)con.Attribute("UId")})");
                                }
                                builder.Append($"    Wire({uid}) [{string.Join(", ", connections)}]\n");
                            }
                        }
                        partial = true; // Currently we just dump a partial outline
                    }
                    else
                    {
                        builder.Append("  // Unsupported network source\n");
                        partial = true;
                    }
                }
                builder.Append("END_NETWORK\n");
            }
            
            return new FlgNetView { Text = builder.ToString(), NetworkCount = units.Count, IsPartial = partial };
        }
    }
}