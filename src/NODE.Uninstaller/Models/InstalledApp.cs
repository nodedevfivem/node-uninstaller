using System.Windows.Media;
using NODE.Uninstaller.Helpers;

namespace NODE.Uninstaller.Models;

public enum AppSource
{
    Desktop,
    MicrosoftStore
}

public sealed class InstalledApp
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Publisher { get; init; }
    public required string Version { get; init; }
    public required AppSource Source { get; init; }
    public string? InstallLocation { get; init; }
    public string? DisplayIconPath { get; init; }
    public string? RegistryPath { get; init; }
    public string? UninstallCommand { get; init; }
    public bool IsWindowsInstaller { get; init; }
    public bool IsMachineWide => RegistryPath?.StartsWith("HKLM ", StringComparison.OrdinalIgnoreCase) == true;
    public string? PackageFullName { get; init; }
    public DateTime? InstalledAt { get; init; }
    public long? ReportedSizeBytes { get; init; }

    public string Initial => DisplayName.Length > 0 ? DisplayName[..1].ToUpperInvariant() : "?";
    public string SourceLabel => Source == AppSource.Desktop ? "Desktop" : "Store";
    public string VersionLabel => string.IsNullOrWhiteSpace(Version) ? "Not reported" : Version;
    public string PublisherLabel => string.IsNullOrWhiteSpace(Publisher) ? "Unknown publisher" : Publisher;
    public string InstalledAtLabel => InstalledAt?.ToString("MMM d, yyyy") ?? "Not reported";
    public string SizeLabel => ReportedSizeBytes switch
    {
        null => "Not reported",
        < 1024L * 1024L => $"{ReportedSizeBytes.Value / 1024.0:0.#} KB",
        < 1024L * 1024L * 1024L => $"{ReportedSizeBytes.Value / (1024.0 * 1024):0.#} MB",
        _ => $"{ReportedSizeBytes.Value / (1024.0 * 1024 * 1024):0.##} GB"
    };
    public string LocationLabel => string.IsNullOrWhiteSpace(InstallLocation) ? "Not reported" : InstallLocation;
    public string IdentifierLabel => Source == AppSource.MicrosoftStore
        ? PackageFullName ?? "Not reported"
        : RegistryPath ?? "Not reported";

    private bool _iconLoaded;
    private ImageSource? _iconSource;

    // Lazily resolve icons on the UI thread while virtualized rows are bound.
    public ImageSource? IconSource
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconLoaded = true;
                _iconSource = IconReader.TryLoad(DisplayIconPath);
            }
            return _iconSource;
        }
    }
}
