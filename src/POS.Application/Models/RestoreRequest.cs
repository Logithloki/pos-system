namespace POS.Application.Models;

public sealed class RestoreRequest
{
    public string BackupFilePath { get; init; } = string.Empty;

    public bool CreateSafetyBackupBeforeRestore { get; init; } = true;
}
