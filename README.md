# NODE Uninstaller — v0.1.0 (Foundation)

**Developed by NODE Development** · *Clean Removal. Complete Control.*

**Windows 10/11 x64 | C# / .NET 10 | WPF | Fully local inventory**

This is the initial real source project for the approved Option B **Advanced Uninstaller** roadmap. It includes the uploaded, user-provided official logo and a matching red/cyan dark interface. The public preview demonstrates the visual direction; actual UI may differ slightly.

## What works in v0.1.0

- Polished, dark WPF main window, branded sidebar, custom title controls, stats and app details.
- **Read-only** detection of traditional desktop programs registered under the current user and local-machine 32/64-bit Registry uninstall keys.
- **Read-only** detection of the current user's Microsoft Store **main packages**, using built-in Windows PowerShell (`Get-AppxPackage`).
- Search by application name, publisher or version, source filters, counts, icon display where Registry icon paths exist, and detailed installation metadata.
- Open an existing reported installation folder in Windows Explorer; copy selected metadata to clipboard.
- Feature-navigation panels that **clearly identify unimplemented tools** and do not pretend to uninstall or clean anything.
- Self-contained Windows x64 GitHub Actions workflow plus NSIS installer source.

## Not yet implemented

**No uninstallation, file deletion, Registry deletion, forced removal, leftover scanner, batch actions, Hunter Mode, backups, update checking or history operations work in v0.1.0.** They are planned across later releases. The **Uninstall** button is intentionally disabled. This build performs inventory only and does not request Administrator privileges.

## Build using GitHub Actions (no Android Studio and no local SDK needed)

1. Create an empty GitHub repository (e.g., `NODE-Uninstaller` under your account).
2. Upload/extract **all** files from the full source ZIP to the repository root, including `.github/workflows/build-windows.yml`.
3. On GitHub: **Actions → Build NODE Uninstaller (Windows) → Run workflow** (or push changes to `main`).
4. Open the completed workflow run → **Artifacts → NODE-Uninstaller-v0.1.0-Windows-Build**.
5. Extract the downloaded workflow artifact ZIP. It should contain:
   - `NODE-Uninstaller-Setup-v0.1.0.exe` — Windows installer.
   - `NODE-Uninstaller-v0.1.0-WINDOWS-PORTABLE.zip` — self-contained portable build.
   - `SHA256SUMS.txt` — integrity hashes for the two outputs.
6. Start by testing inside a Windows 10/11 VM or other disposable test environment.

The source ZIP is **not** an already compiled .exe. The Windows installer/portable build will exist only after GitHub Actions successfully compiles it. The source has **not** been compiled or tested on Windows in this environment.

## Local Windows build (if .NET 10 SDK is installed)

```powershell
dotnet restore src\NODE.Uninstaller\NODE.Uninstaller.csproj -r win-x64
dotnet build src\NODE.Uninstaller\NODE.Uninstaller.csproj -c Release -r win-x64
dotnet run --project src\NODE.Uninstaller\NODE.Uninstaller.csproj
```

For packaging, run the publish command in `.github/workflows/build-windows.yml` followed by NSIS from the repo root.

## Detection limitations

- Registry values are **reported metadata**, not authoritative installed size or date.
- Some portable apps, hidden system components, Windows updates, and applications with corrupted/missing registrations won't be detected.
- Store package display names can be technical identifiers; some applications may appear in both Registry and package inventories.
- If Windows PowerShell package querying is unavailable or times out, desktop-app inventory still loads with a warning.
- Only packages for the current user are queried, without elevation.
- File system icon paths are interpreted as icon sources only; no uninstall commands are executed.

## Safety / architecture

`Services/AppInventoryService.cs`: read-only machine/user inventory.

`Models/InstalledApp.cs`: record metadata and display helpers.

`Helpers/IconReader.cs`: read-only icon extraction.

`MainWindow.xaml` / `.cs`: view and filtering/navigation glue for this foundation version.

**Planned future modules:** official uninstaller execution, confidence-scored leftover scan, explicit approval, backup/recovery, forced removal, batch, Hunter Mode, standalone previous-app leftovers, secure update checks.

Before adding a destructive feature, design dry-run mode and protected-path / shared-component checks, and test in a VM.

## Installer packaging repair (October 25, 2026)

GitHub Actions run #2 compiled/published the Windows WPF app, but NSIS installer packaging failed. This source includes corrected root-relative installer paths and an NSIS-compatible BMP-backed icon, plus better build diagnostics. See `BUILD-REPAIR-INSTRUCTIONS.md` for applying the update and limitations of local verification.
