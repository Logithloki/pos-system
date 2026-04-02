namespace POS.Infrastructure.Backup;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    public string DatabaseFilePath { get; set; } = string.Empty;

    public string BackupDirectory { get; set; } = string.Empty;

    public int RetentionDays { get; set; } = 30;

    public int AutomaticBackupIntervalMinutes { get; set; } = 240;
}
