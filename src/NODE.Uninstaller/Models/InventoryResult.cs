namespace NODE.Uninstaller.Models;

public sealed record InventoryResult(
    IReadOnlyList<InstalledApp> Applications,
    IReadOnlyList<string> Warnings);
