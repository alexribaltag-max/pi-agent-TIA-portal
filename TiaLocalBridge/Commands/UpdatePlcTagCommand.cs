using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class UpdatePlcTagCommand : ITiaCommand
    {
        public string Name => "UPDATEPLCTAG";
        public string Description => "Updates the data type and logical address of an existing PLC tag.";
        public string Usage => "UPDATEPLCTAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<logical-address>";
        public string Example => "UPDATEPLCTAG|DemoProject/PLC_1|Default tag table|MyTag|Bool|%M10.1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<table-reference>", "<tag-name>", "<data-type>", "<logical-address>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var tagResolution = CommandSupport.ResolvePlcTag(plcSoftware, providedArgs[1], providedArgs[2]);
            var dataTypeName = providedArgs[3].Trim();
            var logicalAddress = providedArgs[4].Trim();

            tagResolution.Tag.DataTypeName = dataTypeName;
            tagResolution.Tag.LogicalAddress = logicalAddress;

            return $"Updated PLC tag '{tagResolution.Tag.Name}' in '{resolvedReference}' table '{tagResolution.Table.TableReference}' [DataType={tagResolution.Tag.DataTypeName}, Address={tagResolution.Tag.LogicalAddress}]";
        }
    }
}
