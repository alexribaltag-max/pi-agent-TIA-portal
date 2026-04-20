using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiTagCrossReferencesCommand : ITiaCommand
    {
        public string Name => "GETHMITAGXREF";
        public string Description => "Lists cross references for one Unified HMI tag, including where the tag is used by screens or other HMI objects. Optional filter values: AllObjects, ObjectsWithReferences, ObjectsWithoutReferences, UnusedObjects.";
        public string Usage => "GETHMITAGXREF|<device-reference>|<table-reference>|<tag-name>|[filter]";
        public string Example => "GETHMITAGXREF|DemoProject/HMI_1|Default tag table|HmiSpeed|AllObjects";
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
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var tagResolution = CommandSupport.ResolveUnifiedHmiTag(hmiSoftware, providedArgs[1], providedArgs[2]);
            var filter = providedArgs.Length == 4
                ? CommandSupport.ParseCrossReferenceFilter(providedArgs[3])
                : Siemens.Engineering.CrossReference.CrossReferenceFilter.AllObjects;
            var references = CommandSupport.GetCrossReferenceSummaries(tagResolution.Tag, filter);

            return references.Count > 0
                ? $"Unified HMI tag '{tagResolution.Table.TableReference}/{tagResolution.Tag.Name}' cross references on device '{resolvedReference}' (Filter={filter}): {string.Join(" || ", references)}"
                : $"Unified HMI tag '{tagResolution.Table.TableReference}/{tagResolution.Tag.Name}' on device '{resolvedReference}' has no cross references for filter '{filter}'.";
        }
    }
}
