# DotNet Scaffold Studio 交接文件

## 交接快照

- 日期：2026-09-28（Asia/Taipei）
- 儲存庫：`https://github.com/yuhaw0715/AddDotNetCoreComponent.git`
- 分支：`main`
- 目前已推送基準：`main` 與 `origin/main` 應於最近完成的 task commit 同步；實際 commit 以 `git log -1` 驗證。
- OpenSpec change：`build-dotnet-scaffold-studio`
OpenSpec 進度：55/55；最終狀態一律以 `tasks.md` 與 `openspec instructions apply` 的輸出為準。
本次 9.7 已完成完整驗證；此 change 的所有 task 均已完成，可進入封存流程。

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
- 8.1：新增具 schema version 的 `LocalSettingsDocument` 與 `ILocalSettingsStore`；`LocalSettingsStore` 使用 `System.Text.Json`、同目錄暫存檔 flush 後 atomic replace，讀取無效 JSON 或不支援 schema 時隔離原檔並回傳安全預設值；中斷寫入、有效 JSON、無效 JSON 與不支援版本均以隔離暫存目錄測試驗證。
- 8.2：設定文件保存最近工作區與各工作區最後目標專案、視窗尺寸/位置/最大化狀態及導覽選擇/收合偏好；App 啟動時驗證並恢復最近有效工作區，失效路徑會清除，關閉時保存目前狀態；隔離重啟測試驗證有效資料恢復與失效項目移除。
- 8.3：新增記憶體內 `SessionExecutionHistory` 與不可變 `ExecutionHistoryEntry`，正式命令 workflow 與 Demo 都保存時間、功能、遮蔽命令、工作目錄、結束碼、取消狀態、摘要及遮蔽輸出；歷程頁顯示本次工作階段資料，序列化測試掃描連線字串、Token 與密碼均不會出現。
- 8.4：稽核確認產品沒有遙測 SDK、`HttpClient`、`WebClient` 或 socket 背景連線；新增 `INetworkAccessAdapter`，相依性計畫只有在有效確認後才授權需網路步驟，本機步驟不觸發 adapter，並以 fake/實際 adapter 及 production source audit 測試驗證離線邊界。
- 9.1：補齊 Domain/Application 規則的有效與失敗單元案例，涵蓋功能目錄、參數驗證、三類命令工廠、風險/機密遮蔽與產生狀態機；加入 `coverlet.collector 6.0.4`，隔離 Cobertura 報告確認總 line 59.33%、branch 48.61%，核心規則類別均有覆蓋。
- 9.2：新增 `CliTestIsolation` 共用整合測試 harness，將本機工具與 NuGet CLI 的 `DOTNET_CLI_HOME`、`NUGET_PACKAGES`、HTTP cache 指向專用暫存目錄，並以使用者 profile 前後快照確認未修改全域工具、NuGet 設定或真實工作區；LocalTool service 支援測試環境注入但仍固定禁止 `--global`。
- 9.3：新增 `MainWindowSmokeTests`，以不需 windowing backend 的 headless-compatible UI 測試驗證 App/MainWindow XAML、工作區與導覽、參數錯誤、確認/執行結果，以及無效 JSON 設定隔離後的預設值啟動；UI 測試專案直接參考 App、Infrastructure 與 Avalonia。
- 9.4：新增 `osx-arm64`/`osx-x64` PublishProfile 與共用 MSBuild bundle target，固定 Release self-contained、保留完整 Runtime 檔案，並產生帶 `Contents/MacOS`、`Info.plist` 與 RID 名稱的 `.app` 成品。
- 9.5：啟動時以唯讀 `IDotNetEnvironmentDiscovery` 探索 SDK；缺少或偵測失敗時主視窗仍可啟動，顯示繁中功能受限與 .NET 10 SDK 安裝指引，Demo 瀏覽不被阻擋。App/ViewModel/XAML smoke test 與 arm64/x64 隔離 HOME/PATH 實際啟動驗收均通過；x64 於本機透過 Rosetta 2 執行。
- 9.6：發布 bundle 產生 `DotNetScaffoldStudio.Release.json`，標示版本 `0.1.0-demo`、RID、開發發行、未 Developer ID 簽章、未公證，並以成品檢查確認沒有 DMG/pkg 或 Homebrew Cask；apphost 的 ad hoc 簽章明確標示為非 Developer ID。
- 9.7：完成完整 build、Integration/UI/Unit 測試、format、OpenSpec strict、diff check 與隱私/機密來源掃描；確認無直接網路/遙測 API、明文 credential pattern、shell wrapper 入口或超出第一版範圍的 DMG/pkg/Cask 成品，change 可封存。
- 9.7 後啟動修正：新增 `MacOsDisplayLinkReadiness`，在 Avalonia 初始化前以 CoreVideo 檢查 `CVDisplayLink`；遇到 macOS `RenderTimer -6661` 時等待可用螢幕，不再讓 App 直接崩潰，並以重試與取消測試驗證。

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
- UI smoke suite：`tests/DotNetScaffoldStudio.UiTests/MainWindowSmokeTests.cs`、`tests/DotNetScaffoldStudio.UiTests/DotNetScaffoldStudio.UiTests.csproj`
- self-contained 發布：`src/DotNetScaffoldStudio.App/Properties/PublishProfiles/osx-arm64.pubxml`、`src/DotNetScaffoldStudio.App/Properties/PublishProfiles/osx-x64.pubxml`、`packaging/macos/SelfContainedMacOsApp.targets`
- 發布成品 metadata 測試：`tests/DotNetScaffoldStudio.UnitTests/MacOsReleaseMetadataTests.cs`
- macOS display link 啟動防護：`src/DotNetScaffoldStudio.App/MacOsDisplayLinkReadiness.cs`、`tests/DotNetScaffoldStudio.UiTests/MacOsDisplayLinkReadinessTests.cs`
- 啟動環境受限狀態：`src/DotNetScaffoldStudio.Application/MainWindowViewModel.cs`、`src/DotNetScaffoldStudio.App/App.axaml.cs`、`src/DotNetScaffoldStudio.App/MainWindow.axaml`、`src/DotNetScaffoldStudio.App/Resources/Strings.zh-TW.axaml`
- 本機設定儲存：`src/DotNetScaffoldStudio.Domain/LocalSettingsModels.cs`、`src/DotNetScaffoldStudio.Application/LocalSettingsServices.cs`、`src/DotNetScaffoldStudio.Infrastructure/LocalSettingsStore.cs`；隔離測試：`tests/DotNetScaffoldStudio.IntegrationTests/LocalSettingsStoreTests.cs`
- 設定恢復與視窗狀態：`src/DotNetScaffoldStudio.Application/MainWindowViewModel.cs`、`src/DotNetScaffoldStudio.App/App.axaml.cs`、`src/DotNetScaffoldStudio.App/MainWindow.axaml.cs`；重啟測試：`tests/DotNetScaffoldStudio.IntegrationTests/LocalSettingsRestoreTests.cs`
- 工作階段執行歷程：`src/DotNetScaffoldStudio.Domain/ExecutionHistoryModels.cs`、`src/DotNetScaffoldStudio.Application/ExecutionHistory.cs`、`src/DotNetScaffoldStudio.Application/CommandExecutionWorkflow.cs`；序列化遮蔽測試：`tests/DotNetScaffoldStudio.UnitTests/ExecutionHistoryTests.cs`
- 網路授權與隱私稽核：`src/DotNetScaffoldStudio.Application/NetworkAccess.cs`、`src/DotNetScaffoldStudio.Infrastructure/UserInitiatedNetworkAccessAdapter.cs`、`src/DotNetScaffoldStudio.Application/DependencyPlanWorkflow.cs`；測試：`tests/DotNetScaffoldStudio.UnitTests/NetworkPrivacyTests.cs`、`tests/DotNetScaffoldStudio.IntegrationTests/UserInitiatedNetworkAccessAdapterTests.cs`
- 領域/Application coverage 測試：`tests/DotNetScaffoldStudio.UnitTests/ApplicationRulesCoverageTests.cs`、`tests/DotNetScaffoldStudio.UnitTests/GenerationWorkflowTests.cs`；coverage collector：`Directory.Packages.props`、`tests/DotNetScaffoldStudio.UnitTests/DotNetScaffoldStudio.UnitTests.csproj`
- CLI 隔離 harness：`tests/DotNetScaffoldStudio.IntegrationTests/CliTestIsolation.cs`、`tests/DotNetScaffoldStudio.IntegrationTests/LocalToolInstallationServiceTests.cs`、`tests/DotNetScaffoldStudio.IntegrationTests/NuGetPackageInstallationServiceTests.cs`；環境注入：`src/DotNetScaffoldStudio.Infrastructure/LocalToolInstallationService.cs`

