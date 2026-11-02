using System.Drawing;
using System.Windows.Forms;

namespace ImmichBridge;

// Brief, non-activating confirmation in the corner of the screen, so copying does not steal
// focus from the browser and needs no click to dismiss.
public sealed class CopyNoticeForm : Form
{
    private const int DisplayMilliseconds = 1800;
    private readonly System.Windows.Forms.Timer closeTimer = new() { Interval = DisplayMilliseconds };

    public CopyNoticeForm(string localPath)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(14, 10, 14, 10),
            MaximumSize = new Size(420, 0),
            AutoEllipsis = true,
            Text = $"Copied {Path.GetFileName(localPath)}"
        });

        closeTimer.Tick += (_, _) => Close();
        Click += (_, _) => Close();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WsExToolWindow = 0x00000080;
            const int WsExNoActivate = 0x08000000;
            var createParams = base.CreateParams;
            createParams.ExStyle |= WsExToolWindow | WsExNoActivate;
            return createParams;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
        closeTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            closeTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
