## Why

.NET 開發者在建立專案、加入 Controller、Razor/Blazor 元件、執行 Scaffolding 或管理 EF Core 時，必須記憶並手動輸入大量 CLI 命令與參數。DotNet Scaffold Studio 將這些官方功能整理為可探索、可驗證且具安全防護的 macOS 桌面介面，同時保留未來擴充至 Windows 與 Linux 的能力。

## What Changes

- 建立以 .NET 10、Avalonia UI 與 MVVM 開發的繁體中文桌面應用程式。
- 支援選擇資料夾或 Solution、自動掃描 `.csproj`，並指定命令的目標專案。
- 依功能分群呈現 .NET SDK 內建專案/項目/設定範本、ASP.NET Core Scaffolding、EF Core 與已安裝的自訂範本。
- 以表單呈現常用及進階參數，並在執行前產生完整、唯讀的命令預覽。
- 偵測 .NET SDK、工作負載、CLI 工具與 NuGet 相依套件，經使用者同意後優先以專案本機工具安裝缺少項目。
- 建立命令風險分級、覆寫與資料庫操作二次確認、Git 前後狀態比較、可取消執行及完整紀錄。
- 保存最近專案與非敏感偏好，不保存機密、不收集遙測，只有使用者觸發需要網路的操作時才連線。
- 建立單元測試、暫存專案 CLI 整合測試、主要流程 UI 測試，以及 macOS self-contained `.app` 建置流程。

## Capabilities

### New Capabilities

- `workspace-management`: 選擇、掃描、驗證及保存 Solution、資料夾與目標 `.csproj` 工作區。
- `cli-feature-catalog`: 探索並分群官方 `dotnet new` 範本、ASP.NET Core/EF Core 功能與自訂範本，並標示平台相容性。
- `parameterized-generation`: 以驗證過的表單收集各產生器參數、顯示命令預覽並建立專案或程式碼成品。
- `dependency-management`: 偵測並在取得同意後安裝所需 SDK、工作負載、本機工具與 NuGet 套件。
- `safe-command-execution`: 安全啟動及取消 CLI、風險分級、確認高風險操作、收集輸出並呈現檔案與 Git 變更。
- `desktop-experience`: 提供繁體中文、可存取、依功能分群的 Avalonia 桌面體驗，以及非敏感本機偏好保存。
- `quality-and-distribution`: 規範自動化測試、隱私限制及 macOS self-contained `.app` 建置與驗收。

### Modified Capabilities

無；目前專案尚無既有能力規格。

## Impact

- 新增 Avalonia UI、MVVM、相依性注入、設定保存及程序執行相關套件。
- 將呼叫使用者電腦上的 `dotnet`、`git`、`dotnet-ef` 與 `dotnet-aspnet-codegenerator`，但不透過 shell 字串執行命令。
- 會在使用者選定且確認的專案內產生或修改檔案；安裝本機工具與 NuGet 套件也可能修改工具資訊清單及專案檔。
- 第一版以 macOS 為主要驗收平台，程式架構與跨平台抽象需保留 Windows/Linux 擴充空間。
- 第一版不包含資料庫刪除、Apple 簽章、公證、DMG、Homebrew Cask、英文介面及應用程式遙測。
