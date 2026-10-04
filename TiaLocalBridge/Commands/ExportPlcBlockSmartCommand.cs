using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;
using TiaLocalBridge.Services;

namespace TiaLocalBridge.Commands
{
    internal class ExportPlcBlockSmartCommand : ITiaCommand
    {
        public string Name => "EXPORTPLCBLOCKSMART";
        public string Description => "Exports one PLC block using the existing documents/XML policy, with XML fallback for failed document export.";
        public string Usage => "EXPORTPLCBLOCKSMART|<device-reference>|<block-reference>|<target-directory>";
        public string Example => @"EXPORTPLCBLOCKSMART|DemoProject/PLC_1|Main/FB_Machine|C:\Exports";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var provided = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<block-reference>", "<target-directory>");
            var device = CommandSupport.ResolveDeviceByReference(portal, provided[0]);
            var software = CommandSupport.TryGetPlcSoftware(device.Device);
            var deviceRef = CommandSupport.GetDeviceReference(device.Project, device.Device);
            if (software == null) throw new InvalidOperationException($"Device '{deviceRef}' does not contain PLC software.");
            var resolution = CommandSupport.ResolvePlcBlock(software, provided[1]);
            var block = resolution.Block;
            var directory = new DirectoryInfo(provided[2]);
            var baseName = CommandSupport.SanitizeFileName(block.Name);
            var export = BlockExportService.Export(block, directory, baseName);
            var info = $"[Type={CommandSupport.GetPlcBlockTypeName(block)}, Language={block.ProgrammingLanguage}].";
            if (export.PreferredMode == "Xml")
                return $"Smart export used XML for PLC block '{resolution.BlockReference}' from '{deviceRef}' to '{export.Files[0]}' {info}";
            if (export.UsedFallback)
                return $"Smart export fell back to XML for PLC block '{resolution.BlockReference}' from '{deviceRef}' because document export failed: {export.FallbackReason} XML file: '{export.Files[0]}' {info}";
            var result = $"Smart export used documents for PLC block '{resolution.BlockReference}' from '{deviceRef}' to '{Path.Combine(directory.FullName, baseName + "_docs")}' with state '{export.State}' {info}";
            if (export.Files.Any()) result += $" Files: {string.Join(", ", export.Files)}.";
            if (export.Messages.Any()) result += $" Messages: {string.Join(" | ", export.Messages)}.";
            return result;
        }
    }
}
