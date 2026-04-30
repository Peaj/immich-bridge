using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ImmichBridge;

public sealed class SetupWizardForm : Form
{
    private readonly ConfigService configService;
    private readonly IProtocolRegistrar registrar;
    private readonly IPlatformLauncher launcher;
    private readonly string executablePath;
    private readonly List<Panel> pages = [];
    private readonly Button backButton = new();
    private readonly Button nextButton = new();
    private readonly Button cancelButton = new();
    private readonly TextBox remotePrefixBox = new();
    private readonly TextBox localPrefixBox = new();
    private readonly TextBox samplePathBox = new();
    private readonly Label validationLabel = new();
    private readonly Label protocolLabel = new();
    private int pageIndex;

    public SetupWizardForm(
        ConfigService configService,
        IProtocolRegistrar registrar,
        IPlatformLauncher launcher,
        string executablePath)
    {
        this.configService = configService;
        this.registrar = registrar;
        this.launcher = launcher;
        this.executablePath = executablePath;

        Text = "Immich Bridge Setup";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 460);
        Size = new Size(760, 520);
        Font = new Font("Segoe UI", 9F);

        BuildPages();
        BuildButtons();
        ShowPage(0);
    }

    private void BuildPages()
    {
        pages.Add(CreateWelcomePage());
        pages.Add(CreateMappingPage());
        pages.Add(CreateValidationPage());
        pages.Add(CreateProtocolPage());
        pages.Add(CreateFinishPage());

        foreach (var page in pages)
        {
            page.Dock = DockStyle.Fill;
            page.Padding = new Padding(28);
            Controls.Add(page);
        }
    }

    private Panel CreateWelcomePage()
    {
        var page = CreatePage("Welcome to Immich Bridge");
        page.Controls.Add(CreateBodyLabel(
            "This setup creates your local path mapping, registers the immich-bridge:// protocol for your Windows account, and points you to the Tampermonkey userscript.\n\nNo admin rights are required because protocol registration uses HKEY_CURRENT_USER."));
        return page;
    }

    private Panel CreateMappingPage()
    {
        var page = CreatePage("Path Mapping");
        var body = CreateBodyLabel("Enter the Immich path prefix exactly as Immich reports it, then choose the matching local Windows folder.");
        body.Dock = DockStyle.Top;

        remotePrefixBox.Text = "/external/fotos";
        localPrefixBox.ReadOnly = true;

        var browseButton = new Button { Text = "Browse...", AutoSize = true };
        browseButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose the local folder that matches the Immich remote prefix",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                localPrefixBox.Text = dialog.SelectedPath;
            }
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 18, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Immich prefix", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(remotePrefixBox, 1, 0);
        layout.SetColumnSpan(remotePrefixBox, 2);
        layout.Controls.Add(new Label { Text = "Local folder", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(localPrefixBox, 1, 1);
        layout.Controls.Add(browseButton, 2, 1);

        page.Controls.Add(layout);
        page.Controls.Add(body);
        return page;
    }

    private Panel CreateValidationPage()
    {
        var page = CreatePage("Validate Mapping");
        var body = CreateBodyLabel("Optionally paste a full Immich asset path to verify it maps to the expected local file.");
        body.Dock = DockStyle.Top;
        samplePathBox.PlaceholderText = "/external/fotos/album/photo.jpg";

        var testButton = new Button { Text = "Test Mapping", AutoSize = true };
        testButton.Click += (_, _) => ValidateSamplePath();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(0, 18, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(samplePathBox, 0, 0);
        layout.Controls.Add(testButton, 1, 0);
        layout.SetColumnSpan(validationLabel, 2);
        layout.Controls.Add(validationLabel, 0, 1);

        page.Controls.Add(layout);
        page.Controls.Add(body);
        return page;
    }

    private Panel CreateProtocolPage()
    {
        var page = CreatePage("Protocol Registration");
        protocolLabel.AutoSize = true;
        protocolLabel.MaximumSize = new Size(640, 0);
        protocolLabel.Text = "Setup will register immich-bridge:// for your Windows user account.";
        page.Controls.Add(protocolLabel);
        return page;
    }

    private Panel CreateFinishPage()
    {
        var page = CreatePage("Ready to Use");
        var body = CreateBodyLabel(
            "Install or update userscript/immich-bridge.user.js in Tampermonkey, then refresh Immich.\n\nUse the Immich Bridge toolbar button on asset pages to reveal files or open Windows' Open With dialog.");
        body.Dock = DockStyle.Top;

        var openUserscriptButton = new Button { Text = "Open userscript folder", AutoSize = true };
        openUserscriptButton.Click += (_, _) =>
        {
            var userscriptPath = Path.Combine(AppContext.BaseDirectory, "userscript");
            if (!Directory.Exists(userscriptPath))
            {
                userscriptPath = AppContext.BaseDirectory;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = userscriptPath,
                UseShellExecute = true
            });
        };

        var testRevealButton = new Button { Text = "Test Reveal", AutoSize = true };
        testRevealButton.Click += (_, _) => TestReveal();

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 18, 0, 0)
        };
        flow.Controls.Add(openUserscriptButton);
        flow.Controls.Add(testRevealButton);

        page.Controls.Add(flow);
        page.Controls.Add(body);
        return page;
    }

    private Panel CreatePage(string title)
    {
        var page = new Panel();
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            Height = 48
        };
        page.Controls.Add(titleLabel);
        return page;
    }

    private static Label CreateBodyLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(640, 0),
            Padding = new Padding(0, 8, 0, 0)
        };
    }

    private void BuildButtons()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 56,
            Padding = new Padding(12)
        };

        cancelButton.Text = "Cancel";
        cancelButton.AutoSize = true;
        cancelButton.Click += (_, _) => Close();

        nextButton.Text = "Next";
        nextButton.AutoSize = true;
        nextButton.Click += (_, _) => Next();

        backButton.Text = "Back";
        backButton.AutoSize = true;
        backButton.Click += (_, _) => ShowPage(pageIndex - 1);

        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(nextButton);
        buttons.Controls.Add(backButton);
        Controls.Add(buttons);
    }

    private void Next()
    {
        if (pageIndex == 1 && !ValidateMappingFields())
        {
            return;
        }

        if (pageIndex == 3)
        {
            SaveConfigAndRegisterProtocol();
        }

        if (pageIndex == pages.Count - 1)
        {
            Close();
            return;
        }

        ShowPage(pageIndex + 1);
    }

    private bool ValidateMappingFields()
    {
        if (string.IsNullOrWhiteSpace(remotePrefixBox.Text) || !remotePrefixBox.Text.Trim().StartsWith('/'))
        {
            MessageBox.Show(this, "Immich prefix must start with '/'.", "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(localPrefixBox.Text) || !Directory.Exists(localPrefixBox.Text))
        {
            MessageBox.Show(this, "Choose an existing local folder.", "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private void ValidateSamplePath()
    {
        try
        {
            var samplePath = samplePathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(samplePath))
            {
                validationLabel.Text = "Paste a sample Immich path first.";
                return;
            }

            var config = CreateConfigFromFields();
            var localPath = new PathMapper(config).MapPath(samplePath);
            validationLabel.Text = File.Exists(localPath)
                ? $"OK: {localPath}"
                : $"Mapped path does not exist yet: {localPath}";
        }
        catch (Exception ex)
        {
            validationLabel.Text = ex.Message;
        }
    }

    private void SaveConfigAndRegisterProtocol()
    {
        var config = CreateConfigFromFields();
        configService.Save(config);
        registrar.Register(executablePath);
        protocolLabel.Text = "Protocol registered and config saved.";
    }

    private void TestReveal()
    {
        try
        {
            var samplePath = samplePathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(samplePath))
            {
                MessageBox.Show(this, "Paste a sample Immich path on the validation page first.", "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var localPath = new PathMapper(CreateConfigFromFields()).MapPath(samplePath);
            launcher.RevealFile(localPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private BridgeConfig CreateConfigFromFields()
    {
        return configService.CreateDefaultConfig(remotePrefixBox.Text, localPrefixBox.Text);
    }

    private void ShowPage(int index)
    {
        pageIndex = Math.Clamp(index, 0, pages.Count - 1);
        for (var i = 0; i < pages.Count; i++)
        {
            pages[i].Visible = i == pageIndex;
        }

        backButton.Enabled = pageIndex > 0;
        nextButton.Text = pageIndex == pages.Count - 1 ? "Finish" : "Next";
        cancelButton.Visible = pageIndex < pages.Count - 1;
    }
}
