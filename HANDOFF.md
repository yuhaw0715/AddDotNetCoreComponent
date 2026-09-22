# DotNet Scaffold Studio 交接文件

## 交接快照

- 日期：2026-09-22（Asia/Taipei）
- 儲存庫：`https://github.com/yuhaw0715/AddDotNetCoreComponent.git`
- 分支：`main`
- 目前已推送基準：`main` 與 `origin/main` 應於本次 7.7 task commit 同步；實際 commit 以 `git log -1` 驗證。
- OpenSpec change：`build-dotnet-scaffold-studio`
- OpenSpec 進度：44/55；最終狀態一律以 `tasks.md` 與 `openspec instructions apply` 的輸出為準。
- 本次 7.7 已完成驗證，commit/push 依使用者要求在本次交付中執行；後續從 8.1 開始，每個 task 仍須獨立完成驗證後再交付。

## 產品與目前可操作成果

DotNet Scaffold Studio 是以 .NET 10、Avalonia UI 與 MVVM 建立的 macOS 優先桌面應用程式，用圖形介面包裝 `dotnet new`、ASP.NET Core Scaffolding 與 EF Core CLI。

目前已有可操作的安全 Demo：可選擇真實工作區、掃描 `.csproj`、切換功能群組、輸入 Controller 名稱、即時查看命令預覽、確認執行、取消以及查看模擬檔案差異。執行階段仍固定使用 `DemoExecutionService`，因此不會執行真實 CLI 或修改使用者專案。

本機 Demo 成品若尚未被清除，位於：

```text
artifacts/DotNet Scaffold Studio.app
```

`artifacts/` 已被忽略，不會出現在 Git 儲存庫。

## 已完成範圍

OpenSpec 已勾選以下區段：

