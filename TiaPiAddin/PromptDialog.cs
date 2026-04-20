using System.Drawing;
using System.Windows.Forms;

namespace TiaPiAddin
{
    internal static class PromptDialog
    {
        public static string ShowCustomPrompt(int selectionCount)
        {
            using (var form = new Form())
            using (var promptLabel = new Label())
            using (var textBox = new TextBox())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                form.Text = "Pi Agent Custom Prompt";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.TopMost = true;
                form.ClientSize = new Size(520, 260);

                promptLabel.AutoSize = false;
                promptLabel.Location = new Point(12, 12);
                promptLabel.Size = new Size(496, 40);
                promptLabel.Text = selectionCount == 1
                    ? "Enter the custom instruction to send to Pi Agent for the selected object:"
                    : "Enter the custom instruction to send to Pi Agent for the selected objects:";

                textBox.Location = new Point(12, 60);
                textBox.Size = new Size(496, 140);
                textBox.Multiline = true;
                textBox.AcceptsReturn = true;
                textBox.AcceptsTab = true;
                textBox.ScrollBars = ScrollBars.Vertical;

                okButton.Text = "Send";
                okButton.DialogResult = DialogResult.OK;
                okButton.Location = new Point(352, 215);
                okButton.Size = new Size(75, 28);

                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(433, 215);
                cancelButton.Size = new Size(75, 28);

                form.Controls.Add(promptLabel);
                form.Controls.Add(textBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
            }
        }
    }
}
