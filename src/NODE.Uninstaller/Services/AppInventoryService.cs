using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using NODE.Uninstaller.Models;

namespace NODE.Uninstaller.Services;

/// <summary>
/// Read-only application inventory. This component does not invoke uninstall commands,
/// write Registry data, remove packages, or delete files.
/// </summary>
public sealed class AppInventoryService
{
    private const string UninstallRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public async Task<InventoryResult> ScanAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var desktop = await Task.Run(() => ScanDesktopApps(warnings, cancellationToken), cancellationToken);
        var combined = new List<InstalledApp>(desktop);

        try
        {
            combined.AddRange(await ScanStoreAppsAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnings.Add($"Microsoft Store package inventory was unavailable: {ex.Message}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new InventoryResult(
            combined.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList(),
            warnings);
    }

    private static List<InstalledApp> ScanDesktopApps(List<string> warnings, CancellationToken cancellationToken)
    {
        var entries = new List<InstalledApp>();
        var locations = new (RegistryHive Hive, RegistryView View)[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Registry64),
            (RegistryHive.CurrentUser, RegistryView.Registry32)
        };

        foreach (var (hive, view) in locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstallKey = root.OpenSubKey(UninstallRegistryPath, writable: false);
                if (uninstallKey is null) continue;

                foreach (var subkeyName in uninstallKey.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        using var subkey = uninstallKey.OpenSubKey(subkeyName, writable: false);
                        if (subkey is null) continue;

                        var displayName = ReadString(subkey, "DisplayName");
                        if (string.IsNullOrWhiteSpace(displayName)) continue;
                        if (ReadInteger(subkey, "SystemComponent") == 1) continue;
                        if (!string.IsNullOrWhiteSpace(ReadString(subkey, "ParentKeyName"))) continue;
                        if (!string.IsNullOrWhiteSpace(ReadString(subkey, "ParentDisplayName"))) continue;

                        var releaseType = ReadString(subkey, "ReleaseType");
                        if (releaseType.Equals("Update", StringComparison.OrdinalIgnoreCase) ||
                            releaseType.Equals("Hotfix", StringComparison.OrdinalIgnoreCase) ||
                            releaseType.Equals("Security Update", StringComparison.OrdinalIgnoreCase)) continue;

                        string registryPath = $"{(hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU")} " +
                            $"({(view == RegistryView.Registry64 ? "64-bit" : "32-bit")})\\{UninstallRegistryPath}\\{subkeyName}";

                        long? sizeInBytes = null;
                        var estimatedKilobytes = ReadInteger(subkey, "EstimatedSize");
                        if (estimatedKilobytes is > 0)
                            sizeInBytes = (long)estimatedKilobytes.Value * 1024;

                        entries.Add(new InstalledApp
                        {
                            Id = registryPath,
                            DisplayName = displayName,
                            Publisher = ReadString(subkey, "Publisher"),
                            Version = ReadString(subkey, "DisplayVersion"),
                            Source = AppSource.Desktop,
                            DisplayIconPath = ReadString(subkey, "DisplayIcon"),
                            InstallLocation = ReadString(subkey, "InstallLocation"),
                            RegistryPath = registryPath,
                            UninstallCommand = ReadString(subkey, "UninstallString"),
                            IsWindowsInstaller = ReadInteger(subkey, "WindowsInstaller") == 1,
                            InstalledAt = TryParseInstallDate(ReadString(subkey, "InstallDate")),
                            ReportedSizeBytes = sizeInBytes
                        });
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
                    {
                        // One damaged/inaccessible entry must not prevent listing other programs.
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                warnings.Add($"Could not read {hive} ({view}) uninstall entries: {ex.Message}");
            }
        }
        return entries;
    }

    private static string ReadString(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value is string s ? s.Trim() : string.Empty;
    }

    private static int? ReadInteger(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName, null);
        return value switch
        {
            int i => i,
            long l when l <= int.MaxValue && l >= int.MinValue => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            _ => null
        };
    }

    private static DateTime? TryParseInstallDate(string value)
    {
        return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    private static async Task<IReadOnlyList<InstalledApp>> ScanStoreAppsAsync(CancellationToken cancellationToken)
    {
        // This command queries the CURRENT USER and is strictly read-only.
        // Don't elevate the process: an elevated PowerShell might enumerate a different profile.
        const string script = """
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $packages = @(Get-AppxPackage -PackageTypeFilter Main | Where-Object { -not $_.IsFramework } | ForEach-Object {
              [PSCustomObject]@{
                Name = [string]$_.Name
                Version = [string]$_.Version
                Publisher = [string]$_.Publisher
                InstallLocation = [string]$_.InstallLocation
                PackageFullName = [string]$_.PackageFullName
              }
            })
            ConvertTo-Json -InputObject $packages -Depth 3 -Compress
            """;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -NonInteractive -EncodedCommand {encoded}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        if (!process.Start()) throw new InvalidOperationException("Could not start Windows PowerShell.");

        string stdout;
        string stderr;
        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeout.Token));
            stdout = await outputTask;
            stderr = await errorTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("The package inventory exceeded the 25-second time limit.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)
                ? $"PowerShell exited with code {process.ExitCode}."
                : stderr.Trim().Split('\n').Last().Trim());

        if (string.IsNullOrWhiteSpace(stdout)) return [];
        using var document = JsonDocument.Parse(stdout.TrimStart('\uFEFF').Trim());
        var results = new List<InstalledApp>();
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in document.RootElement.EnumerateArray()) AddStoreEntry(child, results);
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            AddStoreEntry(document.RootElement, results);
        }
        return results;
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static void AddStoreEntry(JsonElement element, List<InstalledApp> destination)
    {
        string name = ReadJsonString(element, "Name");
        string fullName = ReadJsonString(element, "PackageFullName");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(fullName)) return;

        destination.Add(new InstalledApp
        {
            Id = "appx:" + fullName,
            DisplayName = name,
            Publisher = ReadJsonString(element, "Publisher"),
            Version = ReadJsonString(element, "Version"),
            Source = AppSource.MicrosoftStore,
            PackageFullName = fullName,
            InstallLocation = ReadJsonString(element, "InstallLocation")
        });
    }

    private static string ReadJsonString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;
}
