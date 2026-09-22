using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class DemoExecutionService : IDemoExecutionService
{
    private readonly IUiTextProvider _text;

    public DemoExecutionService(IUiTextProvider? text = null)
    {
        _text = text ?? new DefaultUiTextProvider();
    }

    public async Task<DemoExecutionResult> ExecuteAsync(
        CommandPreview command,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var steps = new[]
        {
            _text.Get("Execution.StepValidate"),
            _text.Get("Execution.StepDependencies"),
            _text.Format("Execution.StepPrepare", command.DisplayText),
            _text.Get("Execution.StepGenerate"),
            _text.Get("Execution.StepDiff")
        };

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(step);
            await Task.Delay(420, cancellationToken);
        }

        var generatedName = GetArgumentValue(command.Arguments, "--controllerName") ?? "GeneratedItem";
        var generatedFolder = GetArgumentValue(command.Arguments, "--relativeFolderPath") ?? "Generated";

        return new DemoExecutionResult(
            true,
            _text.Get("Result.DemoTitle"),
            _text.Get("Result.DemoSummary"),
            steps,
            [$"A  {generatedFolder}/{generatedName}.cs"],
            ["M  Program.cs"],
            "demo/main",
            _text.Get("Result.DemoGitStatus"));
    }

    private static string? GetArgumentValue(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], option, StringComparison.Ordinal))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }
}
