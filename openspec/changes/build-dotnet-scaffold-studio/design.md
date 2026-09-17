## Context

目前儲存庫只有 OpenSpec 設定，尚無應用程式或既有架構。需求涵蓋檔案系統探索、多種外部 CLI、版本/平台差異、工作區寫入、可能含機密的 EF Core 參數及桌面 UI，因此需要先建立明確的邊界與安全執行模型。動機與產品範圍見 `proposal.md`，可觀察行為以七份能力規格為準。

第一版以 .NET 10 與 macOS 為主要開發/驗收環境，UI 必須可延伸至 Windows 與 Linux。應用程式本身採 self-contained 發布，但使用 CLI 功能的電腦仍須具備相容的 .NET SDK。

## Goals / Non-Goals

**Goals:**

- 將 UI、使用案例、領域模型與作業系統/CLI 整合分離，使命令規則能在無 UI、無真實 shell 的情況下測試。
- 對每一條外部命令建立結構化引數、固定工作目錄、風險分類、遮蔽策略及結果模型。
- 同時維護「.NET 10 官方基準目錄」與「本機實際能力」，避免將文件清單誤當成實際可執行能力。
- 在不自動回復使用者檔案的前提下，提供足夠的前後差異與錯誤證據。
- 將平台、檔案選擇、程序啟動、設定儲存與開啟 Finder 等行為置於可替換介面後方。

**Non-Goals:**

- 不在應用程式內重新實作 Razor、Scaffolding 或 EF Core 產生引擎。
- 不提供任意終端機或允許使用者輸入整段 shell 指令。
- 不保證第三方範本的所有自訂 UI 都可由說明文字完整推導。
- 不自動提交、stash、reset 或回復 Git/檔案變更。
- 第一版不處理資料庫刪除、簽章、公證、安裝程式、自動更新或雲端同步。

## Decisions

### 1. 採用分層 Solution 與單向相依

預計建立下列專案：

```text
src/
  DotNetScaffoldStudio.App/             Avalonia Views、資源與組合根
  DotNetScaffoldStudio.Application/     使用案例、ViewModels、驗證與介面
  DotNetScaffoldStudio.Domain/          功能、參數、風險與執行結果模型
  DotNetScaffoldStudio.Infrastructure/  CLI、檔案系統、Git、設定與平台服務
tests/
  DotNetScaffoldStudio.UnitTests/
  DotNetScaffoldStudio.IntegrationTests/
  DotNetScaffoldStudio.UiTests/
```

相依方向為 App → Application → Domain，Infrastructure 實作 Application 定義的 ports，並只在 App 組合根註冊。這使 ViewModel 不直接依賴 `Process`、檔案系統或 Avalonia 視窗物件。

替代方案是單一桌面專案；其初期檔案較少，但會使 CLI 安全策略、版本偵測與 UI 狀態耦合，降低可測試性及未來跨平台能力，因此不採用。

### 2. Avalonia UI、MVVM 與資源化繁體中文

採用 Avalonia UI 與 MVVM，命令/屬性通知可使用 CommunityToolkit.Mvvm，組合根使用 Microsoft.Extensions.DependencyInjection。所有顯示文字由繁體中文資源檔取得，畫面不得直接硬編碼使用者可見字串。導航採左側功能群組加右側內容/命令預覽，窄視窗可折疊導航。

.NET MAUI 未被選用，因其正式桌面平台不包含 Linux；內嵌 Web UI 也未被選用，因本產品不需要 Web Server 或瀏覽器執行模型。

### 3. 功能目錄採「版本化基準＋執行階段探索」

官方 .NET 10 功能以內嵌、可測試的資料檔描述：穩定識別字、顯示名稱、群組、命令種類、平台限制、必要工具、參數 schema 及風險。啟動或重新整理時，以固定為英文的 CLI UI 語言執行唯讀探索命令，將實際安裝範本/工具與基準目錄合併。

自訂 `dotnet new` 範本從本機清單建立，參數先嘗試解析 `--help`；解析器必須保留原始輸出並可回退到有限的結構化額外參數輸入。官方功能不依賴易受在地化及欄寬影響的說明解析來定義關鍵參數。

替代方案一是完全硬編碼，無法反映不同 SDK/已安裝範本；替代方案二是完全解析 CLI 輸出，會被在地化、版本與格式變動破壞。混合模型兼顧一致 UI 與真實能力。

### 4. 命令以不可變結構描述，不經 shell

所有命令都先轉換為 `CommandRequest` 類型，至少包含 executable、逐項 arguments、working directory、環境變數白名單、風險、機密引數位置、預期輸出及取消策略。Infrastructure 使用 `ProcessStartInfo.ArgumentList` 啟動程序，禁止 `UseShellExecute` 與 shell wrapper，並同時非同步讀取 stdout/stderr 以避免死鎖。

畫面上的命令字串只供預覽，不會被重新解析成執行輸入。顯示層使用平台相符的 quoting 呈現，但執行層始終使用原始引數陣列。一次只允許一個修改型命令；唯讀偵測可由協調器限量並行。

