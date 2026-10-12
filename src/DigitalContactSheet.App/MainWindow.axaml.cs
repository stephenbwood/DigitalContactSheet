using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DigitalContactSheet.App.ViewModels;

namespace DigitalContactSheet.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel { PickFolderAsync = PickFolderAsync };
        DataContext = _viewModel;
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a folder",
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.SaveSettings();
        base.OnClosed(e);
    }
}
