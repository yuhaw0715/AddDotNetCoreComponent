# AGENTS.md

## 專案目標

本儲存庫用於開發 **DotNet Scaffold Studio**：以 .NET 10、Avalonia UI 與 MVVM 建立的跨平台桌面應用程式。第一版以 macOS 為主要平台，將 `.NET CLI`、ASP.NET Core Scaffolding 與 EF Core 的產生/管理功能轉換為安全、可預覽的繁體中文圖形介面。

## 目前實作基準

- Solution 已建立為 `DotNetScaffoldStudio.slnx`，使用 `net10.0`、Avalonia 12.1.1、CommunityToolkit.Mvvm 8.4.2 與集中套件版本管理。
- OpenSpec change 為 `build-dotnet-scaffold-studio`；目前進度以 `openspec/changes/build-dotnet-scaffold-studio/tasks.md` 為準，不可只依賴本文件或 `HANDOFF.md` 的數字。
- 已完成工程基礎、官方功能目錄、安全命令核心，以及工作區/Git/檔案差異基礎；最新交接狀態與下一步見 `HANDOFF.md`。
- OpenSpec 3.4 已完成：正式命令工作流程會在執行前後擷取 Git/檔案狀態；取消時嘗試正常終止程序，必要時終止程序樹，並在取消後重新掃描以回報部分輸出差異。
- OpenSpec 5.1 已完成：`IDotNetEnvironmentDiscovery` 透過 `CommandRequest` 與 `ProcessCommandRunner` 探索 .NET SDK、Runtime、工作負載及工作區本機工具；這是唯讀服務，不安裝全域工具。
- OpenSpec 5.2 已完成：`IDotNetTemplateDiscovery` 使用固定 `DOTNET_CLI_UI_LANGUAGE=en` 探索並解析 `dotnet new list`，以多版本、不同欄寬 fixture 驗證官方與自訂範本辨識。
- OpenSpec 5.3 已完成：`ICustomTemplateHelpDiscovery` 以結構化 `dotnet new <template> --help` 解析自訂範本選項；無法可靠解析時使用 `SafeFallback`，並以受限制的結構化額外引數保留安全性。
- OpenSpec 5.4 已完成：`IDependencyDiscovery` 重用唯讀環境探索結果，偵測本機 `dotnet-ef`、`dotnet-aspnet-codegenerator` 與目標 `.csproj`／中央套件版本，區分缺少、偵測失敗與主要版本不相容。
- OpenSpec 5.5 已完成：`DependencyPlanWorkflow` 以一次性計畫確認 token 執行結構化相依性步驟，逐步重驗證並保留部分成功、失敗與 Git／檔案差異。
- OpenSpec 5.6 已完成：`LocalToolInstallationService` 先建立工作區 `.config/dotnet-tools.json`，再以 `--local` 安裝或更新工具；隔離整合測試驗證 executable、檔案差異、重驗證與無 `--global` 命令。
- OpenSpec 5.7 已完成：`NuGetPackageInstallationService` 僅針對選定 `.csproj` 建立 `dotnet add package --no-restore` 與 `dotnet restore` 兩階段計畫，逐步重驗證並在 restore 失敗時保留專案檔差異與錯誤階段。下一個待辦是 5.8，仍以 `tasks.md` 和最新 `openspec instructions apply` 結果為準。
- OpenSpec 5.8 已完成：`DependencyGuidanceService` 對缺少 SDK 提供官方說明、對缺少工作負載要求獨立確認，`WorkloadInstallationWorkflow` 在確認前不呼叫 adapter；fake adapter 單元測試驗證未確認時不啟動任何網路或修改操作。下一個待辦是 6.1，仍以 `tasks.md` 和最新 `openspec instructions apply` 結果為準。
- OpenSpec 6.1 已完成：`ParameterFormState` 依 schema 建立布林、列舉、路徑、清單、一般文字與機密欄位的 typed value，常用/進階切換只改變可見性且保留所有值。下一個待辦是 6.2，仍以 `tasks.md` 和最新 `openspec instructions apply` 結果為準。
- OpenSpec 6.2 已完成：`ParameterValidator` 依功能 schema 驗證必填、名稱、工作區路徑、條件式必填與互斥欄位，回傳帶欄位識別字的繁中錯誤且不輸出機密值。
- OpenSpec 6.3 已完成：`DotNetNewCommandFactory` 依官方 46 個範本 schema 建立結構化 `dotnet new` 命令，固定使用 canonical short name、工作區工作目錄與相對輸出目標，並在建立前執行參數驗證。下一個待辦是 6.4，仍以 `tasks.md` 和最新 `openspec instructions apply` 結果為準。
- OpenSpec 6.4 已完成：`AspNetScaffoldingCommandFactory` 支援八種 generator，將模式、`-p` 目標專案、名稱、模型、DbContext、資料庫提供者、Identity 檔案與輸出位置建立為結構化引數，並在建立前驗證工作區邊界。下一個待辦是 6.5，仍以 `tasks.md` 和最新 `openspec instructions apply` 結果為準。
- OpenSpec 6.5 已完成：`EfCoreCommandFactory` 支援目錄內 12 個 EF Core 命令，將位置引數、專案、DbContext、連線與輸出轉為結構化引數；`database update` 產生不含原始連線值的二次確認資料，並明確拒絕 `database drop`。下一個待辦是 6.6，仍以 `tasks.md` 和最新 `openspec instructions apply` 結果為準。
- `src/DotNetScaffoldStudio.Application/DemoCatalog.cs` 是 UI 示意資料；完整的 .NET 10、Scaffolding 與 EF Core 基準位於 `OfficialFeatureCatalog.cs`。兩者尚未整合，不得把 Demo 子集合誤認為正式目錄。
- 目前 Avalonia UI 的執行按鈕只呼叫 `DemoExecutionService`。它不會啟動外部 CLI、修改工作區或連線資料庫；接上正式執行流程前必須保留清楚的 Demo 標示。
- `artifacts/` 已由 Git 忽略；本機 `.app` 是可重建成品，不是原始碼交付的一部分。

