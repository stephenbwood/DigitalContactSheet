using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using DigitalContactSheet.App.ViewModels;
using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.App.Tests;

/// <summary>
/// Verifies that the control types used in the window bind correctly to the view model. The numeric
/// controls expose a <c>decimal?</c> value, so these tests guard the two-way conversions the UI relies on.
/// </summary>
public class BindingTests
{
    [AvaloniaFact]
    public void NumericUpDown_RoundTripsDoubleProperty()
    {
        var vm = new MainViewModel();
        var control = new NumericUpDown();
        control.Bind(NumericUpDown.ValueProperty, new Binding(nameof(MainViewModel.FontSize)) { Source = vm, Mode = BindingMode.TwoWay });

        control.Value = 14m;
        Assert.Equal(14d, vm.FontSize);

        vm.FontSize = 20d;
        Assert.Equal(20m, control.Value);
    }

    [AvaloniaFact]
    public void NumericUpDown_RoundTripsIntProperty()
    {
        var vm = new MainViewModel();
        var control = new NumericUpDown();
        control.Bind(NumericUpDown.ValueProperty, new Binding(nameof(MainViewModel.Columns)) { Source = vm, Mode = BindingMode.TwoWay });

        control.Value = 6m;
        Assert.Equal(6, vm.Columns);

        vm.Columns = 8;
        Assert.Equal(8m, control.Value);
    }

    [AvaloniaFact]
    public void ComboBox_RoundTripsEnumProperty()
    {
        var vm = new MainViewModel();
        var control = new ComboBox { ItemsSource = vm.ZoomModes };
        control.Bind(ComboBox.SelectedItemProperty, new Binding(nameof(MainViewModel.ZoomMode)) { Source = vm, Mode = BindingMode.TwoWay });

        control.SelectedItem = ZoomMode.Fill;
        Assert.Equal(ZoomMode.Fill, vm.ZoomMode);

        vm.ZoomMode = ZoomMode.Fit;
        Assert.Equal(ZoomMode.Fit, control.SelectedItem);
    }

    [AvaloniaFact]
    public void CheckBox_RoundTripsBoolProperty()
    {
        var vm = new MainViewModel();
        var control = new CheckBox();
        control.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(MainViewModel.ShowCaptions)) { Source = vm, Mode = BindingMode.TwoWay });

        control.IsChecked = false;
        Assert.False(vm.ShowCaptions);

        vm.ShowCaptions = true;
        Assert.True(control.IsChecked);
    }

    [AvaloniaFact]
    public void BuildOptions_ReflectsViewModelValues()
    {
        var vm = new MainViewModel
        {
            Columns = 5,
            Rows = 3,
            FontSize = 11.5,
            BackgroundColorHex = "#102030",
            ZoomMode = ZoomMode.Fill,
            OutputFormat = OutputFormat.Tiff,
        };

        var options = vm.BuildOptions();

        Assert.Equal(5, options.Columns);
        Assert.Equal(3, options.Rows);
        Assert.Equal(11.5, options.FontSize);
        Assert.Equal(new RgbaColor(16, 32, 48), options.BackgroundColor);
        Assert.Equal(ZoomMode.Fill, options.ZoomMode);
        Assert.Equal(OutputFormat.Tiff, options.OutputFormat);
    }

    [AvaloniaFact]
    public void InvalidColour_FallsBackToDefault()
    {
        var vm = new MainViewModel { BackgroundColorHex = "not-a-colour" };
        Assert.Equal(RgbaColor.White, vm.BuildOptions().BackgroundColor);
    }
}
