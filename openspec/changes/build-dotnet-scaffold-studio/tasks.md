## 1. 建立 Solution 與工程基礎

- [x] 1.1 建立 .NET 10 Solution，以及 App、Application、Domain、Infrastructure 四個 `src` 專案，並以 `dotnet build` 驗證全部專案可建置。
- [x] 1.2 建立 UnitTests、IntegrationTests、UiTests 三個 `tests` 專案及正確專案參考，並以 `dotnet test` 驗證空白測試基線通過。
- [x] 1.3 加入 Avalonia、CommunityToolkit.Mvvm、Microsoft.Extensions.DependencyInjection 與必要的測試套件，並以 restore/build 驗證套件版本與 .NET 10 相容。
- [x] 1.4 設定 nullable、implicit usings、分析器、警告政策與統一格式規則，並以建置無新增警告及格式檢查通過驗證。
- [x] 1.5 建立 App 組合根與單向專案相依，並以架構測試驗證 Domain/Application 不參考 Avalonia、Infrastructure 或作業系統實作。

## 2. 建立核心模型與官方功能目錄

- [x] 2.1 建立功能定義、參數 schema、平台相容性、相依條件、風險層級及命令結果等不可變領域模型，並以單元測試驗證有效與無效組合。
- [x] 2.2 建立 .NET 10 官方 `dotnet new` 基準資料，涵蓋規格列出的 46 個短名稱/別名，並以資料完整性測試驗證無遺漏、重複或未知群組。
- [x] 2.3 建立八種 ASP.NET Core Scaffolding 產生器及 Blazor/Razor 子範本的功能定義，並以 snapshot 測試驗證命令、必要參數與群組。
- [x] 2.4 建立規格內 EF Core 功能定義並排除 `database drop`，以單元測試驗證所有允許/禁止功能及其風險等級。
- [x] 2.5 實作官方基準與本機能力合併規則，並以 fixture 測試驗證可用、缺少、版本不相容及未知範本狀態。

## 3. 實作安全 CLI 基礎設施

- [x] 3.1 實作結構化 `CommandRequest` 建構與預覽格式化，並以包含空白、引號、Unicode 與 shell 特殊字元的測試驗證執行引數不經字串重解析。
- [x] 3.2 以 `ProcessStartInfo.ArgumentList` 實作非 shell 程序執行器，並以整合測試驗證 stdout/stderr 同步串流、結束碼與大量輸出不死鎖。
- [x] 3.3 實作單一修改命令協調器與唯讀偵測併發限制，並以併發測試驗證第二個修改命令會被拒絕且狀態可恢復。
- [x] 3.4 實作取消、正常終止及必要時終止程序樹的流程，並以長時間測試程序驗證取消結果與殘留檔案重新掃描。
- [x] 3.5 實作機密引數標記及預覽、歷程、日誌與錯誤遮蔽，並以含連線字串/Token 的測試驗證任何輸出皆不含原值。
- [x] 3.6 實作集中式風險政策與一次性確認 token，並以單元測試驗證參數變更會使舊確認失效，覆寫、移除 Migration、資料庫更新及安裝均需確認。

## 4. 工作區、專案與差異偵測

- [x] 4.1 實作資料夾、`.sln`、`.slnx` 選擇結果解析與路徑正規化，並以越界、symlink、遺失與無權限案例測試工作區邊界。
- [x] 4.2 實作 `.csproj` 遞迴探索並排除 `.git`、`bin`、`obj` 等目錄，以多專案 fixture 驗證名稱、相對路徑、SDK 類型及 TFM 顯示資料。
- [x] 4.3 實作目標專案選擇及執行前重新驗證，並以專案在載入後被移動/刪除的測試驗證命令不會啟動。
- [x] 4.4 實作 Git 分支與 porcelain 狀態 adapter，並以乾淨、dirty、未追蹤及非 Git 工作區 fixture 驗證結果。
- [x] 4.5 實作排除大型輸出目錄的檔案 metadata snapshot 與前後差異，並以新增、修改、刪除及取消後部分輸出測試驗證摘要。
- [x] 4.6 實作結果彙整，區分執行前既有 Git 變更與執行後新增差異，並以整合測試驗證不會執行 commit、stash、reset 或自動回復。

## 5. 執行階段探索與相依性管理

- [x] 5.1 實作 `dotnet --info`、SDK/Runtime、工作負載與工具清單偵測，並以缺少 SDK、只有 Runtime、多 SDK 及命令失敗 fixture 驗證狀態。
- [x] 5.2 實作固定 CLI UI 語言的 `dotnet new list` 探索與解析，並以多版本/不同欄寬輸出 fixture 驗證官方與自訂範本辨識。
- [x] 5.3 實作自訂範本 `--help` 解析及安全降級模式，並以可解析、部分解析與無法解析 fixture 驗證不會產生 shell 命令入口。
- [ ] 5.4 實作 `dotnet-ef`、`dotnet-aspnet-codegenerator` 與目標專案必要 NuGet 套件的版本/能力偵測，並以相容與主要版本衝突案例測試。
- [ ] 5.5 建立 `DependencyPlan` 及計畫—確認—執行—重驗證流程，並以拒絕確認、部分成功與重驗證失敗案例測試狀態與檔案差異。
- [ ] 5.6 實作建立工具資訊清單及專案本機工具安裝，不使用全域安裝，並在隔離整合測試中驗證 `.config/dotnet-tools.json` 與工具可執行。
- [ ] 5.7 實作只針對已選 `.csproj` 的 NuGet 套件安裝/restore，並以暫存專案驗證套件版本、錯誤階段與失敗時不自動回復。
- [ ] 5.8 實作缺少 SDK/工作負載的說明與獨立確認流程，並以假的安裝 adapter 驗證未確認時不啟動任何網路或修改操作。

