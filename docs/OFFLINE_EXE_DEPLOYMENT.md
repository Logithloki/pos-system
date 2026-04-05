# Offline EXE Deployment Guide

This POS system can run fully offline as a Windows desktop EXE.

## Deployment Model

- Primary runtime: `POS.Desktop.exe`
- Database: local SQLite file on customer machine
- Internet/cloud: not required for normal operation
- API executable: optional (only if customer needs API endpoints)

## Minimum Client Requirements

- Windows 10 or Windows 11 (x64)
- Local user account with write access to chosen data folders
- No .NET runtime install required when using the self-contained bundle

## Build Client Bundle

From repository root:

```powershell
./scripts/publish-client-bundle.ps1
```

Optional: include API executable too:

```powershell
./scripts/publish-client-bundle.ps1 -IncludeApi
```

Output folder:

- `artifacts/client-bundle/Desktop/POS.Desktop.exe`
- `artifacts/client-bundle/Docs/*`
- `artifacts/client-bundle/configure-pos-client.ps1`
- `artifacts/client-bundle/clear-pos-seed-vars.ps1`

## Customer Setup (First Run)

On customer PC, open PowerShell and run:

```powershell
./configure-pos-client.ps1 -AdminUsername "admin" -AdminPassword "ChangeThisNow_123!" -StoreName "My Store" -TaxRatePercent 5
```

Then launch:

- `Start-POS-Desktop.cmd` (or run `Desktop/POS.Desktop.exe` directly)

After first successful startup, clear seed credentials:

```powershell
./clear-pos-seed-vars.ps1
```

## Runtime Configuration Variables

### Required for first run only

- `POS_ADMIN_USERNAME`
- `POS_ADMIN_PASSWORD`

### Recommended operational settings

- `POS_STORE_NAME` (printed on receipts)
- `POS_TAX_RATE_PERCENT` (default checkout tax)

### Optional desktop storage overrides

- `POS_DESKTOP_DB_PATH`
- `POS_DESKTOP_BACKUP_DIR`
- `POS_DESKTOP_SPOOL_DIR`

### Optional backup/receipt tuning

- `POS_BACKUP_RETENTION_DAYS`
- `POS_BACKUP_INTERVAL_MINUTES`
- `POS_RECEIPT_MAX_RETRY_ATTEMPTS`
- `POS_RECEIPT_RETRY_DELAY_MS`

## Default Storage Locations

If path overrides are not set, desktop app uses:

- Database: `%LOCALAPPDATA%/POS/data/pos.db`
- Backups: `%LOCALAPPDATA%/POS/backups`
- Receipt spool: `%LOCALAPPDATA%/POS/spool/receipts`

## Operational Notes for Customer

- Back up `%LOCALAPPDATA%/POS/data` and `%LOCALAPPDATA%/POS/backups` regularly.
- Verify at least one backup restore on a test machine before go-live.
- Keep seed credentials disabled after initial setup.
- Use admin account only for management tasks and refund operations.