- 1.1–1.5：Solution、四層 src 專案、三個測試專案、套件、格式與架構測試。
- 2.1–2.5：不可變領域模型、46 個 canonical `dotnet new` 功能（含別名共 49 個 CLI 名稱）、8 種 Scaffolding、12 種 EF Core 功能，以及本機能力合併。
- 3.1–3.4：結構化 `CommandRequest`、非 shell 程序執行器、修改命令與唯讀命令併發協調、取消後 Git/檔案重掃與結果整合。
- 3.5–3.6：機密遮蔽、集中風險政策與一次性確認 token。
- 4.1–4.6：工作區/Solution 正規化、專案掃描、執行前目標驗證、Git status、檔案快照與前後差異彙整。
- 5.1：以結構化 `CommandRequest` 偵測 `dotnet --info`、SDK、Runtime、工作負載和工作區本機工具；含無 SDK、僅 Runtime、多 SDK 與命令失敗 fixture。
- 5.2：以固定英文 CLI UI 語言探索並解析 `dotnet new list`，支援多版本/欄寬 fixture 與官方/自訂範本辨識。
- 5.3：以結構化 `dotnet new <template> --help` 探索自訂範本選項，支援完整、部分解析與安全降級模式；額外參數維持為結構化引數，不提供 shell 入口。
- 5.4：以唯讀 `IDependencyDiscovery` 偵測本機 `dotnet-ef`、`dotnet-aspnet-codegenerator` 及目標 `.csproj`／`Directory.Packages.props` 的必要 NuGet 套件，區分可用、缺少、偵測失敗與主要版本不相容。
- 5.5：以 `DependencyPlan` 與一次性確認 token 建立計畫—確認—執行—重驗證流程；每個步驟完成後重驗證，並保留拒絕、部分成功、重驗證失敗與 Git／檔案差異。
- 5.6：以 `LocalToolInstallationService` 建立 `.config/dotnet-tools.json`，再以 `dotnet tool install/update --local` 安裝工作區本機工具；隔離整合測試驗證工具資訊清單、可執行 fixture、重驗證、環境變數與檔案差異。
- 5.7：以 `NuGetPackageInstallationService` 只對選定 `.csproj` 建立 `dotnet add package --no-restore` 與 `dotnet restore` 兩階段計畫；整合測試驗證套件版本、其他專案不受影響、restore 失敗階段與不自動回復的檔案差異。
- 5.8：以 `DependencyGuidanceService` 區分缺少 SDK 的官方說明與缺少工作負載的獨立確認；`WorkloadInstallationWorkflow` 使用一次性確認 token，未確認不呼叫 `IWorkloadInstallationAdapter`，fake adapter 測試驗證零網路/修改操作。
- 6.1：以 `ParameterFormState` 和 typed `ParameterValue` 依 schema 保存布林、列舉、路徑、清單、一般文字與機密值；進階欄位切換只改變 visibility，不丟失已輸入值。
- 6.2：以 `ParameterConstraint` 與 `ParameterValidator` 實作必填、名稱格式、工作區路徑、條件式必填與互斥驗證；Controller、Razor Page、Blazor、EF Core 代表測試驗證欄位級繁中錯誤與機密不外洩。
- 6.3：以 `DotNetNewCommandFactory` 依 46 個官方範本 schema 建立結構化 `dotnet new` 命令；參數化單元測試驗證 canonical short name、工作目錄、目標輸出，以及無效名稱與越界路徑拒絕。
- 6.4：以 `AspNetScaffoldingCommandFactory` 支援八種 generator；各模式 snapshot 驗證 `-p`、模型、DbContext、資料庫提供者、Identity 檔案、模式與輸出引數，並驗證名稱/路徑錯誤不會建立命令。
- 6.5：以 `EfCoreCommandFactory` 支援 12 個官方 EF Core 命令；以 `DatabaseUpdateConfirmationData` 搭配遮蔽預覽與 `CommandRiskPolicy` 驗證二次確認，並以測試確保 `database drop` 無法建立。
- 6.6：以 `ExpectedOutputConflictChecker` 檢查命令預期輸出與既有相對路徑；同名 Controller、既有 `dotnet new` 輸出目錄會阻擋一般執行，`force` 只有在檔案覆寫風險確認後才可繼續。
- 6.7：以 `GenerationWorkflow` 串接目標專案重驗證、相依性探索、參數/輸出檢查、確認 token、取消與既有差異摘要；隔離整合測試以 fixture runner 完成 Controller、Razor Page、EF Migration 三條流程。
- 7.1：Avalonia 主視窗加入可收合左側導覽，提供八組主要功能群組；功能群組顯示功能清單與參數內容，首頁/工作區、執行紀錄及設定有獨立內容區，導航測試驗證切換不會重設工作區與目標專案。
- 7.2：首頁接上工作區掃描、空/單一/多專案狀態與目標專案 ComboBox；單一專案自動選取，多專案要求明確選取，空工作區停用 Scaffolding/EF Core 等既有專案功能但保留專案範本流程，掃描失敗保留原狀態。
- 7.3：功能清單加入搜尋過濾與結果數量，卡片呈現命令相依性、可用性與風險；新增 Windows Forms 示意功能，和 WPF 一樣在 macOS 保持可見但停用執行。
- 7.4：新增 `ParameterEditorViewModel`，依 schema 建立 typed 參數欄位，支援常用/進階切換、欄位級繁中驗證、即時命令預覽更新及機密欄位遮罩。
- 7.5：確認對話框依一般、檔案覆寫、本機工具安裝與 `database update` 顯示不同標題、風險與範圍；本機工具明確限於 `.config/dotnet-tools.json` 且不使用 `--global`，取消不會呼叫執行服務。
- 7.6：執行面板顯示階段、即時 Demo 輸出、成功/失敗/取消結果、執行前既有與執行後檔案差異及 Git 摘要；`FinderRevealService` 只以 `open` 加 `ArgumentList` 提供 macOS 顯示輸出位置。
- 7.7：`Strings.zh-TW.axaml` 集中 Avalonia 固定文字，`IUiTextProvider` 提供 ViewModel、Demo 執行與 Finder adapter 的可替換文字；主要控制項補上 `AutomationProperties.Name`、TabIndex 與文字化狀態提示，資源掃描及鍵盤／非顏色 UI 測試通過。

