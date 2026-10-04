using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class DeleteHmiTagCommand : ITiaCommand
    {
        public string Name => "DELETEHMITAG";
        public string Description => "Deletes an existing HMI tag from a Unified tag table or classic WinCC tag table.";
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
                var classicHmi = CommandSupport.TryGetHmiSoftware(resolution.Device);
                if (classicHmi == null)
                {
                    throw new InvalidOperationException($"Device '{resolvedReference}' does not contain HMI software.");
                }

                var table = CommandSupport.ResolveClassicHmiTagTable(classicHmi, providedArgs[1]);
                var tags = table.Item.GetType().GetProperty("Tags")?.GetValue(table.Item, null) as System.Collections.IEnumerable;
                var tag = tags?.Cast<object>().FirstOrDefault(candidate => string.Equals(
                    candidate.GetType().GetProperty("Name")?.GetValue(candidate, null)?.ToString(),
                    providedArgs[2],
                    StringComparison.OrdinalIgnoreCase));
                if (tag == null)
                {
                    throw new InvalidOperationException($"Classic HMI tag '{providedArgs[2]}' was not found in table '{table.ObjectReference}'.");
                }

                var name = tag.GetType().GetProperty("Name")?.GetValue(tag, null)?.ToString() ?? providedArgs[2];
                tag.GetType().GetMethod("Delete", Type.EmptyTypes)?.Invoke(tag, null);
                return $"Deleted classic HMI tag '{name}' from '{resolvedReference}' table '{table.ObjectReference}'.";
            }

            var tagResolution = CommandSupport.ResolveUnifiedHmiTag(hmiSoftware, providedArgs[1], providedArgs[2]);
            var deletedTagName = tagResolution.Tag.Name;
            var tableReference = tagResolution.Table.TableReference;
            tagResolution.Tag.Delete();

            return $"Deleted Unified HMI tag '{deletedTagName}' from '{resolvedReference}' table '{tableReference}'.";
        }
    }
}
