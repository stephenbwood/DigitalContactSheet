namespace DigitalContactSheet.Core.Model;

/// <summary>
/// The complete set of user-controlled settings for a contact sheet. This is a mutable
/// POCO so it can be bound directly by the GUI; <see cref="Clone"/> produces a snapshot.
/// </summary>
public sealed class ContactSheetOptions
{
    // ---- Layout ----------------------------------------------------------
    public PageSizePreset PageSizePreset { get; set; } = PageSizePreset.A4;

    public Orientation Orientation { get; set; } = Orientation.Portrait;

    public Length CustomPageWidth { get; set; } = Length.Mm(210);

    public Length CustomPageHeight { get; set; } = Length.Mm(297);

    /// <summary>Output resolution in dots per inch. Also drives raster page pixel dimensions.</summary>
    public int Resolution { get; set; } = 300;

    public Length PageMargin { get; set; } = Length.Mm(10);

    public Length ImageSpacing { get; set; } = Length.Mm(5);

    public int Columns { get; set; } = 4;

    public int Rows { get; set; } = 5;

    // ---- Appearance ------------------------------------------------------
    public RgbaColor BackgroundColor { get; set; } = RgbaColor.White;

    public RgbaColor TextColor { get; set; } = RgbaColor.Black;

    public ZoomMode ZoomMode { get; set; } = ZoomMode.Fit;

    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Center;

    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Middle;

    // ---- Typography ------------------------------------------------------
    public string FontFamily { get; set; } = "Inter";

    public FontWeightOption FontWeight { get; set; } = FontWeightOption.Regular;

    /// <summary>Font size in points.</summary>
    public double FontSize { get; set; } = 10;

    // ---- Captions --------------------------------------------------------
    public bool ShowCaptions { get; set; } = true;

    /// <summary>Whether the file extension is included in the caption.</summary>
    public bool ShowFileExtensions { get; set; } = true;

    /// <summary>Vertical gap between the bottom of the image area and the caption text.</summary>
    public Length CaptionGap { get; set; } = Length.Mm(2);

    // ---- Output ----------------------------------------------------------
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Pdf;

    /// <summary>JPEG encoder quality, 1-100.</summary>
    public int JpgQuality { get; set; } = 90;

    public OutputMode OutputMode { get; set; } = OutputMode.MirrorUnderOutputDirectory;

    public string OutputDirectory { get; set; } = string.Empty;

    public string FileNamePrefix { get; set; } = "contact-sheet";

    /// <summary>Zero-padding width for the page number in raster filenames.</summary>
    public int PageNumberDigits { get; set; } = 3;

    // ---- Batch -----------------------------------------------------------
    public bool RecurseSubfolders { get; set; }

    public SortOrder SortOrder { get; set; } = SortOrder.NameNatural;

    /// <summary>Resolved page size (preset or custom), already oriented.</summary>
    public PageSize ResolvePageSize() =>
        PageSize.FromPreset(PageSizePreset, CustomPageWidth, CustomPageHeight).WithOrientation(Orientation);

    public ContactSheetOptions Clone() => (ContactSheetOptions)MemberwiseClone();

    /// <summary>Maps the UI weight option onto a numeric CSS-style weight.</summary>
    public static int WeightToNumeric(FontWeightOption weight) => weight switch
    {
        FontWeightOption.Light => 300,
        FontWeightOption.Regular => 400,
        FontWeightOption.Medium => 500,
        FontWeightOption.Bold => 700,
        FontWeightOption.Black => 900,
        _ => 400,
    };
}