重要實作位置：

- 完整官方功能目錄：`src/DotNetScaffoldStudio.Application/OfficialFeatureCatalog.cs`
- Demo 精簡目錄：`src/DotNetScaffoldStudio.Application/DemoCatalog.cs`
- 安全命令模型：`src/DotNetScaffoldStudio.Domain/CommandModels.cs`
- 預覽、遮蔽與確認：`src/DotNetScaffoldStudio.Application/CommandSafety.cs`
- 非 shell 程序執行：`src/DotNetScaffoldStudio.Infrastructure/ProcessCommandRunner.cs`
- 命令協調：`src/DotNetScaffoldStudio.Application/CommandCoordinator.cs`
- 正式命令執行與取消後差異彙整：`src/DotNetScaffoldStudio.Application/CommandExecutionWorkflow.cs`
- .NET SDK/Runtime/工作負載/本機工具探索：`src/DotNetScaffoldStudio.Infrastructure/DotNetEnvironmentDiscoveryService.cs`
- 環境探索領域模型：`src/DotNetScaffoldStudio.Domain/EnvironmentModels.cs`
- 相依性能力探索：`src/DotNetScaffoldStudio.Infrastructure/DependencyDiscoveryService.cs`、`src/DotNetScaffoldStudio.Domain/DependencyModels.cs`
- 相依性計畫流程：`src/DotNetScaffoldStudio.Application/DependencyPlanWorkflow.cs`
- 本機工具安裝計畫：`src/DotNetScaffoldStudio.Infrastructure/LocalToolInstallationService.cs`；隔離 fixture 與測試：`tests/DotNetScaffoldStudio.CommandProbe/Program.cs`、`tests/DotNetScaffoldStudio.IntegrationTests/LocalToolInstallationServiceTests.cs`
- NuGet 套件安裝計畫：`src/DotNetScaffoldStudio.Infrastructure/NuGetPackageInstallationService.cs`；階段 fixture 與測試：`tests/DotNetScaffoldStudio.IntegrationTests/NuGetPackageInstallationServiceTests.cs`
- SDK/工作負載說明與確認：`src/DotNetScaffoldStudio.Application/DependencyGuidanceService.cs`、`src/DotNetScaffoldStudio.Domain/DependencyGuidanceModels.cs`；fake adapter 測試：`tests/DotNetScaffoldStudio.UnitTests/DependencyGuidanceTests.cs`
- Schema 表單狀態：`src/DotNetScaffoldStudio.Application/ParameterFormState.cs`、`src/DotNetScaffoldStudio.Domain/ParameterFormModels.cs`；單元測試：`tests/DotNetScaffoldStudio.UnitTests/ParameterFormStateTests.cs`
- Schema 驗證：`src/DotNetScaffoldStudio.Application/ParameterValidator.cs`、`src/DotNetScaffoldStudio.Domain/ParameterValidationModels.cs`；代表案例測試：`tests/DotNetScaffoldStudio.UnitTests/ParameterValidatorTests.cs`
- 工作區與邊界：`WorkspaceService.cs`、`WorkspacePathResolver.cs`
- Git 與檔案差異：`GitStatusService.cs`、`FileSnapshotService.cs`、`ExecutionDifferenceAggregator.cs`
- Avalonia Demo：`src/DotNetScaffoldStudio.App/MainWindow.axaml` 與 `MainWindowViewModel.cs`
- Schema 參數編輯器：`src/DotNetScaffoldStudio.Application/ParameterEditorViewModels.cs`
- Finder 輸出位置 adapter：`src/DotNetScaffoldStudio.Infrastructure/FinderRevealService.cs`
- UI 資源與文字 provider：`src/DotNetScaffoldStudio.App/Resources/Strings.zh-TW.axaml`、`src/DotNetScaffoldStudio.Application/UiTextProvider.cs`、`src/DotNetScaffoldStudio.App/AvaloniaUiTextProvider.cs`
- 導覽與桌面內容測試：`tests/DotNetScaffoldStudio.UiTests/DemoFlowTests.cs`

