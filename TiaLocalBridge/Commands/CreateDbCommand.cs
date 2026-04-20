using System;
using System.IO;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class CreateDbCommand : ITiaCommand
    {
        private const string TemplateFileName = "CreateDb_Template.xml";
        private const string TemplateNameToken = "__PI_CREATEDB_NAME__";
        private const string TemplateNumberToken = "__PI_CREATEDB_NUMBER__";

        public string Name => "CREATEDB";
        public string Description => "Creates a new disposable global DB by importing a built-in minimal XML template so DB creation follows the same template-based workflow as CREATEFB and CREATEFC.";
        public string Usage => "CREATEDB|<device-reference>|<target-group-reference>|<block-name>";
        public string Example => "CREATEDB|DemoProject/PLC_1|02_Global|Db_AgentTest";
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
            var dbNumber = CommandSupport.GetNextAvailableDbNumber(plcSoftware);
            var importedBlock = CommandSupport.ImportPlcBlockTemplate<DataBlock>(
                plcSoftware,
                targetGroup,
                blockName,
                ResolveTemplatePath(),
                Name,
                TemplateNameToken,
                TemplateNumberToken,
                dbNumber,
                "DB");

            var targetGroupReference = string.IsNullOrWhiteSpace(targetGroup.GroupReference) ? "<root>" : targetGroup.GroupReference;
            var createdBlockReference = string.Equals(targetGroupReference, "<root>", StringComparison.OrdinalIgnoreCase)
                ? importedBlock.Name
                : targetGroupReference + "/" + importedBlock.Name;

            return $"Created DB '{createdBlockReference}' in '{resolvedReference}' via XML template import [Type={CommandSupport.GetPlcBlockTypeName(importedBlock)}, Number={importedBlock.Number}, Language={importedBlock.ProgrammingLanguage}, Group={targetGroupReference}, AutoNumber={importedBlock.AutoNumber}]. The imported block may still require COMPILEPLCBLOCK, COMPILEPLC, or UPDATEPROGRAM afterward.";
        }

        private static string ResolveTemplatePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", TemplateFileName);
        }
    }
}
