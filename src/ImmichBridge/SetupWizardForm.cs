using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ImmichBridge;

public sealed class SetupWizardForm : Form
{
    private const string ChromeAddonUrl = "https://chromewebstore.google.com/detail/ohghjemcjnaickehejdokfmjphpjoiag";
    private const string EdgeAddonUrl = "https://microsoftedge.microsoft.com/addons/detail/immich-bridge/bgipocndkokcllfjgmiicakhlbddnjij";
    private const string FirefoxAddonUrl = "https://addons.mozilla.org/firefox/addon/immich-bridge/";
    private const string UserscriptUrl = "https://raw.githubusercontent.com/Peaj/immich-bridge/main/userscript/immich-bridge.user.js";
    private const string TampermonkeyUrl = "https://www.tampermonkey.net/";
    private const string GitHubUrl = "https://github.com/Peaj/immich-bridge";

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
    private readonly TextBox localFilePathBox = new();
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
        Icon = TryLoadWindowIcon();
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
        var introLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 210,
            ColumnCount = 2,
            RowCount = 1
        };
        introLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        introLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        introLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var body = CreateBodyLabel(
            "Immich Bridge connects your Immich web app to files on this Windows computer.\n\nAfter setup, Immich gets a small toolbar button for local actions like revealing the original file in Explorer or opening it with a desktop app. The setup only needs to know how Immich's server paths map to your Windows folders.");
        body.MaximumSize = new Size(300, 0);
        body.Dock = DockStyle.Fill;
        introLayout.Controls.Add(body, 0, 0);

        var promoImage = CreatePromoTitleImage();
        if (promoImage is not null)
        {
            introLayout.Controls.Add(promoImage, 1, 0);
        }

        content.Controls.Add(introLayout);
        return CreatePage(content);
    }

    private Panel CreateMappingPage()
    {
        var content = CreateContentLayout("Path Mapping");
        var body = CreateBodyLabel(
            "In Immich, open an asset, expand Details, click Show file location, and copy the full file path. Then choose the matching local file. If that folder has many files, you can choose the folder that contains it instead. Immich Bridge will derive and validate the mapping automatically.");

        samplePathBox.PlaceholderText = "/external/fotos/2026/album/photo.jpg";
        samplePathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        samplePathBox.TextChanged += (_, _) => TryDeriveMapping(false);

        localFilePathBox.ReadOnly = true;
        localFilePathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;

        remotePrefixBox.ReadOnly = true;
        remotePrefixBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        localPrefixBox.ReadOnly = true;
        localPrefixBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;

        var chooseFileButton = new Button { Text = "Choose file...", AutoSize = true };
        chooseFileButton.Click += (_, _) => ChooseMatchingLocalFile();

        var chooseFolderButton = new Button { Text = "Choose folder...", AutoSize = true };
        chooseFolderButton.Click += (_, _) => ChooseContainingLocalFolder();

        testRevealButton.Text = "Test Reveal";
        testRevealButton.AutoSize = true;
        testRevealButton.Enabled = false;
        testRevealButton.Click += (_, _) => TestReveal();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 6,
            Padding = new Padding(0, 18, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Immich file path", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(samplePathBox, 1, 0);
        layout.SetColumnSpan(samplePathBox, 2);
        layout.Controls.Add(new Label { Text = "Local match", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(localFilePathBox, 1, 1);
        layout.Controls.Add(chooseFileButton, 2, 1);
        layout.Controls.Add(chooseFolderButton, 3, 1);
        layout.Controls.Add(new Label { Text = "Immich prefix", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(remotePrefixBox, 1, 2);
        layout.SetColumnSpan(remotePrefixBox, 3);
        layout.Controls.Add(new Label { Text = "Local folder", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(localPrefixBox, 1, 3);
        layout.SetColumnSpan(localPrefixBox, 3);
        layout.Controls.Add(testRevealButton, 1, 4);
        validationLabel.AutoSize = true;
        validationLabel.MaximumSize = new Size(640, 0);
        validationLabel.Padding = new Padding(0, 10, 0, 0);
        layout.SetColumnSpan(validationLabel, 4);
        layout.Controls.Add(validationLabel, 0, 5);

        content.Controls.Add(body);
        content.Controls.Add(layout);
        return CreatePage(content);
    }

    private void ChooseMatchingLocalFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the matching local file",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            localFilePathBox.Text = dialog.FileName;
            TryDeriveMapping(false);
        }
    }

    private void ChooseContainingLocalFolder()
    {
        string fileName;
        try
        {
            fileName = PathMappingDeriver.GetRemoteFileName(samplePathBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = $"Choose the local folder that contains {fileName}",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var localFilePath = Path.Combine(dialog.SelectedPath, fileName);
        if (!File.Exists(localFilePath))
        {
            MessageBox.Show(
                this,
                $"The selected folder does not contain {fileName}. Choose the folder that contains the matching local file.",
                "Immich Bridge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        localFilePathBox.Text = localFilePath;
        TryDeriveMapping(false);
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
            "Immich Bridge is configured. The last step is adding the browser integration to Immich.\n\n1. Install the add-on for your browser.\n2. Enter your Immich URL in the add-on options page and approve access for that site.\n3. Refresh Immich and open an asset. The Immich Bridge button appears in the asset toolbar.\n\nIf your browser is unsupported, use the Tampermonkey userscript fallback.");

        var openUserscriptButton = new Button { Text = "Open userscript fallback", AutoSize = true };
        openUserscriptButton.Click += (_, _) =>
        {
            OpenUserscript();
        };

        var tampermonkeyLink = CreateLink("Open Tampermonkey website", TampermonkeyUrl);
        var githubLink = CreateLink("View Immich Bridge on GitHub", GitHubUrl);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 18, 0, 0)
        };
        flow.Controls.Add(CreateStoreBadgeButton(
            "chrome-web-store.png",
            "Available in the Chrome Web Store",
            "Install Chrome add-on",
            ChromeAddonUrl));
        flow.Controls.Add(CreateStoreBadgeButton(
            "edge-add-ons.png",
            "Get it from Microsoft Edge",
            "Install Edge add-on",
            EdgeAddonUrl));
        flow.Controls.Add(CreateStoreBadgeButton(
            "firefox-add-ons.png",
            "Get the add-on for Firefox",
            "Install Firefox add-on",
            FirefoxAddonUrl));
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
            OpenUrl(url);
        };
        return link;
    }

    private static Control CreateStoreBadgeButton(string fileName, string accessibleName, string fallbackText, string url)
    {
        var badgePath = FindBrowserExtensionAssetPath("store-badges", fileName);
        if (badgePath is null)
        {
            var fallbackButton = new Button
            {
                Text = fallbackText,
                AutoSize = true,
                AccessibleName = accessibleName
            };
            fallbackButton.Click += (_, _) => OpenUrl(url);
            return fallbackButton;
        }

        var image = LoadImageWithoutLockingFile(badgePath);
        const int badgeHeight = 48;
        var badgeWidth = Math.Max(120, (int)Math.Round(image.Width * (badgeHeight / (double)image.Height)));

        var pictureBox = new PictureBox
        {
            Image = image,
            Size = new Size(badgeWidth, badgeHeight),
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Hand,
            AccessibleName = accessibleName,
            AccessibleRole = AccessibleRole.PushButton,
            Margin = new Padding(0, 0, 10, 10)
        };
        pictureBox.Click += (_, _) => OpenUrl(url);
        return pictureBox;
    }

    private static PictureBox? CreatePromoTitleImage()
    {
        var imagePath = FindBrowserExtensionAssetPath("icons", "immich-bridge-promo-title.png");
        if (imagePath is null)
        {
            return null;
        }

        return new PictureBox
        {
            Image = LoadImageWithoutLockingFile(imagePath),
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill,
            Margin = new Padding(16, 0, 0, 0)
        };
    }

    private static Icon? TryLoadWindowIcon()
    {
        var iconPath = FindBrowserExtensionAssetPath("icons", "immich-bridge-64.png");
        if (iconPath is null)
        {
            return null;
        }

        try
        {
            using var image = LoadImageWithoutLockingFile(iconPath);
            using var bitmap = new Bitmap(image, new Size(64, 64));
            var handle = bitmap.GetHicon();
            try
            {
                using var icon = Icon.FromHandle(handle);
                return (Icon)icon.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch
        {
            return null;
        }
    }

    private static Image LoadImageWithoutLockingFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static string? FindBrowserExtensionAssetPath(params string[] relativePathParts)
    {
        var roots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "browser-extension"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "browser-extension")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "browser-extension"))
        };

        foreach (var root in roots)
        {
            var path = Path.Combine([root, .. relativePathParts]);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
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

        if (pageIndex == 2)
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
        if (!TryDeriveMapping(true))
        {
            return false;
        }

        if (!Directory.Exists(localPrefixBox.Text))
        {
            MessageBox.Show(this, "The derived local folder does not exist.", "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private bool TryDeriveMapping(bool showError)
    {
        try
        {
            ResetSampleValidation();
            if (string.IsNullOrWhiteSpace(samplePathBox.Text) || string.IsNullOrWhiteSpace(localFilePathBox.Text))
            {
                validationLabel.Text = "Paste the Immich file path, then choose the matching local file or the folder that contains it.";
                return false;
            }

            if (!File.Exists(localFilePathBox.Text))
            {
                validationLabel.ForeColor = Color.Firebrick;
                validationLabel.Text = "Choose an existing local file.";
                return false;
            }

            var mapping = PathMappingDeriver.Derive(samplePathBox.Text, localFilePathBox.Text);
            remotePrefixBox.Text = mapping.RemotePrefix;
            localPrefixBox.Text = mapping.LocalPrefix;

            var config = CreateConfigFromFields();
            var mappedLocalPath = new PathMapper(config).MapPath(samplePathBox.Text);
            if (!mappedLocalPath.Equals(Path.GetFullPath(localFilePathBox.Text), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Derived mapping points to a different file: {mappedLocalPath}");
            }

            validatedLocalSamplePath = mappedLocalPath;
            validationLabel.ForeColor = Color.ForestGreen;
            validationLabel.Text = $"✓ Mapping verified using shared path: {mapping.SharedRelativePath}";
            testRevealButton.Enabled = true;
            return true;
        }
        catch (Exception ex)
        {
            validatedLocalSamplePath = null;
            validationLabel.ForeColor = Color.Firebrick;
            validationLabel.Text = ex.Message;
            testRevealButton.Enabled = false;
            if (showError)
            {
                MessageBox.Show(this, ex.Message, "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return false;
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
            FileName = UserscriptUrl,
            UseShellExecute = true
        });
    }

    private static string? FindUserscriptPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "userscript", "immich-bridge.user.js"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "userscript", "immich-bridge.user.js")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "userscript", "immich-bridge.user.js"))
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
