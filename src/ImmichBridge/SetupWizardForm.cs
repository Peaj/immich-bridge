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
    private readonly Button testRevealButton = new();
    private string? validatedLocalSamplePath;
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
        var content = CreateContentLayout("Welcome to Immich Bridge");
        content.Controls.Add(CreateBodyLabel(
            "Immich Bridge connects your Immich web app to files on this Windows computer.\n\nAfter setup, Immich gets a small toolbar button for local actions like revealing the original file in Explorer or opening it with a desktop app. The setup only needs to know how Immich's server paths map to your Windows folders."));
        return CreatePage(content);
    }

    private Panel CreateMappingPage()
    {
        var content = CreateContentLayout("Path Mapping");
        var body = CreateBodyLabel("Enter the Immich path prefix exactly as Immich reports it, then choose the matching local Windows folder.");

        remotePrefixBox.Text = "/external/fotos";
        remotePrefixBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        localPrefixBox.ReadOnly = true;
        localPrefixBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;

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
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Immich prefix", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(remotePrefixBox, 1, 0);
        layout.SetColumnSpan(remotePrefixBox, 2);
        layout.Controls.Add(new Label { Text = "Local folder", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(localPrefixBox, 1, 1);
        layout.Controls.Add(browseButton, 2, 1);

        content.Controls.Add(body);
        content.Controls.Add(layout);
        return CreatePage(content);
    }

    private Panel CreateValidationPage()
    {
        var content = CreateContentLayout("Validate Mapping");
        var body = CreateBodyLabel("Optionally paste a full Immich asset path to verify it maps to the expected local file.");
        samplePathBox.PlaceholderText = "/external/fotos/album/photo.jpg";
        samplePathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        samplePathBox.TextChanged += (_, _) => ResetSampleValidation();

        var testButton = new Button { Text = "Test Mapping", AutoSize = true };
        testButton.Click += (_, _) => ValidateSamplePath();

        testRevealButton.Text = "Test Reveal";
        testRevealButton.AutoSize = true;
        testRevealButton.Enabled = false;
        testRevealButton.Click += (_, _) => TestReveal();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(0, 18, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(samplePathBox, 0, 0);
        layout.Controls.Add(testButton, 1, 0);
        layout.Controls.Add(testRevealButton, 2, 0);
        validationLabel.AutoSize = true;
        validationLabel.MaximumSize = new Size(640, 0);
        validationLabel.Padding = new Padding(0, 10, 0, 0);
        layout.SetColumnSpan(validationLabel, 3);
        layout.Controls.Add(validationLabel, 0, 1);

        content.Controls.Add(body);
        content.Controls.Add(layout);
        return CreatePage(content);
    }

    private Panel CreateProtocolPage()
    {
        var content = CreateContentLayout("Browser Link");
        protocolLabel.AutoSize = true;
        protocolLabel.MaximumSize = new Size(640, 0);
        protocolLabel.Text = "Immich Bridge will register its local browser link so clicks from Immich can be handed to this app.\n\nThis is registered only for your Windows user account and does not require administrator rights.";
        content.Controls.Add(protocolLabel);
        return CreatePage(content);
    }

    private Panel CreateFinishPage()
    {
        var content = CreateContentLayout("Ready to Use");
        var body = CreateBodyLabel(
            "Immich Bridge is configured. The last step is adding the browser script to Immich.\n\n1. Install the Tampermonkey browser extension if you do not have it yet.\n2. Open the Immich Bridge userscript and approve Tampermonkey's install/update screen.\n3. Refresh Immich and open an asset. The Immich Bridge button appears in the asset toolbar.");

        var tampermonkeyLink = CreateLink("Open Tampermonkey website", "https://www.tampermonkey.net/");
        var githubLink = CreateLink("View Immich Bridge on GitHub", "https://github.com/Peaj/immich-bridge");

        var openUserscriptButton = new Button { Text = "Open userscript", AutoSize = true };
        openUserscriptButton.Click += (_, _) =>
        {
            OpenUserscript();
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 18, 0, 0)
        };
        flow.Controls.Add(openUserscriptButton);

        content.Controls.Add(body);
        content.Controls.Add(flow);
        content.Controls.Add(tampermonkeyLink);
        content.Controls.Add(githubLink);
        return CreatePage(content);
    }

    private static Panel CreatePage(Control content)
    {
        var page = new Panel { AutoScroll = true };
        page.Controls.Add(content);
        return page;
    }

    private TableLayoutPanel CreateContentLayout(string title)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 18)
        }, 0, 0);

        return layout;
    }

    private static Label CreateBodyLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Top,
            MaximumSize = new Size(640, 0),
            Padding = new Padding(0, 0, 0, 0)
        };
    }

    private static LinkLabel CreateLink(string text, string url)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 10, 0, 0),
            LinkBehavior = LinkBehavior.HoverUnderline
        };
        link.LinkClicked += (_, _) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        };
        return link;
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
            if (File.Exists(localPath))
            {
                validatedLocalSamplePath = localPath;
                validationLabel.ForeColor = Color.ForestGreen;
                validationLabel.Text = $"✓ Mapping verified: {localPath}";
                testRevealButton.Enabled = true;
            }
            else
            {
                validatedLocalSamplePath = null;
                validationLabel.ForeColor = Color.DarkOrange;
                validationLabel.Text = $"Mapped path does not exist yet: {localPath}";
                testRevealButton.Enabled = false;
            }
        }
        catch (Exception ex)
        {
            validatedLocalSamplePath = null;
            validationLabel.ForeColor = Color.Firebrick;
            validationLabel.Text = ex.Message;
            testRevealButton.Enabled = false;
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
            if (string.IsNullOrWhiteSpace(validatedLocalSamplePath))
            {
                MessageBox.Show(this, "Validate an existing sample file before testing reveal.", "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            launcher.RevealFile(validatedLocalSamplePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetSampleValidation()
    {
        validatedLocalSamplePath = null;
        testRevealButton.Enabled = false;
        validationLabel.ForeColor = SystemColors.ControlText;
        validationLabel.Text = string.Empty;
    }

    private void OpenUserscript()
    {
        var userscriptPath = FindUserscriptPath();
        if (userscriptPath is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = userscriptPath,
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "https://raw.githubusercontent.com/Peaj/immich-bridge/main/userscript/immich-bridge.user.js",
            UseShellExecute = true
        });
    }

    private static string? FindUserscriptPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "userscript", "immich-bridge.user.js"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "userscript", "immich-bridge.user.js"))
        };

        return candidates.FirstOrDefault(File.Exists);
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
