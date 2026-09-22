using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public static class DemoCatalog
{
    public static IReadOnlyList<NavigationItem> Navigation { get; } =
    [
        new("home", "工作區", "選擇 Solution 與目標專案"),
        new("project", "專案範本", "建立 Web、服務與測試專案"),
        new("component", "元件與檔案", "Controller、Razor 與設定檔"),
        new("scaffolding", "ASP.NET Scaffolding", "CRUD、Identity 與 Minimal API"),
        new("efcore", "Entity Framework Core", "Migration、DbContext 與資料庫"),
        new("custom", "自訂範本", "本機安裝的 dotnet new 範本"),
        new("history", "執行紀錄", "檢視命令與檔案差異"),
        new("settings", "設定", "工具、語言與本機偏好")
    ];

    public static IReadOnlyList<FeatureDefinition> Features { get; } =
    [
        new("webapi", "project", "ASP.NET Core Web API", "webapi", "建立具備 OpenAPI 的 Web API 專案。", ".NET 10", "dotnet-new"),
        new("mvc", "project", "ASP.NET Core MVC", "mvc", "建立 Model-View-Controller Web 應用程式。", ".NET 10", "dotnet-new"),
        new("blazor", "project", "Blazor Web App", "blazor", "建立支援互動式元件的 Blazor 應用程式。", ".NET 10", "dotnet-new"),
        new("console", "project", "主控台應用程式", "console", "建立跨平台 .NET 主控台專案。", "C#", "dotnet-new"),
        new("wpf", "project", "WPF 應用程式", "wpf", "Windows 專用桌面專案。", "Windows only", "dotnet-new", Availability: FeatureAvailability.UnsupportedPlatform, AvailabilityReason: "WPF 不支援 macOS，因此只能瀏覽，無法在此平台執行。"),
        new("winforms", "project", "Windows Forms 應用程式", "winforms", "Windows 專用 Windows Forms 專案。", "Windows only", "dotnet-new", Availability: FeatureAvailability.UnsupportedPlatform, AvailabilityReason: "Windows Forms 不支援 macOS，因此只能瀏覽，無法在此平台執行。"),

        new("apicontroller", "component", "API Controller", "apicontroller", "建立 API Controller，可選擇加入讀寫動作。", "常用", "dotnet-new"),
        new("mvccontroller", "component", "MVC Controller", "mvccontroller", "建立 MVC Controller。", "MVC", "dotnet-new"),
        new("razorcomponent", "component", "Razor Component", "razorcomponent", "建立 Blazor Razor 元件。", "Blazor", "dotnet-new"),
        new("page", "component", "Razor Page", "page", "建立 Razor Page 與 PageModel。", "Razor", "dotnet-new"),

        new("controller", "scaffolding", "Controller Scaffolding", "controller", "依模式建立空白、CRUD 或 REST Controller。", "CRUD", "aspnet-codegenerator"),
        new("minimalapi", "scaffolding", "Minimal API Endpoints", "minimalapi", "由 Model 與 DbContext 建立 CRUD endpoints。", "API", "aspnet-codegenerator"),
        new("area", "scaffolding", "MVC Area", "area", "建立 Area 及 Controllers、Models、Views 結構。", "MVC", "aspnet-codegenerator"),
        new("blazor-crud", "scaffolding", "Blazor CRUD", "blazor", "建立 Create、Edit、Delete、Details 與 List 元件。", "CRUD", "aspnet-codegenerator"),

        new("migration-add", "efcore", "新增 Migration", "migrations add", "依目前模型差異建立新的 EF Core Migration。", "EF Core", "dotnet-ef"),
        new("dbcontext-scaffold", "efcore", "反向工程 DbContext", "dbcontext scaffold", "由既有資料庫產生 DbContext 與 Entity。", "Code gen", "dotnet-ef"),
        new("database-update", "efcore", "更新資料庫", "database update", "將 Migration 套用到指定資料庫。", "高風險", "dotnet-ef", FeatureRisk.DatabaseChange),

        new("company-api", "custom", "Company API Starter", "company-api", "示意：公司內部安裝的自訂專案範本。", "自訂", "dotnet-new"),
        new("tool-install", "custom", "安裝本機 EF Core 工具", "dotnet-ef", "示意：只在目前工作區建立或更新本機工具，不使用全域安裝。", "本機工具", "dotnet-tool", FeatureRisk.EnvironmentChange)
    ];
}
