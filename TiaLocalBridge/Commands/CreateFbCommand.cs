using System;
using System.IO;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class CreateFbCommand : ITiaCommand
    {
        private const string TemplateFileName = "CreateFb_SclTemplate.xml";
        private const string TemplateNameToken = "__PI_CREATEFB_NAME__";
        private const string TemplateNumberToken = "__PI_CREATEFB_NUMBER__";

        public string Name => "CREATEFB";
        public string Description => "Creates a new disposable SCL FB by importing a built-in XML template, which avoids the direct Openness CreateFB limitation observed in this environment where non-ProDiag FB creation is rejected.";
        public string Usage => "CREATEFB|<device-reference>|<target-group-reference>|<block-name>";
        public string Example => "CREATEFB|DemoProject/PLC_1|02_Global|Fb_AgentTest";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<target-group-reference>", "<block-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var targetGroup = CommandSupport.ResolvePlcBlockGroup(plcSoftware, providedArgs[1]);
            var blockName = CommandSupport.RequireNewPlcBlockName(plcSoftware, providedArgs[2]);
            var fbNumber = CommandSupport.GetNextAvailableFbNumber(plcSoftware);
            var importedBlock = CommandSupport.ImportPlcBlockTemplate<FB>(
                plcSoftware,
                targetGroup,
                blockName,
                ResolveTemplatePath(),
                Name,
                TemplateNameToken,
                TemplateNumberToken,
                fbNumber,
                "FB");

            var targetGroupReference = string.IsNullOrWhiteSpace(targetGroup.GroupReference) ? "<root>" : targetGroup.GroupReference;
            var createdBlockReference = string.Equals(targetGroupReference, "<root>", StringComparison.OrdinalIgnoreCase)
                ? importedBlock.Name
                : targetGroupReference + "/" + importedBlock.Name;

            return $"Created FB '{createdBlockReference}' in '{resolvedReference}' via XML template import [Type={CommandSupport.GetPlcBlockTypeName(importedBlock)}, Number={importedBlock.Number}, Language={importedBlock.ProgrammingLanguage}, Group={targetGroupReference}, AutoNumber={importedBlock.AutoNumber}]. The imported block may still require COMPILEPLCBLOCK, COMPILEPLC, or UPDATEPROGRAM afterward.";
        }

        private static string ResolveTemplatePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", TemplateFileName);
        }
    }
}
