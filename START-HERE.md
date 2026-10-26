# NODE Uninstaller v0.1.1 — Portable Windows

## Updating an existing GitHub repository

Use `NODE-Uninstaller-v0.1.1-STANDARD-UNINSTALL-UPDATE-ONLY.zip`. Extract and merge its contents into the root of `nodedevfivem/node-uninstaller`, preserving directory paths and overwriting updated files. Commit to `main`.

For a clean upload, use the `FULL-SOURCE.zip` instead. Its contents must be placed at repository root, **not** nested inside an extra folder. Keep the hidden `.github` directory.

## Building

Open GitHub **Actions** → **Build NODE Uninstaller (Portable Windows)** → the newest run. When green, download the artifact **NODE-Uninstaller-v0.1.1-PORTABLE-WINDOWS** and extract it. Double-click **NODE-Uninstaller.exe**. You do not need the NSIS installer. The Actions artifact also includes a portable ZIP and `SHA256SUMS.txt`.

If the build is red, open the failed step, copy the exact error text and provide it for repair. A green build only proves the files compiled and packaged; test the app on Windows before relying on it.

## Windows test sequence

1. Close the old v0.1.0 program and start the new EXE directly, not an old Start Menu shortcut.
2. Confirm the title badge reads **0.1.1**, the application list is dark, inventory populates, search works and maximize respects the taskbar.
3. Select a harmless, disposable desktop test application whose installer/uninstaller you trust. Do not start testing by removing drivers, antivirus, Windows components or Visual C++/.NET runtimes.
4. Select **Uninstall selected application**. Review the original uninstall command and warning, then cancel once to verify that nothing runs.
5. Repeat and approve only when you actually intend to uninstall that test app. Complete the original vendor wizard. When it exits, check NODE's report and refreshed inventory.
6. Select a Microsoft Store app and verify the button remains disabled, with a reason displayed. This release intentionally does not remove Store packages.

**No leftover cleanup / forced uninstallation / batch actions are enabled.** Microsoft Store uninstall remains in Windows Settings.

If Windows shows a SmartScreen prompt for an unsigned build, don't override it unless you trust the exact source and the CI build that produced it. We have not signed this portable EXE.
