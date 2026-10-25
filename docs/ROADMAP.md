# NODE Uninstaller — Approved Option B Roadmap

**Status:** v0.1.0 foundation source candidate, awaiting first GitHub Actions compilation and Windows UI testing.

| Release | Scope | Status |
|---|---|---|
| 0.1.0 | Branded UI, installed application inventory, search/filter, details | Source prepared / Windows testing pending |
| 0.1.1 | Launch registered official uninstallers with safe command parsing, exit tracking | Planned |
| 0.1.2 | Read-only leftovers scanning and risk classification; opt-in cleanup gated behind backups | Planned |
| 0.1.3 | Transaction history, registry exports, recoverable deletion, restore checks | Planned |
| 0.1.4 | Forced uninstall with conservative safeguards and compatibility checks | Planned |
| 0.1.5 | Sequential batch removal with pause/stop between apps | Planned |
| 0.1.6 | Hunter Mode process/window-to-installation association | Planned |
| 0.1.7 | Standalone scanning for previously removed applications | Planned |
| 0.1.8 | Compatibility, telemetry-free performance, accessibility improvements | Planned |
| 1.0.0 | Stable tested release | Planned |

## Non-negotiable safety rules

1. Scan-only operations never mutate application files or the Registry.
2. Never treat a folder-name string match as sufficient proof of application ownership.
3. Shared folders, Microsoft/Windows system directories, drivers, services and protected app components are out of automatic-cleanup scope.
4. Normal uninstallers are preferred; forced cleanup is last resort.
5. Users review detailed findings and explicitly authorize each destructive stage.
6. Store package removal uses appropriate supported Windows interfaces, not manual deletion from WindowsApps.
7. Backup/recoverability claims are limited to what is actually captured; a Registry backup isn't a full reinstall.
8. Version releases are signed/verified as a separate future milestone; do not claim signed updates until implemented.
