namespace POS.Application.Models;

public sealed class RestoreResult
{
    public bool IsSuccessful { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? SafetyBackupPath { get; init; }
}
