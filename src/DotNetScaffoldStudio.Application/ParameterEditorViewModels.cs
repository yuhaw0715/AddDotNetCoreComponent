using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public abstract class ParameterFieldViewModel : ObservableObject
{
    private string _errorMessage = string.Empty;

    protected ParameterFieldViewModel(ParameterDefinition definition)
    {
        Definition = definition;
    }

    public ParameterDefinition Definition { get; }
    public bool IsAdvanced => Definition.IsAdvanced;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    internal Action<ParameterFieldViewModel>? Changed { get; set; }

    internal void SetError(string? message)
    {
        if (SetProperty(ref _errorMessage, message ?? string.Empty, nameof(ErrorMessage)))
        {
            OnPropertyChanged(nameof(HasError));
        }
    }
    internal abstract ParameterValue ToParameterValue();
}

public sealed class TextParameterFieldViewModel : ParameterFieldViewModel
{
    private string _textValue;

    public TextParameterFieldViewModel(ParameterDefinition definition, string initialValue)
        : base(definition)
    {
        _textValue = initialValue;
    }

    public string TextValue
    {
        get => _textValue;
        set
        {
            if (SetProperty(ref _textValue, value))
            {
                Changed?.Invoke(this);
            }
        }
    }

    public char PasswordChar => Definition.IsSecret ? '●' : '\0';

    internal override ParameterValue ToParameterValue() => Definition.Kind switch
    {
        ParameterValueKind.Path => ParameterValue.Path(TextValue),
        ParameterValueKind.Secret => ParameterValue.Secret(TextValue),
        _ => ParameterValue.Text(TextValue)
    };
}

public sealed class EnumerationParameterFieldViewModel : ParameterFieldViewModel
{
    private string _selectedValue;

    public EnumerationParameterFieldViewModel(ParameterDefinition definition, string initialValue)
        : base(definition)
    {
        _selectedValue = initialValue;
    }

    public IReadOnlyList<string> AllowedValues => Definition.AllowedValues;

    public string SelectedValue
    {
        get => _selectedValue;
        set
        {
            if (SetProperty(ref _selectedValue, value))
            {
                Changed?.Invoke(this);
            }
        }
    }

    internal override ParameterValue ToParameterValue() => ParameterValue.Enumeration(SelectedValue);
}

public sealed class BooleanParameterFieldViewModel : ParameterFieldViewModel
{
    private bool _isChecked;

    public BooleanParameterFieldViewModel(ParameterDefinition definition, bool initialValue)
        : base(definition)
    {
        _isChecked = initialValue;
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
            {
                Changed?.Invoke(this);
            }
        }
    }

    internal override ParameterValue ToParameterValue() => ParameterValue.Boolean(IsChecked);
}

public sealed class ParameterEditorViewModel : ObservableObject
{
    private readonly CatalogFeature _feature;
    private readonly ParameterValidator _validator;
    private readonly ParameterFormState _state;
    private string _workspaceRoot;
    private bool _showAdvanced;
    private ParameterValidationResult _validation = new([]);

    public ParameterEditorViewModel(
        CatalogFeature feature,
        ParameterValidator validator,
        string workspaceRoot,
        IReadOnlyDictionary<string, string>? initialTextValues = null)
    {
        _feature = feature;
        _validator = validator;
        _workspaceRoot = workspaceRoot;
        _state = new ParameterFormState(feature.Parameters);

        foreach (var definition in feature.Parameters)
        {
            var initialText = initialTextValues?.GetValueOrDefault(definition.Id) ?? string.Empty;
            ParameterFieldViewModel field = definition.Kind switch
            {
                ParameterValueKind.Boolean => new BooleanParameterFieldViewModel(definition, false),
                ParameterValueKind.Enumeration => new EnumerationParameterFieldViewModel(
                    definition,
                    definition.AllowedValues.Contains(initialText, StringComparer.Ordinal)
                        ? initialText
                        : definition.AllowedValues[0]),
                _ => new TextParameterFieldViewModel(definition, initialText)
            };
            field.Changed = HandleFieldChanged;
            Fields.Add(field);
            _state.SetValue(definition.Id, field.ToParameterValue());
        }

        Validate();
    }

    public ObservableCollection<ParameterFieldViewModel> Fields { get; } = [];
    public IReadOnlyList<ParameterFieldViewModel> VisibleFields =>
        Fields.Where(parameterField => ShowAdvanced || !parameterField.IsAdvanced).ToArray();
    public bool ShowAdvanced
    {
        get => _showAdvanced;
        set
        {
            if (SetProperty(ref _showAdvanced, value))
            {
                OnPropertyChanged(nameof(VisibleFields));
            }
        }
    }

    public bool IsValid => _validation.IsValid;
    public IReadOnlyList<ParameterValidationError> Errors => _validation.Errors;
    public string FirstErrorMessage => _validation.Errors.FirstOrDefault()?.Message ?? string.Empty;

    public event Action? ValuesChanged;

    public void SetWorkspaceRoot(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = workspaceRoot;
        Validate();
    }

    public string? GetTextValue(string parameterId) =>
        Fields.FirstOrDefault(field => field.Definition.Id == parameterId) switch
        {
            TextParameterFieldViewModel text => text.TextValue,
            EnumerationParameterFieldViewModel enumeration => enumeration.SelectedValue,
            _ => null
        };

    public bool GetBooleanValue(string parameterId) =>
        Fields.FirstOrDefault(parameterField => parameterField.Definition.Id == parameterId) is BooleanParameterFieldViewModel boolean &&
        boolean.IsChecked;

    private void HandleFieldChanged(ParameterFieldViewModel field)
    {
        _state.SetValue(field.Definition.Id, field.ToParameterValue());
        Validate();
        ValuesChanged?.Invoke();
    }

    private void Validate()
    {
        _validation = _validator.Validate(_feature, _state, _workspaceRoot);
        foreach (var field in Fields)
        {
            field.SetError(_validation.Errors.FirstOrDefault(error => error.ParameterId == field.Definition.Id)?.Message);
        }

        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(Errors));
        OnPropertyChanged(nameof(FirstErrorMessage));
    }
}