## 規格優先工作方式

- 開始實作前，先閱讀 `openspec/changes/build-dotnet-scaffold-studio/` 下的 `proposal.md`、`design.md`、`specs/*/spec.md` 與 `tasks.md`。
- 接續既有工作時也要閱讀 `HANDOFF.md`，再以 `openspec instructions apply --change build-dotnet-scaffold-studio --json` 確認最新進度。
- OpenSpec 規格是可觀察行為的依據；`design.md` 是技術方向，`tasks.md` 是實作與驗證清單。
- 未經使用者明確要求，不得跳過 OpenSpec 直接擴張產品範圍。
- 實作時一次完成一個可驗證的小項目；只有驗證成功後才勾選對應 task。
- 若實作發現規格衝突或需要改變外部行為，先更新/確認 OpenSpec，而不是在程式碼中默默決定。

## 實際目錄與相依方向

```text
src/
  DotNetScaffoldStudio.App/
  DotNetScaffoldStudio.Application/
  DotNetScaffoldStudio.Domain/
  DotNetScaffoldStudio.Infrastructure/
tests/
  DotNetScaffoldStudio.CommandProbe/
  DotNetScaffoldStudio.UnitTests/
  DotNetScaffoldStudio.IntegrationTests/
  DotNetScaffoldStudio.UiTests/
packaging/
  macos/Info.plist
```

- `Domain` 不得參考 Avalonia、Infrastructure 或作業系統 API。
- `Application` 定義使用案例與 ports；不得直接建立 `Process`、讀寫真實檔案或呼叫 Avalonia 視窗。
- `Infrastructure` 實作 CLI、Git、檔案系統、本機設定與平台服務。
- `App` 包含 Views、繁體中文資源、組合根與平台啟動程式。
- `CommandProbe` 只供整合測試驗證大量 stdout/stderr、結束碼、機密遮蔽與取消；不得成為正式應用程式相依。
- 相依方向維持 App → Application → Domain；Infrastructure 由 App 組合根注入 Application ports。

## 程式碼規範

- 目標 Framework 為 `net10.0`；啟用 nullable reference types 與 implicit usings。
- 類型、成員與檔名使用英文；文件、OpenSpec、使用者可見文字與必要註解使用繁體中文。
- View 採 XAML，狀態與命令放在 ViewModel；不得在 code-behind 實作商業邏輯。
- 優先使用不可變 record/value object 表達功能、參數、命令、風險與結果。
- 非同步 I/O 與程序工作必須接受並傳遞 `CancellationToken`；不得使用 `.Result` 或 `.Wait()` 阻塞 UI thread。
- 所有使用者可見固定文字必須放在資源檔，不得散落硬編碼。
- 現有 Demo XAML 尚有未資源化文字，屬 OpenSpec 7.7 的待辦；新增文字不得擴大這項技術債。
- 不新增不必要的套件；新增套件時要說明用途、鎖定相容版本並補測試。

## CLI 與安全硬性規則

