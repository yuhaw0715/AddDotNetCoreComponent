using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class OfficialFeatureCatalogTests
{
    private static readonly string[] ExpectedDotNetNewNames =
    [
        "webapiaot", "web", "webapi", "mvc", "webapp", "razor", "grpc", "blazor", "blazorwasm", "console", "classlib", "worker", "razorclasslib",
        "mstest", "xunit", "nunit", "mstest-playwright", "nunit-playwright",
        "winforms", "winformslib", "winformscontrollib", "wpf", "wpflib", "wpfcustomcontrollib", "wpfusercontrollib",
        "apicontroller", "mvccontroller", "razorcomponent", "view", "page", "viewimports", "viewstart", "proto", "mstest-class", "nunit-test",
        "sln", "solution", "slnf", "solutionfilter", "editorconfig", "gitignore", "gitattributes", "globaljson", "nugetconfig", "tool-manifest", "buildprops", "buildtargets", "packagesprops", "webconfig"
    ];

    [Fact]
    public void DotNetNew_HasAll46CanonicalFeaturesAndEveryDocumentedAlias()
    {
        var catalog = OfficialFeatureCatalog.DotNetNew;
        var names = catalog.SelectMany(feature => feature.ShortNames).ToArray();

        Assert.Equal(46, catalog.Count);
        Assert.Equal(ExpectedDotNetNewNames.Order(), names.Order());
        Assert.Equal(catalog.Count, catalog.Select(feature => feature.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(catalog, feature => Assert.True(Enum.IsDefined(feature.Group)));
    }

    [Fact]
    public void DotNetNew_WindowsDesktopTemplatesRemainVisibleButMacOsUnsupported()
    {
        var installed = OfficialFeatureCatalog.DotNetNew.Select(feature =>
            new LocalTemplateCapability(feature.DisplayName, feature.ShortNames, ["C#"], []));

        var merged = FeatureCatalogMerger.MergeDotNetNew(OfficialFeatureCatalog.DotNetNew, installed, PlatformFamily.MacOS);
        var desktop = merged.Where(item => item.Feature.Group == FeatureGroup.WindowsDesktop).ToArray();

        Assert.Equal(7, desktop.Length);
        Assert.All(desktop, item => Assert.Equal(CatalogAvailability.UnsupportedPlatform, item.Availability));
    }

    [Fact]
    public void AspNetScaffolding_DefinesEightGeneratorsAndRequiredSubtemplates()
    {
        var catalog = OfficialFeatureCatalog.AspNetScaffolding;
        var snapshot = catalog.Select(feature =>
            $"{feature.Id}|{feature.CommandKind}|{feature.Group}|{string.Join(',', feature.Parameters.Where(parameter => parameter.IsRequired).Select(parameter => parameter.Id))}|{string.Join(',', feature.Variants)}");

        Assert.Equal(8, catalog.Count);
        Assert.Equal(
            new[] { "area", "blazor", "blazor-identity", "controller", "identity", "minimalapi", "razorpage", "view" },
            catalog.Select(feature => feature.Id).Order());
        var expectedCrudVariants = new HashSet<string>(["CRUD", "Create", "Delete", "Details", "Edit", "Empty", "List"], StringComparer.Ordinal);
        Assert.True(expectedCrudVariants.SetEquals(catalog.Single(feature => feature.Id == "blazor").Variants));
        Assert.True(expectedCrudVariants.SetEquals(catalog.Single(feature => feature.Id == "razorpage").Variants));
        Assert.All(catalog, feature => Assert.Contains(feature.Dependencies, dependency => dependency.Id == "dotnet-aspnet-codegenerator"));
        Assert.Equal(
        [
            "area|aspnet-codegenerator|AspNetScaffolding|project|Empty",
            "controller|aspnet-codegenerator|AspNetScaffolding|project|Empty,ReadWriteActions,MvcWithViews,RestApi",
            "blazor|aspnet-codegenerator|AspNetScaffolding|project|Empty,Create,Edit,Delete,Details,List,CRUD",
            "blazor-identity|aspnet-codegenerator|AspNetScaffolding|project|IdentityFiles",
            "identity|aspnet-codegenerator|AspNetScaffolding|project|IdentityFiles",
            "minimalapi|aspnet-codegenerator|AspNetScaffolding|project|CRUD",
            "razorpage|aspnet-codegenerator|AspNetScaffolding|project|Empty,Create,Edit,Delete,Details,List,CRUD",
            "view|aspnet-codegenerator|AspNetScaffolding|project|Empty"
        ], snapshot);
    }

    [Fact]
    public void EfCore_DefinesAllowedFeaturesAndNeverDatabaseDrop()
    {
        var catalog = OfficialFeatureCatalog.EfCore;
        var commands = catalog.SelectMany(feature => feature.ShortNames).ToArray();

        Assert.Equal(12, catalog.Count);
        Assert.DoesNotContain("database drop", commands);
        Assert.Equal(FeatureRisk.FileOverwrite, catalog.Single(feature => feature.Id == "migrations-remove").Risk);
        Assert.Equal(FeatureRisk.DatabaseChange, catalog.Single(feature => feature.Id == "database-update").Risk);
    }

    [Fact]
    public void Merge_ReportsAvailableMissingIncompatibleAndCustomCapabilities()
    {
        var baseline = OfficialFeatureCatalog.DotNetNew.Take(3).ToArray();
        var local = new[]
        {
            new LocalTemplateCapability("AOT API", ["webapiaot"], ["C#"], ["Web"]),
            new LocalTemplateCapability("Empty Web", ["web"], ["C#"], ["Web"], IsVersionCompatible: false),
            new LocalTemplateCapability("Company API", ["company-api"], ["C#"], ["Custom"])
        };

        var merged = FeatureCatalogMerger.MergeDotNetNew(baseline, local, PlatformFamily.MacOS);

        Assert.Equal(CatalogAvailability.Available, merged.Single(item => item.Feature.Id == "webapiaot").Availability);
        Assert.Equal(CatalogAvailability.VersionIncompatible, merged.Single(item => item.Feature.Id == "web").Availability);
        Assert.Equal(CatalogAvailability.Missing, merged.Single(item => item.Feature.Id == "webapi").Availability);
        Assert.Equal(CatalogAvailability.UnknownLocalTemplate, merged.Single(item => item.Feature.Id == "custom:company-api").Availability);
    }
}
