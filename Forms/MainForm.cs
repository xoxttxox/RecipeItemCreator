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

    // 0 = aktiv
    // 1 = Dispose wurde bereits gestartet
    private int _disposeState;

    // Wird gesetzt, sobald das Fenster wirklich geschlossen wird.
    private bool _isClosing;

    private bool _updateCheckStarted;

    private string _latestReleaseUrl = string.Empty;

    private const int PreviewRenderSize = 512;

    /// <summary>
    /// Liefert das aktuell verwendete Template.
    /// Nach Dispose() ist ein Zugriff absichtlich nicht mehr erlaubt.
    /// </summary>
    private Bitmap Template =>
        _template ?? throw new ObjectDisposedException(nameof(MainForm));

    /// <summary>
    /// Verhindert UI- und Rendering-Zugriffe während bzw. nach dem Schließen.
    /// </summary>
    private bool IsShuttingDown =>
        _isClosing ||
        Volatile.Read(ref _disposeState) != 0 ||
        IsDisposed ||
        Disposing;

    public MainForm()
    {
        InitializeComponent();

        // Eigene Bitmap-Kopie der VS-Ressource.
        // Dadurch gehört die Instanz ausschließlich MainForm
        // und darf später sicher disposed werden.
        _template = new Bitmap(Properties.Resources.Template);

        versionStatusLabel.Text = $"v{AppInfo.DisplayVersion}";

        githubStatusLabel.Text = AppSettings.GitHubUpdatesConfigured
            ? "Update: checking..."
            : "Update: not set up";

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
        // Zuerst normale WinForms-Events ausführen.
        // Dadurch können andere Handler das Schließen noch abbrechen.
        base.OnFormClosing(e);

        if (!e.Cancel)
            _isClosing = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(false);
            return;
        }

        // Atomarer Guard:
        // selbst wenn Dispose() aus irgendeinem Grund mehrfach
        // oder theoretisch gleichzeitig aufgerufen wird,
        // läuft unser Cleanup nur einmal.
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            base.Dispose(true);
            return;
        }

        _isClosing = true;

        try
        {
            DisposePreviewImage();

            // Referenz zuerst atomar auf null setzen.
            // Danach kann dieselbe Instanz nicht nochmals über
            // dieses Feld disposed werden.
            Bitmap? item = Interlocked.Exchange(ref _item, null);
            item?.Dispose();

            Bitmap? template = Interlocked.Exchange(ref _template, null);
            template?.Dispose();

            _latestReleaseUrl = string.Empty;

            components?.Dispose();
        }
        finally
        {
            // Base.Dispose muss auch dann ausgeführt werden,
            // falls beim eigenen Cleanup unerwartet etwas schiefgeht.
            base.Dispose(true);
        }
    }

    private void DisposePreviewImage()
    {
        Image? preview = previewPictureBox.Image;

        // Erst vom Control trennen.
        previewPictureBox.Image = null;

        // Danach Bitmap freigeben.
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

        // Item-ID verändert das Bild nicht.
        // Deshalb kein unnötiges RefreshPreview().
        UpdatePreviewStatus();
    }

    private void OutputSizeComboBox_SelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (IsShuttingDown)
            return;

        // Die sichtbare Vorschau bleibt 512x512.
        // Nur das tatsächliche Exportbild verwendet diese Größe.
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
                "The image could not be loaded.",
                ex);

            return;
        }

        if (IsShuttingDown)
        {
            loaded.Dispose();
            return;
        }

        // Neue Instanz zuerst übernehmen.
        Bitmap? previous = Interlocked.Exchange(
            ref _item,
            loaded);

        // Alte danach sicher freigeben.
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
                "The template could not be loaded.",
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

        SetStatus("Custom template loaded.");

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
            // Neue Preview zuerst komplett rendern.
            //
            // Scheitert das Rendering, bleibt die bisherige
            // Preview unverändert erhalten.
            nextPreview =
                ImageComposer.ComposePreview(
                    Template,
                    _item,
                    PreviewRenderSize);

            if (IsShuttingDown)
                return;

            Image? previousPreview =
                previewPictureBox.Image;

            // Erst neues Bild übernehmen.
            previewPictureBox.Image = nextPreview;

            // Ownership wurde jetzt an MainForm/PictureBox übertragen.
            nextPreview = null;

            // Danach alte Preview freigeben.
            previousPreview?.Dispose();

            UpdatePreviewStatus();
        }
        catch (ObjectDisposedException)
            when (IsShuttingDown)
        {
            // Beim Beenden bewusst ignorieren.
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
            // Falls die Bitmap noch nicht ans PictureBox übergeben
            // wurde, gehört sie weiterhin dieser Methode.
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
                "please select item image");

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

        // Normalisierte ID zurück ins Textfeld schreiben.
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

            // Exportbild wird ausschließlich hier erzeugt.
            //
            // Es gibt daher kein dauerhaftes _result-Feld mehr.
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
            // Programm wird gerade beendet.
        }
        catch (Exception ex)
        {
            if (IsShuttingDown)
                return;

            ShowError(
                "The PNG could not be saved.",
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

        try
        {
            result =
                await GitHubUpdateService.CheckAsync(
                    AppInfo.CurrentVersion);
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

        // Entscheidend:
        // Während des await könnte das Fenster geschlossen worden sein.
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
                    "Enter GitHubRepositoryUrl in " +
                    "Configuration/AppSettings.cs.";

                break;

            case GitHubUpdateState.UpToDate:
                githubStatusLabel.Text =
                    "GitHub: up to date";

                githubStatusLabel.ToolTipText =
                    string.IsNullOrWhiteSpace(result.Tag)
                        ? "No newer release version found."
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

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = _latestReleaseUrl,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
            when (ex is
                InvalidOperationException or
                System.ComponentModel.Win32Exception)
        {
            SetStatus(
                "Could not open GitHub release.");
        }
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

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

        // Deutsche Sonderzeichen möglichst sinnvoll
        // auf Item-ID-kompatible Zeichen reduzieren.
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

            // Diakritische Zeichen entfernen:
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
}