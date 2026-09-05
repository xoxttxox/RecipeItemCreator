using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SkiaSharp;

namespace RecipeItemCreator.Services;

internal static class ImageComposer
{
    // ---------------------------------------------------------------------
    // Recipe template layout
    // ---------------------------------------------------------------------

    // Normalisierter innerer Papierbereich der Rezeptvorlage.
    //
    // Ausgangsbasis: 128x128 Template.
    private const float PaperX = 20f / 128f;
    private const float PaperY = 32f / 128f;
    private const float PaperW = 88f / 128f;
    private const float PaperH = 65f / 128f;

    private const float DefaultDpi = 96f;

    // Verhindert versehentlich extrem große Ausgabebilder.
    private const int MaxOutputSize = 4096;

    // Alpha <= diesem Wert gilt beim Zuschneiden als transparent.
    private const byte AlphaThreshold = 5;

    // Standardmäßig belegt ein Item maximal 72 % des Papierbereichs.
    private const float DefaultItemAreaFactor = 0.72f;

    // Sichtbare Bildbereiche werden pro Bitmap nur einmal berechnet.
    //
    // ConditionalWeakTable hält die Bitmap nicht künstlich am Leben.
    private static readonly ConditionalWeakTable<Bitmap, VisibleBoundsCacheEntry>
        VisibleBoundsCache = new();

    // ---------------------------------------------------------------------
    // Image loading
    // ---------------------------------------------------------------------