## 已知限制與不可誤判事項

1. `ProcessCommandRunner` 已完成且有整合測試，但尚未接到 Avalonia UI；UI 只執行模擬服務。
2. `OfficialFeatureCatalog` 尚未取代 `DemoCatalog`，所以畫面只顯示少量代表功能。
3. OpenSpec 3.4 已完成：正式工作流程會在修改命令前後擷取 Git/檔案狀態；取消後以不受取消 token 影響的掃描回報部分輸出。CommandProbe 整合測試驗證正常終止嘗試、必要時終止程序樹與差異摘要。
4. OpenSpec 5.1–5.8 已完成：環境、範本與相依性探索服務是唯讀流程，計畫流程以結構化命令、一次性確認與重驗證執行；5.6/5.7 的本機工具與 NuGet 服務、5.8 的 guidance service 已註冊至 App 組合根，但 Avalonia UI 尚未呼叫正式相依性流程。
5. OpenSpec 6.1–6.7 已完成 schema 表單狀態、欄位驗證、三類命令工廠、輸出衝突檢查與可取消產生流程；7.1–7.6 已建立桌面導覽、工作區狀態、功能搜尋、參數編輯、確認與結果差異骨架，但正式產生流程尚未接入 Avalonia UI。
6. UI 是操作流程 Demo；正式產生流程尚未接入 Avalonia UI。7.7 已完成資源化、焦點順序與非顏色狀態提示；9.3 smoke suite 可在不需 windowing backend 的 headless-compatible 測試環境執行，但不等同於實體桌面互動驗收。
7. OpenSpec 9.1–9.7 已完成；此 change 已通過最終驗收，可另行執行封存流程。
8. 既有未含 RID 的 `.app` 仍是 Debug、framework-dependent 示意成品；9.4 的 Release self-contained 成品位於 `artifacts/DotNet Scaffold Studio-osx-arm64.app` 與 `artifacts/DotNet Scaffold Studio-osx-x64.app`，且 `artifacts/` 不納入版本控制。
9. 不得提供 `database drop`，也不得加入任意 shell/終端機入口。
10. 若 macOS 沒有可用的 active display，啟動防護會等待螢幕恢復後再建立 Avalonia 視窗；這是避免 `CVDisplayLink` 原生錯誤的預期行為。

