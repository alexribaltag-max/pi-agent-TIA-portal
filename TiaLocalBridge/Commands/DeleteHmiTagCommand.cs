using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class DeleteHmiTagCommand : ITiaCommand
    {
        public string Name => "DELETEHMITAG";
        public string Description => "Deletes an existing Unified HMI tag from the specified Unified HMI tag table.";
        public string Usage => "DELETEHMITAG|<device-reference>|<table-reference>|<tag-name>";
        public string Example => "DELETEHMITAG|DemoProject/HMI_1|Default tag table|HmiSpeed";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<table-reference>", "<tag-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var tagResolution = CommandSupport.ResolveUnifiedHmiTag(hmiSoftware, providedArgs[1], providedArgs[2]);
            var deletedTagName = tagResolution.Tag.Name;
            var tableReference = tagResolution.Table.TableReference;
            tagResolution.Tag.Delete();

            return $"Deleted Unified HMI tag '{deletedTagName}' from '{resolvedReference}' table '{tableReference}'.";
        }
    }
}
