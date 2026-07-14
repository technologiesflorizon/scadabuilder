namespace ScadaBuilderV2.Infrastructure.ModernProjects;

internal enum WorkspaceSavePhase
{
    Prepared,
    WritingScenes,
    ProjectCommitting,
    ProjectCommitted,
    Completed
}

internal sealed record WorkspaceSaveFileEntry(
    string TargetRelativePath,
    string StagedRelativePath,
    string BackupRelativePath,
    bool TargetExisted);

internal sealed record WorkspaceSaveJournal(
    string TransactionId,
    long SnapshotVersion,
    WorkspaceSavePhase Phase,
    IReadOnlyList<WorkspaceSaveFileEntry> Writes,
    IReadOnlyList<string> Deletions);
