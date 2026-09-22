using System.Globalization;

namespace DotNetScaffoldStudio.Application;

public interface IUiTextProvider
{
    string Get(string key);
    string Format(string key, params object[] arguments);
}

public sealed class DefaultUiTextProvider : IUiTextProvider
{
    private static readonly IReadOnlyDictionary<string, string> Texts =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Status.WorkspacePlaceholder"] = "尚未選擇工作區",
            ["Status.ExecutionOutputEmpty"] = "尚未執行命令。",
            ["Status.DemoSafe"] = "Demo 模式 · 不會修改任何檔案",
            ["Status.InvalidWorkspace"] = "請先選擇有效的工作區。",
            ["Status.Scanning"] = "正在掃描 .NET 專案…",
            ["Status.NoProjects"] = "找不到 .csproj；仍可使用專案範本功能。",
            ["Status.SingleProject"] = "已找到 1 個專案，已自動選取目標。",
            ["Status.MultipleProjects"] = "已找到 {0} 個專案，請選擇目標。",
            ["Status.LoadFailure"] = "無法載入工作區：{0}",
            ["Status.DemoWorkspaceLoaded"] = "已載入示範工作區 · 2 個專案",
            ["Status.HighRiskConfirmation"] = "等待高風險操作二次確認",
            ["Status.ConfirmationRequired"] = "請確認命令與目標位置",
            ["Status.DemoExecuting"] = "Demo 執行中…",
            ["Status.DemoCompleted"] = "示意流程已完成",
            ["Status.DemoFailed"] = "示意流程失敗",
            ["Status.Cancelled"] = "已取消",
            ["Status.FinderFailure"] = "無法顯示輸出位置：{0}",
            ["Stage.Idle"] = "閒置",
            ["Stage.AwaitingConfirmation"] = "等待確認",
            ["Stage.Executing"] = "執行中",
            ["Stage.Succeeded"] = "成功",
            ["Stage.Failed"] = "失敗",
            ["Stage.Cancelled"] = "已取消",
            ["Result.CancelledTitle"] = "已取消示意執行",
            ["Result.CancelledSummary"] = "正式版本會在取消後重新掃描檔案差異，不會假設工作區未被修改。",
            ["Result.CancelledDifference"] = "取消後已重新掃描差異；Demo 未修改工作區。",
            ["Result.DemoTitle"] = "示意產生成功",
            ["Result.DemoSummary"] = "這是流程 Demo；沒有執行外部 CLI，也沒有修改工作區。正式版本將在此顯示真實結束碼、耗時與差異。",
            ["Result.DemoGitStatus"] = "工作樹包含 1 個執行前既有變更；Demo 不會修改工作區。",
            ["Feature.Select"] = "選擇一項功能",
            ["Feature.DescriptionPlaceholder"] = "從左側選擇功能群組，再挑選要執行的項目。",
            ["Feature.CommandPreviewPlaceholder"] = "選擇功能後將顯示命令預覽",
            ["Feature.TargetProjectRequired"] = "此功能需要先選擇現有的目標專案。",
            ["Feature.AvailableOnPlatform"] = "此功能可在目前平台使用。",
            ["Feature.Count"] = "{0} 項功能",
            ["Validation.RequiredName"] = "名稱為必填欄位。",
            ["Confirmation.DbContextFallback"] = "依專案設定",
            ["Confirmation.OverwriteTitle"] = "確認覆寫既有檔案",
            ["Confirmation.ToolTitle"] = "確認安裝工作區本機工具",
            ["Confirmation.DatabaseTitle"] = "確認更新資料庫",
            ["Confirmation.GeneralTitle"] = "確認示意執行",
            ["Confirmation.OverwriteMessage"] = "此操作可能覆寫目前工作區內的檔案，請確認輸出位置與命令。",
            ["Confirmation.ToolMessage"] = "此操作只會使用目前工作區的本機工具資訊清單，不會執行全域工具安裝。",
            ["Confirmation.DatabaseMessage"] = "此操作會變更目標資料庫；請確認目標專案、DbContext 與連線資訊來源。",
            ["Confirmation.GeneralMessage"] = "請再次確認目標與命令。Demo 不會執行外部 CLI，也不會修改檔案。",
            ["Confirmation.OverwriteDetails"] = "風險：既有檔案可能被覆寫；覆寫選項必須由使用者明確啟用。",
            ["Confirmation.ToolDetails"] = "範圍：工作區 .config/dotnet-tools.json；禁止 --global。",
            ["Confirmation.DatabaseDetails"] = "目標專案：{0}\nDbContext：{1}\n連線來源：具名連線或專案設定（不顯示機密值）",
            ["Confirmation.GeneralDetails"] = "安全 Demo 模式：不會啟動外部 CLI，也不會修改檔案或資料庫。",
            ["Result.GitDifferenceFallback"] = "Git 分支：{0}；執行後新增 {1} 項差異，執行前既有 {2} 項變更。",
            ["Result.NonGitWorkspace"] = "非 Git 工作區",
            ["FileReveal.DemoReady"] = "Demo 已準備在 Finder 顯示：{0}",
            ["FileReveal.UnsupportedPlatform"] = "Finder 開啟動作只支援 macOS。",
            ["FileReveal.StartFailure"] = "無法啟動 Finder。",
            ["FileReveal.Success"] = "已要求 Finder 顯示輸出位置。",
            ["FileReveal.Failure"] = "Finder 無法顯示輸出位置。",
            ["Execution.StepValidate"] = "[Demo] 正在驗證目標專案…",
            ["Execution.StepDependencies"] = "[Demo] 正在檢查所需工具與套件…",
            ["Execution.StepPrepare"] = "[Demo] 預備執行：{0}",
            ["Execution.StepGenerate"] = "[Demo] 正在模擬產生檔案…",
            ["Execution.StepDiff"] = "[Demo] 正在比較 Git 與檔案差異…",
            ["Parameter.Name"] = "名稱",
            ["Parameter.OutputPath"] = "輸出位置",
            ["Parameter.ForceOutput"] = "覆寫既有輸出",
            ["Parameter.TemplateMode"] = "範本模式",
            ["Parameter.Model"] = "模型",
            ["Parameter.DbContext"] = "DbContext",
            ["Parameter.NamedConnection"] = "具名連線或來源",
            ["Template.Empty"] = "空白",
            ["Template.ReadWrite"] = "含讀寫動作",
            ["Template.MvcCrud"] = "MVC CRUD",
            ["Template.RestApi"] = "REST API",
            ["Defaults.ControllerName"] = "OrdersController",
            ["Defaults.OutputPath"] = "Controllers",
            ["Defaults.GeneratedOutputPath"] = "./generated",
            ["Defaults.ProjectName"] = "MyNewApp",
            ["Defaults.MigrationName"] = "AddOrderStatus",
            ["Defaults.LatestMigration"] = "Latest",
            ["Defaults.RazorComponentName"] = "OrderSummary",
            ["Defaults.PageName"] = "Orders",
            ["Defaults.WorkingDirectory"] = "/path/to/workspace"
        };

    public string Get(string key) => Texts.TryGetValue(key, out var value) ? value : key;

    public string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), arguments);
}
