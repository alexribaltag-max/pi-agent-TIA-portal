using System;
using System.IO;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class ExportPlcBlockCommand : ITiaCommand
    {
        public string Name => "EXPORTPLCBLOCK";
        public string Description => "Safely exports one PLC block to a file using the standard TIA block export format so you can inspect the content before attempting modifications.";
        public string Usage => "EXPORTPLCBLOCK|<device-reference>|<block-reference>|<target-file-path>";
        public string Example => @"EXPORTPLCBLOCK|DemoProject/PLC_1|Main/FB_Machine|C:\Exports\FB_Machine.xml";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<block-reference>", "<target-file-path>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blockResolution = CommandSupport.ResolvePlcBlock(plcSoftware, providedArgs[1]);
            var targetFile = new FileInfo(providedArgs[2]);
            if (targetFile.Directory != null && !targetFile.Directory.Exists)
            {
                targetFile.Directory.Create();
            }

            blockResolution.Block.Export(targetFile, ExportOptions.None, DocumentInfoOptions.None);

            return $"Exported PLC block '{blockResolution.BlockReference}' from '{resolvedReference}' to '{targetFile.FullName}'.";
        }
    }
}
