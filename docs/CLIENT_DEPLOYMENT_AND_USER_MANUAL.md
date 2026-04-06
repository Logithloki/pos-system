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

## 6. Core Usage Guide

### A. Add Products (Admin)

1. Open `Inventory` tab.
2. Click `New Product`.
3. Fill required fields:
   - Barcode
   - Name
   - Price
   - Cost
   - Stock
   - Reorder
4. Click `Save Product`.
5. Click `Refresh` to verify product appears in list.

### B. Checkout (Cashier/Admin)

1. Go to `Checkout` tab.
2. Scan or type barcode.
3. Adjust quantity if needed.
4. Confirm payment method and amount.
5. Press `F2` or click Pay.

### C. Quick Add During Checkout

If scanned barcode is unknown:

1. Inline quick-add panel appears.
2. Fill name, price, cost, stock.
3. Save quick add.
4. Item is added to cart.

### D. Refund (Admin)

1. Open refund flow.
2. Select completed sale.
3. Submit refund reversal.

Note: partial refunds are not supported in this release. Refund always reverses full sale.

### E. Backup and Restore (Admin)

1. Open `Backup/Restore` tab.
2. Create manual backup regularly.
3. Use restore only when required.

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
