using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.App;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public void ApplyWindowSettings(LocalWindowSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Width = SanitizeDimension(settings.Width, 1360, MinWidth);
        Height = SanitizeDimension(settings.Height, 860, MinHeight);
        if (settings.PositionX is double positionX && settings.PositionY is double positionY &&
            double.IsFinite(positionX) && double.IsFinite(positionY))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)Math.Round(positionX), (int)Math.Round(positionY));
        }

        if (settings.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    public LocalWindowSettings CaptureWindowSettings() =>
        new(Width, Height, Position.X, Position.Y, WindowState == WindowState.Maximized);

    private async void ChooseWorkspace_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = new AvaloniaUiTextProvider().Get("ChooseWorkspaceDialogTitle"),
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.LoadWorkspaceAsync(folders[0].Path.LocalPath);
        }
    }

    private static double SanitizeDimension(double value, double fallback, double minimum)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return Math.Clamp(value, minimum, 10000);
    }
}
