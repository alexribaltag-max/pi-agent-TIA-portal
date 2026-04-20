using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcBlockCrossReferencesCommand : ITiaCommand
    {
        public string Name => "GETPLCBLOCKXREF";
        public string Description => "Lists cross references for one PLC block, including where the block is called or otherwise referenced. Optional filter values: AllObjects, ObjectsWithReferences, ObjectsWithoutReferences, UnusedObjects.";
        public string Usage => "GETPLCBLOCKXREF|<device-reference>|<block-reference>|[filter]";
        public string Example => "GETPLCBLOCKXREF|DemoProject/PLC_1|02_Global/MyBlock|AllObjects";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 2 || providedArgs.Length > 3)
            {
                throw new ArgumentException($"Expected two or three arguments (<device-reference>, <block-reference>, optional [filter]). {Description} Usage: {Usage}. Example: {Example}");
            }

            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blockResolution = CommandSupport.ResolvePlcBlock(plcSoftware, providedArgs[1]);
            var filter = providedArgs.Length == 3
                ? CommandSupport.ParseCrossReferenceFilter(providedArgs[2])
                : Siemens.Engineering.CrossReference.CrossReferenceFilter.AllObjects;
            var references = CommandSupport.GetCrossReferenceSummaries(blockResolution.Block, filter);

            return references.Count > 0
                ? $"PLC block '{blockResolution.BlockReference}' cross references on device '{resolvedReference}' (Filter={filter}): {string.Join(" || ", references)}"
                : $"PLC block '{blockResolution.BlockReference}' on device '{resolvedReference}' has no cross references for filter '{filter}'.";
        }
    }
}
