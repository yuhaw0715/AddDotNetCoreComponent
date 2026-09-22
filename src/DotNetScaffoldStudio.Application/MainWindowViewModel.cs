using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IDemoExecutionService _executionService;
    private CancellationTokenSource? _executionCancellation;
    private NavigationItem? _selectedNavigation;
    private FeatureDefinition? _selectedFeature;
    private ProjectInfo? _selectedProject;
    private string _workspacePath = "尚未選擇工作區";
    private string _componentName = "OrdersController";
    private string _outputPath = "Controllers";
    private string _selectedTemplateMode = "含讀寫動作";
    private bool _useAsyncActions = true;
    private bool _includeApiActions;
    private bool _isNavigationCollapsed;
    private bool _isScanning;
    private bool _isConfirmationVisible;
    private bool _isRunning;
    private bool _hasResult;
    private string _statusMessage = "Demo 模式 · 不會修改任何檔案";
    private string _executionOutput = "尚未執行命令。";
    private string _resultTitle = string.Empty;
    private string _resultSummary = string.Empty;

    public MainWindowViewModel(IWorkspaceService workspaceService, IDemoExecutionService executionService)
    {
        _workspaceService = workspaceService;
        _executionService = executionService;

        ScanWorkspaceCommand = new AsyncRelayCommand(ScanWorkspaceAsync, CanScanWorkspace);
        UseDemoWorkspaceCommand = new RelayCommand(UseDemoWorkspace);
        RequestExecutionCommand = new RelayCommand(RequestExecution, CanRequestExecution);
        ConfirmExecutionCommand = new AsyncRelayCommand(ConfirmExecutionAsync);
        CancelConfirmationCommand = new RelayCommand(() => IsConfirmationVisible = false);
        CancelExecutionCommand = new RelayCommand(CancelExecution, () => IsRunning);
        ClearResultCommand = new RelayCommand(ClearResult);
        ToggleNavigationCommand = new RelayCommand(ToggleNavigation);

        foreach (var item in DemoCatalog.Navigation)
        {
            NavigationItems.Add(item);
        }

        SelectedNavigation = NavigationItems[2];
        SelectedFeature = VisibleFeatures.FirstOrDefault();
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];
    public ObservableCollection<FeatureDefinition> VisibleFeatures { get; } = [];
    public ObservableCollection<ProjectInfo> Projects { get; } = [];
    public ObservableCollection<string> ResultFiles { get; } = [];
    public IReadOnlyList<string> TemplateModes { get; } = ["空白", "含讀寫動作", "MVC CRUD", "REST API"];

    public IAsyncRelayCommand ScanWorkspaceCommand { get; }
    public IRelayCommand UseDemoWorkspaceCommand { get; }
    public IRelayCommand RequestExecutionCommand { get; }
    public IAsyncRelayCommand ConfirmExecutionCommand { get; }
    public IRelayCommand CancelConfirmationCommand { get; }
    public IRelayCommand CancelExecutionCommand { get; }
    public IRelayCommand ClearResultCommand { get; }
    public IRelayCommand ToggleNavigationCommand { get; }

    public NavigationItem? SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (!SetProperty(ref _selectedNavigation, value) || value is null)
            {
                return;
            }

            VisibleFeatures.Clear();
            foreach (var feature in DemoCatalog.Features.Where(feature => feature.Category == value.Id))
            {
                VisibleFeatures.Add(feature);
            }

            SelectedFeature = VisibleFeatures.FirstOrDefault();
            OnPropertyChanged(nameof(IsFeatureGroup));
            OnPropertyChanged(nameof(IsWorkspacePage));
            OnPropertyChanged(nameof(IsHistoryPage));
            OnPropertyChanged(nameof(IsSettingsPage));
            OnPropertyChanged(nameof(IsContentPage));
        }
    }

    public FeatureDefinition? SelectedFeature
    {
        get => _selectedFeature;
        set
        {
            if (!SetProperty(ref _selectedFeature, value))
            {
                return;
            }

            ComponentName = value?.Id switch
            {
                "migration-add" => "AddOrderStatus",
                "database-update" => "Latest",
                "webapi" or "mvc" or "blazor" or "console" => "MyNewApp",
                "razorcomponent" => "OrderSummary",
                "page" => "Orders",
                _ => "OrdersController"
            };

            OutputPath = value?.Category == "project" ? "./generated" : "Controllers";
            OnPropertyChanged(nameof(SelectedFeatureTitle));
            OnPropertyChanged(nameof(SelectedFeatureDescription));
            OnPropertyChanged(nameof(SelectedFeatureBadge));
            OnPropertyChanged(nameof(AvailabilityMessage));
            OnPropertyChanged(nameof(CanExecuteSelectedFeature));
            OnPropertyChanged(nameof(IsDatabaseRisk));
            OnPropertyChanged(nameof(CommandPreviewText));
            RequestExecutionCommand.NotifyCanExecuteChanged();
        }
    }

    public ProjectInfo? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                OnPropertyChanged(nameof(CommandPreviewText));
                OnPropertyChanged(nameof(RequiresProjectSelection));
                OnPropertyChanged(nameof(CanExecuteSelectedFeature));
                OnPropertyChanged(nameof(AvailabilityMessage));
                RequestExecutionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string WorkspacePath
    {
        get => _workspacePath;
        set
        {
            if (SetProperty(ref _workspacePath, value))
            {
                ScanWorkspaceCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CommandPreviewText));
                OnPropertyChanged(nameof(WorkingDirectoryText));
            }
        }
    }

    public string ComponentName
    {
        get => _componentName;
        set
        {
            if (SetProperty(ref _componentName, value))
            {
                OnPropertyChanged(nameof(CommandPreviewText));
                OnPropertyChanged(nameof(ValidationMessage));
                RequestExecutionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                OnPropertyChanged(nameof(CommandPreviewText));
            }
        }
    }

    public string SelectedTemplateMode
    {
        get => _selectedTemplateMode;
        set
        {
            if (SetProperty(ref _selectedTemplateMode, value))
            {
                OnPropertyChanged(nameof(CommandPreviewText));
            }
        }
    }

    public bool UseAsyncActions
    {
        get => _useAsyncActions;
        set
        {
            if (SetProperty(ref _useAsyncActions, value))
            {
                OnPropertyChanged(nameof(CommandPreviewText));
            }
        }
    }

    public bool IncludeApiActions
    {
        get => _includeApiActions;
        set
        {
            if (SetProperty(ref _includeApiActions, value))
            {
                OnPropertyChanged(nameof(CommandPreviewText));
            }
        }
    }

    public bool IsConfirmationVisible
    {
        get => _isConfirmationVisible;
        set => SetProperty(ref _isConfirmationVisible, value);
    }

    public bool IsNavigationCollapsed
    {
        get => _isNavigationCollapsed;
        private set
        {
            if (SetProperty(ref _isNavigationCollapsed, value))
            {
                OnPropertyChanged(nameof(IsNavigationExpanded));
                OnPropertyChanged(nameof(NavigationPaneWidth));
            }
        }
    }

    public bool IsNavigationExpanded => !IsNavigationCollapsed;
    public double NavigationPaneWidth => IsNavigationCollapsed ? 72 : 270;

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                ScanWorkspaceCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                CancelExecutionCommand.NotifyCanExecuteChanged();
                RequestExecutionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasResult
    {
        get => _hasResult;
        private set => SetProperty(ref _hasResult, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ExecutionOutput
    {
        get => _executionOutput;
        private set => SetProperty(ref _executionOutput, value);
    }

    public string ResultTitle
    {
        get => _resultTitle;
        private set => SetProperty(ref _resultTitle, value);
    }

    public string ResultSummary
    {
        get => _resultSummary;
        private set => SetProperty(ref _resultSummary, value);
    }

    public bool IsFeatureGroup => SelectedNavigation?.Id is "project" or "component" or "scaffolding" or "efcore" or "custom";
    public bool IsContentPage => !IsFeatureGroup;
    public bool IsWorkspacePage => SelectedNavigation?.Id == "home";
    public bool IsHistoryPage => SelectedNavigation?.Id == "history";
    public bool IsSettingsPage => SelectedNavigation?.Id == "settings";
    public bool IsProjectsEmpty => Projects.Count == 0;
    public bool HasProjects => !IsProjectsEmpty;
    public bool IsMultipleProjects => Projects.Count > 1;
    public bool RequiresProjectSelection => IsMultipleProjects && SelectedProject is null;
    public bool RequiresTargetProject => SelectedFeature?.Category is "scaffolding" or "efcore";
    public bool IsDatabaseRisk => SelectedFeature?.Risk == FeatureRisk.DatabaseChange;
    public bool CanExecuteSelectedFeature =>
        SelectedFeature?.Availability == FeatureAvailability.Available &&
        (!RequiresTargetProject || SelectedProject is not null);
    public string SelectedFeatureTitle => SelectedFeature?.DisplayName ?? "選擇一項功能";
    public string SelectedFeatureDescription => SelectedFeature?.Description ?? "從左側選擇功能群組，再挑選要執行的項目。";
    public string SelectedFeatureBadge => SelectedFeature?.Badge ?? string.Empty;
    public string AvailabilityMessage => RequiresTargetProject && SelectedProject is null
        ? "此功能需要先選擇現有的目標專案。"
        : SelectedFeature?.AvailabilityReason ?? "此功能可在目前平台使用。";
    public string ValidationMessage => string.IsNullOrWhiteSpace(ComponentName) ? "名稱為必填欄位。" : string.Empty;
    public string CommandPreviewText => BuildCommandPreview()?.DisplayText ?? "選擇功能後將顯示命令預覽";
    public string WorkingDirectoryText => BuildCommandPreview()?.WorkingDirectory ?? WorkspacePath;

    public async Task LoadWorkspaceAsync(string path)
    {
        await LoadWorkspaceCoreAsync(path);
    }

    private bool CanScanWorkspace() => !IsScanning && !string.IsNullOrWhiteSpace(WorkspacePath) && WorkspacePath != "尚未選擇工作區";

    private Task ScanWorkspaceAsync() => LoadWorkspaceCoreAsync(WorkspacePath);

    private async Task LoadWorkspaceCoreAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "尚未選擇工作區")
        {
            StatusMessage = "請先選擇有效的工作區。";
            return;
        }

        IsScanning = true;
        StatusMessage = "正在掃描 .NET 專案…";

        try
        {
            var projects = await _workspaceService.ScanProjectsAsync(path, CancellationToken.None);
            WorkspacePath = path;
            Projects.Clear();
            foreach (var project in projects)
            {
                Projects.Add(project);
            }

            OnPropertyChanged(nameof(IsProjectsEmpty));
            OnPropertyChanged(nameof(HasProjects));
            OnPropertyChanged(nameof(IsMultipleProjects));

            SelectedProject = Projects.Count == 1 ? Projects[0] : null;
            StatusMessage = Projects.Count == 0
                ? "找不到 .csproj；仍可使用專案範本功能。"
                : Projects.Count == 1
                    ? "已找到 1 個專案，已自動選取目標。"
                    : $"已找到 {Projects.Count} 個專案，請選擇目標。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"無法載入工作區：{exception.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void UseDemoWorkspace()
    {
        WorkspacePath = "/Users/demo/Projects/CommerceSuite";
        Projects.Clear();
        OnPropertyChanged(nameof(IsProjectsEmpty));
        OnPropertyChanged(nameof(HasProjects));
        Projects.Add(new ProjectInfo("Commerce.Api", "/Users/demo/Projects/CommerceSuite/src/Commerce.Api/Commerce.Api.csproj", "net10.0", "Microsoft.NET.Sdk.Web"));
        Projects.Add(new ProjectInfo("Commerce.Domain", "/Users/demo/Projects/CommerceSuite/src/Commerce.Domain/Commerce.Domain.csproj", "net10.0", "Microsoft.NET.Sdk"));
        OnPropertyChanged(nameof(IsMultipleProjects));
        OnPropertyChanged(nameof(IsProjectsEmpty));
        OnPropertyChanged(nameof(HasProjects));
        SelectedProject = Projects[0];
        StatusMessage = "已載入示範工作區 · 2 個專案";
    }

    private bool CanRequestExecution() =>
        !IsRunning &&
        CanExecuteSelectedFeature &&
        !string.IsNullOrWhiteSpace(ComponentName);

    private void RequestExecution()
    {
        HasResult = false;
        IsConfirmationVisible = true;
        StatusMessage = IsDatabaseRisk
            ? "等待高風險操作二次確認"
            : "請確認命令與目標位置";
    }

    private async Task ConfirmExecutionAsync()
    {
        var command = BuildCommandPreview();
        if (command is null)
        {
            return;
        }

        IsConfirmationVisible = false;
        IsRunning = true;
        HasResult = false;
        ExecutionOutput = string.Empty;
        StatusMessage = "Demo 執行中…";
        _executionCancellation = new CancellationTokenSource();
        var progress = new Progress<string>(line => ExecutionOutput += $"{line}{Environment.NewLine}");

        try
        {
            var result = await _executionService.ExecuteAsync(command, progress, _executionCancellation.Token);
            ResultTitle = result.Title;
            ResultSummary = result.Summary;
            ResultFiles.Clear();
            foreach (var file in result.FileChanges)
            {
                ResultFiles.Add(file);
            }

            HasResult = true;
            StatusMessage = result.Succeeded ? "示意流程已完成" : "示意流程失敗";
        }
        catch (OperationCanceledException)
        {
            ResultTitle = "已取消示意執行";
            ResultSummary = "正式版本會在取消後重新掃描檔案差異，不會假設工作區未被修改。";
            ResultFiles.Clear();
            HasResult = true;
            StatusMessage = "已取消";
        }
        finally
        {
            IsRunning = false;
            _executionCancellation?.Dispose();
            _executionCancellation = null;
        }
    }

    private void CancelExecution() => _executionCancellation?.Cancel();

    private void ToggleNavigation() => IsNavigationCollapsed = !IsNavigationCollapsed;

    private void ClearResult()
    {
        HasResult = false;
        ExecutionOutput = "尚未執行命令。";
        ResultFiles.Clear();
        StatusMessage = "Demo 模式 · 不會修改任何檔案";
    }

    private CommandPreview? BuildCommandPreview()
    {
        if (SelectedFeature is null)
        {
            return null;
        }

        var projectPath = SelectedProject?.Path ?? "./src/MyApp/MyApp.csproj";
        var workingDirectory = WorkspacePath == "尚未選擇工作區" ? "/path/to/workspace" : WorkspacePath;
        List<string> arguments;

        switch (SelectedFeature.Id)
        {
            case "controller":
                arguments = ["aspnet-codegenerator", "controller", "--controllerName", ComponentName, "--project", projectPath, "--relativeFolderPath", OutputPath];
                if (SelectedTemplateMode == "含讀寫動作") arguments.Add("--readWriteActions");
                if (SelectedTemplateMode == "REST API" || IncludeApiActions) arguments.Add("--restWithNoViews");
                if (UseAsyncActions) arguments.Add("--useAsyncActions");
                break;
            case "minimalapi":
                arguments = ["aspnet-codegenerator", "minimalapi", "--endpoints", ComponentName, "--model", "Commerce.Domain.Order", "--dataContext", "CommerceDbContext", "--project", projectPath, "--open"];
                break;
            case "area":
                arguments = ["aspnet-codegenerator", "area", ComponentName, "--project", projectPath];
                break;
            case "migration-add":
                arguments = ["ef", "migrations", "add", ComponentName, "--project", projectPath];
                break;
            case "database-update":
                arguments = ["ef", "database", "update", ComponentName == "Latest" ? string.Empty : ComponentName, "--project", projectPath];
                arguments.RemoveAll(string.IsNullOrEmpty);
                break;
            case "dbcontext-scaffold":
                arguments = ["ef", "dbcontext", "scaffold", "Name=ConnectionStrings:Commerce", "Microsoft.EntityFrameworkCore.Sqlite", "--project", projectPath, "--output-dir", "Models"];
                break;
            case "blazor-crud":
                arguments = ["aspnet-codegenerator", "blazor", "CRUD", "--model", "Order", "--dataContext", "CommerceDbContext", "--project", projectPath, "--relativeFolderPath", "Components/Pages/Orders"];
                break;
            default:
                arguments = ["new", SelectedFeature.ShortName, "--name", ComponentName, "--output", OutputPath];
                break;
        }

        return new CommandPreview("dotnet", arguments, workingDirectory, SelectedFeature.Risk);
    }
}
