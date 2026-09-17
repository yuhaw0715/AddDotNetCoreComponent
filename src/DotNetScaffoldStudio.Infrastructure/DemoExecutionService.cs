using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Infrastructure;

public sealed class DemoExecutionService : IDemoExecutionService
{
    public async Task<DemoExecutionResult> ExecuteAsync(
        CommandPreview command,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var steps = new[]
        {
            "[Demo] 正在驗證目標專案…",
            "[Demo] 正在檢查所需工具與套件…",
            $"[Demo] 預備執行：{command.DisplayText}",
            "[Demo] 正在模擬產生檔案…",
            "[Demo] 正在比較 Git 與檔案差異…"
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
            "示意產生成功",
            "這是流程 Demo；沒有執行外部 CLI，也沒有修改工作區。正式版本將在此顯示真實結束碼、耗時與差異。",
            steps,
            [$"A  {generatedFolder}/{generatedName}.cs", "M  Program.cs"]);
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
