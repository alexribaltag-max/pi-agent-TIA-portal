using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using TiaLocalBridge.Commands;

namespace TiaLocalBridge
{
    internal class Program
    {
        private static readonly Dictionary<string, ITiaCommand> _commands;

        static Program()
        {
            _commands = new Dictionary<string, ITiaCommand>(StringComparer.OrdinalIgnoreCase)
            {
                { "OPEN", new OpenProjectCommand() },
                { "CREATE", new CreateProjectCommand() },
                { "ADDDEVICE", new AddDeviceCommand() },
                { "ADDMODULE", new AddModuleCommand() },
                { "CREATEFB", new CreateFbCommand() },
                { "CREATEFC", new CreateFcCommand() },
                { "CREATEDB", new CreateDbCommand() },
                { "LIST", new ListProjectsCommand() },
                { "SEARCHHWCATALOG", new SearchHardwareCatalogCommand() },
                { "GETDEVICES", new GetDevicesCommand() },
                { "GETDEVICESJSON", new GetDevicesJsonCommand() },
                { "GETDEVICEITEMS", new GetDeviceItemsCommand() },
                { "GETPLUGLOCATIONS", new GetPlugLocationsCommand() },
                { "GETHWPROPERTIES", new GetHwPropertiesCommand() },
                { "SETHWPROPERTY", new SetHwPropertyCommand() },
                { "GETHWADDRESSES", new GetHwAddressesCommand() },
                { "SETHWADDRESS", new SetHwAddressCommand() },
                { "GETDRIVEOBJECTS", new GetDriveObjectsCommand() },
                { "GETDRIVETELEGRAMS", new GetDriveTelegramsCommand() },
                { "SETDRIVETELEGRAMNUMBER", new SetDriveTelegramNumberCommand() },
                { "SETDRIVETELEGRAMADDRESS", new SetDriveTelegramAddressCommand() },
                { "CREATESUBNET", new CreateSubnetCommand() },
                { "GETNETWORKINTERFACES", new GetNetworkInterfacesCommand() },
                { "GETIPADDRESS", new GetIpAddressCommand() },
                { "GETNODEPROPERTIES", new GetNodePropertiesCommand() },
                { "SETNODEPROPERTY", new SetNodePropertyCommand() },
                { "CONNECTTOSUBNET", new ConnectToSubnetCommand() },
                { "CONNECTPROFINET", new ConnectProfinetCommand() },
                { "GETPLCTAGTABLES", new GetPlcTagTablesCommand() },
                { "GETPLCTAGS", new GetPlcTagsCommand() },
                { "GETPLCTAGXREF", new GetPlcTagCrossReferencesCommand() },
                { "ADDPLCTAG", new AddPlcTagCommand() },
                { "UPDATEPLCTAG", new UpdatePlcTagCommand() },
                { "DELETEPLCTAG", new DeletePlcTagCommand() },
                { "GETPLCBLOCKGROUPS", new GetPlcBlockGroupsCommand() },
                { "DELETEPLCBLOCK", new DeletePlcBlockCommand() },
                { "RENAMEPLCBLOCK", new RenamePlcBlockCommand() },
                { "GETPLCBLOCKS", new GetPlcBlocksCommand() },
                { "GETPLCBLOCKSJSON", new GetPlcBlocksJsonCommand() },
                { "GETPLCBLOCKXREF", new GetPlcBlockCrossReferencesCommand() },
                { "GETPLCBLOCKINFO", new GetPlcBlockInfoCommand() },
                { "GETPLCBLOCKINFOJSON", new GetPlcBlockInfoJsonCommand() },
                { "COMPILEPLCBLOCK", new CompilePlcBlockCommand() },
                { "COMPILEPLC", new CompilePlcCommand() },
                { "UPDATEPROGRAM", new UpdateProgramCommand() },
                { "EXPORTPLCBLOCK", new ExportPlcBlockCommand() },
                { "EXPORTPLCBLOCKDOCS", new ExportPlcBlockDocumentsCommand() },
                { "EXPORTPLCBLOCKSMART", new ExportPlcBlockSmartCommand() },
                { "EXPORTPLCBLOCKSMARTJSON", new ExportPlcBlockSmartJsonCommand() },
                { "IMPORTPLCBLOCKSMART", new ImportPlcBlockSmartCommand() },
                { "IMPORTPLCBLOCKSMARTJSON", new ImportPlcBlockSmartJsonCommand() },
                { "GETHMITAGS", new GetHmiTagsCommand() },
                { "GETHMITAGTABLES", new GetHmiTagTablesCommand() },
                { "GETHMITAGXREF", new GetHmiTagCrossReferencesCommand() },
                { "ADDHMITAG", new AddHmiTagCommand() },
                { "ENSUREHMITAG", new EnsureHmiTagCommand() },
                { "UPDATEHMITAG", new UpdateHmiTagCommand() },
                { "DELETEHMITAG", new DeleteHmiTagCommand() },
                { "GETHMICONNECTIONS", new GetHmiConnectionsCommand() },
                { "GETHMICONNECTIONPROPERTIES", new GetHmiConnectionPropertiesCommand() },
                { "ADDHMICONNECTION", new AddHmiConnectionCommand() },
                { "SETHMICONNECTIONPROPERTY", new SetHmiConnectionPropertyCommand() },
                { "GETHMISCREENGROUPS", new GetHmiScreenGroupsCommand() },
                { "CREATEHMISCREENGROUP", new CreateHmiScreenGroupCommand() },
                { "GETHMISCREENS", new GetHmiScreensCommand() },
                { "CREATEHMISCREEN", new CreateHmiScreenCommand() },
                { "GETHMISCREENPROPERTIES", new GetHmiScreenPropertiesCommand() },
                { "SETHMISCREENPROPERTY", new SetHmiScreenPropertyCommand() },
                { "GETHMISCREENITEMS", new GetHmiScreenItemsCommand() },
                { "GETHMISCREENITEMPROPERTIES", new GetHmiScreenItemPropertiesCommand() },
                { "GETHMISCREENITEMTAGBINDINGS", new GetHmiScreenItemTagBindingsCommand() },
                { "ADDHMISCREENITEM", new AddHmiScreenItemCommand() },
                { "ENSUREHMISCREENITEM", new EnsureHmiScreenItemCommand() },
                { "SETHMISCREENITEMPROPERTY", new SetHmiScreenItemPropertyCommand() },
                { "SETHMISCREENITEMTAGBINDING", new SetHmiScreenItemTagBindingCommand() },
                { "ENSUREHMISCREENITEMTAGBINDING", new EnsureHmiScreenItemTagBindingCommand() },
                { "HELP", new HelpCommand(GetAvailableCommands) }
            };
        }

