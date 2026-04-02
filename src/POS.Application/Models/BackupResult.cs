namespace POS.Application.Models;

public sealed class BackupResult
{
    public string FilePath { get; init; } = string.Empty;

    public string ChecksumSha256 { get; init; } = string.Empty;

    public DateTime CreatedUtc { get; init; }

    public bool IsAutomatic { get; init; }
}