## 6. 參數、命令與產生流程

- [ ] 6.1 實作 schema 驅動表單狀態，支援布林、列舉、路徑、清單、一般文字及機密型別，並以單元測試驗證常用/進階切換不遺失值。
- [ ] 6.2 實作必填、名稱、路徑、條件式、相依及互斥驗證，並以 Controller、Razor Page、Blazor、EF Core 代表案例驗證欄位級中文錯誤。
- [ ] 6.3 實作 `dotnet new` 專案、項目與設定檔命令工廠，並以 46 個官方範本的參數化測試驗證短名稱、工作目錄及目標輸出。
- [ ] 6.4 實作八種 ASP.NET Core Scaffolding 命令工廠，並以各模式 snapshot 測試驗證 `-p`/目標專案、模型、DbContext、資料庫提供者與輸出參數。
- [ ] 6.5 實作規格內 EF Core 命令工廠及 `database update` 二次確認資料，並以單元測試驗證 `database drop` 無法被建立。
- [ ] 6.6 實作預期輸出與衝突檢查，並以同名 Controller、既有輸出目錄及 force 參數測試驗證覆寫預設為關閉。
- [ ] 6.7 整合相依性檢查、參數驗證、確認、執行與結果摘要為可取消工作流程，並以暫存 ASP.NET Core 專案完成至少一個 Controller、Razor/Blazor 及 EF Migration 端到端案例。

## 7. Avalonia 桌面體驗

- [ ] 7.1 建立 Avalonia 主視窗、可折疊左側導覽及內容區，涵蓋首頁/工作區、專案範本、元件與檔案、Scaffolding、EF Core、自訂範本、歷程與設定，並以 UI 測試驗證導航。
- [ ] 7.2 建立工作區選擇、掃描結果及目標專案選擇畫面，並以空工作區、單一與多專案 UI 測試驗證狀態。
- [ ] 7.3 建立功能卡片/清單及搜尋篩選，呈現可用性、相依性、風險與平台限制，並以 macOS UI 測試驗證 WPF/WinForms 可見但停用。
- [ ] 7.4 建立 schema 驅動參數編輯器、進階選項與唯讀命令預覽，並以 UI 測試驗證即時更新、驗證訊息與機密遮蔽。
- [ ] 7.5 建立一般執行、覆寫、工具安裝及 `database update` 對應確認對話框，並以 UI 測試驗證取消不會呼叫執行服務。
- [ ] 7.6 建立即時輸出、階段狀態、取消動作、結果摘要、檔案/Git 差異及 Finder 開啟動作，並以成功、失敗、取消 fixture 驗證畫面。
- [ ] 7.7 將所有使用者可見固定文字移至繁體中文資源並加上可存取名稱、焦點順序與非顏色狀態提示，以資源掃描及鍵盤 UI 測試驗證。

## 8. 本機設定、歷程與隱私

- [ ] 8.1 實作具 schema version、atomic replace 與損壞復原的本機設定儲存，並以中斷寫入與無效 JSON 測試驗證應用程式仍能啟動。
- [ ] 8.2 保存最近工作區、最後專案、視窗狀態與非敏感偏好，並以重啟測試驗證有效路徑恢復及失效路徑清除。
- [ ] 8.3 實作只含遮蔽資料的工作階段執行歷程，並以序列化內容掃描驗證不包含連線字串、Token 或密碼。
- [ ] 8.4 稽核並移除任何應用程式遙測與未經使用者動作的網路呼叫，並以可替換網路 adapter 測試驗證離線瀏覽與本機產生流程不連線。

## 9. 測試、發布與驗收

- [ ] 9.1 為領域/Application 規則補齊單元測試，並以 coverage 報告確認功能目錄、驗證、命令、風險、遮蔽與狀態機均有成功及失敗案例。
- [ ] 9.2 建立隔離 CLI 測試 harness，設定專用暫存工作區、`DOTNET_CLI_HOME` 與 NuGet 快取，並以測試前後檢查驗證未修改真實工作區或全域工具。
- [ ] 9.3 建立主要 UI smoke suite，涵蓋啟動、工作區、導航、參數錯誤、確認、執行結果與設定損壞復原，並以 headless/支援的 macOS 測試命令驗證通過。
- [ ] 9.4 建立 `osx-arm64` 與 `osx-x64` self-contained `.app` 發布設定，並以 `dotnet publish` 及 bundle 結構檢查驗證兩個成品不依賴另裝 Runtime。
- [ ] 9.5 在相容的 macOS 執行 arm64/x64 對應啟動驗收，驗證缺少 .NET SDK 時應用程式仍可啟動並顯示受限狀態。
- [ ] 9.6 建立發布成品資訊，清楚標示版本、RID、未簽章及未公證，並以成品檢查驗證未誤產生 DMG、簽章或 Homebrew Cask。
- [ ] 9.7 執行完整 build、unit/integration/UI tests、OpenSpec strict validation 與隱私/機密掃描，只有全部通過後才將此 change 標記為可封存。
