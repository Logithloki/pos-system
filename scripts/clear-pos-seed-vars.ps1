$ErrorActionPreference = "Stop"

[Environment]::SetEnvironmentVariable("POS_ADMIN_USERNAME", $null, [EnvironmentVariableTarget]::User)
[Environment]::SetEnvironmentVariable("POS_ADMIN_PASSWORD", $null, [EnvironmentVariableTarget]::User)

Write-Host "Cleared user-level seed credentials: POS_ADMIN_USERNAME, POS_ADMIN_PASSWORD"
Write-Host "Restart terminal sessions to pick up changes."
