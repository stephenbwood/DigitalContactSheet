using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DigitalContactSheet.App.Infrastructure;
using DigitalContactSheet.Core.Export;
using DigitalContactSheet.Core.Layout;
using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Rendering;
using DigitalContactSheet.Core.Scanning;
using DigitalContactSheet.Core.Settings;

namespace DigitalContactSheet.App.ViewModels;

/// <summary>Drives the main window: settings, live preview, pagination and export.</summary>
public sealed class MainViewModel : ObservableObject
{
    private const int PreviewMaxWidth = 1200;
    private const int PreviewMaxHeight = 1600;

    private readonly ExportService _exportService = new();
    private readonly ImageCache _previewCache = new();
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _exportCts;
    private bool _isLoading;

    // Source
    private string _sourceFolder = string.Empty;
    private bool _recurseSubfolders;
    private SortOrder _sortOrder = SortOrder.NameNatural;

    // Layout
    private PageSizePreset _pageSizePreset = PageSizePreset.A4;
    private Orientation _orientation = Orientation.Portrait;
    private double _customWidth = 210;
    private double _customHeight = 297;
    private Unit _customUnit = Unit.Millimeters;
    private int _resolution = 300;
    private double _pageMarginValue = 10;
    private Unit _pageMarginUnit = Unit.Millimeters;
    private double _imageSpacingValue = 5;
    private Unit _imageSpacingUnit = Unit.Millimeters;
    private int _columns = 4;
    private int _rows = 5;

    // Appearance
    private string _backgroundColorHex = "#FFFFFF";
    private string _textColorHex = "#000000";
    private ZoomMode _zoomMode = ZoomMode.Fit;
    private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Center;
    private VerticalAlignment _verticalAlignment = VerticalAlignment.Middle;

    // Typography
    private string _fontFamily = FontProvider.BundledFamily;
    private FontWeightOption _fontWeight = FontWeightOption.Regular;
    private double _fontSize = 10;
    private bool _showCaptions = true;
    private bool _showFileExtensions = true;
    private double _captionGapValue = 2;
    private Unit _captionGapUnit = Unit.Millimeters;

    // Output
    private OutputFormat _outputFormat = OutputFormat.Pdf;
    private int _jpgQuality = 90;
    private OutputMode _outputMode = OutputMode.MirrorUnderOutputDirectory;
    private string _outputDirectory = string.Empty;
    private string _fileNamePrefix = "contact-sheet";
    private int _pageNumberDigits = 3;

    // Status
    private Bitmap? _previewImage;
    private string _statusText = "Choose a source folder to begin.";
    private bool _isBusy;
    private double _progressValue;
    private int _currentPage;
    private int _pageCount;
    private string _folderSummary = string.Empty;
    private string? _errorText;
    private SourceFolder? _selectedFolder;

    public MainViewModel()
    {
        Folders = [];
        FontFamilies = FontProvider.GetAvailableFamilies();

        BrowseSourceCommand = new AsyncRelayCommand(BrowseSourceAsync);
        BrowseOutputCommand = new AsyncRelayCommand(BrowseOutputAsync);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync, CanGenerate);
        CancelCommand = new RelayCommand(CancelExport, () => IsBusy);
        PreviousPageCommand = new RelayCommand(() => CurrentPage--, () => CurrentPage > 0);
        NextPageCommand = new RelayCommand(() => CurrentPage++, () => CurrentPage < PageCount - 1);

