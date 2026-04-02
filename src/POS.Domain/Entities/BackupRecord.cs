using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class BackupRecord : EntityBase
{
    public string FilePath { get; set; } = string.Empty;

    public string ChecksumSha256 { get; set; } = string.Empty;

    public bool IsAutomatic { get; set; }

    public bool IsRestoreOperation { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }
}
