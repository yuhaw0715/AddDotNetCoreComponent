using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public static class OfficialFeatureCatalog
{
    private static readonly IReadOnlySet<PlatformFamily> WindowsOnly =
        new HashSet<PlatformFamily> { PlatformFamily.Windows };

    private static readonly DependencyRequirement DotNet10 =
        new(DependencyKind.DotNetSdk, "Microsoft.NET.Sdk", "10.0");

    private static readonly DependencyRequirement CodeGenerator =
        new(DependencyKind.LocalTool, "dotnet-aspnet-codegenerator", "10.0");

    private static readonly DependencyRequirement EfTool =
        new(DependencyKind.LocalTool, "dotnet-ef", "10.0");

    public static IReadOnlyList<CatalogFeature> DotNetNew { get; } =
    [
        New("webapiaot", "ASP.NET Core Web API（Native AOT）", FeatureGroup.ProjectAndService, "webapiaot"),
        New("web", "ASP.NET Core Empty", FeatureGroup.ProjectAndService, "web"),
        New("webapi", "ASP.NET Core Web API", FeatureGroup.ProjectAndService, "webapi"),
        New("mvc", "ASP.NET Core MVC", FeatureGroup.ProjectAndService, "mvc"),
        New("webapp", "ASP.NET Core Web App", FeatureGroup.ProjectAndService, "webapp", "razor"),
        New("grpc", "ASP.NET Core gRPC Service", FeatureGroup.ProjectAndService, "grpc"),
        New("blazor", "Blazor Web App", FeatureGroup.ProjectAndService, "blazor"),
        New("blazorwasm", "Blazor WebAssembly Standalone App", FeatureGroup.ProjectAndService, "blazorwasm"),
        New("console", "主控台應用程式", FeatureGroup.ProjectAndService, "console"),
        New("classlib", "類別庫", FeatureGroup.ProjectAndService, "classlib"),
        New("worker", "Worker Service", FeatureGroup.ProjectAndService, "worker"),
        New("razorclasslib", "Razor 類別庫", FeatureGroup.ProjectAndService, "razorclasslib"),

        New("mstest", "MSTest 測試專案", FeatureGroup.Test, "mstest"),
        New("xunit", "xUnit 測試專案", FeatureGroup.Test, "xunit"),
        New("nunit", "NUnit 測試專案", FeatureGroup.Test, "nunit"),
        New("mstest-playwright", "MSTest Playwright 測試專案", FeatureGroup.Test, "mstest-playwright"),
        New("nunit-playwright", "NUnit Playwright 測試專案", FeatureGroup.Test, "nunit-playwright"),

        New("winforms", "Windows Forms 應用程式", FeatureGroup.WindowsDesktop, WindowsOnly, "winforms"),
        New("winformslib", "Windows Forms 類別庫", FeatureGroup.WindowsDesktop, WindowsOnly, "winformslib"),
        New("winformscontrollib", "Windows Forms 控制項程式庫", FeatureGroup.WindowsDesktop, WindowsOnly, "winformscontrollib"),
        New("wpf", "WPF 應用程式", FeatureGroup.WindowsDesktop, WindowsOnly, "wpf"),
        New("wpflib", "WPF 類別庫", FeatureGroup.WindowsDesktop, WindowsOnly, "wpflib"),
        New("wpfcustomcontrollib", "WPF 自訂控制項程式庫", FeatureGroup.WindowsDesktop, WindowsOnly, "wpfcustomcontrollib"),
        New("wpfusercontrollib", "WPF 使用者控制項程式庫", FeatureGroup.WindowsDesktop, WindowsOnly, "wpfusercontrollib"),

        New("apicontroller", "API Controller", FeatureGroup.Item, "apicontroller"),
        New("mvccontroller", "MVC Controller", FeatureGroup.Item, "mvccontroller"),
        New("razorcomponent", "Razor Component", FeatureGroup.Item, "razorcomponent"),
        New("view", "MVC View", FeatureGroup.Item, "view"),
        New("page", "Razor Page", FeatureGroup.Item, "page"),
        New("viewimports", "MVC ViewImports", FeatureGroup.Item, "viewimports"),
        New("viewstart", "MVC ViewStart", FeatureGroup.Item, "viewstart"),
        New("proto", "Protocol Buffer File", FeatureGroup.Item, "proto"),
        New("mstest-class", "MSTest 測試類別", FeatureGroup.Item, "mstest-class"),
        New("nunit-test", "NUnit 測試類別", FeatureGroup.Item, "nunit-test"),

        New("sln", "Solution File", FeatureGroup.StructureAndConfiguration, "sln", "solution"),
        New("slnf", "Solution Filter File", FeatureGroup.StructureAndConfiguration, "slnf", "solutionfilter"),
        New("editorconfig", "EditorConfig File", FeatureGroup.StructureAndConfiguration, "editorconfig"),
        New("gitignore", "dotnet Git Ignore File", FeatureGroup.StructureAndConfiguration, "gitignore"),
        New("gitattributes", "Git Attributes File", FeatureGroup.StructureAndConfiguration, "gitattributes"),
        New("globaljson", "global.json File", FeatureGroup.StructureAndConfiguration, "globaljson"),
        New("nugetconfig", "NuGet Config", FeatureGroup.StructureAndConfiguration, "nugetconfig"),
        New("tool-manifest", "Dotnet Local Tool Manifest", FeatureGroup.StructureAndConfiguration, "tool-manifest"),
        New("buildprops", "MSBuild Directory.Build.props", FeatureGroup.StructureAndConfiguration, "buildprops"),
        New("buildtargets", "MSBuild Directory.Build.targets", FeatureGroup.StructureAndConfiguration, "buildtargets"),
        New("packagesprops", "MSBuild Directory.Packages.props", FeatureGroup.StructureAndConfiguration, "packagesprops"),
        New("webconfig", "Web Config", FeatureGroup.StructureAndConfiguration, "webconfig")
    ];

    public static IReadOnlyList<CatalogFeature> AspNetScaffolding { get; } =
    [
        Scaffold("area", "MVC Area", ["Empty"]),
        Scaffold("controller", "Controller", ["Empty", "ReadWriteActions", "MvcWithViews", "RestApi"]),
        Scaffold("blazor", "Blazor Components", ["Empty", "Create", "Edit", "Delete", "Details", "List", "CRUD"]),
        Scaffold("blazor-identity", "Blazor Identity", ["IdentityFiles"]),
        Scaffold("identity", "Identity", ["IdentityFiles"]),
        Scaffold("minimalapi", "Minimal API", ["CRUD"]),
        Scaffold("razorpage", "Razor Page", ["Empty", "Create", "Edit", "Delete", "Details", "List", "CRUD"]),
        Scaffold("view", "MVC View", ["Empty"])
    ];

    public static IReadOnlyList<CatalogFeature> EfCore { get; } =
    [
        Ef("dbcontext-scaffold", "反向工程 DbContext", FeatureGroup.EfCoreCodeGeneration, ["dbcontext scaffold"]),
        Ef("dbcontext-optimize", "最佳化 DbContext", FeatureGroup.EfCoreCodeGeneration, ["dbcontext optimize"]),
        Ef("dbcontext-script", "產生 DbContext SQL 指令碼", FeatureGroup.EfCoreCodeGeneration, ["dbcontext script"]),
        Ef("migrations-add", "新增 Migration", FeatureGroup.EfCoreMigration, ["migrations add"]),
        Ef("migrations-remove", "移除 Migration", FeatureGroup.EfCoreMigration, ["migrations remove"], FeatureRisk.FileOverwrite),
        Ef("migrations-bundle", "建立 Migration Bundle", FeatureGroup.EfCoreMigration, ["migrations bundle"]),
        Ef("migrations-list", "列出 Migrations", FeatureGroup.EfCoreInspection, ["migrations list"]),
        Ef("migrations-has-pending-model-changes", "檢查模型變更", FeatureGroup.EfCoreInspection, ["migrations has-pending-model-changes"]),
        Ef("migrations-script", "產生 Migration SQL", FeatureGroup.EfCoreMigration, ["migrations script"]),
        Ef("dbcontext-info", "顯示 DbContext 資訊", FeatureGroup.EfCoreInspection, ["dbcontext info"]),
        Ef("dbcontext-list", "列出 DbContext", FeatureGroup.EfCoreInspection, ["dbcontext list"]),
        Ef("database-update", "更新資料庫", FeatureGroup.EfCoreDatabase, ["database update"], FeatureRisk.DatabaseChange)
    ];

    private static CatalogFeature New(string id, string name, FeatureGroup group, params string[] shortNames) =>
        New(id, name, group, null, shortNames);

    private static CatalogFeature New(
        string id,
        string name,
        FeatureGroup group,
        IReadOnlySet<PlatformFamily>? platforms,
        params string[] shortNames) =>
        new(
            id,
            name,
            group,
            "dotnet-new",
            shortNames,
            supportedPlatforms: platforms,
            parameters:
            [
                new ParameterDefinition("name", "名稱", ParameterValueKind.Text),
                new ParameterDefinition("output", "輸出位置", ParameterValueKind.Path, isAdvanced: true),
                new ParameterDefinition("force", "覆寫既有輸出", ParameterValueKind.Boolean, isAdvanced: true)
            ],
            dependencies: [DotNet10],
            constraints:
            [
                ParameterConstraint.NameFormat("name"),
                ParameterConstraint.PathWithinWorkspace("output")
            ]);

    private static CatalogFeature Scaffold(string id, string name, IReadOnlyList<string> variants) =>
        new(
            id,
            name,
            FeatureGroup.AspNetScaffolding,
            "aspnet-codegenerator",
            [id],
            parameters: CreateScaffoldParameters(id, variants),
            dependencies:
            [
                CodeGenerator,
                new DependencyRequirement(DependencyKind.NuGetPackage, "Microsoft.VisualStudio.Web.CodeGeneration.Design", "10.0")
            ],
            variants: variants,
            constraints: CreateScaffoldConstraints(id));

    private static IReadOnlyList<ParameterConstraint> CreateScaffoldConstraints(string id)
    {
        var constraints = new List<ParameterConstraint>
        {
            ParameterConstraint.PathWithinWorkspace("project"),
            ParameterConstraint.PathWithinWorkspace("output")
        };
        if (id is not "identity" and not "blazor-identity")
        {
            constraints.Add(ParameterConstraint.NameFormat("name"));
            constraints.Add(ParameterConstraint.RequiredWhen("variant", "name", "CRUD"));
        }

        return constraints;
    }

    private static IReadOnlyList<ParameterDefinition> CreateScaffoldParameters(
        string id,
        IReadOnlyList<string> variants)
    {
        var parameters = new List<ParameterDefinition>
        {
            new("project", "目標專案", ParameterValueKind.Path, isRequired: true),
            new("variant", "範本", ParameterValueKind.Enumeration, allowedValues: variants)
        };

        if (id is not "blazor-identity" and not "identity")
        {
            parameters.Add(new ParameterDefinition("name", "名稱", ParameterValueKind.Text, isAdvanced: true));
        }

        if (id is not "area" and not "view" and not "identity" and not "blazor-identity")
        {
            parameters.Add(new ParameterDefinition("model", "模型", ParameterValueKind.Text, isAdvanced: true));
            parameters.Add(new ParameterDefinition("dataContext", "DbContext", ParameterValueKind.Text, isAdvanced: true));
            parameters.Add(new ParameterDefinition(
                "databaseProvider",
                "資料庫提供者",
                ParameterValueKind.Enumeration,
                isAdvanced: true,
                allowedValues: ["SqlServer", "Sqlite", "Cosmos"]));
        }

        if (id is "identity" or "blazor-identity")
        {
            parameters.Add(new ParameterDefinition("dataContext", "DbContext", ParameterValueKind.Text, isAdvanced: true));
            parameters.Add(new ParameterDefinition(
                "databaseProvider",
                "資料庫提供者",
                ParameterValueKind.Enumeration,
                isAdvanced: true,
                allowedValues: ["SqlServer", "Sqlite", "Cosmos"]));
            parameters.Add(new ParameterDefinition("files", "Identity 檔案", ParameterValueKind.List, isAdvanced: true));
        }

        parameters.Add(new ParameterDefinition("force", "覆寫既有輸出", ParameterValueKind.Boolean, isAdvanced: true));
        parameters.Add(new ParameterDefinition("output", "輸出位置", ParameterValueKind.Path, isAdvanced: true));
        return parameters;
    }

    private static CatalogFeature Ef(
        string id,
        string name,
        FeatureGroup group,
        IReadOnlyList<string> shortNames,
        FeatureRisk risk = FeatureRisk.Normal) =>
        new(
            id,
            name,
            group,
            "dotnet-ef",
            shortNames,
            risk,
            parameters: CreateEfParameters(id),
            dependencies:
            [
                EfTool,
                new DependencyRequirement(DependencyKind.NuGetPackage, "Microsoft.EntityFrameworkCore.Design", "10.0")
            ],
            constraints: CreateEfConstraints(id));

    private static IReadOnlyList<ParameterDefinition> CreateEfParameters(string id)
    {
        var parameters = new List<ParameterDefinition>
        {
            new("project", "目標專案", ParameterValueKind.Path, isRequired: true),
            new("context", "DbContext", ParameterValueKind.Text, isAdvanced: true),
            new("connection", "連線字串", ParameterValueKind.Secret, isAdvanced: true, isRequired: id == "dbcontext-scaffold")
        };

        switch (id)
        {
            case "dbcontext-scaffold":
                parameters.Add(new ParameterDefinition("provider", "資料庫提供者", ParameterValueKind.Text, isRequired: true));
                parameters.Add(new ParameterDefinition("output", "輸出位置", ParameterValueKind.Path, isAdvanced: true));
                break;
            case "dbcontext-optimize":
                parameters.Add(new ParameterDefinition("output", "輸出位置", ParameterValueKind.Path, isAdvanced: true));
                break;
            case "dbcontext-script":
            case "migrations-script":
                parameters.Add(new ParameterDefinition("fromMigration", "起始 Migration", ParameterValueKind.Text, isAdvanced: true));
                parameters.Add(new ParameterDefinition("toMigration", "結束 Migration", ParameterValueKind.Text, isAdvanced: true));
                parameters.Add(new ParameterDefinition("output", "輸出檔案", ParameterValueKind.Path, isAdvanced: true));
                break;
            case "migrations-add":
                parameters.Add(new ParameterDefinition("migrationName", "Migration 名稱", ParameterValueKind.Text, isRequired: true));
                break;
            case "migrations-remove":
                parameters.Add(new ParameterDefinition("migrationName", "Migration 名稱", ParameterValueKind.Text, isRequired: true));
                parameters.Add(new ParameterDefinition("force", "強制移除", ParameterValueKind.Boolean, isAdvanced: true));
                break;
            case "migrations-bundle":
                parameters.Add(new ParameterDefinition("output", "輸出檔案", ParameterValueKind.Path, isAdvanced: true));
                break;
            case "database-update":
                parameters.Add(new ParameterDefinition("migration", "目標 Migration", ParameterValueKind.Text, isAdvanced: true));
                break;
        }

        return parameters;
    }

    private static IReadOnlyList<ParameterConstraint> CreateEfConstraints(string id)
    {
        var constraints = new List<ParameterConstraint>
        {
            ParameterConstraint.PathWithinWorkspace("project"),
            ParameterConstraint.MutuallyExclusive("context", "connection")
        };
        if (id is "dbcontext-scaffold" or "dbcontext-optimize" or "dbcontext-script" or "migrations-script" or "migrations-bundle")
        {
            constraints.Add(ParameterConstraint.PathWithinWorkspace("output"));
        }

        if (id is "migrations-add" or "migrations-remove")
        {
            constraints.Add(ParameterConstraint.NameFormat("migrationName"));
        }

        if (id is "dbcontext-script" or "migrations-script")
        {
            constraints.Add(ParameterConstraint.NameFormat("fromMigration"));
            constraints.Add(ParameterConstraint.NameFormat("toMigration"));
        }

        if (id == "database-update")
        {
            constraints.Add(ParameterConstraint.NameFormat("migration"));
        }

        return constraints;
    }
}

