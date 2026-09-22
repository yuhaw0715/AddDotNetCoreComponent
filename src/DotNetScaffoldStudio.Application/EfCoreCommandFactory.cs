using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public interface IEfCoreCommandFactory
{
    CommandRequest Create(
        CatalogFeature feature,
        ParameterFormState state,
        string workspaceRoot);

    DatabaseUpdateConfirmationData CreateDatabaseUpdateConfirmation(
        CatalogFeature feature,
        ParameterFormState state,
        CommandRequest request);
}

public sealed class EfCoreCommandFactory(ParameterValidator validator) : IEfCoreCommandFactory
{
    public CommandRequest Create(
        CatalogFeature feature,
        ParameterFormState state,
        string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        if (!string.Equals(feature.CommandKind, "dotnet-ef", StringComparison.Ordinal))
        {
            throw new ArgumentException("此命令工廠只能建立 EF Core 功能。", nameof(feature));
        }

        if (feature.ShortNames.Any(name => string.Equals(name, "database drop", StringComparison.Ordinal)))
        {
            throw new ArgumentException("第一版不提供 database drop。", nameof(feature));
        }

        var root = Path.GetFullPath(workspaceRoot);
        var validation = validator.Validate(feature, state, root);
        if (!validation.IsValid)
        {
            throw new ParameterValidationException(validation);
        }

        var arguments = new List<CommandArgument>
        {
            new("ef")
        };
        arguments.AddRange(feature.ShortNames[0]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(argument => new CommandArgument(argument)));
        AppendPositionals(arguments, feature.Id, state);
        foreach (var parameter in feature.Parameters)
        {
            AppendParameter(arguments, feature.Id, parameter, state.GetValue(parameter.Id), root);
        }

        return new CommandRequest(
            "dotnet",
            arguments,
            root,
            feature.Risk,
            modifiesWorkspace: feature.Id is not "migrations-list" and not "migrations-has-pending-model-changes" and not "dbcontext-info" and not "dbcontext-list",
            expectedOutputs: GetExpectedOutputs(feature.Id, state, root));
    }

    public DatabaseUpdateConfirmationData CreateDatabaseUpdateConfirmation(
        CatalogFeature feature,
        ParameterFormState state,
        CommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (feature.Id != "database-update" || feature.CommandKind != "dotnet-ef")
        {
            throw new ArgumentException("只有 database update 可以建立資料庫二次確認資料。", nameof(feature));
        }

        var connection = GetValue(state, "connection");
        var source = string.IsNullOrWhiteSpace(connection)
            ? DatabaseConnectionSource.ProjectConfiguration
            : connection.StartsWith("Name=", StringComparison.OrdinalIgnoreCase)
                ? DatabaseConnectionSource.NamedConnection
                : DatabaseConnectionSource.DirectConnectionString;
        return new DatabaseUpdateConfirmationData(
            GetValue(state, "project") ?? string.Empty,
            GetValue(state, "context"),
            source,
            CommandPreviewFormatter.Format(request),
            "database update 會修改目標資料庫，必須取得二次確認。");
    }

    private static void AppendPositionals(
        ICollection<CommandArgument> arguments,
        string featureId,
        ParameterFormState state)
    {
        switch (featureId)
        {
            case "dbcontext-scaffold":
                AddText(arguments, GetValue(state, "connection"), isSecret: true);
                AddText(arguments, GetValue(state, "provider"));
                break;
            case "migrations-add":
            case "migrations-remove":
                AddText(arguments, GetValue(state, "migrationName"));
                break;
            case "database-update":
                AddText(arguments, GetValue(state, "migration"));
                break;
        }
    }

    private static void AppendParameter(
        ICollection<CommandArgument> arguments,
        string featureId,
        ParameterDefinition definition,
        ParameterValue value,
        string workspaceRoot)
    {
        if (definition.Id is "migrationName" or "provider" or "migration" ||
            featureId == "dbcontext-scaffold" && definition.Id == "connection" ||
            IsEmpty(value))
        {
            return;
        }

        var option = definition.Id switch
        {
            "project" => "--project",
            "context" => "--context",
            "connection" => "--connection",
            "fromMigration" => "--from",
            "toMigration" => "--to",
            "output" when featureId is "dbcontext-scaffold" or "dbcontext-optimize" => "--output-dir",
            "output" => "--output",
            "force" => "--force",
            _ => throw new InvalidOperationException($"不支援的 EF Core 參數：{definition.Id}")
        };

        if (value.Kind == ParameterValueKind.Boolean)
        {
            arguments.Add(new CommandArgument(option));
            return;
        }

        arguments.Add(new CommandArgument(option));
        var argumentValue = value.Kind == ParameterValueKind.Path
            ? NormalizeWorkspaceRelativePath(workspaceRoot, value.TextValue!)
            : value.TextValue!;
        arguments.Add(new CommandArgument(argumentValue, definition.IsSecret));
    }

    private static IReadOnlyList<string> GetExpectedOutputs(
        string featureId,
        ParameterFormState state,
        string workspaceRoot)
    {
        var output = GetValue(state, "output");
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        return [NormalizeWorkspaceRelativePath(workspaceRoot, output)];
    }

    private static void AddText(ICollection<CommandArgument> arguments, string? value, bool isSecret = false)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments.Add(new CommandArgument(value, isSecret));
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
            throw new InvalidOperationException("專案與輸出路徑必須位於目前工作區內。");
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
}