        static void Main(string[] args)
        {
            TiaPortal portal = null;

            WriteEvent("INITIALIZING", "TiaLocalBridge is starting.");
            WriteEvent("READY", "Command bridge ready. TIA Portal will be connected on demand.");

            try
            {
                while (true)
                {
                    string input = Console.ReadLine();
                    if (input == null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        continue;
                    }

                    string[] parts = input.Split(new[] { '|' }, StringSplitOptions.None);
                    string commandName = (parts[0] ?? string.Empty).Trim();
                    string[] commandArgs = parts.Skip(1).ToArray();

                    if (commandName.Length == 0)
                    {
                        WriteErrorResponse(null, "Missing command name.", portal != null);
                        continue;
                    }

                    if (string.Equals(commandName, "EXIT", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteSuccessResponse("EXIT", "Exiting.", false, portal != null);
                        break;
                    }

                    if (!_commands.TryGetValue(commandName, out ITiaCommand command))
                    {
                        WriteErrorResponse(commandName.ToUpperInvariant(), $"Unknown command '{commandName}'. Use HELP for available commands.", portal != null);
                        continue;
                    }

                    try
                    {
                        if (command.RequiresPortal && portal == null)
                        {
                            portal = ConnectToPortal();
                        }

                        string result = command.Execute(commandArgs, portal);
                        WriteSuccessResponse(command.Name, result, command.ProducesJson, portal != null);
                    }
                    catch (Exception ex)
                    {
                        WriteErrorResponse(command.Name, ex.Message, portal != null);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteFatalError(ex.Message, portal != null);
            }
            finally
            {
                portal?.Dispose();
            }
        }

        private static TiaPortal ConnectToPortal()
        {
            WriteEvent("CONNECTING_TIA_PORTAL", "Looking for an existing TIA Portal instance.");
            var existingPortalProcess = TiaPortal.GetProcesses().FirstOrDefault();

            if (existingPortalProcess != null)
            {
                try
                {
                    WriteEvent("FOUND_EXISTING_TIA_PORTAL", $"Found existing TIA Portal instance with PID {existingPortalProcess.Id}.");
                    WriteEvent("ATTACHING_TO_EXISTING_TIA_PORTAL", "Attaching to existing TIA Portal instance.");
                    var attachedPortal = existingPortalProcess.Attach();
                    WriteEvent("ATTACHED_TO_EXISTING_TIA_PORTAL", "Attached to existing TIA Portal instance.");
                    return attachedPortal;
                }
                catch (Exception ex)
                {
                    WriteEvent("ATTACH_FAILED", $"Failed to attach to existing TIA Portal instance. A new instance will be started instead. Details: {ex.Message}");
                }
            }

            WriteEvent("STARTING_NEW_TIA_PORTAL", "Starting a new TIA Portal instance with user interface.");
            var portal = new TiaPortal(TiaPortalMode.WithUserInterface);
            WriteEvent("STARTED_NEW_TIA_PORTAL", "Started a new TIA Portal instance with user interface.");
            return portal;
        }

        private static string GetAvailableCommands()
        {
            return string.Join(
                " || ",
                _commands.Values
                    .OrderBy(command => command.Name)
                    .Select(command => string.Format(
                        "{0}: {1} | Usage: {2} | Example: {3} | RequiresPortal: {4} | ProducesJson: {5}",
                        command.Name,
                        command.Description,
                        command.Usage,
                        command.Example,
                        command.RequiresPortal ? "true" : "false",
                        command.ProducesJson ? "true" : "false")));
        }

        private static void WriteEvent(string eventName, string message)
        {
            Console.WriteLine(
                $"{{\"type\":\"event\",\"event\":\"{CommandSupport.EscapeJson(eventName)}\",\"message\":\"{CommandSupport.EscapeJson(message)}\"}}");
        }

        private static void WriteSuccessResponse(string commandName, string result, bool resultIsJson, bool portalConnected)
        {
            var resultProperty = resultIsJson
                ? $"\"result\":{result}"
                : $"\"result\":\"{CommandSupport.EscapeJson(result)}\"";

            Console.WriteLine(
                $"{{\"type\":\"response\",\"status\":\"success\",\"command\":\"{CommandSupport.EscapeJson(commandName)}\",\"portalConnected\":{(portalConnected ? "true" : "false")},\"resultType\":\"{(resultIsJson ? "json" : "text")}\",{resultProperty}}}");
        }

        private static void WriteErrorResponse(string commandName, string errorMessage, bool portalConnected)
        {
            var commandProperty = string.IsNullOrWhiteSpace(commandName)
                ? "null"
                : $"\"{CommandSupport.EscapeJson(commandName)}\"";

            Console.WriteLine(
                $"{{\"type\":\"response\",\"status\":\"error\",\"command\":{commandProperty},\"portalConnected\":{(portalConnected ? "true" : "false")},\"error\":\"{CommandSupport.EscapeJson(errorMessage)}\"}}");
        }

        private static void WriteFatalError(string errorMessage, bool portalConnected)
        {
            Console.WriteLine(
                $"{{\"type\":\"fatal\",\"status\":\"error\",\"portalConnected\":{(portalConnected ? "true" : "false")},\"error\":\"{CommandSupport.EscapeJson(errorMessage)}\"}}");
        }
    }
}
