using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcTagCrossReferencesCommand : ITiaCommand
    {
        public string Name => "GETPLCTAGXREF";
        public string Description => "Lists cross references for one PLC tag, including the locations where the tag is used. Optional filter values: AllObjects, ObjectsWithReferences, ObjectsWithoutReferences, UnusedObjects.";
        public string Usage => "GETPLCTAGXREF|<device-reference>|<table-reference>|<tag-name>|[filter]";
        public string Example => "GETPLCTAGXREF|DemoProject/PLC_1|Default tag table|MyTag|AllObjects";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 3 || providedArgs.Length > 4)
            {
                throw new ArgumentException($"Expected three or four arguments (<device-reference>, <table-reference>, <tag-name>, optional [filter]). {Description} Usage: {Usage}. Example: {Example}");
            }

            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var tagResolution = CommandSupport.ResolvePlcTag(plcSoftware, providedArgs[1], providedArgs[2]);
            var filter = providedArgs.Length == 4
                ? CommandSupport.ParseCrossReferenceFilter(providedArgs[3])
                : Siemens.Engineering.CrossReference.CrossReferenceFilter.AllObjects;
            var references = CommandSupport.GetCrossReferenceSummaries(tagResolution.Tag, filter);

            return references.Count > 0
                ? $"PLC tag '{tagResolution.Table.TableReference}/{tagResolution.Tag.Name}' cross references on device '{resolvedReference}' (Filter={filter}): {string.Join(" || ", references)}"
                : $"PLC tag '{tagResolution.Table.TableReference}/{tagResolution.Tag.Name}' on device '{resolvedReference}' has no cross references for filter '{filter}'.";
        }
    }
}
