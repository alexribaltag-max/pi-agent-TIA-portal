using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class DeletePlcTagCommand : ITiaCommand
    {
        public string Name => "DELETEPLCTAG";
        public string Description => "Deletes an existing PLC tag from the specified PLC tag table.";
        public string Usage => "DELETEPLCTAG|<device-reference>|<table-reference>|<tag-name>";
        public string Example => "DELETEPLCTAG|DemoProject/PLC_1|Default tag table|MyTag";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<table-reference>", "<tag-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var tagResolution = CommandSupport.ResolvePlcTag(plcSoftware, providedArgs[1], providedArgs[2]);
            var deletedTagName = tagResolution.Tag.Name;
            var tableReference = tagResolution.Table.TableReference;
            tagResolution.Tag.Delete();

            return $"Deleted PLC tag '{deletedTagName}' from '{resolvedReference}' table '{tableReference}'.";
        }
    }
}
