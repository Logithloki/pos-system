# POS Client Deployment and User Manual

This manual is for store owners and cashiers using the offline POS desktop application.

## 1. What You Receive

You will receive a folder called `client-bundle` containing:

- `Desktop/POS.Desktop.exe` (main application)
- `Start-POS-Desktop.cmd` (simple launcher)
- `configure-pos-client.ps1` (first-time setup)
- `clear-pos-seed-vars.ps1` (security cleanup after first setup)
- `Docs/*` (documentation)

Keep this folder structure unchanged.

## 2. System Requirements

- Windows 10 or Windows 11 (64-bit)
- Local user account with permission to write files
- No internet required for normal operation
- No cloud hosting required

## 3. First-Time Setup (One Time Only)

1. Open PowerShell in the `client-bundle` folder.
2. If script execution is blocked, run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

3. Run setup script:

```powershell
.\configure-pos-client.ps1 -AdminUsername "admin" -AdminPassword "ChangeThisNow_123!" -StoreName "My Store" -TaxRatePercent 5
```

4. Close PowerShell and open a new one.
5. Start the application:

```powershell
.\Start-POS-Desktop.cmd
```

6. After successful first startup, remove seed credentials:

```powershell
.\clear-pos-seed-vars.ps1
```

## 4. Daily Start and Stop

### Start

1. Open `client-bundle` folder.
2. Double-click `Start-POS-Desktop.cmd`.

### Stop

1. Finish current checkout.
2. Close the POS window normally.

## 5. Operator Roles

- Admin:
  - Full access (inventory, suppliers, customers, reports, users, backup/restore, refunds)
- Cashier:
  - Checkout-only workflow
  - No access to admin management tabs

## 6. Core Usage Guide (Step-by-Step)

### A. Recommended first-day setup order (Admin)

1. Open `Suppliers` tab and add suppliers first.
2. Open `Inventory` tab and add products.
3. Open `Users` tab and create cashier accounts.
4. Open `Customers` tab and add known customers if needed.
5. Open `Checkout` tab and run one test sale.
6. Open `Backup/Restore` tab and create one manual backup.

### B. Add supplier (Admin)

1. Open `Suppliers` tab.
2. Click `New Supplier`.
3. Fill fields: Name, Contact, Address, Phone, Email.
4. Click `Save`.
5. Click `Refresh` and confirm supplier appears in list.

### C. Add product (Admin)

1. Open `Inventory` tab.
2. Click `New Product`.
3. Fill fields:
  - Barcode
  - Name
  - Price
  - Cost
  - Stock
  - Reorder
  - Supplier (optional)
4. Click `Save Product`.
5. Click `Refresh` and verify product appears in grid.

Important:

1. Use `Save Product` to create or update products.
2. `Apply Adjustment` is only for changing stock of an existing selected product.

### D. Create cashier user (Admin)

1. Open `Users` tab.
2. In `Create Cashier` panel, fill Username, Full Name, Email (optional), Password.
3. Click `Create Cashier`.
4. Click `Refresh` and verify user appears in grid.
5. Use `Toggle Active` to enable or disable user access.

### E. Add customer (Admin)

1. Open `Customers` tab.
2. Click `New Customer`.
3. Fill Name, Phone, Email (optional), Loyalty Points.
4. Click `Save`.
5. Click `Refresh` and verify customer appears in list.

### F. Checkout sale (Cashier/Admin)

1. Open `Checkout` tab.
2. Scan barcode or type barcode and press `Enter`.
3. Repeat for each item.
4. Optional: adjust quantities using `+`, `-`, and `Delete`.
5. Select payment method.
6. For cash, enter amount received (or use `Exact`, `+10`, `+20`, `+50`).
7. Press `F2` or click `Pay (F2)`.
8. Confirm receipt number in status message.

### G. Unknown barcode during checkout (Quick Add)

1. Scan unknown barcode.
2. Quick Add panel appears automatically.
3. Fill Name, Price, Cost, Stock.
4. Click `Save + Add`.
5. Product is created and added to cart in one step.

### H. Customer history lookup (Admin)

1. Open `Customers` tab.
2. Select a customer in the top grid.
3. View lifetime spend and purchase count in snapshot card.
4. View recent purchase history in the lower grid.

### I. Reports (Admin)

1. Open `Reports` tab.
2. Choose period: Daily, Weekly, or Monthly.
3. Click `Refresh`.
4. Review Total Sales, Total Profit, Transactions, Average Sale.
5. Review `Top Products` table.

### J. Backup and restore (Admin)

Create backup:

1. Open `Backup/Restore` tab.
2. Click `Create Backup`.
3. Confirm backup appears in list.

Restore backup:

1. Open `Backup/Restore` tab.
2. Select backup row.
3. Tick `I confirm restore will replace current database`.
4. Click `Restore Backup`.
5. Re-open tabs and validate data after restore.

### K. Refund handling

This desktop build does not include a dedicated refund tab.

1. Refunds are admin-only operations.
2. In this release, refunds are full-sale reversal only (no partial refund).
3. If your workflow requires refund UI from desktop, contact your software provider for the next release option.

## 7. Keyboard Shortcuts (Checkout)