        Folders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFolders));

        LoadSettings();

        // Reload the remembered source folder (settings are assigned directly, bypassing setters).
        if (!string.IsNullOrWhiteSpace(_sourceFolder) && Directory.Exists(_sourceFolder))
        {
            _ = RescanAsync();
        }
    }

    /// <summary>Set by the view so the view model can show the native folder picker.</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    // ---- Commands --------------------------------------------------------
    public AsyncRelayCommand BrowseSourceCommand { get; }

    public AsyncRelayCommand BrowseOutputCommand { get; }

    public AsyncRelayCommand GenerateCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand PreviousPageCommand { get; }

    public RelayCommand NextPageCommand { get; }

    // ---- Option lists for combo boxes -----------------------------------
    public IReadOnlyList<string> FontFamilies { get; }

    public PageSizePreset[] PageSizePresets { get; } = Enum.GetValues<PageSizePreset>();

    public Orientation[] Orientations { get; } = Enum.GetValues<Orientation>();

    public Unit[] Units { get; } = [Unit.Millimeters, Unit.Inches, Unit.Points];

    public ZoomMode[] ZoomModes { get; } = Enum.GetValues<ZoomMode>();

    public HorizontalAlignment[] HorizontalAlignments { get; } = Enum.GetValues<HorizontalAlignment>();

    public VerticalAlignment[] VerticalAlignments { get; } = Enum.GetValues<VerticalAlignment>();

    public FontWeightOption[] FontWeights { get; } = Enum.GetValues<FontWeightOption>();

    public OutputFormat[] OutputFormats { get; } = Enum.GetValues<OutputFormat>();

    public OutputMode[] OutputModes { get; } = Enum.GetValues<OutputMode>();

    public SortOrder[] SortOrders { get; } = Enum.GetValues<SortOrder>();

    public ObservableCollection<SourceFolder> Folders { get; }

    public bool HasFolders => Folders.Count > 0;

    // ---- Source ----------------------------------------------------------
    public string SourceFolder
    {
        get => _sourceFolder;
        set
        {
            if (Set(ref _sourceFolder, value))
            {
                _ = RescanAsync();
            }
        }
    }

    public bool RecurseSubfolders
    {
        get => _recurseSubfolders;
        set
        {
            if (Set(ref _recurseSubfolders, value))
            {
                _ = RescanAsync();
            }
        }
    }

    public SortOrder SortOrder
    {
        get => _sortOrder;
        set
        {
            if (Set(ref _sortOrder, value))
            {
                _ = RescanAsync();
            }
        }
    }

    public SourceFolder? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (Set(ref _selectedFolder, value))
            {
                CurrentPage = 0;
                GenerateCommand.RaiseCanExecuteChanged();
                SchedulePreview();
            }
        }
    }

    // ---- Layout ----------------------------------------------------------
    public PageSizePreset PageSizePreset
    {
        get => _pageSizePreset;
        set
        {
            if (Set(ref _pageSizePreset, value))
            {
                OnPropertyChanged(nameof(IsCustomPageSize));
                SchedulePreview();
            }
        }
    }

    public bool IsCustomPageSize => PageSizePreset == PageSizePreset.Custom;

    public Orientation Orientation
    {
        get => _orientation;
        set { if (Set(ref _orientation, value)) SchedulePreview(); }
    }

    public double CustomWidth
    {
        get => _customWidth;
        set { if (Set(ref _customWidth, value)) SchedulePreview(); }
    }

    public double CustomHeight
    {
        get => _customHeight;
        set { if (Set(ref _customHeight, value)) SchedulePreview(); }
    }

    public Unit CustomUnit
    {
        get => _customUnit;
        set { if (Set(ref _customUnit, value)) SchedulePreview(); }
    }

    public int Resolution
    {
        get => _resolution;
        set { if (Set(ref _resolution, Math.Clamp(value, 1, 2400))) SchedulePreview(); }
    }

    public double PageMarginValue
    {
        get => _pageMarginValue;
        set { if (Set(ref _pageMarginValue, value)) SchedulePreview(); }
    }

    public Unit PageMarginUnit
    {
        get => _pageMarginUnit;
        set { if (Set(ref _pageMarginUnit, value)) SchedulePreview(); }
    }

    public double ImageSpacingValue
    {
        get => _imageSpacingValue;
        set { if (Set(ref _imageSpacingValue, value)) SchedulePreview(); }
    }

    public Unit ImageSpacingUnit
    {
        get => _imageSpacingUnit;
        set { if (Set(ref _imageSpacingUnit, value)) SchedulePreview(); }
    }

    public int Columns
    {
        get => _columns;
        set { if (Set(ref _columns, Math.Clamp(value, 1, 50))) SchedulePreview(); }
    }

    public int Rows
    {
        get => _rows;
        set { if (Set(ref _rows, Math.Clamp(value, 1, 50))) SchedulePreview(); }
    }

    // ---- Appearance ------------------------------------------------------
    public string BackgroundColorHex
    {
        get => _backgroundColorHex;
        set { if (Set(ref _backgroundColorHex, value)) SchedulePreview(); }
    }

    public string TextColorHex
    {
        get => _textColorHex;
        set { if (Set(ref _textColorHex, value)) SchedulePreview(); }
    }

    public ZoomMode ZoomMode
    {
        get => _zoomMode;
        set { if (Set(ref _zoomMode, value)) SchedulePreview(); }
    }

    public HorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set { if (Set(ref _horizontalAlignment, value)) SchedulePreview(); }
    }

    public VerticalAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set { if (Set(ref _verticalAlignment, value)) SchedulePreview(); }
    }

    // ---- Typography ------------------------------------------------------
    public string FontFamily
    {
        get => _fontFamily;
        set { if (Set(ref _fontFamily, value)) SchedulePreview(); }
    }

    public FontWeightOption FontWeight
    {
        get => _fontWeight;
        set { if (Set(ref _fontWeight, value)) SchedulePreview(); }
    }

    public double FontSize
    {
        get => _fontSize;
        set { if (Set(ref _fontSize, value)) SchedulePreview(); }
    }

    public bool ShowCaptions
    {
        get => _showCaptions;
        set { if (Set(ref _showCaptions, value)) SchedulePreview(); }
    }

    public bool ShowFileExtensions
    {
        get => _showFileExtensions;
        set { if (Set(ref _showFileExtensions, value)) SchedulePreview(); }
    }

    public double CaptionGapValue
    {
        get => _captionGapValue;
        set { if (Set(ref _captionGapValue, value)) SchedulePreview(); }
    }

    public Unit CaptionGapUnit
    {
        get => _captionGapUnit;
        set { if (Set(ref _captionGapUnit, value)) SchedulePreview(); }
    }

    // ---- Output ----------------------------------------------------------
    public OutputFormat OutputFormat
    {
        get => _outputFormat;
        set
        {
            if (Set(ref _outputFormat, value))
            {
                OnPropertyChanged(nameof(IsJpeg));
                OnPropertyChanged(nameof(IsRaster));
            }
        }
    }

    public bool IsJpeg => OutputFormat == OutputFormat.Jpg;

    public bool IsRaster => OutputFormat is OutputFormat.Jpg or OutputFormat.Tiff;

    public int JpgQuality
    {
        get => _jpgQuality;
        set => Set(ref _jpgQuality, Math.Clamp(value, 1, 100));
    }

    public OutputMode OutputMode
    {
        get => _outputMode;
        set
        {
            if (Set(ref _outputMode, value))
            {
                OnPropertyChanged(nameof(UsesOutputDirectory));
            }
        }
    }

    public bool UsesOutputDirectory => OutputMode == OutputMode.MirrorUnderOutputDirectory;

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => Set(ref _outputDirectory, value);
    }

    public string FileNamePrefix
    {
        get => _fileNamePrefix;
        set => Set(ref _fileNamePrefix, value);
    }

    public int PageNumberDigits
    {
        get => _pageNumberDigits;
        set => Set(ref _pageNumberDigits, Math.Clamp(value, 1, 10));
    }

    // ---- Status ----------------------------------------------------------
    public Bitmap? PreviewImage
    {
        get => _previewImage;
        private set => Set(ref _previewImage, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                CancelCommand.RaiseCanExecuteChanged();
                GenerateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => Set(ref _progressValue, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            var clamped = PageCount == 0 ? 0 : Math.Clamp(value, 0, PageCount - 1);
            if (Set(ref _currentPage, clamped))
            {
                OnPropertyChanged(nameof(PageLabel));
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
                SchedulePreview();
            }
        }
    }

    public int PageCount
    {
        get => _pageCount;
        private set
        {
            if (Set(ref _pageCount, value))
            {
                OnPropertyChanged(nameof(PageLabel));
                OnPropertyChanged(nameof(HasPages));
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasPages => PageCount > 0;

    public string PageLabel => PageCount == 0 ? "No pages" : $"Page {CurrentPage + 1} of {PageCount}";

    public string FolderSummary
    {
        get => _folderSummary;
        private set => Set(ref _folderSummary, value);
    }

    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (Set(ref _errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    // ---- Settings load/save ---------------------------------------------
    private void LoadSettings()
    {
        var settings = SettingsStore.Load();
        var o = settings.Options;

        _isLoading = true;
        try
        {
            _sourceFolder = settings.LastSourceDirectory;
            _recurseSubfolders = o.RecurseSubfolders;
            _sortOrder = o.SortOrder;

            _pageSizePreset = o.PageSizePreset;
            _orientation = o.Orientation;
            _customWidth = o.CustomPageWidth.Value;
            _customHeight = o.CustomPageHeight.Value;
            _customUnit = o.CustomPageWidth.Unit;
            _resolution = o.Resolution;
            _pageMarginValue = o.PageMargin.Value;
            _pageMarginUnit = o.PageMargin.Unit;
            _imageSpacingValue = o.ImageSpacing.Value;
            _imageSpacingUnit = o.ImageSpacing.Unit;
            _columns = o.Columns;
            _rows = o.Rows;

            _backgroundColorHex = o.BackgroundColor.ToHex();
            _textColorHex = o.TextColor.ToHex();
            _zoomMode = o.ZoomMode;
            _horizontalAlignment = o.HorizontalAlignment;
            _verticalAlignment = o.VerticalAlignment;

            _fontFamily = o.FontFamily;
            _fontWeight = o.FontWeight;
            _fontSize = o.FontSize;
            _showCaptions = o.ShowCaptions;
            _showFileExtensions = o.ShowFileExtensions;
            _captionGapValue = o.CaptionGap.Value;
            _captionGapUnit = o.CaptionGap.Unit;

            _outputFormat = o.OutputFormat;
            _jpgQuality = o.JpgQuality;
            _outputMode = o.OutputMode;
            _outputDirectory = o.OutputDirectory;
            _fileNamePrefix = o.FileNamePrefix;
            _pageNumberDigits = o.PageNumberDigits;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void SaveSettings()
    {
        SettingsStore.Save(new AppSettings
        {
            LastSourceDirectory = SourceFolder,
            Options = BuildOptions(),
        });
    }

    // ---- Options ---------------------------------------------------------
    public ContactSheetOptions BuildOptions() => new()
    {
        PageSizePreset = PageSizePreset,
        Orientation = Orientation,
        CustomPageWidth = new Length(CustomWidth, CustomUnit),
        CustomPageHeight = new Length(CustomHeight, CustomUnit),
        Resolution = Resolution,
        PageMargin = new Length(PageMarginValue, PageMarginUnit),
        ImageSpacing = new Length(ImageSpacingValue, ImageSpacingUnit),
        Columns = Columns,
        Rows = Rows,
        BackgroundColor = RgbaColor.TryParse(BackgroundColorHex, out var bg) ? bg : RgbaColor.White,
        TextColor = RgbaColor.TryParse(TextColorHex, out var fg) ? fg : RgbaColor.Black,
        ZoomMode = ZoomMode,
        HorizontalAlignment = HorizontalAlignment,
        VerticalAlignment = VerticalAlignment,
        FontFamily = FontFamily,
        FontWeight = FontWeight,
        FontSize = FontSize,
        ShowCaptions = ShowCaptions,
        ShowFileExtensions = ShowFileExtensions,
        CaptionGap = new Length(CaptionGapValue, CaptionGapUnit),
        OutputFormat = OutputFormat,
        JpgQuality = JpgQuality,
        OutputMode = OutputMode,
        OutputDirectory = OutputDirectory,
        FileNamePrefix = FileNamePrefix,
        PageNumberDigits = PageNumberDigits,
        RecurseSubfolders = RecurseSubfolders,
        SortOrder = SortOrder,
    };

    // ---- Scanning --------------------------------------------------------
    private async Task RescanAsync()
    {
        var folder = SourceFolder;
        var recurse = RecurseSubfolders;
        var sort = SortOrder;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Folders.Clear();
            SelectedFolder = null;
            FolderSummary = string.Empty;
            PageCount = 0;
            PreviewImage = null;
            StatusText = "Choose a source folder to begin.";
            return;
        }

        var folders = await Task.Run(() => FolderScanner.FindFoldersWithImages(folder, recurse, sort));

        Folders.Clear();
        foreach (var f in folders)
        {
            Folders.Add(f);
        }

        var totalImages = folders.Sum(f => f.ImageCount);
        FolderSummary = folders.Count == 0
            ? "No images found."
            : $"{folders.Count} folder(s), {totalImages} image(s)";

        SelectedFolder = folders.FirstOrDefault();
        if (SelectedFolder is null)
        {
            PageCount = 0;
            PreviewImage = null;
            StatusText = "No supported images found in the selected folder.";
        }

        GenerateCommand.RaiseCanExecuteChanged();
    }

    // ---- Preview ---------------------------------------------------------
    private void SchedulePreview()
    {
        if (_isLoading)
        {
            return;
        }

        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;
        _ = DebouncedPreviewAsync(cts.Token);
    }

    private async Task DebouncedPreviewAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(150, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        await RenderPreviewAsync(token);
    }

    private async Task RenderPreviewAsync(CancellationToken token)
    {
        var folder = SelectedFolder;
        if (folder is null || folder.ImageCount == 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PreviewImage = null;
                PageCount = 0;
                if (string.IsNullOrEmpty(ErrorText))
                {
                    StatusText = "No images to preview.";
                }
            });
            return;
        }

        var options = BuildOptions();
        var images = folder.Images;
        var page = CurrentPage;

        try
        {
            var result = await Task.Run(() =>
            {
                var metrics = LayoutEngine.Compute(options, images.Count);
                var targetPage = Math.Min(page, Math.Max(0, metrics.PageCount - 1));
                var bitmap = PreviewRenderer.Render(options, images, targetPage, PreviewMaxWidth, PreviewMaxHeight, _previewCache);
                return (metrics.PageCount, bitmap);
            }, token);

            if (token.IsCancellationRequested)
            {
                result.bitmap.Dispose();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PreviewImage = BitmapInterop.ToAvaloniaBitmap(result.bitmap);
                result.bitmap.Dispose();

                ErrorText = null;
                PageCount = result.PageCount;
                if (CurrentPage >= PageCount)
                {
                    CurrentPage = Math.Max(0, PageCount - 1);
                }

                StatusText = $"{folder.ImageCount} image(s) • {PageCount} page(s)";
            });
        }
        catch (LayoutException ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PreviewImage = null;
                PageCount = 0;
                ErrorText = ex.Message;
                StatusText = "Layout error.";
            });
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer request
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PreviewImage = null;
                ErrorText = ex.Message;
                StatusText = "Preview failed.";
            });
        }
    }

    // ---- Export ----------------------------------------------------------
    private bool CanGenerate() => !IsBusy && SelectedFolder is not null && Directory.Exists(SourceFolder);

    private async Task GenerateAsync()
    {
        ErrorText = null;

        if (string.IsNullOrWhiteSpace(SourceFolder) || !Directory.Exists(SourceFolder))
        {
            ErrorText = "Select a valid source folder.";
            return;
        }

        var options = BuildOptions();
        if (options.OutputMode == OutputMode.MirrorUnderOutputDirectory && string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            ErrorText = "Choose an output folder, or switch to writing into each source folder.";
            return;
        }

        ExportPlan plan;
        try
        {
            plan = ExportPlanner.Build(SourceFolder, options);
        }
        catch (LayoutException ex)
        {
            ErrorText = ex.Message;
            return;
        }

        if (plan.TotalImages == 0)
        {
            ErrorText = "No images found to export.";
            return;
        }

        _exportCts = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        StatusText = "Exporting…";

        var progress = new Progress<ExportProgress>(p =>
        {
            var overall = p.FolderCount == 0 ? 0 : (p.FolderIndex + (p.PageIndex / (double)Math.Max(1, p.PageCount))) / p.FolderCount;
            ProgressValue = Math.Clamp(overall * 100, 0, 100);
            StatusText = $"{p.Message} ({p.FolderIndex + 1}/{p.FolderCount})";
        });

        try
        {
            var summary = await Task.Run(() => _exportService.Export(plan, progress, _exportCts.Token));

            ProgressValue = 100;
            StatusText = $"Done • {summary.FileCount} file(s) from {summary.FolderCount} folder(s)";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Export cancelled.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Export failed.";
        }
        finally
        {
            IsBusy = false;
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    private void CancelExport() => _exportCts?.Cancel();

    // ---- Folder pickers --------------------------------------------------
    private async Task BrowseSourceAsync()
    {
        if (PickFolderAsync is null)
        {
            return;
        }

        var picked = await PickFolderAsync();
        if (!string.IsNullOrEmpty(picked))
        {
            SourceFolder = picked;
        }
    }

    private async Task BrowseOutputAsync()
    {
        if (PickFolderAsync is null)
        {
            return;
        }

        var picked = await PickFolderAsync();
        if (!string.IsNullOrEmpty(picked))
        {
            OutputDirectory = picked;
        }
    }
}
