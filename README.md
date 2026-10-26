# NODE Uninstaller — v0.1.1 (Portable Windows)

**NODE Development · Clean Removal. Complete Control.**

This is a **Windows 10/11 x64 WPF app** built on .NET 10. It is a portable self-contained executable; it does not need a setup installer or an existing .NET runtime. Build the EXE via the included GitHub Actions workflow.

## Working feature set

- Desktop application inventory (machine and current user; 32-bit/64-bit Registry views).
- Microsoft Store package inventory (current user's main packages; inventory only).
- Search, filter, dark list UI, application details, shortcut to install folder, clipboard copy.
- **NEW v0.1.1: Interactive registered desktop uninstaller**: review program/publisher/command, explicitly confirm, launch the vendor's standard executable or MSI uninstaller, monitor the launched process, and refresh installed-app records when it exits (or when the user chooses to stop monitoring without killing it).
- MSI uninstall entries get a validated product-code invocation via `msiexec.exe /x {GUID}` instead of accidentally re-running MSI install/repair commands.
- Unsupported, missing or potentially risky interpreter-based uninstall commands are **not executed**. A clear explanation is shown instead.
- Maintains a custom dark notification and confirmation overlay and the taskbar-aware borderless window.

## Important limitations / safety

- **Only registered desktop application uninstallers are launchable in v0.1.1.** Microsoft Store uninstall, forced removal, batch removal, and leftover deletion remain disabled. For Store packages, use Windows Settings.
- NODE does **not** remove files or Registry keys itself; running a selected original vendor uninstaller may make changes to your PC, as expected for any uninstall. This release **does not create a restore point** or a backup.
- External uninstallers may hand off to another process. An exit code 0 or disappearance from Installed Apps does **not** prove every app file was removed. NODE reports only the launched process status and current uninstall inventory.
- Uninstall actions need user confirmation; elevation is requested for registered machine-wide EXE uninstallers when appropriate; MSI handles its own elevation. Portable mode itself does not require elevation.
- Application uninstall entries originate in the Windows Registry and are not a trusted inventory of what a program will do. Review the displayed command and publisher carefully. Shared runtimes, drivers, and software critical to Windows should not be uninstalled casually.
- A failing or missing registered uninstall command is **not** treated as an invitation to forcibly delete files. That feature requires its own safeguards in a future release.

## Build

1. Upload all source files to GitHub, including `.github/workflows/build-windows.yml` (not the ZIP as a single file).
2. Open **Actions → Build NODE Uninstaller (Portable Windows)**.
3. Trigger or inspect the newest workflow on `main`.
4. Download artifact `NODE-Uninstaller-v0.1.1-PORTABLE-WINDOWS` after a **successful build**.
5. Extract the artifact, then double-click `NODE-Uninstaller.exe` (or extract the companion portable ZIP). No installer is needed.

The current chat environment cannot run a Windows/WPF build. **Compilation and Windows runtime behavior must be verified by the GitHub Actions run and on a Windows PC**. See `docs/TEST-PLAN-v0.1.1.md` before trying actual removal.

## Source layout

- `src/NODE.Uninstaller/Services/AppInventoryService.cs`: read-only installed-app discovery.
- `src/NODE.Uninstaller/Services/UninstallService.cs`: standard desktop uninstallation planner/launcher.
- `src/NODE.Uninstaller/MainWindow.xaml[.cs]`: dark WPF interface, user confirmation and process-status UI.
- `.github/workflows/build-windows.yml`: self-contained portable Windows build.
- `installer/`: old NSIS source retained for later; **not used** in this portable workflow.
