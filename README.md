# POS Phase 1 and Phase 2 Checkout UI

This repository now contains the Phase 1 foundation and the Phase 2 cashier checkout screen for an offline-first retail POS.

## Implemented in Phase 1

- Offline-first local runtime with SQLite-backed persistence.
- Decimal-only money handling with a single rounding policy.
- Transactional checkout and refund reversal flows.
- Concurrency protection using row version tokens plus DB transactions.
- Immutable receipt storage.
- Centralized API exception handling and structured logging.
- Automated and manual backup/restore services.
- Receipt print retry and reprint from stored receipt payloads.

## Implemented in Phase 2 (Checkout UI)

- Keyboard-first cashier workflow.
- Fast barcode-first cart updates using in-memory product indexing.
- Always-visible cart, totals, and payment panel.
- Inline error/status messaging with no blocking dialogs in checkout path.
- Inline quick-add product flow when barcode is not found.
- Desktop checkout wired directly to transactional checkout service.

## Implemented in Phase 2.1 (Cashier Throughput)

- Automatic focus return to barcode input after checkout actions and inline errors.
- Cart quantity hotkeys: + to increase, - to decrease, Delete to remove selected line.
- Cash amount-received workflow with automatic change calculation and quick cash preset buttons.
- Optional auto-print toggle after successful payment.
- Audio feedback for successful scans/payments and error conditions.
- DataGrid virtualization enabled for smooth large-cart performance.

## Project Layout

- src/POS.Domain: Domain entities, enums, money policy, concurrency contracts.
- src/POS.Application: Service abstractions and request/response contracts.
- src/POS.Infrastructure: EF Core DbContext, core services, backup, printing, auth.
- src/POS.API: REST API host, middleware, endpoint controllers.
- src/POS.Desktop: WPF cashier checkout application.
- tests/POS.Tests: Phase 1 automated tests.

## Prerequisites

- .NET SDK 10.0.101 or newer.
- Windows environment for desktop target.

## Configuration

Edit API settings in src/POS.API/appsettings.json and src/POS.API/appsettings.Development.json.

Set these environment variables before first API run:

- POS_ADMIN_USERNAME
- POS_ADMIN_PASSWORD
- POS_STORE_NAME (optional)
- POS_DB_PATH (optional, design-time migration path)

A template is provided in .env.example.

## Build and Test

```powershell
dotnet build POS.slnx
dotnet test POS.slnx
```

## Run Desktop Checkout

```powershell
dotnet run --project src/POS.Desktop/POS.Desktop.csproj
```

Hotkeys:

- Enter: add barcode item
- F2: pay
- Esc: clear cart
- Up/Down arrows: cart row navigation
- +: increase selected cart quantity
- -: decrease selected cart quantity
- Delete: remove selected cart line

## Database Migrations

Local tool manifest is included. To add a migration:

```powershell
dotnet dotnet-ef migrations add <MigrationName> --project src/POS.Infrastructure/POS.Infrastructure.csproj --output-dir Data/Migrations
```

To apply migrations, start the API. Startup runs migration + optional admin seed.

## API Endpoints (Phase 1)

- POST /api/auth/login
- POST /api/checkout
- POST /api/refunds
- GET /api/backup
- POST /api/backup/manual
- POST /api/backup/restore
- POST /api/receipts/print/{receiptId}
- POST /api/receipts/reprint/{receiptNumber}
- GET /api/system/health

## Notes

- Receipt records are immutable and cannot be edited or deleted.
- Refunds are implemented as reversal sales records linked to the original sale.
- Backup and print behavior is validated in automated tests.
- Checkout calls the same transactional service used by API flows to preserve integrity guarantees.