## 下一步建議順序

OpenSpec `build-dotnet-scaffold-studio` 的 55/55 tasks 已完成，change 可封存。若要進行正式收尾，請另行執行封存流程；本次不自動封存。

## 前次完整驗證（2026-09-22）

OpenSpec 7.7 完成後的驗證結果：

- Build：0 警告、0 錯誤。
- Test：121 項通過（Integration 37、UI 18、Unit 66）；另有 6.7 workflow preflight、確認/取消與三條暫存專案端到端案例，以及 7.1 導航/收合/狀態保留、7.2 空/單一/多專案與掃描失敗、7.3 搜尋/平台限制、7.4 schema/驗證/預覽/遮蔽、7.5 分類確認/取消、7.6 成功/失敗/取消/差異/Finder、7.7 資源／焦點／非顏色狀態案例通過。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 8.1）

- Build：0 警告、0 錯誤。
- Test：125 項通過（Integration 41、UI 18、Unit 66）；新增本機設定有效 JSON、atomic replace 暫存檔清理、中斷寫入保留舊設定、無效 JSON 隔離復原及不支援 schema 復原案例通過。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 8.2）

- Build：0 警告、0 錯誤。
- Test：127 項通過（Integration 43、UI 18、Unit 66）；新增設定重啟恢復工作區/目標專案/視窗/導覽偏好，以及失效最近工作區清除案例通過。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 8.3）

- Build：0 警告、0 錯誤。
- Test：129 項通過（Integration 43、UI 18、Unit 68）；新增正式 workflow/Demo 歷程資料模型與序列化機密掃描測試通過。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 8.4）

- Build：0 警告、0 錯誤。
- Test：133 項通過（Integration 44、UI 18、Unit 71）；新增網路授權邊界、離線步驟不觸發 adapter、實際 adapter 拒絕背景操作與 production source privacy audit 案例通過。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 9.1）

- Build：0 警告、0 錯誤。
- Test：139 項通過（Integration 44、UI 18、Unit 77）。
- Coverage：UnitTests Cobertura，line 59.33%、branch 48.61%；核心類別 ParameterValidator 98.21%、EfCoreCommandFactory 93.84%、AspNetScaffoldingCommandFactory 92.56%、CommandRiskPolicy/SensitiveDataRedactor 100%、GenerationWorkflow 100%。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 9.2）

- Build：0 警告、0 錯誤。
- Test：139 項通過（Integration 44、UI 18、Unit 77）；LocalTool/NuGet CLI 隔離案例 3/3 通過，使用者 profile 前後快照一致。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 9.3）

- Build：0 警告、0 錯誤。
- Test：144 項通過（Integration 44、UI 23、Unit 77）；新增 UI smoke suite 5/5 通過。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 9.4）

