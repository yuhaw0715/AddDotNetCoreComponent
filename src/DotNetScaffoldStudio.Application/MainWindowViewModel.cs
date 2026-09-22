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
    private readonly IUiTextProvider _text;
    private CancellationTokenSource? _executionCancellation;
    private NavigationItem? _selectedNavigation;
    private FeatureDefinition? _selectedFeature;
    private ProjectInfo? _selectedProject;
    private string _workspacePath = string.Empty;
    private string _componentName = string.Empty;
    private string _outputPath = string.Empty;
    private string _selectedTemplateMode = string.Empty;
    private string _featureSearchText = string.Empty;
    private bool _useAsyncActions = true;
    private bool _includeApiActions;
    private bool _isNavigationCollapsed;
    private bool _isScanning;
    private bool _isConfirmationVisible;
    private bool _isRunning;
    private bool _hasResult;
    private ExecutionStage _executionStage;
    private string _statusMessage = string.Empty;
    private string _executionOutput = string.Empty;
    private string _resultTitle = string.Empty;
    private string _resultSummary = string.Empty;
    private string _gitDifferenceSummary = string.Empty;

    public MainWindowViewModel(IWorkspaceService workspaceService, IDemoExecutionService executionService)
        : this(workspaceService, executionService, new ParameterValidator(), new DemoFileRevealService(), new DefaultUiTextProvider())
    {
    }

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IDemoExecutionService executionService,
        ParameterValidator parameterValidator)
        : this(workspaceService, executionService, parameterValidator, new DemoFileRevealService(), new DefaultUiTextProvider())
    {
    }

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IDemoExecutionService executionService,
        ParameterValidator parameterValidator,
        IFileRevealService fileRevealService)
        : this(workspaceService, executionService, parameterValidator, fileRevealService, new DefaultUiTextProvider())
    {
    }

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IDemoExecutionService executionService,
        ParameterValidator parameterValidator,
        IFileRevealService fileRevealService,
        IUiTextProvider text)
    {
        _workspaceService = workspaceService;
        _executionService = executionService;
        _parameterValidator = parameterValidator;
        _fileRevealService = fileRevealService;
        _text = text;
        _workspacePath = _text.Get("Status.WorkspacePlaceholder");
        _statusMessage = _text.Get("Status.DemoSafe");
        _executionOutput = _text.Get("Status.ExecutionOutputEmpty");
        _componentName = _text.Get("Defaults.ControllerName");
        _outputPath = _text.Get("Defaults.OutputPath");
        _selectedTemplateMode = _text.Get("Template.ReadWrite");

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
    public IReadOnlyList<string> TemplateModes =>
    [
        _text.Get("Template.Empty"),
        _text.Get("Template.ReadWrite"),
        _text.Get("Template.MvcCrud"),
        _text.Get("Template.RestApi")
    ];

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
                "migration-add" => _text.Get("Defaults.MigrationName"),
                "database-update" => _text.Get("Defaults.LatestMigration"),
                "webapi" or "mvc" or "blazor" or "console" => _text.Get("Defaults.ProjectName"),
                "razorcomponent" => _text.Get("Defaults.RazorComponentName"),
                "page" => _text.Get("Defaults.PageName"),
                _ => _text.Get("Defaults.ControllerName")
            };

            OutputPath = value?.Category == "project"
                ? _text.Get("Defaults.GeneratedOutputPath")
                : _text.Get("Defaults.OutputPath");
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
        ConfirmationKind.FileOverwrite => _text.Get("Confirmation.OverwriteTitle"),
        ConfirmationKind.ToolInstallation => _text.Get("Confirmation.ToolTitle"),
        ConfirmationKind.DatabaseUpdate => _text.Get("Confirmation.DatabaseTitle"),
        _ => _text.Get("Confirmation.GeneralTitle")
    };
    public string ConfirmationMessage => CurrentConfirmationKind switch
    {
        ConfirmationKind.FileOverwrite => _text.Get("Confirmation.OverwriteMessage"),
        ConfirmationKind.ToolInstallation => _text.Get("Confirmation.ToolMessage"),
        ConfirmationKind.DatabaseUpdate => _text.Get("Confirmation.DatabaseMessage"),
        _ => _text.Get("Confirmation.GeneralMessage")
    };
    public string ConfirmationDetails => CurrentConfirmationKind switch
    {
        ConfirmationKind.FileOverwrite => _text.Get("Confirmation.OverwriteDetails"),
        ConfirmationKind.ToolInstallation => _text.Get("Confirmation.ToolDetails"),
        ConfirmationKind.DatabaseUpdate => _text.Format(
            "Confirmation.DatabaseDetails",
            SelectedProject?.Path ?? _text.Get("Status.WorkspacePlaceholder"),
            ParameterEditor?.GetTextValue("dbContext") ?? _text.Get("Confirmation.DbContextFallback")),
        _ => _text.Get("Confirmation.GeneralDetails")
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
        ExecutionStage.AwaitingConfirmation => _text.Get("Stage.AwaitingConfirmation"),
        ExecutionStage.Executing => _text.Get("Stage.Executing"),
        ExecutionStage.Succeeded => _text.Get("Stage.Succeeded"),
        ExecutionStage.Failed => _text.Get("Stage.Failed"),
        ExecutionStage.Cancelled => _text.Get("Stage.Cancelled"),
        _ => _text.Get("Stage.Idle")
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
    public string SelectedFeatureTitle => SelectedFeature?.DisplayName ?? _text.Get("Feature.Select");
    public string SelectedFeatureDescription => SelectedFeature?.Description ?? _text.Get("Feature.DescriptionPlaceholder");
    public string SelectedFeatureBadge => SelectedFeature?.Badge ?? string.Empty;
    public string AvailabilityMessage => RequiresTargetProject && SelectedProject is null
        ? _text.Get("Feature.TargetProjectRequired")
        : SelectedFeature?.AvailabilityReason ?? _text.Get("Feature.AvailableOnPlatform");
    public string ValidationMessage => ParameterEditor?.FirstErrorMessage ??
        (string.IsNullOrWhiteSpace(ComponentName) ? _text.Get("Validation.RequiredName") : string.Empty);
    public string CommandPreviewText => BuildCommandPreview()?.DisplayText ?? _text.Get("Feature.CommandPreviewPlaceholder");
    public string WorkingDirectoryText => BuildCommandPreview()?.WorkingDirectory ?? WorkspacePath;
    public string FeatureCountSummary => _text.Format("Feature.Count", VisibleFeatures.Count);

    public async Task LoadWorkspaceAsync(string path)
    {
        await LoadWorkspaceCoreAsync(path);
    }

    private bool CanScanWorkspace() => !IsScanning && !string.IsNullOrWhiteSpace(WorkspacePath) && !IsWorkspacePlaceholder(WorkspacePath);

    private Task ScanWorkspaceAsync() => LoadWorkspaceCoreAsync(WorkspacePath);

    private async Task LoadWorkspaceCoreAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsWorkspacePlaceholder(path))
        {
            StatusMessage = _text.Get("Status.InvalidWorkspace");
            return;
        }

        IsScanning = true;
        StatusMessage = _text.Get("Status.Scanning");

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
                ? _text.Get("Status.NoProjects")
                : Projects.Count == 1
                    ? _text.Get("Status.SingleProject")
                    : _text.Format("Status.MultipleProjects", Projects.Count);
        }
        catch (Exception exception)
        {
            StatusMessage = _text.Format("Status.LoadFailure", exception.Message);
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
        StatusMessage = _text.Get("Status.DemoWorkspaceLoaded");
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
            ? _text.Get("Status.HighRiskConfirmation")
            : _text.Get("Status.ConfirmationRequired");
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
        StatusMessage = _text.Get("Status.DemoExecuting");
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
                _text.Format(
                    "Result.GitDifferenceFallback",
                    result.GitBranch ?? _text.Get("Result.NonGitWorkspace"),
                    ResultFiles.Count,
                    ExistingChanges.Count);

            HasResult = true;
            ExecutionStage = result.Succeeded ? ExecutionStage.Succeeded : ExecutionStage.Failed;
            StatusMessage = result.Succeeded ? _text.Get("Status.DemoCompleted") : _text.Get("Status.DemoFailed");
        }
        catch (OperationCanceledException)
        {
            ResultTitle = _text.Get("Result.CancelledTitle");
            ResultSummary = _text.Get("Result.CancelledSummary");
            ResultFiles.Clear();
            ExistingChanges.Clear();
            GitDifferenceSummary = _text.Get("Result.CancelledDifference");
            HasResult = true;
            ExecutionStage = ExecutionStage.Cancelled;
            StatusMessage = _text.Get("Status.Cancelled");
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

    private CatalogFeature CreateParameterSchema(FeatureDefinition feature)
    {
        var parameters = new List<ParameterDefinition>
        {
            new("name", _text.Get("Parameter.Name"), ParameterValueKind.Text, isRequired: true),
            new("output", _text.Get("Parameter.OutputPath"), ParameterValueKind.Path, isAdvanced: true),
            new("force", _text.Get("Parameter.ForceOutput"), ParameterValueKind.Boolean, isAdvanced: true)
        };
        var constraints = new List<ParameterConstraint>
        {
            ParameterConstraint.NameFormat("name"),
            ParameterConstraint.PathWithinWorkspace("output")
        };

        if (feature.Category == "scaffolding")
        {
            parameters.Add(new ParameterDefinition(
                "variant",
                _text.Get("Parameter.TemplateMode"),
                ParameterValueKind.Enumeration,
                allowedValues:
                [
                    _text.Get("Template.Empty"),
                    _text.Get("Template.ReadWrite"),
                    _text.Get("Template.MvcCrud"),
                    _text.Get("Template.RestApi")
                ]));
            parameters.Add(new ParameterDefinition("model", _text.Get("Parameter.Model"), ParameterValueKind.Text, isAdvanced: true));
            parameters.Add(new ParameterDefinition("dataContext", _text.Get("Parameter.DbContext"), ParameterValueKind.Text, isAdvanced: true));
        }
        else if (feature.Category == "efcore")
        {
            parameters.Add(new ParameterDefinition("connection", _text.Get("Parameter.NamedConnection"), ParameterValueKind.Secret, isAdvanced: true));
            parameters.Add(new ParameterDefinition("dbContext", _text.Get("Parameter.DbContext"), ParameterValueKind.Text, isAdvanced: true));
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
        ExecutionOutput = _text.Get("Status.ExecutionOutputEmpty");
        ResultFiles.Clear();
        ExistingChanges.Clear();
        GitDifferenceSummary = string.Empty;
        StatusMessage = _text.Get("Status.DemoSafe");
    }

    private bool CanOpenResultInFinderCommand() => CanOpenResultInFinder;

    private async Task OpenResultInFinderAsync()
    {
        var directory = IsWorkspacePlaceholder(WorkspacePath)
            ? OutputPath
            : Path.Combine(WorkspacePath, OutputPath);
        var result = await _fileRevealService.RevealAsync(directory, CancellationToken.None);
        StatusMessage = result.Succeeded ? result.Message : _text.Format("Status.FinderFailure", result.Message);
    }

    private CommandPreview? BuildCommandPreview()
    {
        if (SelectedFeature is null)
        {
            return null;
        }

        var projectPath = SelectedProject?.Path ?? "./src/MyApp/MyApp.csproj";
        var workingDirectory = IsWorkspacePlaceholder(WorkspacePath)
            ? _text.Get("Defaults.WorkingDirectory")
            : WorkspacePath;
        List<string> arguments;

        switch (SelectedFeature.Id)
        {
            case "controller":
                arguments = ["aspnet-codegenerator", "controller", "--controllerName", ComponentName, "--project", projectPath, "--relativeFolderPath", OutputPath];
                if (SelectedTemplateMode == _text.Get("Template.ReadWrite")) arguments.Add("--readWriteActions");
                if (SelectedTemplateMode == _text.Get("Template.RestApi") || IncludeApiActions) arguments.Add("--restWithNoViews");
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

    private bool IsWorkspacePlaceholder(string path) =>
        string.Equals(path, _text.Get("Status.WorkspacePlaceholder"), StringComparison.Ordinal);
}
