namespace DigitalContactSheet.Core.Model;

/// <summary>A page size stored canonically in millimetres (portrait dimensions).</summary>
public readonly record struct PageSize(double WidthMm, double HeightMm)
{
    /// <summary>Returns the effective size after applying orientation.</summary>
    public PageSize WithOrientation(Orientation orientation) => orientation == Orientation.Portrait
        ? this
        : new PageSize(HeightMm, WidthMm);

    public double WidthInches => WidthMm / 25.4;

    public double HeightInches => HeightMm / 25.4;

    /// <summary>Resolves the preset (or custom dimensions) from the supplied options.</summary>
    public static PageSize FromPreset(PageSizePreset preset, Length customWidth, Length customHeight) => preset switch
    {
        PageSizePreset.A5 => new PageSize(148, 210),
        PageSizePreset.A4 => new PageSize(210, 297),
        PageSizePreset.A3 => new PageSize(297, 420),
        PageSizePreset.Letter => new PageSize(215.9, 279.4),
        PageSizePreset.Legal => new PageSize(215.9, 355.6),
        PageSizePreset.Tabloid => new PageSize(279.4, 431.8),
        PageSizePreset.Custom => new PageSize(customWidth.ToInches() * 25.4, customHeight.ToInches() * 25.4),
        _ => new PageSize(210, 297),
    };
}
