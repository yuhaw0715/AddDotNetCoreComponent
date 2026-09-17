## Purpose

定義第一版的品質、隱私與散布驗收底線，使應用程式能在 macOS 穩定執行，並確保測試不會污染開發者的真實專案或全域環境。

## ADDED Requirements

### Requirement: 自動化測試層級
系統 MUST 具備核心邏輯與 ViewModel 單元測試、使用隔離暫存目錄的 CLI 整合測試，以及主要導覽與關鍵流程的基本 UI 自動化測試。

#### Scenario: 執行完整測試套件
- **WHEN** 開發者在支援的建置環境執行完整測試命令
- **THEN** 單元、整合與 UI 測試均回報獨立結果，且任何失敗會使測試命令以非零狀態結束

### Requirement: 測試隔離
CLI 整合測試 MUST 建立專用暫存專案與本機工具目錄，不得修改使用者真實工作區、全域 dotnet 工具、全域 NuGet 設定或正式資料庫。

#### Scenario: CLI 整合測試結束
- **WHEN** 整合測試成功、失敗或被取消
- **THEN** 測試只留下可明確識別的診斷成品，且不對測試範圍外的檔案或工具狀態造成變更

### Requirement: macOS self-contained 應用程式
系統 SHALL 能為 `osx-arm64` 與 `osx-x64` 分別建立可直接啟動的 self-contained `.app`，執行應用程式本身不得要求使用者另行安裝 .NET Runtime。

#### Scenario: 在乾淨的相容 macOS 啟動
- **WHEN** 使用者啟動與其 CPU 架構相符的 `.app`
- **THEN** 應用程式可顯示主畫面；若系統缺少用於產生命令的 .NET SDK，則顯示功能受限與安裝指引，而不是啟動失敗

### Requirement: 本機優先與零遙測
應用程式 MUST 不收集或傳送使用統計、專案內容、命令、錯誤記錄或遙測。只有使用者明確觸發安裝工具、還原套件、查詢線上範本或開啟文件等需要網路的操作時，系統才可連線。

#### Scenario: 一般離線使用
- **WHEN** 使用者瀏覽已偵測功能、編輯參數或執行不需下載內容的本機命令
- **THEN** 應用程式不主動建立網路連線，且核心瀏覽與本機產生流程仍可運作

### Requirement: 第一版散布邊界
第一版 SHALL 產生開發用 self-contained `.app`，但 MUST NOT 將 Developer ID 簽章、公證、DMG 或 Homebrew Cask 宣告為已支援散布方式。

#### Scenario: 檢視建置成品
- **WHEN** macOS 發布流程成功完成
- **THEN** 結果清楚標示成品未簽章且未公證，並提供架構與版本資訊
