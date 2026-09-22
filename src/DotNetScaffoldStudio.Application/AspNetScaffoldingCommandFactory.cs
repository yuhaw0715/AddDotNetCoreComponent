using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public interface IAspNetScaffoldingCommandFactory
{
    CommandRequest Create(
        CatalogFeature feature,
        ParameterFormState state,
        string workspaceRoot);
}

public sealed class AspNetScaffoldingCommandFactory(ParameterValidator validator)
    : IAspNetScaffoldingCommandFactory
{
    public CommandRequest Create(
        CatalogFeature feature,
        ParameterFormState state,
        string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        if (!string.Equals(feature.CommandKind, "aspnet-codegenerator", StringComparison.Ordinal))
        {
            throw new ArgumentException("此命令工廠只能建立 ASP.NET Core Scaffolding 功能。", nameof(feature));
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
            new("aspnet-codegenerator"),
            new(feature.ShortNames[0])
        };
        AppendVariant(arguments, feature.Id, GetValue(state, "variant"));
        foreach (var parameter in feature.Parameters)
        {
            AppendParameter(arguments, feature.Id, parameter, state.GetValue(parameter.Id), root);
        }

        var expectedOutputs = string.IsNullOrWhiteSpace(outputPath)
            ? Array.Empty<string>()
            : GetExpectedOutputs(feature.Id, state, root, outputPath);
        return new CommandRequest(
            "dotnet",
            arguments,
            root,
            GetRisk(feature, state),
            modifiesWorkspace: true,
            expectedOutputs: expectedOutputs);
    }

    private static void AppendVariant(
        ICollection<CommandArgument> arguments,
        string featureId,
        string? variant)
    {
        if (string.IsNullOrWhiteSpace(variant) || variant is "Empty" or "IdentityFiles")
        {
            return;
        }

        if (featureId == "controller")
        {
            var option = variant switch
            {
                "ReadWriteActions" => "--readWriteActions",
                "MvcWithViews" => "--useDefaultLayout",
                "RestApi" => "--restWithNoViews",
                _ => null
            };
            if (option is not null)
            {
                arguments.Add(new CommandArgument(option));
            }

            return;
        }

        arguments.Add(new CommandArgument(variant));
    }

    private static void AppendParameter(
        ICollection<CommandArgument> arguments,
        string featureId,
        ParameterDefinition definition,
        ParameterValue value,
        string workspaceRoot)
    {
        if (definition.Id == "variant" || IsEmpty(value))
        {
            return;
        }

        var option = definition.Id switch
        {
            "project" => "-p",
            "name" => "--name",
            "model" => "--model",
            "dataContext" => "--dataContext",
            "databaseProvider" => "--databaseProvider",
            "output" => "--relativeFolderPath",
            "files" => "--files",
            "force" => "--force",
            _ => throw new InvalidOperationException($"不支援的 Scaffolding 參數：{definition.Id}")
        };

        if (value.Kind == ParameterValueKind.Boolean)
        {
            arguments.Add(new CommandArgument(option));
            return;
        }

        arguments.Add(new CommandArgument(option));
        var argumentValue = value.Kind switch
        {
            ParameterValueKind.Path => NormalizeWorkspaceRelativePath(workspaceRoot, value.TextValue!),
            ParameterValueKind.List => string.Join(';', value.ListValue),
            _ => value.TextValue!
        };
        arguments.Add(new CommandArgument(argumentValue, definition.IsSecret));
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
            throw new InvalidOperationException("輸出與專案路徑必須位於目前工作區內。");
        }

        return relative;
    }

    private static IReadOnlyList<string> GetExpectedOutputs(
        string featureId,
        ParameterFormState state,
        string workspaceRoot,
        string outputPath)
    {
        var output = NormalizeWorkspaceRelativePath(workspaceRoot, outputPath);
        if (featureId != "controller")
        {
            return [output];
        }

        var name = GetValue(state, "name");
        return string.IsNullOrWhiteSpace(name)
            ? [output]
            : [output, Path.Combine(output, $"{name}.cs")];
    }

    private static FeatureRisk GetRisk(CatalogFeature feature, ParameterFormState state) =>
        feature.Risk == FeatureRisk.Normal &&
        state.GetValue("force").BooleanValue == true
            ? FeatureRisk.FileOverwrite
            : feature.Risk;

    private static bool IsEmpty(ParameterValue value) =>
        value.Kind switch
        {
            ParameterValueKind.Boolean => value.BooleanValue != true,
            ParameterValueKind.List => value.ListValue.Count == 0,
            _ => string.IsNullOrWhiteSpace(value.TextValue)
        };
}