public static class FeatureCatalogMerger
{
    public static IReadOnlyList<ResolvedCatalogFeature> MergeDotNetNew(
        IEnumerable<CatalogFeature> baseline,
        IEnumerable<LocalTemplateCapability> localTemplates,
        PlatformFamily currentPlatform)
    {
        var official = baseline.ToArray();
        var local = localTemplates.ToArray();
        var result = new List<ResolvedCatalogFeature>(official.Length + local.Length);

        foreach (var feature in official)
        {
            var capability = local.FirstOrDefault(candidate =>
                candidate.ShortNames.Any(name => feature.ShortNames.Contains(name, StringComparer.Ordinal)));

            if (!feature.SupportedPlatforms.Contains(currentPlatform))
            {
                result.Add(new ResolvedCatalogFeature(feature, CatalogAvailability.UnsupportedPlatform, "不支援目前平台。", capability));
            }
            else if (capability is null)
            {
                result.Add(new ResolvedCatalogFeature(feature, CatalogAvailability.Missing, "本機未安裝此官方範本。"));
            }
            else if (!capability.IsVersionCompatible)
            {
                result.Add(new ResolvedCatalogFeature(feature, CatalogAvailability.VersionIncompatible, "本機範本版本與 .NET 10 不相容。", capability));
            }
            else
            {
                result.Add(new ResolvedCatalogFeature(feature, CatalogAvailability.Available, null, capability));
            }
        }

        var officialNames = official.SelectMany(feature => feature.ShortNames).ToHashSet(StringComparer.Ordinal);
        foreach (var capability in local.Where(item => !item.ShortNames.Any(officialNames.Contains)))
        {
            var primaryName = capability.ShortNames.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(primaryName))
            {
                continue;
            }

            var custom = new CatalogFeature(
                $"custom:{primaryName}",
                capability.DisplayName,
                FeatureGroup.Custom,
                "dotnet-new",
                capability.ShortNames);
            result.Add(new ResolvedCatalogFeature(custom, CatalogAvailability.UnknownLocalTemplate, "本機安裝的自訂範本。", capability));
        }

        return result;
    }
}
