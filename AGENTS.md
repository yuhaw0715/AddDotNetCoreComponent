# AGENTS.md

## 專案目標

本儲存庫用於開發 **DotNet Scaffold Studio**：以 .NET 10、Avalonia UI 與 MVVM 建立的跨平台桌面應用程式。第一版以 macOS 為主要平台，將 `.NET CLI`、ASP.NET Core Scaffolding 與 EF Core 的產生/管理功能轉換為安全、可預覽的繁體中文圖形介面。

## 規格優先工作方式

- 開始實作前，先閱讀 `openspec/changes/build-dotnet-scaffold-studio/` 下的 `proposal.md`、`design.md`、`specs/*/spec.md` 與 `tasks.md`。
- OpenSpec 規格是可觀察行為的依據；`design.md` 是技術方向，`tasks.md` 是實作與驗證清單。
- 未經使用者明確要求，不得跳過 OpenSpec 直接擴張產品範圍。
- 實作時一次完成一個可驗證的小項目；只有驗證成功後才勾選對應 task。
- 若實作發現規格衝突或需要改變外部行為，先更新/確認 OpenSpec，而不是在程式碼中默默決定。

## 預期目錄與相依方向

```text
src/
  DotNetScaffoldStudio.App/
  DotNetScaffoldStudio.Application/
  DotNetScaffoldStudio.Domain/
  DotNetScaffoldStudio.Infrastructure/
tests/
  DotNetScaffoldStudio.UnitTests/
  DotNetScaffoldStudio.IntegrationTests/
  DotNetScaffoldStudio.UiTests/
```

- `Domain` 不得參考 Avalonia、Infrastructure 或作業系統 API。
- `Application` 定義使用案例與 ports；不得直接建立 `Process`、讀寫真實檔案或呼叫 Avalonia 視窗。
- `Infrastructure` 實作 CLI、Git、檔案系統、本機設定與平台服務。
- `App` 包含 Views、繁體中文資源、組合根與平台啟動程式。
- 相依方向維持 App → Application → Domain；Infrastructure 由 App 組合根注入 Application ports。

## 程式碼規範

- 目標 Framework 為 `net10.0`；啟用 nullable reference types 與 implicit usings。
- 類型、成員與檔名使用英文；文件、OpenSpec、使用者可見文字與必要註解使用繁體中文。
- View 採 XAML，狀態與命令放在 ViewModel；不得在 code-behind 實作商業邏輯。
- 優先使用不可變 record/value object 表達功能、參數、命令、風險與結果。
- 非同步 I/O 與程序工作必須接受並傳遞 `CancellationToken`；不得使用 `.Result` 或 `.Wait()` 阻塞 UI thread。
- 所有使用者可見固定文字必須放在資源檔，不得散落硬編碼。
- 不新增不必要的套件；新增套件時要說明用途、鎖定相容版本並補測試。

## CLI 與安全硬性規則

- 外部程序只能以 executable 加 `ProcessStartInfo.ArgumentList` 的結構化引數執行。
- 禁止將使用者輸入串成 shell 命令；禁止透過 `sh`、`bash`、`zsh -c` 或等價方式執行。
- 命令預覽只用於顯示，不得重新解析成實際執行內容。
- 每個命令必須有固定且已驗證的 working directory、風險層級及機密遮蔽資訊。
- 覆寫檔案、移除 Migration、`database update`、工具/套件安裝等操作必須走規格定義的確認流程。
- 第一版不得提供 `database drop`。
- 不得自動執行 Git commit、stash、checkout、reset，也不得在 CLI 失敗或取消後自動刪除/回復檔案。
- 不得保存或輸出連線字串、Token、密碼等機密；優先使用具名連線字串。
- 不得收集遙測；只有使用者明確觸發需要網路的操作才能連線。

## 測試與隔離

- 每個行為變更至少要有相應單元測試；CLI adapter 需有隔離整合測試；關鍵使用流程需有 UI 測試。
- 整合測試只能操作專用暫存目錄，並使用隔離的 `DOTNET_CLI_HOME` 與 NuGet 快取。
- 測試不得安裝全域 dotnet 工具、修改使用者真實專案、讀寫正式資料庫或依賴既有個人設定。
- 測試 CLI 時涵蓋空白、Unicode、引號與 shell 特殊字元，證明不存在命令注入。
- 測試機密遮蔽時要掃描 stdout、stderr、預覽、例外、歷程與序列化設定。

## 常用驗證命令

專案建立後，變更至少執行適用的命令：

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
openspec validate build-dotnet-scaffold-studio --strict
```

發布相關變更另執行：

```bash
dotnet publish src/DotNetScaffoldStudio.App -c Release -r osx-arm64 --self-contained true
dotnet publish src/DotNetScaffoldStudio.App -c Release -r osx-x64 --self-contained true
```

若實際 Solution 或專案路徑與上述預期不同，先更新本文件與 OpenSpec 設計，再調整命令。

## 完成交付前檢查

- 建置與相關測試全部通過，且沒有忽略新警告。
- 新增行為符合對應 spec 的所有 Scenario。
- CLI 命令未經 shell、機密已遮蔽、路徑仍在授權工作區內。
- macOS 上 WPF/WinForms 顯示但不可執行。
- 繁體中文資源、鍵盤操作及可存取狀態已驗證。
- 沒有夾帶簽章、公證、DMG、Homebrew Cask、遙測或其他第一版範圍外功能。
