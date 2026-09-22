namespace DotNetScaffoldStudio.Domain;

public enum OutputConflictStatus
{
    NoConflict,
    Blocked,
    RequiresConfirmation
}

public sealed record OutputConflictResult(
    OutputConflictStatus Status,
    IReadOnlyList<string> ConflictingPaths,
    string Message)
{
    public bool CanExecute => Status != OutputConflictStatus.Blocked;
    public bool RequiresConfirmation => Status == OutputConflictStatus.RequiresConfirmation;
}