- arm64 publish：成功；bundle executable 為 Mach-O arm64，具備 `Info.plist`、`libcoreclr.dylib`、`libhostfxr.dylib` 與 runtimeconfig。
- x64 publish：成功；bundle executable 為 Mach-O x86_64，具備 `Info.plist`、`libcoreclr.dylib`、`libhostfxr.dylib` 與 runtimeconfig。
- Build：0 警告、0 錯誤。
- Test：144 項通過（Integration 44、UI 23、Unit 77）；首次完整測試的既有取消案例時序超時，單獨重跑與第二次完整測試均通過。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 9.5）

- 啟動驗收：arm64 與 x64 bundle 均在隔離 `HOME`、`DOTNET_CLI_HOME` 及 `PATH=/usr/bin:/bin` 下保持執行超過 3 秒；x64 透過 Rosetta 2，未使用系統 .NET CLI，均以 Ctrl-C 正常結束驗收程序。
- 受限狀態 smoke：UI 6/6 通過，缺少 SDK 時顯示功能受限與安裝指引且 Demo 仍可用。
- Build：0 警告、0 錯誤。
- Test：145 項通過（Integration 44、UI 24、Unit 77）。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 9.6）

- arm64/x64 self-contained publish：成功；metadata JSON 分別標示 `0.1.0-demo`、`osx-arm64`/`osx-x64`、`signed=false`、`developerIdSigned=false`、`notarized=false` 與 `development`。
- 成品簽章檢查：兩個 apphost 均為正確架構的 Mach-O，僅有 ad hoc 簽章，沒有 Team Identifier 或 Developer ID；這不代表正式簽章或公證。
- 發布範圍檢查：`artifacts/` 與 `packaging/` 沒有 DMG/pkg/Homebrew Cask，版本庫也沒有追蹤這些成品。
- Build：0 警告、0 錯誤。
- Test：148 項通過（Integration 44、UI 24、Unit 80）。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 最近一次完整驗證（2026-09-28；OpenSpec 9.7）

- Build：0 警告、0 錯誤。
- Test：148 項通過（Integration 44、UI 24、Unit 80）。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。
- 隱私/機密掃描：production source 無直接 HTTP、socket 或遙測 API；未發現常見明文 credential pattern；外部程序未使用 shell wrapper；未產生 DMG/pkg/Homebrew Cask。

## 9.7 後啟動修正驗證（2026-09-28）

- Build：0 警告、0 錯誤。
- Test：150 項通過（Integration 44、UI 26、Unit 80）；新增 display link 重試、成功釋放與取消等待測試。
- arm64/x64 self-contained publish：均成功；arm64 bundle executable 在目前自動化環境無可用 display link 時保持等待，不再拋出 `RenderTimer -6661` 後退出。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。
- `git diff --check`：通過。

## 9.8 主視窗啟動修正（2026-09-28）

- `App.OnFrameworkInitializationCompleted` 先指定 `MainWindow` 給桌面生命週期，再以背景非同步流程載入本機設定、探索 .NET 環境與還原最近工作區，避免 Dock 已啟動但視窗被 I/O 或 CLI 探索阻塞。
- 背景初始化失敗時保留已顯示的主視窗，並寫入診斷記錄；關閉流程仍使用目前設定工作階段保存設定。
- Build：0 警告、0 錯誤。
- Test：150 項通過（Integration 44、UI 26、Unit 80）。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。

## 9.9 繁中資源查找修正（2026-09-28）

- `AvaloniaUiTextProvider` 改以遞迴查找 Avalonia 合併資源字典，修正 `Status.*`、`Feature.*`、`Parameter.*`、`Result.*` 與 `Template.*` 鍵直接顯示在 UI 的問題。
- 資料夾選擇對話框標題也改用同一套資源提供器，避免固定文字繞過資源層。
- 新增 UI 測試驗證合併繁中資源、格式化功能數量與對話框標題。
- Build：0 警告、0 錯誤。
- Test：151 項通過（Integration 44、UI 27、Unit 80）。
- `dotnet format --verify-no-changes`：通過。
- `openspec validate build-dotnet-scaffold-studio --strict`：通過。

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

OpenSpec 9.7 已完成；所有 task 均已驗證並標記完成，change 可封存。所有 Avalonia 相關命令仍須設定 `AVALONIA_TELEMETRY_OPTOUT=1`。

所有 Avalonia 相關命令設定 `AVALONIA_TELEMETRY_OPTOUT=1`。外部程序只能使用 executable 加 `ProcessStartInfo.ArgumentList`，禁止 shell wrapper；不得提供 `database drop`、不得安裝全域工具、不得碰觸真實專案或資料庫。

完成變更後執行適用的 build、unit/integration/UI tests、`dotnet format`、OpenSpec strict validation 與 `git diff --check`。請用繁體中文回報。未取得我對該次操作的明確允許前，不得 commit 或 push。
```