- `Enter`: add barcode item
- `F2`: pay
- `Esc`: clear cart
- `Up/Down`: cart row navigation
- `+`: increase selected quantity
- `-`: decrease selected quantity
- `Delete`: remove selected line

## 8. Configurations You Can Change

Configured using `configure-pos-client.ps1` or user environment variables:

- `POS_STORE_NAME`: store name printed on receipts
- `POS_TAX_RATE_PERCENT`: default tax rate
- `POS_DESKTOP_DB_PATH`: custom database file path
- `POS_DESKTOP_BACKUP_DIR`: custom backup folder
- `POS_DESKTOP_SPOOL_DIR`: custom print spool folder
- `POS_BACKUP_RETENTION_DAYS`: backup retention window
- `POS_BACKUP_INTERVAL_MINUTES`: automatic backup interval
- `POS_RECEIPT_MAX_RETRY_ATTEMPTS`: print retry attempts
- `POS_RECEIPT_RETRY_DELAY_MS`: print retry delay

### Method A (Recommended): Configure with script

Run from the `client-bundle` folder:

```powershell
.\configure-pos-client.ps1 -AdminUsername "admin" -AdminPassword "ChangeThisNow_123!" -StoreName "My Store" -TaxRatePercent 5
```

Advanced example (custom paths + retry/backup tuning):

```powershell
.\configure-pos-client.ps1 -AdminUsername "admin" -AdminPassword "ChangeThisNow_123!" -StoreName "My Store" -TaxRatePercent 5 -DesktopDatabasePath "D:\POSData\pos.db" -DesktopBackupDirectory "D:\POSData\backups" -DesktopSpoolDirectory "D:\POSData\spool\receipts" -BackupRetentionDays 30 -BackupIntervalMinutes 240 -ReceiptRetryAttempts 3 -ReceiptRetryDelayMs 120
```

### Method B: Set environment variables manually

Use this when changing only one or two values:

```powershell
[Environment]::SetEnvironmentVariable("POS_STORE_NAME", "My Store", "User")
[Environment]::SetEnvironmentVariable("POS_TAX_RATE_PERCENT", "5", "User")
[Environment]::SetEnvironmentVariable("POS_DESKTOP_DB_PATH", "D:\POSData\pos.db", "User")
[Environment]::SetEnvironmentVariable("POS_DESKTOP_BACKUP_DIR", "D:\POSData\backups", "User")
[Environment]::SetEnvironmentVariable("POS_DESKTOP_SPOOL_DIR", "D:\POSData\spool\receipts", "User")
[Environment]::SetEnvironmentVariable("POS_BACKUP_RETENTION_DAYS", "30", "User")
[Environment]::SetEnvironmentVariable("POS_BACKUP_INTERVAL_MINUTES", "240", "User")
[Environment]::SetEnvironmentVariable("POS_RECEIPT_MAX_RETRY_ATTEMPTS", "3", "User")
[Environment]::SetEnvironmentVariable("POS_RECEIPT_RETRY_DELAY_MS", "120", "User")
```

To remove a custom override and use default behavior again:

```powershell
[Environment]::SetEnvironmentVariable("POS_DESKTOP_DB_PATH", $null, "User")
```

### Apply changes

1. Close POS application.
2. Close terminal.
3. Open a new terminal (or sign out/sign in).
4. Start POS again.

### Verify current configuration

```powershell
$keys = @("POS_STORE_NAME","POS_TAX_RATE_PERCENT","POS_DESKTOP_DB_PATH","POS_DESKTOP_BACKUP_DIR","POS_DESKTOP_SPOOL_DIR","POS_BACKUP_RETENTION_DAYS","POS_BACKUP_INTERVAL_MINUTES","POS_RECEIPT_MAX_RETRY_ATTEMPTS","POS_RECEIPT_RETRY_DELAY_MS")
foreach ($k in $keys) { Write-Host "$k = $([Environment]::GetEnvironmentVariable($k,'User'))" }
```

## 9. Data and Backup Locations (Default)

If not overridden:

- Database: `%LOCALAPPDATA%/POS/data/pos.db`
- Backups: `%LOCALAPPDATA%/POS/backups`
- Receipt spool: `%LOCALAPPDATA%/POS/spool/receipts`

## 10. Troubleshooting

### Cannot run setup script

Run in PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

### App starts but cannot add product

- Confirm you are in Admin mode (management tabs visible).
- In Inventory tab, click `Save Product` (not `Apply Adjustment`) for new product creation.
- Check bottom status message for validation error details.

### Product save fails

Check these rules:

- Barcode required and unique
- Name required
- Price must be greater than 0
- Cost cannot be negative
- Stock and reorder must be whole numbers >= 0

### Backup or restore fails

- Ensure destination folders are writable.
- Ensure selected restore file is from the configured backup folder.

## 11. Security Best Practices

- Use a strong admin password.
- Create separate cashier accounts for daily operations.
- Remove seed credentials using `clear-pos-seed-vars.ps1` after first setup.
- Keep regular backups and verify restore on a test machine.

## 12. Recommended Daily Operations Checklist

At store opening:

1. Launch POS
2. Run a quick test checkout

During the day:

1. Monitor low stock items
2. Keep one admin user available for management operations

At store closing:

1. Create manual backup
2. Close POS cleanly
