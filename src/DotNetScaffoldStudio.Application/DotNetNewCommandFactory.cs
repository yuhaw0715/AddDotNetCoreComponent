using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public interface IDotNetNewCommandFactory
{
    CommandRequest Create(
        CatalogFeature feature,
        ParameterFormState state,
        string workspaceRoot);
}

public sealed class ParameterValidationException(ParameterValidationResult result)
    : InvalidOperationException("參數驗證未通過，無法建立命令。")
{
    public ParameterValidationResult Result { get; } = result;
}

public sealed class DotNetNewCommandFactory(ParameterValidator validator) : IDotNetNewCommandFactory
{
    public CommandRequest Create(
        CatalogFeature feature,
        ParameterFormState state,
        string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        if (!string.Equals(feature.CommandKind, "dotnet-new", StringComparison.Ordinal))
        {
            throw new ArgumentException("此命令工廠只能建立 dotnet new 功能。", nameof(feature));
        }

        var root = Path.GetFullPath(workspaceRoot);
        var validation = validator.Validate(feature, state, root);
        if (!validation.IsValid)
        {
            throw new ParameterValidationException(validation);
        }

        var outputPath = GetValue(state, "output");
        var arguments = new List<CommandArgument>
        {
            new("new"),
            new(feature.ShortNames[0])
        };
        foreach (var parameter in feature.Parameters)
        {
            var value = state.GetValue(parameter.Id);
            AppendParameter(arguments, parameter, value, root);
        }

        var expectedOutputs = string.IsNullOrWhiteSpace(outputPath)
            ? Array.Empty<string>()
            : [NormalizeWorkspaceRelativePath(root, outputPath)];
        return new CommandRequest(
            "dotnet",
            arguments,
            root,
            GetRisk(feature, state),
            modifiesWorkspace: true,
            expectedOutputs: expectedOutputs);
    }

    private static void AppendParameter(
        ICollection<CommandArgument> arguments,
        ParameterDefinition definition,
        ParameterValue value,
        string workspaceRoot)
    {
        if (IsEmpty(value))
        {
            return;
        }

        var option = new CommandArgument($"--{definition.Id}");
        switch (value.Kind)
        {
            case ParameterValueKind.Boolean when value.BooleanValue == true:
                arguments.Add(option);
                break;
            case ParameterValueKind.Boolean:
                break;
            case ParameterValueKind.List:
                arguments.Add(option);
                arguments.Add(new CommandArgument(string.Join(',', value.ListValue)));
                break;
            case ParameterValueKind.Path:
                arguments.Add(option);
                arguments.Add(new CommandArgument(NormalizeWorkspaceRelativePath(workspaceRoot, value.TextValue!)));
                break;
            default:
                arguments.Add(option);
                arguments.Add(new CommandArgument(value.TextValue!, definition.IsSecret));
                break;
        }
    }

    private static string? GetValue(ParameterFormState state, string parameterId)
    {
        try
        {
            return state.GetValue(parameterId).TextValue;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string NormalizeWorkspaceRelativePath(string workspaceRoot, string path)
    {
        var candidate = Path.GetFullPath(path, workspaceRoot);
        var relative = Path.GetRelativePath(workspaceRoot, candidate);
        if (Path.IsPathRooted(relative) ||
            string.Equals(relative, "..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("輸出路徑必須位於目前工作區內。");
        }

        return relative;
    }

    private static bool IsEmpty(ParameterValue value) =>
        value.Kind switch
        {
            ParameterValueKind.Boolean => value.BooleanValue != true,
            ParameterValueKind.List => value.ListValue.Count == 0,
            _ => string.IsNullOrWhiteSpace(value.TextValue)
        };

    private static FeatureRisk GetRisk(CatalogFeature feature, ParameterFormState state) =>
        feature.Risk == FeatureRisk.Normal &&
        state.GetValue("force").BooleanValue == true
            ? FeatureRisk.FileOverwrite
            : feature.Risk;
}