## 已知限制與不可誤判事項

1. `ProcessCommandRunner` 已完成且有整合測試，但尚未接到 Avalonia UI；UI 只執行模擬服務。
2. `OfficialFeatureCatalog` 尚未取代 `DemoCatalog`，所以畫面只顯示少量代表功能。
3. OpenSpec 3.4 已完成：正式工作流程會在修改命令前後擷取 Git/檔案狀態；取消後以不受取消 token 影響的掃描回報部分輸出。CommandProbe 整合測試驗證正常終止嘗試、必要時終止程序樹與差異摘要。
4. OpenSpec 5.1–5.8 已完成：環境、範本與相依性探索服務是唯讀流程，計畫流程以結構化命令、一次性確認與重驗證執行；5.6/5.7 的本機工具與 NuGet 服務、5.8 的 guidance service 已註冊至 App 組合根，但 Avalonia UI 尚未呼叫正式相依性流程。
5. OpenSpec 6.1–6.7 已完成 schema 表單狀態、欄位驗證、三類命令工廠、輸出衝突檢查與可取消產生流程；7.1–7.6 已建立桌面導覽、工作區狀態、功能搜尋、參數編輯、確認與結果差異骨架，但正式產生流程尚未接入 Avalonia UI。
6. UI 是操作流程 Demo；正式產生流程尚未接入 Avalonia UI。7.7 已完成資源化、焦點順序與非顏色狀態提示；現有 UI 測試主要驗證 ViewModel 與 XAML 靜態標記，不是完整 headless Avalonia smoke suite。
7. OpenSpec 8.1–9.7 尚未完成，包含本機設定、歷程、隱私稽核、coverage、完整 UI smoke、self-contained 發布與最終驗收。
8. 目前 `.app` 是 Debug、framework-dependent、osx-arm64 示意成品；不是 9.4 要求的 arm64/x64 self-contained 發布成果。
9. 不得提供 `database drop`，也不得加入任意 shell/終端機入口。

## 下一步建議順序

接續 OpenSpec 8.1：

1. 實作具 schema version、atomic replace 與損壞復原的本機設定儲存。
2. 完成 8.1 的適用測試與 strict validation 後，再處理 8.2。
3. 每完成一項即獨立 commit/push、更新 `tasks.md`，不要一次提前勾選整個區段。

## 最近一次完整驗證（2026-09-22）

OpenSpec 7.7 完成後的驗證結果：

- Build：0 警告、0 錯誤。
- Test：121 項通過（Integration 37、UI 18、Unit 66）；另有 6.7 workflow preflight、確認/取消與三條暫存專案端到端案例，以及 7.1 導航/收合/狀態保留、7.2 空/單一/多專案與掃描失敗、7.3 搜尋/平台限制、7.4 schema/驗證/預覽/遮蔽、7.5 分類確認/取消、7.6 成功/失敗/取消/差異/Finder、7.7 資源／焦點／非顏色狀態案例通過。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

使用下列命令重新驗證；Avalonia 遙測 opt-out 不可省略：

```bash
AVALONIA_TELEMETRY_OPTOUT=1 dotnet restore DotNetScaffoldStudio.slnx --disable-build-servers --property:UseSharedCompilation=false
AVALONIA_TELEMETRY_OPTOUT=1 dotnet build DotNetScaffoldStudio.slnx --no-restore --disable-build-servers --property:UseSharedCompilation=false --maxcpucount:1 --verbosity minimal
AVALONIA_TELEMETRY_OPTOUT=1 dotnet test DotNetScaffoldStudio.slnx --no-build --disable-build-servers --property:UseSharedCompilation=false --maxcpucount:1 --verbosity minimal
AVALONIA_TELEMETRY_OPTOUT=1 dotnet format DotNetScaffoldStudio.slnx --no-restore --verify-no-changes --verbosity minimal
openspec validate build-dotnet-scaffold-studio --strict
git diff --check
```

