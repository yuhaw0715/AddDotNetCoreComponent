namespace DotNetScaffoldStudio.Domain;

public enum ParameterConstraintKind
{
    NameFormat,
    PathWithinWorkspace,
    RequiredWhen,
    MutuallyExclusive
}

public sealed record ParameterConstraint
{
    private ParameterConstraint(
        ParameterConstraintKind kind,
        IReadOnlyList<string> parameterIds,
        string? pattern,
        string? triggerValue)
    {
        if (parameterIds.Count == 0 || parameterIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("參數驗證規則必須指定參數。", nameof(parameterIds));
        }

        if (kind == ParameterConstraintKind.MutuallyExclusive && parameterIds.Count < 2)
        {
            throw new ArgumentException("互斥規則至少需要兩個參數。", nameof(parameterIds));
        }

        Kind = kind;
        ParameterIds = Array.AsReadOnly(parameterIds.ToArray());
        Pattern = pattern;
        TriggerValue = triggerValue;
    }

    public ParameterConstraintKind Kind { get; }
    public IReadOnlyList<string> ParameterIds { get; }
    public string? Pattern { get; }
    public string? TriggerValue { get; }

    public static ParameterConstraint NameFormat(
        string parameterId,
        string pattern = "^[A-Za-z_][A-Za-z0-9_.-]*$") =>
        new(ParameterConstraintKind.NameFormat, [parameterId], pattern, null);

    public static ParameterConstraint PathWithinWorkspace(string parameterId) =>
        new(ParameterConstraintKind.PathWithinWorkspace, [parameterId], null, null);

    public static ParameterConstraint RequiredWhen(
        string triggerParameterId,
        string dependentParameterId,
        string? triggerValue = null) =>
        new(ParameterConstraintKind.RequiredWhen, [triggerParameterId, dependentParameterId], null, triggerValue);

    public static ParameterConstraint MutuallyExclusive(params string[] parameterIds) =>
        new(ParameterConstraintKind.MutuallyExclusive, parameterIds, null, null);
}

public enum ParameterValidationErrorKind
{
    Required,
    NameFormat,
    PathBoundary,
    ConditionalRequired,
    MutuallyExclusive
}

public sealed record ParameterValidationError(
    string ParameterId,
    ParameterValidationErrorKind Kind,
    string Message);

public sealed record ParameterValidationResult(IReadOnlyList<ParameterValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
