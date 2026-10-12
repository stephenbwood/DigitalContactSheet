namespace DigitalContactSheet.Core.Model;

/// <summary>Unit of measure for user-entered lengths.</summary>
public enum Unit
{
    Millimeters,
    Inches,
    Points,
}

/// <summary>Page orientation. Swaps the width/height of the page size.</summary>
public enum Orientation
{
    Portrait,
    Landscape,
}

/// <summary>How an image is scaled to fill its cell's image area.</summary>
public enum ZoomMode
{
    /// <summary>Letterbox: the whole image is visible, scaled to fit inside the area.</summary>
    Fit,

    /// <summary>Crop: the image covers the area, overflowing edges are clipped.</summary>
    Fill,
}

/// <summary>Horizontal placement of the image within its image area.</summary>
public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>Vertical placement of the image within its image area.</summary>
public enum VerticalAlignment
{
    Top,
    Middle,
    Bottom,
}

/// <summary>Font weight choices exposed in the UI.</summary>
public enum FontWeightOption
{
    Light,
    Regular,
    Medium,
    Bold,
    Black,
}

/// <summary>Standard page sizes plus a custom option.</summary>
public enum PageSizePreset
{
    A5,
    A4,
    A3,
    Letter,
    Legal,
    Tabloid,
    Custom,
}

/// <summary>Output file format.</summary>
public enum OutputFormat
{
    /// <summary>One multi-page PDF per source folder.</summary>
    Pdf,

    /// <summary>One single-page JPEG per page.</summary>
    Jpg,

    /// <summary>One single-page TIFF per page.</summary>
    Tiff,
}

/// <summary>Where batch output is written.</summary>
public enum OutputMode
{
    /// <summary>Mirror the source folder tree beneath the chosen output directory.</summary>
    MirrorUnderOutputDirectory,

    /// <summary>Write output files into each source folder.</summary>
    IntoSourceFolder,
}

/// <summary>Ordering applied to the scanned image files.</summary>
public enum SortOrder
{
    /// <summary>Case-insensitive, numeric-aware filename ordering (img2 before img10).</summary>
    NameNatural,

    /// <summary>Most recently modified first.</summary>
    DateModified,

    /// <summary>Largest file first.</summary>
    Size,
}