受限環境中，測試執行器可能需要允許本機 loopback socket。整合測試不得碰觸使用者真實專案、全域工具或正式資料庫。

## Demo 重建與操作

framework-dependent osx-arm64 Demo 可用下列方式重建：

```bash
AVALONIA_TELEMETRY_OPTOUT=1 dotnet restore src/DotNetScaffoldStudio.App/DotNetScaffoldStudio.App.csproj -r osx-arm64
AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish src/DotNetScaffoldStudio.App/DotNetScaffoldStudio.App.csproj -c Debug -r osx-arm64 --self-contained false --no-restore --output artifacts/publish/osx-arm64
mkdir -p "artifacts/DotNet Scaffold Studio.app/Contents/MacOS"
cp -R artifacts/publish/osx-arm64/. "artifacts/DotNet Scaffold Studio.app/Contents/MacOS/"
cp packaging/macos/Info.plist "artifacts/DotNet Scaffold Studio.app/Contents/Info.plist"
chmod +x "artifacts/DotNet Scaffold Studio.app/Contents/MacOS/DotNetScaffoldStudio.App"
open "artifacts/DotNet Scaffold Studio.app"
```

操作驗證路徑：按「示範資料」→「ASP.NET Scaffolding」→修改 Controller 名稱→檢查命令預覽→「執行示意」→確認。結果檔名必須反映輸入值，例如 `A  Controllers/InvoicesController.cs`。

## Git 與安全提醒

- Commit 與 push 每次都必須先取得使用者明確允許；commit 訊息使用中文。
- 不得自動 commit、stash、checkout、reset 或回復使用者檔案。
- 不得用 shell wrapper 執行使用者參數；所有參數保持 `ArgumentList` 結構。
- 工具或套件安裝只能在使用者確認後執行，優先使用專案本機 tool manifest，不得全域安裝。
- 機密值不得出現在預覽、stdout/stderr、錯誤、歷程或設定序列化內容。

## 可直接使用的交接 Prompt

```text
請接手 /Users/yuhao/Projects/AddDotNetCoreComponent 的 DotNet Scaffold Studio 開發。

先完整閱讀根目錄 AGENTS.md、HANDOFF.md，以及 openspec/changes/build-dotnet-scaffold-studio/ 下的 proposal.md、design.md、所有 specs 與 tasks.md。接著執行：

openspec instructions apply --change build-dotnet-scaffold-studio --json

以 CLI 輸出確認最新進度，不要只相信 HANDOFF.md 內的進度數字。

目前 `main` 與 `origin/main` 應已同步於最新 task commit；開始前仍須檢查 `git log -1` 與 `git status`，並保留任何既有變更。

已有可操作的 Avalonia 安全 Demo、完整官方功能目錄、安全 `CommandRequest`/`ProcessCommandRunner`、取消與程序樹終止流程、取消後 Git/檔案重新掃描、唯讀的 .NET 與相依性能力探索服務，以及相依性計畫—確認—執行—重驗證流程。請注意 UI 目前仍使用 `DemoCatalog` 與 `DemoExecutionService`，不會執行真實 CLI；不要把示意流程誤當成正式功能。

下一個實作目標是 OpenSpec 8.1：實作具 schema version、atomic replace 與損壞復原的本機設定儲存。完成後只在對應驗證全部通過時勾選 8.1，不要提前開始 8.2。

所有 Avalonia 相關命令設定 `AVALONIA_TELEMETRY_OPTOUT=1`。外部程序只能使用 executable 加 `ProcessStartInfo.ArgumentList`，禁止 shell wrapper；不得提供 `database drop`、不得安裝全域工具、不得碰觸真實專案或資料庫。

完成變更後執行適用的 build、unit/integration/UI tests、`dotnet format`、OpenSpec strict validation 與 `git diff --check`。請用繁體中文回報。未取得我對該次操作的明確允許前，不得 commit 或 push。
```
