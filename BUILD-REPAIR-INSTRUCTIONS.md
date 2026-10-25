# NODE Uninstaller v0.1.0 — Installer Build Repair (October 25, 2026)

## Confirmed from GitHub Actions run #2

The earlier **CS0176 WPF XAML** compilation error was fixed. GitHub run #2 advanced to the **Build Windows installer** step, which failed. The public GitHub Actions view does not expose the full NSIS compiler stderr to an unauthenticated viewer. The likely error was identified by inspecting the actual source:

1. NSIS silently changes its working directory to `installer/`, while the prior `.nsi` file incorrectly referenced `src/` and `dist/` as though the current directory were the repository root.
2. The original icon was a valid Windows ICO but **every icon image** was PNG-compressed, which can be incompatible with NSIS icon-resource processing. The same logo was repackaged as a separate conventional **BMP/DIB-backed ICO** for the installer. The application's original icon was not changed.
3. CI now ensures the published application EXE and NSIS icon are present before invoking the installer compiler, writes a readable compiler log on failure, and tests the installer output before uploading it.

## Packages
- FULL SOURCE ZIP: the entire v0.1.0 project incorporating the earlier `IgnoreTitleDrag` build fix and the latest packaging repair.
- UPDATE-ONLY ZIP: only files added or changed relative to the **v0.1.0 CS0176 HOTFIX FULL SOURCE**. Preserve folder paths while applying it.

## Files that must be updated in the GitHub repository

1. `.github/workflows/build-windows.yml` — replace the existing workflow.
2. `installer/NODE-Uninstaller.nsi` — replace the existing installer script.
3. `src/NODE.Uninstaller/Assets/NODE-Uninstaller-NSIS.ico` — **new binary asset; must be uploaded**.

`BUILD-REPAIR-INSTRUCTIONS.md` is optional build documentation and does not affect compilation.

## Deploy the patch

**Option A (Git installed on PC):** Copy the UPDATE-ONLY ZIP's extracted contents into your local Git checkout's root, preserving folders. Then commit and push to `main`.

**Option B (GitHub website):** Replace the contents of the two text files through GitHub's file editor, upload the new `.ico` to `src/NODE.Uninstaller/Assets/`, and commit to `main`.

Every new push to `main` that touches `src/`, `installer/`, or the workflow starts the build. **Do not rerun the old failed commit**, because it still has the old installer script.

## Download the actual Windows app

A successful build produces a GitHub Actions artifact named `NODE-Uninstaller-v0.1.0-Windows-Build`, containing `NODE-Uninstaller-Setup-v0.1.0.exe`, `NODE-Uninstaller-v0.1.0-WINDOWS-PORTABLE.zip`, and `SHA256SUMS.txt`.

If the build still fails, check the failing step or download `NODE-Uninstaller-v0.1.0-Build-Diagnostics` (NSIS-build.log) from that run's artifact list and share the exact error text.

## Honest verification

The source was examined, XML/YAML was parsed, WPF XAML event handlers/resources were checked, both icon containers were validated, and the delivered ZIPs passed integrity tests. GitHub run #2 demonstrated that the .NET build and publish phases succeeded. **This new patched NSIS installer has NOT been compiled in this environment, which lacks Windows/.NET SDK/NSIS.** It must be verified by the next Windows GitHub Actions run before calling it a successful installer build.

No uninstallation or deletion features are enabled in v0.1.0 (read-only application inventory).