### 5. 集中式風險與確認政策

風險政策服務依「功能定義＋實際參數＋檔案衝突」決定層級，而不是由 ViewModel 各自判斷。一般新增在預覽後可執行；覆寫、移除 Migration、資料庫更新、安裝工具/套件及其他環境變更須使用對應確認模型。`database drop` 不建立功能定義，因此無法由 UI 執行。

確認內容包含目標工作區/專案、工作目錄、遮蔽後命令、預期副作用及風險原因。確認 token 只對該次不可變 request 有效；參數變更後必須重新確認。

### 6. 工作區與差異使用正規化路徑和雙重策略

所有路徑先正規化並驗證位於使用者選定工作區；輸出在工作區外的功能只有在官方命令確實需要且另經確認時才允許。專案探索排除建置輸出並保留掃描診斷。

若工作區為 Git 儲存庫且 `git` 可用，執行前後取得 porcelain 狀態與目前分支；同時建立輕量檔案 metadata snapshot，以支援非 Git 工作區並偵測未追蹤檔。系統只報告差異，不自動復原。為控制成本，快照排除 `.git`、`bin`、`obj` 及已知大型輸出。

### 7. 相依性安裝採計畫—確認—執行—重驗證

每項功能宣告 prerequisites。檢查器產生 `DependencyPlan`，列出缺少項目、版本、命令、網路與檔案副作用。使用者確認後，優先在工作區建立/使用 `.config/dotnet-tools.json` 安裝 `dotnet-ef` 與 `dotnet-aspnet-codegenerator`；NuGet 套件只加入目前目標 `.csproj`。每一步完成後重新偵測，而非假設成功。

不自動下載或執行 .NET SDK 安裝器；缺少 SDK 時只提供官方指引。工作負載安裝屬高影響操作，必須獨立確認。版本選擇以目標 Framework、專案套件主要版本及本機 SDK 的相容交集為準。

### 8. 機密與設定分離

設定以 `System.Text.Json` 寫入作業系統使用者應用資料目錄，採 atomic replace 並提供 schema version。只保存最近路徑、最後專案、視窗狀態及非敏感偏好。命令歷程保存遮蔽後資料；機密欄位只存在記憶體中的短生命週期物件。

EF Core 反向工程優先使用 `Name=ConnectionStrings:...`。若使用者直接輸入連線字串，UI 明確警告它可能短暫出現在作業系統程序資訊中，並確保預覽、日誌、錯誤包裝及歷程都使用遮蔽值。第一版不建立自有憑證庫。

### 9. 發布與測試策略

發布以 `osx-arm64` 與 `osx-x64` 分別產生 self-contained `.app`，不宣稱 universal binary。建置資訊顯示版本、RID、未簽章與未公證狀態。

單元測試使用假的 CLI/檔案系統/Git ports 驗證 catalog merge、表單規則、命令建構、風險及遮蔽。整合測試只在 `mktemp` 等隔離目錄建立測試專案，設定獨立 `DOTNET_CLI_HOME` 與 NuGet 快取，且不得安裝全域工具。UI 測試涵蓋啟動、工作區選擇、群組導覽、參數錯誤、確認及執行結果狀態。

## Risks / Trade-offs

- [CLI 輸出格式會隨 SDK、語言與終端寬度改變] → 官方功能使用版本化 schema；探索固定 CLI 語言、保存原始輸出並為解析器建立多版本 fixture。
- [第三方範本參數無共同機器可讀 schema] → 解析失敗時降級為有限引數模式，且永不交由 shell 解譯。
- [命令取消後可能留下部分檔案或子程序] → 終止程序樹、重新掃描檔案/Git 差異並明示「已取消但可能有變更」。
- [直接傳遞資料庫連線字串可能暴露於程序清單] → 優先使用具名連線字串、顯示警告、縮短生命週期並全面遮蔽記錄。
- [本機工具與 NuGet 安裝會修改儲存庫] → 安裝前顯示精確計畫並確認，完成後顯示差異；不自動回復。
- [大型 monorepo 掃描與快照成本高] → 支援取消、排除建置/版本控制目錄、限制並行及以增量/metadata 快照為主。
- [Avalonia 各平台行為不完全一致] → 平台能力置於 adapter，macOS 作為第一版驗收基準，避免在共享 ViewModel 使用平台 API。
- [未簽章 `.app` 可能受到 Gatekeeper 阻擋] → 在成品與文件清楚標示開發版限制，簽章/公證留待後續變更。

## Migration Plan

這是綠地專案，沒有既有資料或程式碼需要遷移。實作依序建立 Solution 與測試基礎、領域/應用層、Infrastructure adapters、Avalonia shell、各功能群組，最後產生發布成品。若某階段失敗，可回退該階段的程式提交；不得以應用程式的自動回復功能處理開發工作樹。

本機設定首次啟動時以目前 schema version 建立。未來 schema 更新須採向前遷移；無法遷移時備份舊檔並使用預設值，不阻止應用程式啟動。
