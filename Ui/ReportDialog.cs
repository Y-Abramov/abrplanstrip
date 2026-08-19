using System.Drawing;
using System.Windows.Forms;
using Abr.Civil.Sdk;

namespace AbrCivil.PlanStrip.Ui
{
    /// <summary>Отчёт о сборе после построения или обновления полосы.</summary>
    internal sealed class ReportDialog : Form
    {
        public ReportDialog(string text)
        {
            Text = "Отчёт о сборе";
            ClientSize = new Size(440, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9f);
            Icon = AbrIcon.Create();

            var rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                Font = new Font("Consolas", 9f),
                TabStop = false,
                Text = text
            };

            var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            var close = new Button
            {
                Text = "Закрыть",
                Width = 90,
                Height = 28,
                Location = new Point(440 - 90 - 12, 8),
                FlatStyle = FlatStyle.System,
                DialogResult = DialogResult.OK
            };
            panelBottom.Controls.Add(close);

            var panelContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 6) };
            panelContent.Controls.Add(rtb);

            Controls.Add(panelContent);
            Controls.Add(panelBottom);

            AcceptButton = close;
        }
    }
}