    /// <summary>
    /// Lädt PNG, JPG/JPEG, WebP und weitere von SkiaSharp unterstützte
    /// Bildformate.
    ///
    /// Die zurückgegebene Bitmap ist vollständig unabhängig von der
    /// Quelldatei und besitzt keinen offenen File-/Stream-Lock.
    ///
    /// Ownership:
    /// Der Aufrufer muss die zurückgegebene Bitmap Dispose()n.
    /// </summary>
    public static Bitmap LoadUnlocked(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "Es wurde keine Bilddatei angegeben.",
                nameof(fileName));
        }

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException(
                "Die Bilddatei wurde nicht gefunden.",
                fileName);
        }

        try
        {
            using FileStream fileStream = new(
                fileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            using SKBitmap? skBitmap =
                SKBitmap.Decode(fileStream);

            if (skBitmap is null)
            {
                throw new InvalidDataException(
                    "Das Bild konnte nicht geladen oder dekodiert werden. " +
                    "Die Datei ist möglicherweise beschädigt oder das " +
                    "Bildformat wird nicht unterstützt.");
            }

            if (skBitmap.Width <= 0 ||
                skBitmap.Height <= 0)
            {
                throw new InvalidDataException(
                    "Das geladene Bild besitzt ungültige Abmessungen.");
            }

            using SKImage skImage =
                SKImage.FromBitmap(skBitmap);

            using SKData? pngData =
                skImage.Encode(
                    SKEncodedImageFormat.Png,
                    100);

            if (pngData is null)
            {
                throw new InvalidDataException(
                    "Das geladene Bild konnte nicht verarbeitet werden.");
            }

            using MemoryStream pngStream = new();

            pngData.SaveTo(pngStream);

            pngStream.Position = 0;

            using System.Drawing.Image temporaryImage =
                System.Drawing.Image.FromStream(
                    pngStream,
                    useEmbeddedColorManagement: true,
                    validateImageData: true);

            // Image.FromStream hält intern eine Verbindung zum Stream.
            // Deshalb wird eine vollständig unabhängige ARGB-Bitmap erzeugt.
            return CloneToArgb(temporaryImage);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                "Das Bild konnte nicht geladen oder dekodiert werden.",
                ex);
        }
    }

    // ---------------------------------------------------------------------
    // Final composition
    // ---------------------------------------------------------------------

    public static Bitmap Compose(
        Bitmap template,
        Bitmap item,
        int outputSize)
    {
        return Compose(
            template,
            item,
            outputSize,
            ItemPlacement.Default);
    }

    public static Bitmap Compose(
        Bitmap template,
        Bitmap item,
        int outputSize,
        ItemPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(item);

        return ComposeInternal(
            template,
            item,
            outputSize,
            placement.Normalize(),
            drawPlaceholder: false);
    }

    // ---------------------------------------------------------------------
    // Preview
    // ---------------------------------------------------------------------

    public static Bitmap ComposePreview(
        Bitmap template,
        Bitmap? item,
        int outputSize)
    {
        return ComposePreview(
            template,
            item,
            outputSize,
            ItemPlacement.Default);
    }

    public static Bitmap ComposePreview(
        Bitmap template,
        Bitmap? item,
        int outputSize,
        ItemPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(template);

        return ComposeInternal(
            template,
            item,
            outputSize,
            placement.Normalize(),
            drawPlaceholder: true);
    }

    // ---------------------------------------------------------------------
    // Bounds cache
    // ---------------------------------------------------------------------

    /// <summary>
    /// Entfernt die gespeicherten sichtbaren Grenzen einer Bitmap.
    ///
    /// Normalerweise nicht notwendig.
    /// Nur aufrufen, wenn der Inhalt derselben Bitmap-Instanz nachträglich
    /// verändert wurde.
    /// </summary>
    public static void InvalidateVisibleBounds(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        VisibleBoundsCache.Remove(image);
    }

    // ---------------------------------------------------------------------
    // Composition core
    // ---------------------------------------------------------------------

    private static Bitmap ComposeInternal(
        Bitmap template,
        Bitmap? item,
        int outputSize,
        ItemPlacement placement,
        bool drawPlaceholder)
    {
        ValidateOutputSize(outputSize);

        ValidateBitmap(
            template,
            nameof(template));

        if (item is not null)
        {
            ValidateBitmap(
                item,
                nameof(item));
        }

        Bitmap canvas = new(
            outputSize,
            outputSize,
            PixelFormat.Format32bppArgb);

        try
        {
            canvas.SetResolution(
                DefaultDpi,
                DefaultDpi);

            using Graphics graphics =
                Graphics.FromImage(canvas);

            ConfigureGraphics(graphics);

            graphics.Clear(Color.Transparent);

            DrawTemplate(
                graphics,
                template,
                outputSize);

            if (item is not null)
            {
                DrawItem(
                    graphics,
                    item,
                    outputSize,
                    placement);
            }
            else if (drawPlaceholder)
            {
                DrawPlaceholder(
                    graphics,
                    outputSize);
            }

            // Ownership geht an den Aufrufer.
            return canvas;
        }
        catch
        {
            // Canvas wurde noch nicht zurückgegeben und gehört daher
            // weiterhin dieser Methode.
            canvas.Dispose();

            throw;
        }
    }

    private static void ConfigureGraphics(
        Graphics graphics)
    {
        graphics.CompositingMode =
            CompositingMode.SourceOver;

        graphics.CompositingQuality =
            CompositingQuality.HighQuality;

        graphics.InterpolationMode =
            InterpolationMode.HighQualityBicubic;

        graphics.SmoothingMode =
            SmoothingMode.HighQuality;

        graphics.PixelOffsetMode =
            PixelOffsetMode.HighQuality;

        graphics.TextRenderingHint =
            TextRenderingHint.AntiAliasGridFit;
    }

    // ---------------------------------------------------------------------
    // Template
    // ---------------------------------------------------------------------

    private static void DrawTemplate(
        Graphics graphics,
        Bitmap template,
        int outputSize)
    {
        Rectangle destination = new(
            0,
            0,
            outputSize,
            outputSize);

        Rectangle source = new(
            0,
            0,
            template.Width,
            template.Height);

        graphics.DrawImage(
            template,
            destination,
            source,
            GraphicsUnit.Pixel);
    }

    // ---------------------------------------------------------------------
    // Item
    // ---------------------------------------------------------------------

    private static void DrawItem(
        Graphics graphics,
        Bitmap item,
        int outputSize,
        ItemPlacement placement)
    {
        Rectangle visibleBounds =
            GetVisibleBounds(item);

        // Bild ist vollständig transparent.
        if (visibleBounds.IsEmpty)
            return;

        RectangleF paper =
            GetPaperArea(outputSize);

        float maxWidth =
            paper.Width * DefaultItemAreaFactor;

        float maxHeight =
            paper.Height * DefaultItemAreaFactor;

        float widthScale =
            maxWidth / visibleBounds.Width;

        float heightScale =
            maxHeight / visibleBounds.Height;

        float fitScale =
            Math.Min(
                widthScale,
                heightScale);

        float scale =
            fitScale * placement.Scale;

        if (!float.IsFinite(scale) ||
            scale <= 0f)
        {
            return;
        }

        float width =
            Math.Max(
                1f,
                visibleBounds.Width * scale);

        float height =
            Math.Max(
                1f,
                visibleBounds.Height * scale);

        // Standard: exakt mittig im Papierbereich.
        float x =
            paper.Left +
            (paper.Width - width) / 2f;

        float y =
            paper.Top +
            (paper.Height - height) / 2f;

        // Benutzerdefinierte Verschiebung relativ zum Papierbereich.
        x +=
            paper.Width * placement.OffsetX;

        y +=
            paper.Height * placement.OffsetY;

        RectangleF destination = new(
            x,
            y,
            width,
            height);

        GraphicsState state =
            graphics.Save();

        try
        {
            // Das Item darf nicht außerhalb des eigentlichen
            // Rezept-Papierbereichs gezeichnet werden.
            graphics.SetClip(
                paper,
                CombineMode.Intersect);

            graphics.DrawImage(
                item,
                destination,
                visibleBounds,
                GraphicsUnit.Pixel);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static RectangleF GetPaperArea(
        int outputSize)
    {
        return new RectangleF(
            PaperX * outputSize,
            PaperY * outputSize,
            PaperW * outputSize,
            PaperH * outputSize);
    }

    // ---------------------------------------------------------------------
    // Placeholder
    // ---------------------------------------------------------------------

    private static void DrawPlaceholder(
        Graphics graphics,
        int outputSize)
    {
        RectangleF paper =
            GetPaperArea(outputSize);

        RectangleF box = new(
            paper.Left + paper.Width * 0.13f,
            paper.Top + paper.Height * 0.14f,
            paper.Width * 0.74f,
            paper.Height * 0.72f);

        using SolidBrush fillBrush = new(
            Color.FromArgb(
                36,
                255,
                255,
                255));

        using Pen borderPen = new(
            Color.FromArgb(
                115,
                92,
                101,
                112),
            Math.Max(
                1f,
                outputSize / 256f))
        {
            DashStyle = DashStyle.Dash
        };

        using SolidBrush accentBrush = new(
            Color.FromArgb(
                155,
                92,
                101,
                112));

        using SolidBrush titleBrush = new(
            Color.FromArgb(
                155,
                46,
                50,
                56));

        using SolidBrush bodyBrush = new(
            Color.FromArgb(
                125,
                78,
                83,
                90));

        float radius =
            Math.Max(
                10f,
                outputSize * 0.03f);

        using GraphicsPath path =
            CreateRoundedRect(
                box,
                radius);

        graphics.FillPath(
            fillBrush,
            path);

        graphics.DrawPath(
            borderPen,
            path);

        float iconSize =
            box.Width * 0.23f;

        RectangleF iconRect = new(
            box.Left +
            (box.Width - iconSize) / 2f,

            box.Top +
            box.Height * 0.10f,

            iconSize,

            iconSize * 0.78f);

        DrawImageIcon(
            graphics,
            accentBrush,
            iconRect,
            Math.Max(
                2f,
                outputSize / 170f));

        using Font titleFont = new(
            "Segoe UI",
            Math.Max(
                10f,
                outputSize / 23f),
            FontStyle.Bold,
            GraphicsUnit.Pixel);

        using Font bodyFont = new(
            "Segoe UI",
            Math.Max(
                8f,
                outputSize / 34f),
            FontStyle.Regular,
            GraphicsUnit.Pixel);

        using StringFormat stringFormat = new()
        {
            Alignment =
                StringAlignment.Center,

            LineAlignment =
                StringAlignment.Center,

            Trimming =
                StringTrimming.EllipsisCharacter
        };

        RectangleF titleRect = new(
            box.Left + box.Width * 0.08f,
            box.Top + box.Height * 0.44f,
            box.Width * 0.84f,
            box.Height * 0.16f);

        RectangleF bodyRect = new(
            box.Left + box.Width * 0.08f,
            box.Top + box.Height * 0.58f,
            box.Width * 0.84f,
            box.Height * 0.18f);

        graphics.DrawString(
            "Item-Bild auswählen",
            titleFont,
            titleBrush,
            titleRect,
            stringFormat);

        graphics.DrawString(
            "PNG, JPG oder WebP",
            bodyFont,
            bodyBrush,
            bodyRect,
            stringFormat);
    }

    private static void DrawImageIcon(
        Graphics graphics,
        Brush brush,
        RectangleF rect,
        float stroke)
    {
        using Pen pen = new(
            brush,
            stroke)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float radius =
            rect.Width * 0.09f;

        using GraphicsPath frame =
            CreateRoundedRect(
                rect,
                radius);

        graphics.DrawPath(
            pen,
            frame);

        RectangleF sun = new(
            rect.Left +
            rect.Width * 0.62f,

            rect.Top +
            rect.Height * 0.18f,

            rect.Width * 0.12f,

            rect.Width * 0.12f);

        graphics.FillEllipse(
            brush,
            sun);

        PointF p1 = new(
            rect.Left + rect.Width * 0.16f,
            rect.Bottom - rect.Height * 0.18f);

        PointF p2 = new(
            rect.Left + rect.Width * 0.40f,
            rect.Top + rect.Height * 0.54f);

        PointF p3 = new(
            rect.Left + rect.Width * 0.57f,
            rect.Bottom - rect.Height * 0.18f);

        PointF p4 = new(
            rect.Left + rect.Width * 0.66f,
            rect.Top + rect.Height * 0.42f);

        PointF p5 = new(
            rect.Right - rect.Width * 0.12f,
            rect.Bottom - rect.Height * 0.18f);

        graphics.DrawLines(
            pen,
            [
                p1,
                p2,
                p3,
                p4,
                p5
            ]);
    }

    // ---------------------------------------------------------------------
    // Geometry
    // ---------------------------------------------------------------------

    private static GraphicsPath CreateRoundedRect(
        RectangleF rect,
        float radius)
    {
        GraphicsPath path = new();

        if (rect.Width <= 0f ||
            rect.Height <= 0f)
        {
            return path;
        }

        float maximumRadius =
            Math.Min(
                rect.Width,
                rect.Height) / 2f;

        radius =
            Math.Clamp(
                radius,
                0f,
                maximumRadius);

        if (radius <= 0f)
        {
            path.AddRectangle(rect);

            return path;
        }

        float diameter =
            radius * 2f;

        path.AddArc(
            rect.Left,
            rect.Top,
            diameter,
            diameter,
            180,
            90);

        path.AddArc(
            rect.Right - diameter,
            rect.Top,
            diameter,
            diameter,
            270,
            90);

        path.AddArc(
            rect.Right - diameter,
            rect.Bottom - diameter,
            diameter,
            diameter,
            0,
            90);

        path.AddArc(
            rect.Left,
            rect.Bottom - diameter,
            diameter,
            diameter,
            90,
            90);

        path.CloseFigure();

        return path;
    }

    // ---------------------------------------------------------------------
    // Transparent bounds
    // ---------------------------------------------------------------------

    private static Rectangle GetVisibleBounds(
        Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        VisibleBoundsCacheEntry cacheEntry =
            VisibleBoundsCache.GetValue(
                image,
                static bitmap =>
                    new VisibleBoundsCacheEntry(
                        CalculateVisibleBounds(bitmap)));

        return cacheEntry.Bounds;
    }

    private static Rectangle CalculateVisibleBounds(
        Bitmap image)
    {
        ValidateBitmap(
            image,
            nameof(image));

        Bitmap? convertedBitmap = null;

        Bitmap source;

        if (image.PixelFormat == PixelFormat.Format32bppArgb)
        {
            source = image;
        }
        else
        {
            convertedBitmap =
                CloneToArgb(image);

            source =
                convertedBitmap;
        }

        try
        {
            Rectangle imageRectangle = new(
                0,
                0,
                source.Width,
                source.Height);

            BitmapData? bitmapData = null;

            try
            {
                bitmapData =
                    source.LockBits(
                        imageRectangle,
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);

                int bytesPerPixel = 4;

                int rowLength =
                    checked(
                        source.Width *
                        bytesPerPixel);

                byte[] rowBuffer =
                    new byte[rowLength];

                int left =
                    source.Width;

                int top =
                    source.Height;

                int right = -1;
                int bottom = -1;

                for (int y = 0;
                     y < source.Height;
                     y++)
                {
                    nint rowPointer =
                        IntPtr.Add(
                            bitmapData.Scan0,
                            checked(
                                y *
                                bitmapData.Stride));

                    Marshal.Copy(
                        rowPointer,
                        rowBuffer,
                        0,
                        rowLength);

                    for (int x = 0;
                         x < source.Width;
                         x++)
                    {
                        // Format32bppArgb liegt im Speicher als BGRA.
                        int pixelOffset =
                            x * bytesPerPixel;

                        byte alpha =
                            rowBuffer[
                                pixelOffset + 3];

                        if (alpha <= AlphaThreshold)
                            continue;

                        if (x < left)
                            left = x;

                        if (x > right)
                            right = x;

                        if (y < top)
                            top = y;

                        if (y > bottom)
                            bottom = y;
                    }
                }

                if (right < left ||
                    bottom < top)
                {
                    return Rectangle.Empty;
                }

                return Rectangle.FromLTRB(
                    left,
                    top,
                    right + 1,
                    bottom + 1);
            }
            finally
            {
                if (bitmapData is not null)
                {
                    source.UnlockBits(bitmapData);
                }
            }
        }
        finally
        {
            convertedBitmap?.Dispose();
        }
    }

    // ---------------------------------------------------------------------
    // Independent System.Drawing bitmap
    // ---------------------------------------------------------------------

    private static Bitmap CloneToArgb(
        System.Drawing.Image source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Width <= 0 ||
            source.Height <= 0)
        {
            throw new ArgumentException(
                "Das Quellbild besitzt ungültige Abmessungen.",
                nameof(source));
        }

        Bitmap result = new(
            source.Width,
            source.Height,
            PixelFormat.Format32bppArgb);

        try
        {
            float dpiX =
                IsValidDpi(
                    source.HorizontalResolution)
                    ? source.HorizontalResolution
                    : DefaultDpi;

            float dpiY =
                IsValidDpi(
                    source.VerticalResolution)
                    ? source.VerticalResolution
                    : DefaultDpi;

            result.SetResolution(
                dpiX,
                dpiY);

            using Graphics graphics =
                Graphics.FromImage(result);

            graphics.Clear(
                Color.Transparent);

            graphics.CompositingMode =
                CompositingMode.SourceCopy;

            graphics.CompositingQuality =
                CompositingQuality.HighQuality;

            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;

            graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            graphics.DrawImage(
                source,
                new Rectangle(
                    0,
                    0,
                    result.Width,
                    result.Height),
                new Rectangle(
                    0,
                    0,
                    source.Width,
                    source.Height),
                GraphicsUnit.Pixel);

            return result;
        }
        catch
        {
            result.Dispose();

            throw;
        }
    }

    private static bool IsValidDpi(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value > 0f;
    }

    // ---------------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------------

    private static void ValidateBitmap(
        Bitmap bitmap,
        string parameterName)
    {
        if (bitmap.Width <= 0 ||
            bitmap.Height <= 0)
        {
            throw new ArgumentException(
                "Das Bild besitzt ungültige Abmessungen.",
                parameterName);
        }
    }

    private static void ValidateOutputSize(
        int outputSize)
    {
        if (outputSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputSize),
                outputSize,
                "Die Ausgabegröße muss größer als 0 Pixel sein.");
        }

        if (outputSize > MaxOutputSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputSize),
                outputSize,
                $"Die Ausgabegröße darf {MaxOutputSize}px nicht überschreiten.");
        }
    }

    // ---------------------------------------------------------------------
    // Cache entry
    // ---------------------------------------------------------------------

    private sealed class VisibleBoundsCacheEntry
    {
        public VisibleBoundsCacheEntry(
            Rectangle bounds)
        {
            Bounds = bounds;
        }

        public Rectangle Bounds { get; }
    }
}

/// <summary>
/// Größe und Position des Item-Bildes innerhalb des Rezeptes.
/// </summary>
internal readonly record struct ItemPlacement(
    float Scale,
    float OffsetX,
    float OffsetY)
{
    public static ItemPlacement Default =>
        new(
            Scale: 1f,
            OffsetX: 0f,
            OffsetY: 0f);

    public ItemPlacement Normalize()
    {
        float normalizedScale =
            float.IsFinite(Scale)
                ? Math.Clamp(
                    Scale,
                    0.25f,
                    2.50f)
                : 1f;

        float normalizedOffsetX =
            float.IsFinite(OffsetX)
                ? Math.Clamp(
                    OffsetX,
                    -1f,
                    1f)
                : 0f;

        float normalizedOffsetY =
            float.IsFinite(OffsetY)
                ? Math.Clamp(
                    OffsetY,
                    -1f,
                    1f)
                : 0f;

        return new ItemPlacement(
            Scale: normalizedScale,
            OffsetX: normalizedOffsetX,
            OffsetY: normalizedOffsetY);
    }
}