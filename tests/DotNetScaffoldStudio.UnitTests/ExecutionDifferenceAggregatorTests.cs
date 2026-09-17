using DotNetScaffoldStudio.Application;
using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.UnitTests;

public sealed class ExecutionDifferenceAggregatorTests
{
    [Fact]
    public void Create_SeparatesPreExistingGitChangesFromNewCommandChanges()
    {
        var existing = new GitFileChange(" M", "existing.cs");
        var generated = new GitFileChange("??", "Controllers/OrdersController.cs");
        var before = new GitWorkspaceState(true, "main", [existing]);
        var after = new GitWorkspaceState(true, "main", [existing, generated]);
        var fileChanges = new[] { new FileChange(FileChangeKind.Added, "Controllers/OrdersController.cs") };

        var summary = ExecutionDifferenceAggregator.Create(before, after, fileChanges);

        Assert.Equal([existing], summary.ExistingGitChanges);
        Assert.Equal([generated], summary.CurrentGitChanges);
        Assert.Equal(fileChanges, summary.CommandFileChanges);
    }
}
