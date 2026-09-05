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

    // Normalized inner paper area of the recipe template.
    //
    // Reference size: 128x128 template.
    private const float PaperX = 20f / 128f;
    private const float PaperY = 32f / 128f;
    private const float PaperW = 88f / 128f;
    private const float PaperH = 65f / 128f;

    private const float DefaultDpi = 96f;

    // Prevents accidentally creating extremely large output images.
    private const int MaxOutputSize = 4096;

    // Alpha values <= this threshold are treated as transparent when trimming.
    private const byte AlphaThreshold = 5;

    // By default, an item occupies at most 72% of the paper area.
    private const float DefaultItemAreaFactor = 0.72f;

    // Visible image bounds are calculated only once per bitmap.
    //
    // ConditionalWeakTable does not keep the bitmap alive artificially.
    private static readonly ConditionalWeakTable<Bitmap, VisibleBoundsCacheEntry>
        VisibleBoundsCache = [];

    // ---------------------------------------------------------------------
    // Image loading
    // ---------------------------------------------------------------------

    /// <summary>
    /// Loads PNG, JPG/JPEG, WebP, and other image formats supported by SkiaSharp.

    ///
    /// The returned bitmap is completely independent of the source file
    /// and does not keep any file or stream lock open.
    ///
    /// Ownership:
    /// The caller must dispose the returned bitmap.
    /// </summary>
    public static Bitmap LoadUnlocked(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "No image file was specified.",
                nameof(fileName));
        }

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException(
                "The image file was not found.",
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
                SKBitmap.Decode(fileStream) ?? throw new InvalidDataException(
                    "The image could not be loaded or decoded. " +
                    "The file may be corrupted or the " +
                    "image format may not be supported.");
            if (skBitmap.Width <= 0 ||
                skBitmap.Height <= 0)
            {
                throw new InvalidDataException(
                    "The loaded image has invalid dimensions.");
            }

            using SKImage skImage =
                SKImage.FromBitmap(skBitmap);

            using SKData? pngData =
                skImage.Encode(
                    SKEncodedImageFormat.Png,
                    100) ?? throw new InvalidDataException(
                    "The loaded image could not be processed.");
            using MemoryStream pngStream = new();

            pngData.SaveTo(pngStream);

            pngStream.Position = 0;

            using System.Drawing.Image temporaryImage =
                System.Drawing.Image.FromStream(
                    pngStream,
                    useEmbeddedColorManagement: true,
                    validateImageData: true);

            // Image.FromStream internally keeps a connection to the stream.
            // Therefore, a completely independent ARGB bitmap is created.
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
                "The image could not be loaded or decoded.",
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
    /// Removes the cached visible bounds of a bitmap.
    ///
    /// Normally not required.
    /// Call this only if the contents of the same bitmap instance were modified
    /// after the bounds had already been calculated.
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

            // Ownership is transferred to the caller.
            return canvas;
        }
        catch
        {
            // The canvas has not been returned yet and therefore
            // is still owned by this method.
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

        // The image is completely transparent.
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

        // Default: exactly centered within the paper area.
        float x =
            paper.Left +
            (paper.Width - width) / 2f;

        float y =
            paper.Top +
            (paper.Height - height) / 2f;

        // User-defined offset relative to the paper area.
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
            // The item must not be drawn outside the actual
            // recipe paper area.
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
            paper.Left + paper.Width * 0.12f,
            paper.Top + paper.Height * 0.12f,
            paper.Width * 0.76f,
            paper.Height * 0.76f);

        float borderWidth =
            Math.Max(
                1f,
                outputSize / 300f);

        float radius =
            Math.Max(
                6f,
                outputSize * 0.018f);

        using SolidBrush fillBrush = new(
            Color.FromArgb(
                22,
                255,
                255,
                255));

        using Pen borderPen = new(
            Color.FromArgb(
                90,
                110,
                110,
                110),
            borderWidth)
        {
            DashStyle = DashStyle.Dash
        };

        using SolidBrush iconBrush = new(
            Color.FromArgb(
                135,
                115,
                115,
                115));

        using SolidBrush titleBrush = new(
            Color.FromArgb(
                180,
                70,
                70,
                70));

        using SolidBrush subtitleBrush = new(
            Color.FromArgb(
                130,
                95,
                95,
                95));

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

        // ------------------------------------------------------------
        // Icon
        // ------------------------------------------------------------

        float iconWidth =
            box.Width * 0.22f;

        float iconHeight =
            iconWidth * 0.78f;

        RectangleF iconRect = new(
            box.Left +
            (box.Width - iconWidth) / 2f,

            box.Top +
            box.Height * 0.13f,

            iconWidth,
            iconHeight);

        DrawImageIcon(
            graphics,
            iconBrush,
            iconRect,
            Math.Max(
                1.4f,
                outputSize / 240f));

        // ------------------------------------------------------------
        // Text
        // ------------------------------------------------------------

        const string titleText =
            "Select item image";

        const string subtitleText =
            "PNG · JPG · WebP";

        RectangleF titleRect = new(
            box.Left + box.Width * 0.06f,
            box.Top + box.Height * 0.50f,
            box.Width * 0.88f,
            box.Height * 0.18f);

        RectangleF subtitleRect = new(
            box.Left + box.Width * 0.06f,
            box.Top + box.Height * 0.68f,
            box.Width * 0.88f,
            box.Height * 0.13f);

        using Font titleFont =
            CreateFittingFont(
                graphics,
                titleText,
                "Segoe UI",
                FontStyle.Bold,
                Math.Max(
                    10f,
                    outputSize / 29f),
                Math.Max(
                    7f,
                    outputSize / 48f),
                titleRect.Width);

        using Font subtitleFont =
            CreateFittingFont(
                graphics,
                subtitleText,
                "Segoe UI",
                FontStyle.Regular,
                Math.Max(
                    8f,
                    outputSize / 39f),
                Math.Max(
                    6f,
                    outputSize / 58f),
                subtitleRect.Width);

        using StringFormat textFormat = new()
        {
            Alignment =
                StringAlignment.Center,

            LineAlignment =
                StringAlignment.Center,

            Trimming =
                StringTrimming.None,

            FormatFlags =
                StringFormatFlags.NoWrap
        };

        graphics.DrawString(
            titleText,
            titleFont,
            titleBrush,
            titleRect,
            textFormat);

        graphics.DrawString(
            subtitleText,
            subtitleFont,
            subtitleBrush,
            subtitleRect,
            textFormat);
    }

    private static Font CreateFittingFont(
        Graphics graphics,
        string text,
        string fontFamily,
        FontStyle style,
        float preferredSize,
        float minimumSize,
        float maximumWidth)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        float safeMinimumSize =
            Math.Max(
                1f,
                minimumSize);

        float size =
            Math.Max(
                preferredSize,
                safeMinimumSize);

        while (size > safeMinimumSize)
        {
            using Font testFont = new(
                fontFamily,
                size,
                style,
                GraphicsUnit.Pixel);

            SizeF measured =
                graphics.MeasureString(
                    text,
                    testFont,
                    int.MaxValue,
                    StringFormat.GenericTypographic);

            if (measured.Width <= maximumWidth)
            {
                return new Font(
                    fontFamily,
                    size,
                    style,
                    GraphicsUnit.Pixel);
            }

            size -= 0.5f;
        }

        return new Font(
            fontFamily,
            safeMinimumSize,
            style,
            GraphicsUnit.Pixel);
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
                        // Format32bppArgb is stored in memory as BGRA.
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
                "The source image has invalid dimensions.",
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
                "The image has invalid dimensions.",
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
                "The output size must be greater than 0 pixels.");
        }

        if (outputSize > MaxOutputSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputSize),
                outputSize,
                $"The output size must not exceed {MaxOutputSize}px.");
        }
    }

    // ---------------------------------------------------------------------
    // Cache entry
    // ---------------------------------------------------------------------

    private sealed class VisibleBoundsCacheEntry(
        Rectangle bounds)
    {
        public Rectangle Bounds { get; } = bounds;
    }
}

/// <summary>
/// Size and position of the item image within the recipe.
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