# NODE Uninstaller v0.1.1 — Validation checklist

## Build/packaging
- [ ] GitHub Actions successfully restores, compiles and publishes win-x64 net10 WPF.
- [ ] Artifact has `NODE-Uninstaller.exe`, `NODE-Uninstaller-v0.1.1-PORTABLE.zip`, `SHA256SUMS.txt`.
- [ ] EXE launches directly without prior .NET installation or NSIS setup.

## UI/inventory
- [ ] Official logo, full dark list, and 0.1.1 badge are visible.
- [ ] Inventory populates without `CollectionView` deferred-refresh errors.
- [ ] Search, source filters, selection, open folder, clipboard copy work.
- [ ] Maximized and restored windows do not overlap the taskbar.

## Uninstall safeguards (use a disposable benign app)
- [ ] Selecting an application with a valid EXE uninstall command enables Uninstall.
- [ ] Clicking Uninstall displays a dark confirmation showing app, publisher and exact command.
- [ ] Clicking Cancel does **not** launch any process.
- [ ] Confirm launches the **original registered uninstaller** (no silent defaults).
- [ ] If UAC is declined, NODE reports that no removal was launched.
- [ ] NODE waits for the launched process to exit, refreshes inventory and reports record status without claiming deep-clean success.
- [ ] "Stop monitoring" releases the NODE UI **without terminating** the vendor's uninstaller.
- [ ] If the Registry points to a missing EXE, the Uninstall button is disabled.
- [ ] Interpreter-based commands (cmd, PowerShell, rundll32, scripts) are blocked from automatic launch.
- [ ] MSI entries invoke `msiexec /x {product-code}` after confirmation (test in a disposable VM).
- [ ] Store application Uninstall remains disabled (inventory-only).
- [ ] Forced Uninstall, Batch Uninstall, Leftover Scanner and Hunter Mode remain previews.

## Important
A build passing CI is not equivalent to runtime validation of removal operations. Use a Windows VM or a nonessential disposable app first, and don't test on components shared by other programs.
