using System.Text.RegularExpressions;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public sealed class ParameterValidator
{
    public ParameterValidationResult Validate(
        CatalogFeature feature,
        ParameterFormState state,
        string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var errors = new List<ParameterValidationError>();
        var fields = state.Fields.ToDictionary(field => field.Definition.Id, StringComparer.Ordinal);
        var root = Path.GetFullPath(workspaceRoot);
        foreach (var parameter in feature.Parameters)
        {
            if (!fields.TryGetValue(parameter.Id, out var field))
            {
                errors.Add(new ParameterValidationError(
                    parameter.Id,
                    ParameterValidationErrorKind.Required,
                    "表單缺少此欄位，無法執行。"));
                continue;
            }

            if (parameter.IsRequired && IsEmpty(field.Value))
            {
                errors.Add(new ParameterValidationError(
                    parameter.Id,
                    ParameterValidationErrorKind.Required,
                    "此欄位為必填。"));
            }
        }

        foreach (var constraint in feature.ValidationConstraints)
        {
            switch (constraint.Kind)
            {
                case ParameterConstraintKind.NameFormat:
                    ValidateName(constraint, fields, errors);
                    break;
                case ParameterConstraintKind.PathWithinWorkspace:
                    ValidatePath(constraint, fields, root, errors);
                    break;
                case ParameterConstraintKind.RequiredWhen:
                    ValidateConditionalRequired(constraint, fields, errors);
                    break;
                case ParameterConstraintKind.MutuallyExclusive:
                    ValidateMutuallyExclusive(constraint, fields, errors);
                    break;
            }
        }

        return new ParameterValidationResult(errors.AsReadOnly());
    }

    private static void ValidateName(
        ParameterConstraint constraint,
        IReadOnlyDictionary<string, ParameterFieldState> fields,
        ICollection<ParameterValidationError> errors)
    {
        var parameterId = constraint.ParameterIds[0];
        if (!fields.TryGetValue(parameterId, out var field) || IsEmpty(field.Value))
        {
            return;
        }

        if (!Regex.IsMatch(field.Value.TextValue ?? string.Empty, constraint.Pattern ?? "^$", RegexOptions.CultureInvariant))
        {
            errors.Add(new ParameterValidationError(
                parameterId,
                ParameterValidationErrorKind.NameFormat,
                "名稱格式無效，請使用英文字母、數字、底線、句點或連字號，且不能以數字開頭。"));
        }
    }

    private static void ValidatePath(
        ParameterConstraint constraint,
        IReadOnlyDictionary<string, ParameterFieldState> fields,
        string workspaceRoot,
        ICollection<ParameterValidationError> errors)
    {
        var parameterId = constraint.ParameterIds[0];
        if (!fields.TryGetValue(parameterId, out var field) || IsEmpty(field.Value))
        {
            return;
        }

        var candidate = Path.GetFullPath(field.Value.TextValue!, workspaceRoot);
        var relative = Path.GetRelativePath(workspaceRoot, candidate);
        if (Path.IsPathRooted(relative) ||
            string.Equals(relative, "..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            errors.Add(new ParameterValidationError(
                parameterId,
                ParameterValidationErrorKind.PathBoundary,
                "路徑必須位於目前工作區內。"));
        }
    }

    private static void ValidateConditionalRequired(
        ParameterConstraint constraint,
        IReadOnlyDictionary<string, ParameterFieldState> fields,
        ICollection<ParameterValidationError> errors)
    {
        var triggerId = constraint.ParameterIds[0];
        var dependentId = constraint.ParameterIds[1];
        if (!fields.TryGetValue(triggerId, out var trigger) ||
            !fields.TryGetValue(dependentId, out var dependent) ||
            !IsActive(trigger.Value, constraint.TriggerValue) ||
            !IsEmpty(dependent.Value))
        {
            return;
        }

        errors.Add(new ParameterValidationError(
            dependentId,
            ParameterValidationErrorKind.ConditionalRequired,
            $"當「{trigger.Definition.DisplayName}」啟用時，此欄位為必填。"));
    }

    private static void ValidateMutuallyExclusive(
        ParameterConstraint constraint,
        IReadOnlyDictionary<string, ParameterFieldState> fields,
        ICollection<ParameterValidationError> errors)
    {
        var active = constraint.ParameterIds
            .Where(fields.ContainsKey)
            .Where(parameterId => IsActive(fields[parameterId].Value, null))
            .ToArray();
        if (active.Length < 2)
        {
            return;
        }

        foreach (var parameterId in active)
        {
            var otherNames = active
                .Where(other => !string.Equals(other, parameterId, StringComparison.Ordinal))
                .Select(other => fields[other].Definition.DisplayName);
            errors.Add(new ParameterValidationError(
                parameterId,
                ParameterValidationErrorKind.MutuallyExclusive,
                $"此欄位不可與「{string.Join("」、「", otherNames)}」同時使用。"));
        }
    }

    private static bool IsActive(ParameterValue value, string? expectedValue) =>
        expectedValue is not null
            ? string.Equals(value.TextValue, expectedValue, StringComparison.Ordinal)
            : value.Kind == ParameterValueKind.Boolean
                ? value.BooleanValue == true
                : !IsEmpty(value);

    private static bool IsEmpty(ParameterValue value) =>
        value.Kind switch
        {
            ParameterValueKind.Boolean => value.BooleanValue != true,
            ParameterValueKind.List => value.ListValue.Count == 0,
            _ => string.IsNullOrWhiteSpace(value.TextValue)
        };
}
