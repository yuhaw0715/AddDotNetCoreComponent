## Purpose

建立版本可辨識且能反映本機實際環境的 CLI 功能目錄，讓使用者不必記憶命令即可找到官方產生器及已安裝的自訂範本。

## ADDED Requirements

### Requirement: 官方 dotnet new 範本目錄
系統 SHALL 以 .NET 10 官方範本為基準，並依本機 `dotnet new list` 的實際結果標示可用性。目錄 MUST 涵蓋下列官方短名稱與別名：

- 專案與服務：`webapiaot`、`web`、`webapi`、`mvc`、`webapp`/`razor`、`grpc`、`blazor`、`blazorwasm`、`console`、`classlib`、`worker`、`razorclasslib`。
- 測試：`mstest`、`xunit`、`nunit`、`mstest-playwright`、`nunit-playwright`。
- Windows 桌面：`winforms`、`winformslib`、`winformscontrollib`、`wpf`、`wpflib`、`wpfcustomcontrollib`、`wpfusercontrollib`。
- 單一項目：`apicontroller`、`mvccontroller`、`razorcomponent`、`view`、`page`、`viewimports`、`viewstart`、`proto`、`mstest-class`、`nunit-test`。
- 結構與設定：`sln`/`solution`、`slnf`/`solutionfilter`、`editorconfig`、`gitignore`、`gitattributes`、`globaljson`、`nugetconfig`、`tool-manifest`、`buildprops`、`buildtargets`、`packagesprops`、`webconfig`。

#### Scenario: 本機具備官方範本
- **WHEN** 官方範本出現在本機範本清單中
- **THEN** 系統在對應功能群組顯示該範本、可用語言與本機回報的版本相關選項

#### Scenario: 本機缺少官方範本
- **WHEN** 基準目錄中的官方範本未出現在本機範本清單中
- **THEN** 系統顯示該功能但標示為不可用，並指出所需 SDK、工作負載或範本套件

### Requirement: ASP.NET Core Scaffolding 目錄
系統 SHALL 呈現 `area`、`controller`、`blazor`、`blazor-identity`、`identity`、`minimalapi`、`razorpage` 與 `view` 八種官方產生器。系統 SHALL 對 `blazor` 呈現 Empty、Create、Edit、Delete、Details、List、CRUD 範本，並對 `razorpage` 呈現 Empty、Create、Edit、Delete、Details、List 及完整 CRUD 流程。

#### Scenario: 瀏覽 Controller 功能
- **WHEN** 使用者開啟 Controller 產生器
- **THEN** 系統提供空白、讀寫動作、MVC 搭配檢視及 REST API 等適用模式，並依模式顯示必要參數

#### Scenario: 瀏覽 Identity 功能
- **WHEN** 使用者開啟 Identity 或 Blazor Identity 產生器
- **THEN** 系統顯示可產生檔案清單及資料庫提供者等官方支援選項

### Requirement: EF Core 功能目錄
系統 SHALL 提供 `dbcontext scaffold`、`dbcontext optimize`、`dbcontext script`、`migrations add`、`migrations remove`、`migrations bundle`、`migrations list`、`migrations has-pending-model-changes`、`migrations script`、`dbcontext info`、`dbcontext list` 及 `database update`。第一版 MUST NOT 提供 `database drop`。

#### Scenario: 顯示 EF Core 分群
- **WHEN** 使用者進入 EF Core 功能群組
- **THEN** 系統分開顯示程式碼產生、Migration、查詢/檢查與資料庫變更功能，並清楚標示其風險層級

#### Scenario: 尋找 database drop
- **WHEN** 使用者搜尋或瀏覽 EF Core 功能
- **THEN** 系統不提供可執行的 `database drop` 動作，並可在說明中標示其不屬於第一版範圍

### Requirement: 自訂範本探索
系統 SHALL 動態探索使用者已安裝且不屬於官方基準目錄的 `dotnet new` 範本，並將其放在獨立的「自訂範本」群組。

#### Scenario: 發現第三方範本
- **WHEN** 本機範本清單包含未知短名稱
- **THEN** 系統在「自訂範本」群組顯示名稱、短名稱、語言與標籤，且不將其誤標示為官方範本

#### Scenario: 無法解析自訂選項
- **WHEN** 系統無法可靠解析自訂範本的說明輸出
- **THEN** 系統顯示解析限制並提供受限制且經安全處理的額外參數輸入，不得直接交由 shell 解譯

### Requirement: 平台相容性標示
系統 SHALL 依目前作業系統顯示功能相容性；在 macOS 上，WPF 與 Windows Forms 功能 MUST 可見但不可執行，並顯示「不支援 macOS」及原因。

#### Scenario: 在 macOS 瀏覽 Windows 桌面範本
- **WHEN** 使用者在 macOS 開啟 Windows 桌面功能群組
- **THEN** 系統顯示全部 WPF/Windows Forms 官方範本，但停用建立按鈕並呈現平台限制
