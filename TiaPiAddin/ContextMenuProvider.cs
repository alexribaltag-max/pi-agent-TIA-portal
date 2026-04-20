using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Siemens.Engineering;
using Siemens.Engineering.AddIn.Menu;

namespace TiaPiAddin
{
    public class ContextMenuProvider
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string PI_SERVER_URL = "http://127.0.0.1:31415/api/tia-action";

        public void BuildContextMenuItems(ContextMenuAddInRoot addInRootSubmenu)
        {
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("Pi Agent: Review Code", OnReviewBlockClick);
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("Pi Agent: Refactor Code", OnRefactorBlockClick);
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("Pi Agent: Explain Block", OnExplainBlockClick);
            addInRootSubmenu.Items.AddActionItem<IEngineeringObject>("Pi Agent: Custom Prompt...", OnCustomPromptClick);
        }

        private async void OnReviewBlockClick(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            await SendActionToPiAsync("review code", GetSelection(menuSelectionProvider));
        }

        private async void OnRefactorBlockClick(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            await SendActionToPiAsync("refactor code", GetSelection(menuSelectionProvider));
        }

        private async void OnExplainBlockClick(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            await SendActionToPiAsync("explain block", GetSelection(menuSelectionProvider));
        }

        private async void OnCustomPromptClick(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            var selection = GetSelection(menuSelectionProvider);
            if (selection.Count == 0)
            {
                return;
            }

            string prompt = PromptDialog.ShowCustomPrompt(selection.Count);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return;
            }

            await SendActionToPiAsync(prompt.Trim(), selection);
        }

        private List<IEngineeringObject> GetSelection(MenuSelectionProvider<IEngineeringObject> menuSelectionProvider)
        {
            var selection = new List<IEngineeringObject>();
            foreach (IEngineeringObject obj in menuSelectionProvider.GetSelection())
            {
                selection.Add(obj);
            }
            return selection;
        }

        private async Task SendActionToPiAsync(string action, IEnumerable<IEngineeringObject> selection)
        {
            foreach (var item in selection)
            {
                string name = string.Empty;
                dynamic dynObj = item;
                try { name = dynObj.Name; } catch { }

                var deviceName = GetDeviceName(item);

                string jsonPayload = $@"{{
                    ""action"": ""{EscapeJson(action)}"",
                    ""device"": ""{EscapeJson(deviceName)}"",
                    ""target"": ""{EscapeJson(name)}"",
                    ""type"": ""{EscapeJson(item.GetType().Name)}""
                }}";

                try
                {
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync(PI_SERVER_URL, content);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Failed to trigger Pi Agent: " + ex.Message,
                        "Pi Agent Integration",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private string GetDeviceName(IEngineeringObject item)
        {
            var current = item.Parent;
            while (current != null)
            {
                if (current.GetType().Name.Contains("Device"))
                {
                    dynamic d = current;
                    try { return d.Name; } catch { }
                }
                current = current.Parent;
            }
            return "Unknown Device";
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
