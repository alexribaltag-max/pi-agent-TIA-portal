using System;
using System.IO;
using System.Xml;
using TiaLocalBridge.Services;

internal static class Tests
{
    private static int count;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
    private static void Main()
    {
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../TiaLocalBridge/exports-kinds/SCL_ClearAlarms.xml"));
        var result = StructuredTextReader.Read(fixture, "en-US");
        Check(result.UnitCount == 1, "compile units");
        Check(result.SourceMap.Exists(e => e.xmlUid == "112" && e.section == "unit:3" && e.viewLine > 1), "top-level XML UID to analysis line map");
        Check(result.SourceMap.Exists(e => e.xmlUid == "175" && e.section == "unit:3" && e.viewLine == result.SourceMap.Find(e2 => e2.xmlUid == "169").viewLine), "nested array component maps to containing source line");
        Check(result.SourceMap.Exists(e => e.section == "interface:InOut/error" && e.viewLine > 1), "interface source location");
        Check(result.SafeBoundaryLines.Count >= 2 && result.SafeBoundaryLines[0] > 0 && result.SafeBoundaryLines.Exists(n => result.SourceMap.Exists(e => e.section == "unit:3" && e.xmlUid == null && e.viewLine == n)), "safe section/unit boundaries sourced from parser");
        Check(InventoryJson.Serialize(result.SourceMap).Contains("\"xmlUid\":\"112\""), "net48-compatible source map serialization");
        Check(result.Text.Contains("INTERFACE InOut") && result.Text.Contains("error : Array[*] of \"LAF_typeAlarm\""), "interface members");
        Check(result.Text.Contains("// Errors") && result.Text.Contains("INTERFACE Constant"), "comments/sections");
        Check(result.Text.Contains("FOR #tempIndex := LOWER_BOUND(ARR := #error, DIM := 1) TO UPPER_BOUND"), "nested call, parameter and local scope");
        Check(result.Text.Contains("#error[#tempIndex].trigger := false;") && result.Text.Contains("#criteriaAnalysis[#tempIndex].trigger := false;"), "array access and component ordering");
        Check(result.Text.Contains("// Siemens AG / (c)Copyright 2025") && result.Text.Contains("END_REGION"), "comment and token order");
        var xml = File.ReadAllText(fixture);
        var temp = Path.Combine(Path.GetTempPath(), "scl-reader-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            void Rejected(string text, string why)
            {
                File.WriteAllText(temp, text);
                bool failed = false;
                try { StructuredTextReader.Read(temp, "en-US"); }
                catch (FormatException) { failed = true; }
                catch (XmlException) { failed = true; }
                Check(failed, why);
            }
            Rejected(xml.Replace("<Token Text=\"REGION\" UId=\"21\" />", "<SilentExecution UId=\"21\" />"), "unknown semantic node");
            Rejected(xml.Replace("<Token Text=\"REGION\" UId=\"21\" />", "<Token Text=\"REGION\" Negated=\"true\" UId=\"21\" />"), "unknown semantic attribute");
            Rejected(xml.Replace("StructuredText/v4", "StructuredText/v999"), "unsupported schema");
            Rejected(xml.Replace("<MultiLanguageText Lang=\"en-US\">Errors</MultiLanguageText>", "<MultiLanguageText Lang=\"en-US\" Hidden=\"true\">Errors</MultiLanguageText>"), "unknown interface comment attribute");
            Rejected(xml.Replace("<SW.Blocks.CompileUnit ID=\"3\" CompositionName=\"CompileUnits\">", "<SW.Blocks.CompileUnit ID=\"3\" Extra=\"true\" CompositionName=\"CompileUnits\">"), "unknown compile unit attribute");
            Rejected(xml.Replace("Interface/v5", "Interface/v999"), "unsupported interface schema");
            Rejected("<!DOCTYPE Document [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>" + xml.TrimStart('\ufeff'), "DTD prohibited");
            Rejected(xml.Replace("<Component Name=\"error\" UId=\"171\">", "<Component UId=\"171\">"), "missing identifier");
            Rejected(xml.Replace("<Component Name=\"trigger\" UId=\"178\" />", "<Component Name=\"tr.igger\" UId=\"178\" />"), "ambiguous component cannot be flattened to qualified access");
            var attrs = "<AttributeList><ProgrammingLanguage>SCL</ProgrammingLanguage><Interface><Sections xmlns='http://www.siemens.com/automation/Openness/SW/Interface/v5'><Section Name='Input'><Member Name='x' Datatype='Bool'><StartValue>true</StartValue></Member></Section></Sections></Interface></AttributeList>";
            var ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v4";
            var unit = "<SW.Blocks.CompileUnit ID='7'><NetworkSource><StructuredText xmlns='" + ns + "'><Access Scope='GlobalVariable' UId='1'><Symbol><Component Name='Data'/><Token Text='.'/><Component Name='motor'/></Symbol></Access><Blank/><Token Text=':='/><Blank/><Access Scope='LiteralConstant'><Constant><ConstantValue>'a'</ConstantValue></Constant></Access><Token Text=';'/></StructuredText></NetworkSource></SW.Blocks.CompileUnit>";
            var sample = "<Document><SW.Blocks.FC>" + attrs + "<ObjectList>" + unit + unit.Replace("ID='7'", "ID='8'") + "</ObjectList></SW.Blocks.FC></Document>";
            File.WriteAllText(temp, sample);
            var two = StructuredTextReader.Read(temp, "en-US");
            Check(two.UnitCount == 2 && two.Text.Contains("\"Data\".motor := 'a';") && two.Text.Contains("x : Bool := true"), "global symbol, literal, start value, two units");
            Check(two.SourceMap.Exists(e => e.section == "unit:8" && e.xmlUid == "1"), "second unit source map");
            Rejected(sample.Replace("<ConstantValue>'a'</ConstantValue>", "<ConstantValue Type='S7'>1</ConstantValue>"), "unknown typed literal attribute fails closed");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Console.WriteLine("StructuredText production fixture checks: " + count + " passed");
    }
}
