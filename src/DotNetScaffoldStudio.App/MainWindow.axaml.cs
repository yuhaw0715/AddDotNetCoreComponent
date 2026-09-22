using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DotNetScaffoldStudio.Application;

namespace DotNetScaffoldStudio.App;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void ChooseWorkspace_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = (Avalonia.Application.Current?.Resources["ChooseWorkspaceDialogTitle"] as string) ?? string.Empty,
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.LoadWorkspaceAsync(folders[0].Path.LocalPath);
        }
    }
}