- 外部程序只能以 executable 加 `ProcessStartInfo.ArgumentList` 的結構化引數執行。
- 正式命令使用 `CommandRequest`；實際程序入口集中在 `ProcessCommandRunner`。不得新增第二套直接啟動 `Process` 的捷徑。
- 禁止將使用者輸入串成 shell 命令；禁止透過 `sh`、`bash`、`zsh -c` 或等價方式執行。
- 命令預覽只用於顯示，不得重新解析成實際執行內容。
- 每個命令必須有固定且已驗證的 working directory、風險層級及機密遮蔽資訊。
- 覆寫檔案、移除 Migration、`database update`、工具/套件安裝等操作必須走規格定義的確認流程。
- 第一版不得提供 `database drop`。
- 不得自動執行 Git commit、stash、checkout、reset，也不得在 CLI 失敗或取消後自動刪除/回復檔案。
- 不得保存或輸出連線字串、Token、密碼等機密；優先使用具名連線字串。
- 不得收集遙測；只有使用者明確觸發需要網路的操作才能連線。
- 所有 Avalonia restore/build/test/publish 命令都要設定 `AVALONIA_TELEMETRY_OPTOUT=1`。

## 測試與隔離

- 每個行為變更至少要有相應單元測試；CLI adapter 需有隔離整合測試；關鍵使用流程需有 UI 測試。
- 整合測試只能操作專用暫存目錄；凡會呼叫 dotnet 工具或套件還原的測試，必須使用隔離的 `DOTNET_CLI_HOME` 與 NuGet 快取。
- 測試不得安裝全域 dotnet 工具、修改使用者真實專案、讀寫正式資料庫或依賴既有個人設定。
- 測試 CLI 時涵蓋空白、Unicode、引號與 shell 特殊字元，證明不存在命令注入。
- 測試機密遮蔽時要掃描 stdout、stderr、預覽、例外、歷程與序列化設定。

## 常用驗證命令

變更至少執行適用的命令：

```bash
AVALONIA_TELEMETRY_OPTOUT=1 dotnet restore DotNetScaffoldStudio.slnx --disable-build-servers --property:UseSharedCompilation=false
AVALONIA_TELEMETRY_OPTOUT=1 dotnet build DotNetScaffoldStudio.slnx --no-restore --disable-build-servers --property:UseSharedCompilation=false --maxcpucount:1
AVALONIA_TELEMETRY_OPTOUT=1 dotnet test DotNetScaffoldStudio.slnx --no-build --disable-build-servers --property:UseSharedCompilation=false --maxcpucount:1
AVALONIA_TELEMETRY_OPTOUT=1 dotnet format DotNetScaffoldStudio.slnx --no-restore --verify-no-changes
openspec validate build-dotnet-scaffold-studio --strict
git diff --check
```

在受限執行環境中，`dotnet test` 可能因測試執行器需要本機 loopback socket 而要求額外權限；`dotnet restore` 只有在確實需要下載或更新資產時才可要求網路權限。

發布相關變更另執行：

```bash
AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish src/DotNetScaffoldStudio.App/DotNetScaffoldStudio.App.csproj -c Release -r osx-arm64 --self-contained true
AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish src/DotNetScaffoldStudio.App/DotNetScaffoldStudio.App.csproj -c Release -r osx-x64 --self-contained true
```

若要重建目前的 framework-dependent 操作示意版，可先 publish 到 `artifacts/publish/osx-arm64`，再依 `packaging/macos/Info.plist` 組成 `artifacts/DotNet Scaffold Studio.app`。這不代表 OpenSpec 9.4 的 self-contained 雙架構發布已完成。

## Git 與交付規則

- 不得自行 commit 或 push；每次都必須取得使用者對該次操作的明確允許。
- 使用者允許 commit 時，commit 訊息使用中文；push 前確認 remote、分支及欲推送的 commit。
- 不得把 `artifacts/`、`bin/`、`obj/`、測試結果或本機設定加入版本控制。
- 工作樹若已有與目前任務無關的變更，必須保留並避免覆蓋。

## 完成交付前檢查

- 建置與相關測試全部通過，且沒有忽略新警告。
- 新增行為符合對應 spec 的所有 Scenario。
- CLI 命令未經 shell、機密已遮蔽、路徑仍在授權工作區內。
- macOS 上 WPF/WinForms 顯示但不可執行。
- 繁體中文資源、鍵盤操作及可存取狀態已驗證。
- 沒有夾帶簽章、公證、DMG、Homebrew Cask、遙測或其他第一版範圍外功能。
- `tasks.md` 只勾選已有對應實作且驗證成功的項目；局部完成要記在 `HANDOFF.md`，不得提前勾選。
