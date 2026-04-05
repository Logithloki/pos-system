# Production Readiness Checklist (Offline EXE Delivery)

Use this checklist before handing the POS package to a customer.

## 1. Release Integrity

- [ ] Working tree is clean (`git status` shows no changes)
- [ ] Tests pass (`dotnet test POS.slnx`)
- [ ] Security scans on the selected release commit are green
- [ ] Release tag created (example: `v1.0.0`)

## 2. Build Artifacts

- [ ] Run `scripts/publish-client-bundle.ps1`
- [ ] Confirm `artifacts/client-bundle/Desktop/POS.Desktop.exe` exists
- [ ] Confirm deployment docs exist in `artifacts/client-bundle/Docs`
- [ ] Confirm config scripts exist in bundle root

## 3. First-Run Bootstrap

- [ ] Seed credentials defined using `configure-pos-client.ps1`
- [ ] App starts and creates initial admin successfully
- [ ] Seed credentials removed using `clear-pos-seed-vars.ps1` after first successful startup

## 4. Functional Validation

- [ ] Cashier checkout works with barcode + keyboard flow
- [ ] Admin can access management areas
- [ ] Cashier is blocked from admin-only actions
- [ ] Backup creation and restore both succeed
- [ ] Receipt print failure/retry path validated

## 5. Stress and Failure Validation

- [ ] 100 rapid transaction scenario passes
- [ ] Duplicate submission handling passes
- [ ] Large cart scenario (100 items) passes
- [ ] Concurrent stock update scenario passes
- [ ] Crash during checkout rollback scenario passes
- [ ] DB lock/unavailable scenario passes

## 6. Manual UI Acceptance

- [ ] Scan 30 items quickly
- [ ] Complete 10 consecutive transactions
- [ ] Keyboard-only operation validated
- [ ] Wrong barcode, remove item, and cancel flows validated
- [ ] No lag, freeze, wrong totals, or focus-loss issues observed

## 7. Customer Handoff

- [ ] Deliver the full `client-bundle` folder
- [ ] Include `OFFLINE_EXE_DEPLOYMENT.md`
- [ ] Explain backup location and restore procedure
- [ ] Explain where database file is stored
- [ ] Explain how to set store name and tax rate
