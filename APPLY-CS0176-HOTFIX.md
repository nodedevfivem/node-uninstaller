# NODE Uninstaller v0.1.0 — CS0176 WPF BUILD HOTFIX

This fixes the failed GitHub Actions Windows build reported at `src/NODE.Uninstaller/MainWindow.xaml` line 180:

> Member `MainWindow.IgnoreTitleDrag(object, MouseButtonEventArgs)` cannot be accessed with an instance reference; qualify it with a type name instead.

## Root cause and repair

`MouseLeftButtonDown="IgnoreTitleDrag"` is wired through WPF-generated XAML code to the `MainWindow` instance. The original C# method was declared `static`, while WPF generated instance event hookup. The handler must be an instance method.

One source line changed in `src/NODE.Uninstaller/MainWindow.xaml.cs`:

```diff
-    private static void IgnoreTitleDrag(object sender, MouseButtonEventArgs e) => e.Handled = true;
+    private void IgnoreTitleDrag(object sender, MouseButtonEventArgs e) => e.Handled = true;
```

No functionality, logo, UI design, GitHub Actions workflow, installer, or app version was changed.

## Apply to your existing GitHub repository

**Recommended: Use the UPDATE-ONLY ZIP**. Extract it on your computer, then open `src/NODE.Uninstaller/MainWindow.xaml.cs` and copy its entire contents. On GitHub, open the same repository path, click Edit (pencil), replace the content and Commit directly to `main`. Preserve the exact file path. This is the only app source file that needs replacing.

Alternatively, extract and upload the FULL SOURCE ZIP to a clean repository; do not upload a ZIP as a single source file.

GitHub Actions automatically rebuilds on a push to `main` that changes `src/**`. Or use Actions > Build NODE Uninstaller (Windows) > Run workflow. **Do not re-run the old failed run without a new commit**: re-running an old run usually uses its original revision.

After a green result: open the latest run > Artifacts > NODE-Uninstaller-v0.1.0-Windows-Build. The expected produced files are:
- `NODE-Uninstaller-Setup-v0.1.0.exe`
- `NODE-Uninstaller-v0.1.0-WINDOWS-PORTABLE.zip`
- `SHA256SUMS.txt`

## Validation status

Source patched and statically checked, including XML/XAML parsing, event-handler signature checks, and ZIP integrity tests. **A full Windows WPF compile and runtime test were not possible here**, because the current environment lacks the .NET SDK and Windows WPF build toolchain. The next GitHub Actions run is the authoritative build validation.

Safety: v0.1.0 remains **read-only**: uninstall and leftover deletion features are not implemented yet.
