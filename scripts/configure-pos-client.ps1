param(
    [Parameter(Mandatory = $true)]
    [string]$AdminUsername,

    [Parameter(Mandatory = $true)]
    [string]$AdminPassword,

    [string]$StoreName = "POS Store",
    [decimal]$TaxRatePercent = 5,

    [string]$DesktopDatabasePath = "",
    [string]$DesktopBackupDirectory = "",
    [string]$DesktopSpoolDirectory = "",

    [int]$BackupRetentionDays = 30,
    [int]$BackupIntervalMinutes = 240,
    [int]$ReceiptRetryAttempts = 3,
    [int]$ReceiptRetryDelayMs = 120
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AdminUsername)) {
    throw "AdminUsername is required."
}

if ([string]::IsNullOrWhiteSpace($AdminPassword) -or $AdminPassword.Length -lt 8) {
    throw "AdminPassword is required and must be at least 8 characters."
}

if ($TaxRatePercent -lt 0 -or $TaxRatePercent -gt 100) {
    throw "TaxRatePercent must be between 0 and 100."
}

function Set-UserEnvironmentVariable {
    param(
        [string]$Name,
        [string]$Value
    )

    [Environment]::SetEnvironmentVariable($Name, $Value, [EnvironmentVariableTarget]::User)
    Write-Host "Set user environment variable: $Name"
}

Set-UserEnvironmentVariable -Name "POS_ADMIN_USERNAME" -Value $AdminUsername
Set-UserEnvironmentVariable -Name "POS_ADMIN_PASSWORD" -Value $AdminPassword
Set-UserEnvironmentVariable -Name "POS_STORE_NAME" -Value $StoreName
Set-UserEnvironmentVariable -Name "POS_TAX_RATE_PERCENT" -Value $TaxRatePercent.ToString([System.Globalization.CultureInfo]::InvariantCulture)

Set-UserEnvironmentVariable -Name "POS_BACKUP_RETENTION_DAYS" -Value $BackupRetentionDays.ToString([System.Globalization.CultureInfo]::InvariantCulture)
Set-UserEnvironmentVariable -Name "POS_BACKUP_INTERVAL_MINUTES" -Value $BackupIntervalMinutes.ToString([System.Globalization.CultureInfo]::InvariantCulture)
Set-UserEnvironmentVariable -Name "POS_RECEIPT_MAX_RETRY_ATTEMPTS" -Value $ReceiptRetryAttempts.ToString([System.Globalization.CultureInfo]::InvariantCulture)
Set-UserEnvironmentVariable -Name "POS_RECEIPT_RETRY_DELAY_MS" -Value $ReceiptRetryDelayMs.ToString([System.Globalization.CultureInfo]::InvariantCulture)

if (-not [string]::IsNullOrWhiteSpace($DesktopDatabasePath)) {
    Set-UserEnvironmentVariable -Name "POS_DESKTOP_DB_PATH" -Value $DesktopDatabasePath
}

if (-not [string]::IsNullOrWhiteSpace($DesktopBackupDirectory)) {
    Set-UserEnvironmentVariable -Name "POS_DESKTOP_BACKUP_DIR" -Value $DesktopBackupDirectory
}

if (-not [string]::IsNullOrWhiteSpace($DesktopSpoolDirectory)) {
    Set-UserEnvironmentVariable -Name "POS_DESKTOP_SPOOL_DIR" -Value $DesktopSpoolDirectory
}

Write-Host ""
Write-Host "Client configuration saved."
Write-Host "Next steps:"
Write-Host "1. Close and reopen terminal or sign out/sign in so user environment updates are visible."
Write-Host "2. Start POS.Desktop.exe."
Write-Host "3. After first successful startup and admin creation, run scripts/clear-pos-seed-vars.ps1 to remove seed credentials from user environment."
