using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using NODE.Uninstaller.Models;

namespace NODE.Uninstaller.Services;

/// <summary>
/// Runs a desktop program's registered, interactive Windows uninstaller only.
/// Never deletes leftover files, Registry entries, drivers or package data itself.
/// The user's confirmation is collected by the WPF window before this is called.
/// </summary>
public sealed class UninstallService
{
    public sealed record UninstallPlan(string Executable, string Arguments, bool RequestElevation, string Description);
    public sealed record UninstallResult(bool Started, int? ExitCode, bool ElevationDeclined, string Message);

    private static readonly Regex QuotedExe = new(
        "^\\s*\"(?<exe>[^\"]+\\.exe)\"\\s*(?<args>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ExePrefix = new(
        @"^\s*(?<exe>.+?\.exe)(?<args>(?:\s+.*)?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MsiProduct = new(
        @"(?i)(?:^|\s)[/-][ix]\s*(?<guid>\{[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public bool TryCreatePlan(InstalledApp app, out UninstallPlan? plan, out string reason)
    {
        plan = null;
        reason = string.Empty;
        if (app.Source != AppSource.Desktop)
        {
            reason = "Microsoft Store package removal will be added after separate safety testing. Use Windows Settings for this package.";
            return false;
        }

        // MSI product codes are unambiguous. Prefer the documented MSI interface
        // instead of following an install/repair command that may contain /I.
        if (app.IsWindowsInstaller && TryExtractProductGuid(app.RegistryPath, out var productGuid))
        {
            plan = CreateMsiPlan(productGuid);
            return true;
        }

        var rawCommand = app.UninstallCommand?.Trim();
        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            reason = "This application has no registered uninstall command. Forced Uninstall is not available in this version.";
            return false;
        }
        var command = Environment.ExpandEnvironmentVariables(rawCommand);
        if (TryGetMsiProductFromCommand(command, out var msiGuid))
        {
            plan = CreateMsiPlan(msiGuid);
            return true;
        }
        if (!TrySplitExecutable(command, out var executable, out var arguments))
        {
            reason = "The uninstall command does not identify a supported executable file. No command was launched.";
            return false;
        }

        // Do not run command shells, script interpreters, DLL entry points or
        // other shell trampolines from an untrusted Registry entry.
        var name = Path.GetFileName(executable);
        if (new[] { "cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "mshta.exe", "rundll32.exe", "regsvr32.exe", "explorer.exe", "conhost.exe" }
            .Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            reason = "This uninstall record uses a shell or system interpreter and requires manual review in Windows Settings.";
            return false;
        }
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
        {
            reason = "The executable referenced by this uninstall record cannot be found.";
            return false;
        }
        if (!Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Only registered executable uninstallers are supported.";
            return false;
        }
        plan = new UninstallPlan(executable, arguments, app.IsMachineWide,
            $"Registered uninstaller: {executable}{(string.IsNullOrWhiteSpace(arguments) ? "" : " " + arguments)}");
        return true;
    }

    private static UninstallPlan CreateMsiPlan(Guid productGuid) => new(
        Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
        "/x " + productGuid.ToString("B"),
        false, // Windows Installer handles elevation at the appropriate point.
        "Windows Installer: msiexec.exe /x " + productGuid.ToString("B"));

    private static bool TryExtractProductGuid(string? registryPath, out Guid guid)
    {
        var leaf = registryPath?.Split('\\').LastOrDefault();
        return Guid.TryParse(leaf, out guid);
    }

    private static bool TryGetMsiProductFromCommand(string command, out Guid guid)
    {
        guid = default;
        if (!TrySplitExecutable(command, out var executable, out var arguments)) return false;
        if (!Path.GetFileName(executable).Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase)) return false;
        var match = MsiProduct.Match(arguments);
        return match.Success && Guid.TryParse(match.Groups["guid"].Value, out guid);
    }

    private static bool TrySplitExecutable(string command, out string executable, out string arguments)
    {
        executable = arguments = string.Empty;
        var quoted = QuotedExe.Match(command);
        if (quoted.Success)
        {
            executable = quoted.Groups["exe"].Value;
            arguments = quoted.Groups["args"].Value.Trim();
            return true;
        }
        var match = ExePrefix.Match(command);
        if (!match.Success) return false;
        executable = match.Groups["exe"].Value.Trim();
        arguments = match.Groups["args"].Value.Trim();
        // Some uninstall entries store an unquoted absolute path containing
        // spaces. Only accept the prefix if it really resolves to a file.
        if (!Path.GetFileName(executable).Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) &&
            !File.Exists(executable))
        {
            const string marker = ".exe";
            var pos = command.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            while (pos >= 0)
            {
                var candidate = command[..(pos + marker.Length)].Trim();
                if (File.Exists(candidate))
                {
                    executable = candidate;
                    arguments = command[(pos + marker.Length)..].Trim();
                    break;
                }
                pos = command.IndexOf(marker, pos + marker.Length, StringComparison.OrdinalIgnoreCase);
            }
        }
        return true;
    }

    public async Task<UninstallResult> RunAsync(UninstallPlan plan, CancellationToken monitorCancellation)
    {
        try
        {
            var startInfo = new ProcessStartInfo(plan.Executable)
            {
                Arguments = plan.Arguments,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(plan.Executable) ?? Environment.SystemDirectory
            };
            if (plan.RequestElevation) startInfo.Verb = "runas";
            using var process = Process.Start(startInfo);
            if (process is null)
                return new UninstallResult(false, null, false, "Windows did not start the registered uninstaller.");
            try
            {
                await process.WaitForExitAsync(monitorCancellation);
            }
            catch (OperationCanceledException) when (monitorCancellation.IsCancellationRequested)
            {
                // Cancelling monitoring MUST NOT terminate the external uninstaller.
                return new UninstallResult(true, null, false,
                    "Monitoring was stopped. The publisher's uninstaller may still be running; NODE did not stop or kill that process.");
            }
            return new UninstallResult(true, process.ExitCode, false,
                process.ExitCode == 0
                    ? "The registered uninstaller exited with code 0. We will refresh Windows' application records to see if its entry remains."
                    : $"The registered uninstaller exited with code {process.ExitCode}. It may have failed or been cancelled.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new UninstallResult(false, null, true, "The Windows administrator permission prompt was cancelled. No uninstaller was started by NODE Uninstaller.");
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException or UnauthorizedAccessException)
        {
            return new UninstallResult(false, null, false, $"Could not start or monitor the registered uninstaller: {ex.Message}");
        }
    }
}
