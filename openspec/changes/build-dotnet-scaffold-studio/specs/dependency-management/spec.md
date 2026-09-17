## Purpose

在不意外污染全域環境的前提下，偵測每項功能所需的 SDK、工作負載、工具及套件，並以明確同意流程補齊可安全安裝的相依性。

## ADDED Requirements

### Requirement: 環境健全性檢查
系統 SHALL 偵測 `dotnet` 可執行檔、SDK/Runtime 版本、工作負載、本機工具資訊清單、`dotnet-ef`、`dotnet-aspnet-codegenerator` 及目標專案必要套件，並區分可用、缺少、版本不相容與偵測失敗。

#### Scenario: 電腦未安裝 .NET SDK
- **WHEN** 系統找不到可執行的 `dotnet` 或只找到 Runtime
- **THEN** 系統停用 CLI 功能、說明需要 .NET SDK，並提供官方安裝文件入口而不自行下載執行檔

#### Scenario: 工具版本與專案不相容
- **WHEN** 已安裝工具的主要版本不符合目標專案套件版本
- **THEN** 系統標示版本衝突並提出相容版本，不得直接執行可能不相容的產生器

### Requirement: 同意後安裝
系統 MUST 在安裝任何工具、工作負載或 NuGet 套件前列出將執行的命令、預期修改位置與網路需求，並取得使用者明確確認。

#### Scenario: 安裝本機 dotnet 工具
- **WHEN** 使用者確認安裝缺少的 `dotnet-ef` 或 `dotnet-aspnet-codegenerator`
- **THEN** 系統優先使用工作區的工具資訊清單安裝為本機工具，並在成功後重新檢查能力

#### Scenario: 使用者拒絕安裝
- **WHEN** 使用者取消相依性安裝
- **THEN** 系統不修改工作區或全域環境，並保持相依功能停用

### Requirement: 本機工具資訊清單
系統 SHALL 在工作區沒有工具資訊清單時說明將新增 `.config/dotnet-tools.json`，並將建立資訊清單與安裝工具視為可在同一確認視窗檢閱的兩個明確步驟。

#### Scenario: 首次加入本機工具
- **WHEN** 工作區沒有工具資訊清單且使用者同意本機安裝
- **THEN** 系統先建立資訊清單，再安裝指定工具，並在結果摘要中列出所有新增或修改的檔案

### Requirement: NuGet 套件變更
系統 SHALL 只對使用者目前選定的 `.csproj` 提議必要套件，顯示套件識別字與版本，並在執行 `dotnet add package` 前取得確認。

#### Scenario: Scaffolding 缺少 Design 套件
- **WHEN** 目標專案缺少產生器要求的設計階段套件
- **THEN** 系統列出套件、版本與目標專案，經確認安裝並還原成功後才啟用產生操作

### Requirement: 安裝失敗可診斷
系統 SHALL 將安裝、還原及重新偵測視為可分辨的階段，任一階段失敗時 MUST 顯示失敗階段與已發生的檔案變更。

#### Scenario: NuGet 還原失敗
- **WHEN** 套件已寫入專案檔但還原失敗
- **THEN** 系統不得宣稱安裝成功或自動回復專案檔，並顯示錯誤輸出及實際 Git/檔案差異
