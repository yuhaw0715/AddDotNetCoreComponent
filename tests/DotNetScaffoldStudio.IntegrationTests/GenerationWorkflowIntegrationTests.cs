using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;
using DotNetScaffoldStudio.Infrastructure;

namespace DotNetScaffoldStudio.IntegrationTests;

public sealed class GenerationWorkflowIntegrationTests
{
    [Fact]
    public async Task TemporaryProject_CompletesControllerRazorPageAndEfMigrationFlows()
    {
        using var workspace = TemporaryDirectory.Create();
        var projectPath = Path.Combine(workspace.Path, "Api.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var runner = new FixtureGenerationRunner();
        var execution = new CommandExecutionWorkflow(
            new CommandCoordinator(runner),
            new GitStatusService(runner),
            new FileSnapshotService());
        var workflow = new GenerationWorkflow(
            new AvailableDependencyDiscovery(),
            new TargetProjectValidator(),
            new ExpectedOutputConflictChecker(),
            execution,
            new CommandRiskPolicy());

        var controller = OfficialFeatureCatalog.AspNetScaffolding.Single(item => item.Id == "controller");
        var controllerState = new ParameterFormState(controller.Parameters);
        controllerState.SetValue("project", ParameterValue.Path("Api.csproj"));
        controllerState.SetValue("variant", ParameterValue.Enumeration("Empty"));
        controllerState.SetValue("name", ParameterValue.Text("OrdersController"));
        controllerState.SetValue("output", ParameterValue.Path("Controllers"));
        var controllerRequest = new AspNetScaffoldingCommandFactory(new ParameterValidator())
            .Create(controller, controllerState, workspace.Path);
        var controllerResult = await Execute(
            workflow,
            controller,
            controllerRequest,
            workspace.Path,
            projectPath);

        var razorPage = OfficialFeatureCatalog.AspNetScaffolding.Single(item => item.Id == "razorpage");
        var razorState = new ParameterFormState(razorPage.Parameters);
        razorState.SetValue("project", ParameterValue.Path("Api.csproj"));
        razorState.SetValue("variant", ParameterValue.Enumeration("Empty"));
        razorState.SetValue("name", ParameterValue.Text("Orders"));
        razorState.SetValue("output", ParameterValue.Path("Pages"));
        var razorRequest = new AspNetScaffoldingCommandFactory(new ParameterValidator())
            .Create(razorPage, razorState, workspace.Path);
        var razorResult = await Execute(
            workflow,
            razorPage,
            razorRequest,
            workspace.Path,
            projectPath);

        var migration = OfficialFeatureCatalog.EfCore.Single(item => item.Id == "migrations-add");
        var migrationState = new ParameterFormState(migration.Parameters);
        migrationState.SetValue("project", ParameterValue.Path("Api.csproj"));
        migrationState.SetValue("context", ParameterValue.Text("AppDbContext"));
        migrationState.SetValue("migrationName", ParameterValue.Text("InitialCreate"));
        var migrationRequest = new EfCoreCommandFactory(new ParameterValidator())
            .Create(migration, migrationState, workspace.Path);
        var migrationResult = await Execute(
            workflow,
            migration,
            migrationRequest,
            workspace.Path,
            projectPath);

        Assert.True(controllerResult.Succeeded);
        Assert.True(razorResult.Succeeded);
        Assert.True(migrationResult.Succeeded);
        Assert.Contains(
            new FileChange(FileChangeKind.Added, "Controllers/OrdersController.cs"),
            controllerResult.Execution!.Differences!.CommandFileChanges);
        Assert.Contains(
            new FileChange(FileChangeKind.Added, "Pages/Orders.cshtml"),
            razorResult.Execution!.Differences!.CommandFileChanges);
        Assert.Contains(
            new FileChange(FileChangeKind.Added, "Migrations/InitialCreate.cs"),
            migrationResult.Execution!.Differences!.CommandFileChanges);
        Assert.Equal(3, runner.GenerationCommandCount);
    }

    private static async Task<GenerationExecutionResult> Execute(
        GenerationWorkflow workflow,
        CatalogFeature feature,
        CommandRequest request,
        string workspaceRoot,
        string projectPath) =>
        await workflow.ExecuteAsync(
            await workflow.CreatePlanAsync(
                feature,
                request,
                workspaceRoot,
                projectPath,
                [],
                CancellationToken.None),
            null,
            null,
            CancellationToken.None);

    private sealed class AvailableDependencyDiscovery : IDependencyDiscovery
    {
        public Task<DependencyDiscoveryResult> DiscoverAsync(
            string workspaceRoot,
            string? targetProjectPath,
            IReadOnlyList<DependencyRequirement> requirements,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DependencyDiscoveryResult(
                EnvironmentDetectionStatus.Available,
                requirements.Select(requirement => new DependencyCapability(
                    requirement,
                    EnvironmentDetectionStatus.Available,
                    requirement.MinimumVersion,
                    "fixture",
                    null)).ToArray(),
                []));
    }

    private sealed class FixtureGenerationRunner : ICommandRunner
    {
        public int GenerationCommandCount { get; private set; }

        public Task<CommandResult> RunAsync(
            CommandRequest request,
            IProgress<CommandOutputLine>? progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            if (request.Executable == "git")
            {
                var gitArguments = request.Arguments.Select(argument => argument.Value).ToArray();
                var output = gitArguments[0] switch
                {
                    "rev-parse" => ["true"],
                    "branch" => ["main"],
                    _ => Array.Empty<string>()
                };
                return Task.FromResult(new CommandResult(0, now, now, output, []));
            }

            GenerationCommandCount++;
            var arguments = request.Arguments.Select(argument => argument.Value).ToArray();
            if (arguments.Contains("aspnet-codegenerator") &&
                arguments.Contains("controller"))
            {
                Write(request.WorkingDirectory, "Controllers/OrdersController.cs");
            }
            else if (arguments.Contains("aspnet-codegenerator") &&
                     arguments.Contains("razorpage"))
            {
                Write(request.WorkingDirectory, "Pages/Orders.cshtml");
            }
            else if (arguments.SequenceEqual(["ef", "migrations", "add", "InitialCreate", "--project", "Api.csproj", "--context", "AppDbContext"]))
            {
                Write(request.WorkingDirectory, "Migrations/InitialCreate.cs");
            }

            return Task.FromResult(new CommandResult(0, now, now, ["fixture generated"], []));
        }

        private static void Write(string root, string relativePath)
        {
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "// fixture generated");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"dotnet-scaffold-studio-generation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
