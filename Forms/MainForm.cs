using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

using RecipeItemCreator.Configuration;
using RecipeItemCreator.Services;

namespace RecipeItemCreator.Forms;

public partial class MainForm : Form
{
    private Bitmap? _template;
    private Bitmap? _item;

    // 0 = active
    // 1 = owned-resource cleanup has already been performed
    private int _cleanupState;

    // Set once the window is actually closing.
    private bool _isClosing;

    private bool _updateCheckStarted;

    private readonly CancellationTokenSource _updateCancellationSource = new();

    private string _latestReleaseUrl = string.Empty;

    private const int PreviewRenderSize = 512;

    /// <summary>
    /// Returns the currently used template.
    /// Access is intentionally no longer allowed after Dispose().
    /// </summary>
    private Bitmap Template =>
        _template ?? throw new ObjectDisposedException(nameof(MainForm));

    /// <summary>
    /// Prevents UI and rendering access while or after the form is closing.
    /// </summary>
    private bool IsShuttingDown =>
        _isClosing ||
        Volatile.Read(ref _cleanupState) != 0 ||
        IsDisposed ||
        Disposing;

    public MainForm()
    {
        InitializeComponent();

        // Create an independent bitmap copy of the Visual Studio resource.
        // This makes the instance exclusively owned by MainForm
        // so it can be disposed safely later.
        _template = new Bitmap(Properties.Resources.Template);

        ConfigureOutputSizes();

        versionStatusLabel.Text = $"v{AppInfo.DisplayVersion}";

        githubStatusLabel.Text = AppSettings.GitHubUpdatesConfigured
            ? "Update: checking..."
            : "Update: not configured";

        RefreshPreview();
    }

    // ------------------------------------------------------------------------
    // Form lifecycle
    // ------------------------------------------------------------------------

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (!IsShuttingDown)
            WindowsTheme.EnableDarkTitleBar(Handle);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (IsShuttingDown || _updateCheckStarted)
            return;

        _updateCheckStarted = true;

        _ = CheckForUpdatesAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Run the normal WinForms events first.
        // This allows other handlers to cancel closing if necessary.
        base.OnFormClosing(e);

        if (!e.Cancel)
        {
            _isClosing = true;
            CancelUpdateCheck();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _isClosing = true;

        CancelUpdateCheck();
        CleanupOwnedImages();

        _updateCancellationSource.Dispose();

        base.OnFormClosed(e);
    }

    private void CancelUpdateCheck()
    {
        try
        {
            _updateCancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // During final shutdown, the source may already have been disposed.
        }
    }

    private void CleanupOwnedImages()
    {
        if (Interlocked.Exchange(ref _cleanupState, 1) != 0)
            return;

        DisposePreviewImage();

        Bitmap? item = Interlocked.Exchange(ref _item, null);
        item?.Dispose();

        Bitmap? template = Interlocked.Exchange(ref _template, null);
        template?.Dispose();

        _latestReleaseUrl = string.Empty;
    }

    private void DisposePreviewImage()
    {
        Image? preview = previewPictureBox.Image;

        // Detach it from the control first.
        previewPictureBox.Image = null;

        // Dispose the bitmap afterwards.
        preview?.Dispose();
    }

    // ------------------------------------------------------------------------
    // Buttons
    // ------------------------------------------------------------------------

    private void ChooseItemButton_Click(object? sender, EventArgs e)
    {
        if (IsShuttingDown)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Select item image",

            Filter =
                "Images (*.png;*.webp;*.jpg;*.jpeg)|*.png;*.webp;*.jpg;*.jpeg|" +
                "PNG Images (*.png)|*.png|" +
                "WebP Images (*.webp)|*.webp|" +
                "JPEG Images (*.jpg;*.jpeg)|*.jpg;*.jpeg",

            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        LoadItem(dialog.FileName);
    }

    private void ChooseTemplateButton_Click(object? sender, EventArgs e)
    {
        if (IsShuttingDown)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Select recipe template",
            Filter = "PNG Images (*.png)|*.png",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        LoadTemplate(dialog.FileName);
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        if (IsShuttingDown)
            return;

        ExportPng();
    }

    // ------------------------------------------------------------------------
    // UI events
    // ------------------------------------------------------------------------

    private void ItemIdTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (IsShuttingDown)
            return;

