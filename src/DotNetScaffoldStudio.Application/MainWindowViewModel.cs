using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IDemoExecutionService _executionService;
    private readonly ParameterValidator _parameterValidator;
    private readonly IFileRevealService _fileRevealService;
    private CancellationTokenSource? _executionCancellation;
    private NavigationItem? _selectedNavigation;
    private FeatureDefinition? _selectedFeature;
    private ProjectInfo? _selectedProject;
    private string _workspacePath = "尚未選擇工作區";
    private string _componentName = "OrdersController";
    private string _outputPath = "Controllers";
    private string _selectedTemplateMode = "含讀寫動作";
    private string _featureSearchText = string.Empty;
    private bool _useAsyncActions = true;
    private bool _includeApiActions;
    private bool _isNavigationCollapsed;
    private bool _isScanning;
    private bool _isConfirmationVisible;
    private bool _isRunning;
    private bool _hasResult;
    private ExecutionStage _executionStage;
    private string _statusMessage = "Demo 模式 · 不會修改任何檔案";
    private string _executionOutput = "尚未執行命令。";
    private string _resultTitle = string.Empty;
    private string _resultSummary = string.Empty;
    private string _gitDifferenceSummary = string.Empty;

    public MainWindowViewModel(IWorkspaceService workspaceService, IDemoExecutionService executionService)
        : this(workspaceService, executionService, new ParameterValidator(), new DemoFileRevealService())
    {
    }

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IDemoExecutionService executionService,
        ParameterValidator parameterValidator)
        : this(workspaceService, executionService, parameterValidator, new DemoFileRevealService())
    {
    }

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IDemoExecutionService executionService,
        ParameterValidator parameterValidator,
        IFileRevealService fileRevealService)
    {
        _workspaceService = workspaceService;
        _executionService = executionService;
        _parameterValidator = parameterValidator;
        _fileRevealService = fileRevealService;

        ScanWorkspaceCommand = new AsyncRelayCommand(ScanWorkspaceAsync, CanScanWorkspace);
        UseDemoWorkspaceCommand = new RelayCommand(UseDemoWorkspace);
        RequestExecutionCommand = new RelayCommand(RequestExecution, CanRequestExecution);
        ConfirmExecutionCommand = new AsyncRelayCommand(ConfirmExecutionAsync);
        CancelConfirmationCommand = new RelayCommand(() => IsConfirmationVisible = false);
        CancelExecutionCommand = new RelayCommand(CancelExecution, () => IsRunning);
        ClearResultCommand = new RelayCommand(ClearResult);
        ToggleNavigationCommand = new RelayCommand(ToggleNavigation);
        OpenResultInFinderCommand = new AsyncRelayCommand(OpenResultInFinderAsync, CanOpenResultInFinderCommand);

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
    public ObservableCollection<string> ExistingChanges { get; } = [];
    public IReadOnlyList<string> TemplateModes { get; } = ["空白", "含讀寫動作", "MVC CRUD", "REST API"];

    public IAsyncRelayCommand ScanWorkspaceCommand { get; }
    public IRelayCommand UseDemoWorkspaceCommand { get; }
    public IRelayCommand RequestExecutionCommand { get; }
    public IAsyncRelayCommand ConfirmExecutionCommand { get; }
    public IRelayCommand CancelConfirmationCommand { get; }
    public IRelayCommand CancelExecutionCommand { get; }
    public IRelayCommand ClearResultCommand { get; }
    public IRelayCommand ToggleNavigationCommand { get; }
    public IAsyncRelayCommand OpenResultInFinderCommand { get; }
    public ParameterEditorViewModel? ParameterEditor { get; private set; }

    public NavigationItem? SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (!SetProperty(ref _selectedNavigation, value) || value is null)
            {
                return;
            }

            RefreshVisibleFeatures();
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
            ReplaceParameterEditor(value);
            OnPropertyChanged(nameof(SelectedFeatureTitle));
            OnPropertyChanged(nameof(SelectedFeatureDescription));
            OnPropertyChanged(nameof(SelectedFeatureBadge));
            OnPropertyChanged(nameof(AvailabilityMessage));
            OnPropertyChanged(nameof(CanExecuteSelectedFeature));
            OnPropertyChanged(nameof(IsDatabaseRisk));
            OnPropertyChanged(nameof(CurrentConfirmationKind));
            OnPropertyChanged(nameof(IsHighRiskConfirmation));
            OnPropertyChanged(nameof(ConfirmationTitle));
            OnPropertyChanged(nameof(ConfirmationMessage));
            OnPropertyChanged(nameof(ConfirmationDetails));
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

    public string FeatureSearchText
    {
        get => _featureSearchText;
        set
        {
            if (SetProperty(ref _featureSearchText, value))
            {
                RefreshVisibleFeatures();
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

    public ConfirmationKind CurrentConfirmationKind => SelectedFeature?.Id switch
    {
        "database-update" => ConfirmationKind.DatabaseUpdate,
        "tool-install" => ConfirmationKind.ToolInstallation,
        _ when CurrentFeatureRisk == FeatureRisk.FileOverwrite => ConfirmationKind.FileOverwrite,
        _ => ConfirmationKind.General
    };

    public bool IsHighRiskConfirmation => CurrentConfirmationKind != ConfirmationKind.General;
    public bool IsDatabaseRisk => CurrentConfirmationKind == ConfirmationKind.DatabaseUpdate;
    public string ConfirmationTitle => CurrentConfirmationKind switch
    {
        ConfirmationKind.FileOverwrite => "確認覆寫既有檔案",
        ConfirmationKind.ToolInstallation => "確認安裝工作區本機工具",
        ConfirmationKind.DatabaseUpdate => "確認更新資料庫",
        _ => "確認示意執行"
    };
    public string ConfirmationMessage => CurrentConfirmationKind switch
    {
        ConfirmationKind.FileOverwrite => "此操作可能覆寫目前工作區內的檔案，請確認輸出位置與命令。",
        ConfirmationKind.ToolInstallation => "此操作只會使用目前工作區的本機工具資訊清單，不會執行全域工具安裝。",
        ConfirmationKind.DatabaseUpdate => "此操作會變更目標資料庫；請確認目標專案、DbContext 與連線資訊來源。",
        _ => "請再次確認目標與命令。Demo 不會執行外部 CLI，也不會修改檔案。"
    };
    public string ConfirmationDetails => CurrentConfirmationKind switch
    {
        ConfirmationKind.FileOverwrite => "風險：既有檔案可能被覆寫；覆寫選項必須由使用者明確啟用。",
        ConfirmationKind.ToolInstallation => "範圍：工作區 .config/dotnet-tools.json；禁止 --global。",
        ConfirmationKind.DatabaseUpdate => $"目標專案：{SelectedProject?.Path ?? "尚未選擇"}\nDbContext：{ParameterEditor?.GetTextValue("dbContext") ?? "依專案設定"}\n連線來源：具名連線或專案設定（不顯示機密值）",
        _ => "安全 Demo 模式：不會啟動外部 CLI，也不會修改檔案或資料庫。"
    };

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
        private set
        {
            if (SetProperty(ref _hasResult, value))
            {
                OpenResultInFinderCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ExecutionStage ExecutionStage
    {
        get => _executionStage;
        private set
        {
            if (SetProperty(ref _executionStage, value))
            {
                OnPropertyChanged(nameof(ExecutionStageLabel));
            }
        }
    }

    public string ExecutionStageLabel => ExecutionStage switch
    {
        ExecutionStage.AwaitingConfirmation => "等待確認",
        ExecutionStage.Executing => "執行中",
        ExecutionStage.Succeeded => "成功",
        ExecutionStage.Failed => "失敗",
        ExecutionStage.Cancelled => "已取消",
        _ => "閒置"
    };

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

    public string GitDifferenceSummary
    {
        get => _gitDifferenceSummary;
        private set => SetProperty(ref _gitDifferenceSummary, value);
    }

    public bool CanOpenResultInFinder => HasResult && ResultFiles.Count > 0;

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
    public bool HasVisibleFeatures => VisibleFeatures.Count > 0;
    public bool CanExecuteSelectedFeature =>
        SelectedFeature?.Availability == FeatureAvailability.Available &&
        (!RequiresTargetProject || SelectedProject is not null);
    public string SelectedFeatureTitle => SelectedFeature?.DisplayName ?? "選擇一項功能";
    public string SelectedFeatureDescription => SelectedFeature?.Description ?? "從左側選擇功能群組，再挑選要執行的項目。";
    public string SelectedFeatureBadge => SelectedFeature?.Badge ?? string.Empty;
    public string AvailabilityMessage => RequiresTargetProject && SelectedProject is null
        ? "此功能需要先選擇現有的目標專案。"
        : SelectedFeature?.AvailabilityReason ?? "此功能可在目前平台使用。";
    public string ValidationMessage => ParameterEditor?.FirstErrorMessage ??
        (string.IsNullOrWhiteSpace(ComponentName) ? "名稱為必填欄位。" : string.Empty);
    public string CommandPreviewText => BuildCommandPreview()?.DisplayText ?? "選擇功能後將顯示命令預覽";
    public string WorkingDirectoryText => BuildCommandPreview()?.WorkingDirectory ?? WorkspacePath;
    public string FeatureCountSummary => $"{VisibleFeatures.Count} 項功能";

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
            ParameterEditor?.SetWorkspaceRoot(path);
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
        ParameterEditor?.SetWorkspaceRoot(WorkspacePath);
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
        !string.IsNullOrWhiteSpace(ComponentName) &&
        (ParameterEditor?.IsValid ?? true);

    private void RequestExecution()
    {
        HasResult = false;
        ExecutionStage = ExecutionStage.AwaitingConfirmation;
        IsConfirmationVisible = true;
        StatusMessage = IsHighRiskConfirmation
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
        ExecutionStage = ExecutionStage.Executing;
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

            ExistingChanges.Clear();
            foreach (var change in result.ExistingChanges ?? [])
            {
                ExistingChanges.Add(change);
            }

            GitDifferenceSummary = result.GitStatus ??
                $"Git 分支：{result.GitBranch ?? "非 Git 工作區"}；執行後新增 {ResultFiles.Count} 項差異，執行前既有 {ExistingChanges.Count} 項變更。";

            HasResult = true;
            ExecutionStage = result.Succeeded ? ExecutionStage.Succeeded : ExecutionStage.Failed;
            StatusMessage = result.Succeeded ? "示意流程已完成" : "示意流程失敗";
        }
        catch (OperationCanceledException)
        {
            ResultTitle = "已取消示意執行";
            ResultSummary = "正式版本會在取消後重新掃描檔案差異，不會假設工作區未被修改。";
            ResultFiles.Clear();
            ExistingChanges.Clear();
            GitDifferenceSummary = "取消後已重新掃描差異；Demo 未修改工作區。";
            HasResult = true;
            ExecutionStage = ExecutionStage.Cancelled;
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

    private void ReplaceParameterEditor(FeatureDefinition? feature)
    {
        if (ParameterEditor is not null)
        {
            ParameterEditor.ValuesChanged -= ParameterEditorOnValuesChanged;
        }

        ParameterEditor = feature is null
            ? null
            : new ParameterEditorViewModel(
                CreateParameterSchema(feature),
                _parameterValidator,
                WorkspacePath,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = ComponentName,
                    ["output"] = OutputPath,
                    ["variant"] = SelectedTemplateMode
                });
        if (ParameterEditor is not null)
        {
            ParameterEditor.ValuesChanged += ParameterEditorOnValuesChanged;
        }

        OnPropertyChanged(nameof(ParameterEditor));
        OnPropertyChanged(nameof(ValidationMessage));
    }

    private void ParameterEditorOnValuesChanged()
    {
        if (ParameterEditor is null)
        {
            return;
        }

        var name = ParameterEditor.GetTextValue("name");
        if (name is not null && !string.Equals(ComponentName, name, StringComparison.Ordinal))
        {
            ComponentName = name;
        }

        var output = ParameterEditor.GetTextValue("output");
        if (output is not null && !string.Equals(OutputPath, output, StringComparison.Ordinal))
        {
            OutputPath = output;
        }

        var variant = ParameterEditor.GetTextValue("variant");
        if (variant is not null && !string.Equals(SelectedTemplateMode, variant, StringComparison.Ordinal))
        {
            SelectedTemplateMode = variant;
        }

        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(CommandPreviewText));
        OnPropertyChanged(nameof(IsDatabaseRisk));
        OnPropertyChanged(nameof(CurrentConfirmationKind));
        OnPropertyChanged(nameof(IsHighRiskConfirmation));
        OnPropertyChanged(nameof(ConfirmationTitle));
        OnPropertyChanged(nameof(ConfirmationMessage));
        OnPropertyChanged(nameof(ConfirmationDetails));
        RequestExecutionCommand.NotifyCanExecuteChanged();
    }

    private static CatalogFeature CreateParameterSchema(FeatureDefinition feature)
    {
        var parameters = new List<ParameterDefinition>
        {
            new("name", "名稱", ParameterValueKind.Text, isRequired: true),
            new("output", "輸出位置", ParameterValueKind.Path, isAdvanced: true),
            new("force", "覆寫既有輸出", ParameterValueKind.Boolean, isAdvanced: true)
        };
        var constraints = new List<ParameterConstraint>
        {
            ParameterConstraint.NameFormat("name"),
            ParameterConstraint.PathWithinWorkspace("output")
        };

        if (feature.Category == "scaffolding")
        {
            parameters.Add(new ParameterDefinition("variant", "範本模式", ParameterValueKind.Enumeration, allowedValues: ["空白", "含讀寫動作", "MVC CRUD", "REST API"]));
            parameters.Add(new ParameterDefinition("model", "模型", ParameterValueKind.Text, isAdvanced: true));
            parameters.Add(new ParameterDefinition("dataContext", "DbContext", ParameterValueKind.Text, isAdvanced: true));
        }
        else if (feature.Category == "efcore")
        {
            parameters.Add(new ParameterDefinition("connection", "具名連線或來源", ParameterValueKind.Secret, isAdvanced: true));
            parameters.Add(new ParameterDefinition("dbContext", "DbContext", ParameterValueKind.Text, isAdvanced: true));
        }

        return new CatalogFeature(
            feature.Id,
            feature.DisplayName,
            FeatureGroup.Custom,
            feature.CommandKind,
            [feature.ShortName],
            feature.Risk,
            parameters: parameters,
            constraints: constraints);
    }

    private void RefreshVisibleFeatures()
    {
        var category = SelectedNavigation?.Id;
        var search = FeatureSearchText.Trim();
        var features = DemoCatalog.Features.Where(feature =>
            feature.Category == category &&
            (search.Length == 0 ||
             feature.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             feature.ShortName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             feature.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             feature.DependencySummary.Contains(search, StringComparison.OrdinalIgnoreCase)));

        VisibleFeatures.Clear();
        foreach (var feature in features)
        {
            VisibleFeatures.Add(feature);
        }

        SelectedFeature = VisibleFeatures.FirstOrDefault();
        OnPropertyChanged(nameof(HasVisibleFeatures));
        OnPropertyChanged(nameof(FeatureCountSummary));
    }

    private void ClearResult()
    {
        HasResult = false;
        ExecutionStage = ExecutionStage.Idle;
        ExecutionOutput = "尚未執行命令。";
        ResultFiles.Clear();
        ExistingChanges.Clear();
        GitDifferenceSummary = string.Empty;
        StatusMessage = "Demo 模式 · 不會修改任何檔案";
    }

    private bool CanOpenResultInFinderCommand() => CanOpenResultInFinder;

    private async Task OpenResultInFinderAsync()
    {
        var directory = WorkspacePath == "尚未選擇工作區"
            ? OutputPath
            : Path.Combine(WorkspacePath, OutputPath);
        var result = await _fileRevealService.RevealAsync(directory, CancellationToken.None);
        StatusMessage = result.Message;
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
            case "tool-install":
                arguments = ["tool", "install", "dotnet-ef", "--local"];
                break;
            case "blazor-crud":
                arguments = ["aspnet-codegenerator", "blazor", "CRUD", "--model", "Order", "--dataContext", "CommerceDbContext", "--project", projectPath, "--relativeFolderPath", "Components/Pages/Orders"];
                break;
            default:
                arguments = ["new", SelectedFeature.ShortName, "--name", ComponentName, "--output", OutputPath];
                break;
        }

        if (ParameterEditor?.GetBooleanValue("force") == true && !arguments.Contains("--force", StringComparer.Ordinal))
        {
            arguments.Add("--force");
        }

        return new CommandPreview("dotnet", arguments, workingDirectory, CurrentFeatureRisk);
    }

    private FeatureRisk CurrentFeatureRisk =>
        SelectedFeature?.Risk == FeatureRisk.Normal && ParameterEditor?.GetBooleanValue("force") == true
            ? FeatureRisk.FileOverwrite
            : SelectedFeature?.Risk ?? FeatureRisk.Normal;
}