        // Changing the item ID does not affect the image.
        // Therefore, no unnecessary RefreshPreview() call is needed.
        UpdatePreviewStatus();
    }

    private void OutputSizeComboBox_SelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (IsShuttingDown)
            return;

        // The visible preview remains 512x512.
        // Only the actual exported image uses this size.
        UpdatePreviewStatus();
    }

    // ------------------------------------------------------------------------
    // Item loading
    // ------------------------------------------------------------------------

    private void LoadItem(string fileName)
    {
        if (IsShuttingDown)
            return;

        Bitmap loaded;

        try
        {
            loaded = ImageComposer.LoadUnlocked(fileName);
        }
        catch (Exception ex)
        {
            ShowError(
                "The item image could not be loaded.",
                ex);

            return;
        }

        if (IsShuttingDown)
        {
            loaded.Dispose();
            return;
        }

        // Store the new instance first.
        Bitmap? previous = Interlocked.Exchange(
            ref _item,
            loaded);

        // Safely dispose the previous instance afterwards.
        previous?.Dispose();

        itemPathTextBox.Text = fileName;

        if (string.IsNullOrWhiteSpace(itemIdTextBox.Text))
        {
            string baseName =
                Path.GetFileNameWithoutExtension(fileName);

            string generatedId =
                NormalizeItemId(baseName);

            if (!string.IsNullOrWhiteSpace(generatedId))
            {
                itemIdTextBox.Text =
                    generatedId + "_recipe";
            }
        }

        SetStatus(
            $"Loaded: {loaded.Width} x {loaded.Height}px");

        RefreshPreview();
    }

    // ------------------------------------------------------------------------
    // Template loading
    // ------------------------------------------------------------------------

    private void LoadTemplate(string fileName)
    {
        if (IsShuttingDown)
            return;

        Bitmap loaded;

        try
        {
            loaded = ImageComposer.LoadUnlocked(fileName);
        }
        catch (Exception ex)
        {
            ShowError(
                "The recipe template could not be loaded.",
                ex);

            return;
        }

        if (IsShuttingDown)
        {
            loaded.Dispose();
            return;
        }

        Bitmap? previous = Interlocked.Exchange(
            ref _template,
            loaded);

        previous?.Dispose();

        templatePathTextBox.Text = fileName;

        SetStatus("Custom recipe template loaded.");

        RefreshPreview();
    }

    // ------------------------------------------------------------------------
    // Drag & Drop
    // ------------------------------------------------------------------------

    private void PreviewPictureBox_DragEnter(
        object? sender,
        DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;

        if (IsShuttingDown)
            return;

        if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
            return;

        string[]? files =
            e.Data.GetData(DataFormats.FileDrop) as string[];

        if (files is not { Length: > 0 })
            return;

        if (!IsSupportedImage(files[0]))
            return;

        e.Effect = DragDropEffects.Copy;
    }

    private void PreviewPictureBox_DragDrop(
        object? sender,
        DragEventArgs e)
    {
        if (IsShuttingDown)
            return;

        string[]? files =
            e.Data?.GetData(DataFormats.FileDrop) as string[];

        if (files is not { Length: > 0 })
            return;

        string fileName = files[0];

        if (!IsSupportedImage(fileName))
            return;

        LoadItem(fileName);
    }

    private static bool IsSupportedImage(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (!File.Exists(fileName))
            return false;

        string extension =
            Path.GetExtension(fileName);

        return extension.Equals(
                   ".png",
                   StringComparison.OrdinalIgnoreCase) ||

               extension.Equals(
                   ".webp",
                   StringComparison.OrdinalIgnoreCase) ||

               extension.Equals(
                   ".jpg",
                   StringComparison.OrdinalIgnoreCase) ||

               extension.Equals(
                   ".jpeg",
                   StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------------
    // Preview
    // ------------------------------------------------------------------------

    private void RefreshPreview()
    {
        if (IsShuttingDown)
            return;

        Bitmap? nextPreview = null;

        try
        {
            // Render the new preview completely first.
            //
            // If rendering fails, the existing
            // preview remains unchanged.
            nextPreview =
                ImageComposer.ComposePreview(
                    Template,
                    _item,
                    PreviewRenderSize);

            if (IsShuttingDown)
                return;

            Image? previousPreview =
                previewPictureBox.Image;

            // Assign the new image first.
            previewPictureBox.Image = nextPreview;

            // Ownership has now been transferred to MainForm/PictureBox.
            nextPreview = null;

            // Dispose the previous preview afterwards.
            previousPreview?.Dispose();

            UpdatePreviewStatus();
        }
        catch (ObjectDisposedException)
            when (IsShuttingDown)
        {
            // Intentionally ignore this while shutting down.
        }
        catch (Exception ex)
        {
            if (IsShuttingDown)
                return;

            SetStatus("Preview failed.");

            ShowError(
                "The preview could not be generated.",
                ex);
        }
        finally
        {
            // If the bitmap has not yet been assigned to the PictureBox,
            // it is still owned by this method.
            nextPreview?.Dispose();
        }
    }

    private void UpdatePreviewStatus()
    {
        if (IsShuttingDown)
            return;

        int exportSize =
            SelectedOutputSize();

        if (_item is null)
        {
            SetStatus(
                $"Template ready · " +
                $"Output {exportSize} x {exportSize}px · " +
                "please select an item image");

            return;
        }

        SetStatus(
            $"Preview ready · " +
            $"Output {exportSize} x {exportSize}px");
    }

    // ------------------------------------------------------------------------
    // Export
    // ------------------------------------------------------------------------

    private void ExportPng()
    {
        if (IsShuttingDown)
            return;

        Bitmap? item = _item;

        if (item is null)
        {
            MessageBox.Show(
                this,
                "Please select an item image first.",
                "Notice",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        string id =
            NormalizeItemId(itemIdTextBox.Text);

        if (string.IsNullOrWhiteSpace(id))
        {
            MessageBox.Show(
                this,
                "Please enter an item ID, e.g. sushi_recipe.",
                "Notice",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        // Write the normalized ID back to the text box.
        if (!string.Equals(
                itemIdTextBox.Text,
                id,
                StringComparison.Ordinal))
        {
            itemIdTextBox.Text = id;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save recipe PNG",
            Filter = "PNG Image (*.png)|*.png",
            FileName = id + ".png",
            AddExtension = true,
            DefaultExt = "png",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        if (IsShuttingDown)
            return;

        try
        {
            int exportSize =
                SelectedOutputSize();

            // The export image is created only here.
            //
            // Therefore, no persistent _result field is required.
            using Bitmap result =
                ImageComposer.Compose(
                    Template,
                    item,
                    exportSize);

            result.Save(
                dialog.FileName,
                System.Drawing.Imaging.ImageFormat.Png);

            SetStatus(
                $"Saved: {Path.GetFileName(dialog.FileName)} · " +
                $"{exportSize} x {exportSize}px");
        }
        catch (ObjectDisposedException)
            when (IsShuttingDown)
        {
            // The application is currently shutting down.
        }
        catch (Exception ex)
        {
            if (IsShuttingDown)
                return;

            ShowError(
                "The PNG file could not be saved.",
                ex);
        }
    }

    // ------------------------------------------------------------------------
    // GitHub update check
    // ------------------------------------------------------------------------

    private async Task CheckForUpdatesAsync()
    {
        if (IsShuttingDown)
            return;

        GitHubUpdateResult result;
        CancellationToken cancellationToken =
            _updateCancellationSource.Token;

        try
        {
            result =
                await GitHubUpdateService.CheckAsync(
                    AppInfo.CurrentVersion,
                    cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (IsShuttingDown)
                return;

            githubStatusLabel.IsLink = false;
            githubStatusLabel.Text =
                "GitHub: check failed";

            githubStatusLabel.ToolTipText =
                ex.Message;

            _latestReleaseUrl =
                string.Empty;

            return;
        }

        // Important:
        // The form may have been closed while awaiting the request.
        if (IsShuttingDown)
            return;

        githubStatusLabel.IsLink = false;
        githubStatusLabel.ToolTipText =
            string.Empty;

        _latestReleaseUrl =
            string.Empty;

        switch (result.State)
        {
            case GitHubUpdateState.NotConfigured:
                githubStatusLabel.Text =
                    "GitHub: not configured";

                githubStatusLabel.ToolTipText =
                    "Set GitHubRepositoryUrl in " +
                    "Configuration/AppSettings.cs.";

                break;

            case GitHubUpdateState.UpToDate:
                githubStatusLabel.Text =
                    "GitHub: up to date";

                githubStatusLabel.ToolTipText =
                    string.IsNullOrWhiteSpace(result.Tag)
                        ? "No newer release version was found."
                        : $"Latest release: {result.Tag}";

                break;

            case GitHubUpdateState.UpdateAvailable:
                githubStatusLabel.Text =
                    string.IsNullOrWhiteSpace(result.Tag)
                        ? "Update available"
                        : $"Update available: {result.Tag}";

                githubStatusLabel.IsLink =
                    !string.IsNullOrWhiteSpace(
                        result.ReleaseUrl);

                githubStatusLabel.ToolTipText =
                    githubStatusLabel.IsLink
                        ? "Click to open the GitHub release."
                        : "A newer version is available.";

                _latestReleaseUrl =
                    result.ReleaseUrl ?? string.Empty;

                break;

            case GitHubUpdateState.Failed:
                githubStatusLabel.Text =
                    "GitHub: check failed";

                githubStatusLabel.ToolTipText =
                    string.IsNullOrWhiteSpace(
                        result.ErrorMessage)
                        ? "The update check failed."
                        : result.ErrorMessage;

                break;

            default:
                githubStatusLabel.Text =
                    "GitHub: unknown state";

                githubStatusLabel.ToolTipText =
                    string.Empty;

                break;
        }
    }

    private void GitHubStatusLabel_Click(
        object? sender,
        EventArgs e)
    {
        if (IsShuttingDown)
            return;

        if (!githubStatusLabel.IsLink)
            return;

        if (string.IsNullOrWhiteSpace(
                _latestReleaseUrl))
        {
            return;
        }

        if (!TryGetSafeGitHubReleaseUri(
            _latestReleaseUrl,
            out Uri? releaseUri) || releaseUri is null)
        {
            SetStatus("Invalid GitHub release URL.");
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = releaseUri.AbsoluteUri,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
            when (ex is
                InvalidOperationException or
                System.ComponentModel.Win32Exception)
        {
            SetStatus(
                "The GitHub release could not be opened.");
        }
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private static bool TryGetSafeGitHubReleaseUri(
        string value,
        out Uri? uri)
    {
        uri = null;

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri? parsed))
        {
            return false;
        }

        if (!parsed.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!parsed.Host.Equals(
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private int SelectedOutputSize()
    {
        return outputSizeComboBox.SelectedIndex switch
        {
            0 => 128,
            1 => 256,
            _ => 512
        };
    }

    private void SetStatus(string text)
    {
        if (IsShuttingDown)
            return;

        footerStatusLabel.Text = text;
    }

    private void ShowError(
        string message,
        Exception ex)
    {
        if (IsShuttingDown)
            return;

        MessageBox.Show(
            this,
            message + "\n\n" + ex.Message,
            "Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static string NormalizeItemId(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Convert German special characters where practical
        // to item-ID-compatible characters.
        string input = value
            .Trim()
            .ToLowerInvariant()
            .Replace(
                "ß",
                "ss",
                StringComparison.Ordinal)
            .Normalize(
                NormalizationForm.FormD);

        StringBuilder builder =
            new(input.Length);

        bool previousWasUnderscore = true;

        foreach (char rawChar in input)
        {
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(
                    rawChar);

            // Remove diacritical marks:
            // ä -> a
            // ö -> o
            // ü -> u
            // é -> e
            if (category ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            char c = rawChar;

            if (char.IsWhiteSpace(c) || c == '-')
                c = '_';

            if ((c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9'))
            {
                builder.Append(c);

                previousWasUnderscore = false;
                continue;
            }

            if (c == '_' &&
                !previousWasUnderscore)
            {
                builder.Append('_');

                previousWasUnderscore = true;
            }
        }

        return builder
            .ToString()
            .Trim('_');
    }

    private void ConfigureOutputSizes()
    {
        outputSizeComboBox.BeginUpdate();

        try
        {
            outputSizeComboBox.Items.Clear();

            outputSizeComboBox.Items.AddRange(
            [
                "128 x 128",
                "256 x 256",
                "512 x 512"
            ]);

            // Default selection at application startup
            outputSizeComboBox.SelectedIndex = 2;
        }
        finally
        {
            outputSizeComboBox.EndUpdate();
        }
    }
}