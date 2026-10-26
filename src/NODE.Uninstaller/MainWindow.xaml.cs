using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using NODE.Uninstaller.Models;
using NODE.Uninstaller.Services;

namespace NODE.Uninstaller;

public partial class MainWindow : Window
{
    private List<InstalledApp> _applications = [];
    private readonly AppInventoryService _inventory = new();
    private readonly UninstallService _uninstaller = new();
    private InstalledApp? _pendingUninstallApp;
    private UninstallService.UninstallPlan? _pendingUninstallPlan;
    private bool _uninstallRunning;
    private bool _lastInventorySuccess;
    private CancellationTokenSource? _uninstallMonitorCts;
    private ICollectionView _view;
    private CancellationTokenSource? _scanCts;
    private AppSource? _sourceFilter;
    private bool _isLoaded;

    public MainWindow()
    {
        InitializeComponent();
        _view = CreateInventoryView(_applications);
        AppList.ItemsSource = _view;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded) return;
        _isLoaded = true;
        KeepWindowInsideWorkArea();
        await RefreshInventoryAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_uninstallRunning)
        {
            e.Cancel = true;
            ShowNotice("An uninstaller is running", "Wait until the manufacturer's uninstaller closes before exiting NODE Uninstaller. Closing NODE Uninstaller would not cancel the external removal process.");
            return;
        }
        _scanCts?.Cancel();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshInventoryAsync();

    private async Task RefreshInventoryAsync()
    {
        if (_scanCts is not null || _uninstallRunning) return;
        using var cancellation = new CancellationTokenSource();
        _scanCts = cancellation;
        RefreshButton.IsEnabled = false;
        _lastInventorySuccess = false;
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 195, 102));
        StatusText.Text = "Scanning desktop and Microsoft Store application records...";

        try
        {
            var result = await _inventory.ScanAsync(cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            var selectedId = (AppList.SelectedItem as InstalledApp)?.Id;

            // IMPORTANT: Do not mutate an ObservableCollection under DeferRefresh().
            // WPF selection/currency tracking can throw while refresh is deferred.
            // Build a complete replacement view, then swap ItemsSource only once.
            var replacement = result.Applications.ToList();
            _lastInventorySuccess = true;
            var newView = CreateInventoryView(replacement);
            _applications = replacement;
            _view = newView;
            AppList.ItemsSource = newView;

            TotalCountText.Text = _applications.Count.ToString("N0");
            DesktopCountText.Text = _applications.Count(a => a.Source == AppSource.Desktop).ToString("N0");
            StoreCountText.Text = _applications.Count(a => a.Source == AppSource.MicrosoftStore).ToString("N0");
            AppList.SelectedItem = _applications.FirstOrDefault(a => a.Id == selectedId);
            RefreshFilter();
            if (result.Warnings.Count > 0)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 181, 96));
                StatusText.Text = $"Scan completed with {result.Warnings.Count} warning(s) · {string.Join(" | ", result.Warnings.Take(1))}";
                StatusText.ToolTip = string.Join(Environment.NewLine, result.Warnings);
            }
            else
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(94, 226, 176));
                StatusText.Text = $"Scan complete · {_applications.Count:N0} application records";
                StatusText.ToolTip = "Inventory scanning is read-only. Uninstall requires an explicit confirmation.";
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusText.Text = "Application inventory cancelled.";
        }
        catch (Exception ex)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 106, 127));
            StatusText.Text = "Inventory could not be completed.";
            StatusText.ToolTip = ex.ToString();
            ShowNotice("Inventory scan could not finish", ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_scanCts, cancellation)) _scanCts = null;
            RefreshButton.IsEnabled = !_uninstallRunning;
        }
    }

    private ICollectionView CreateInventoryView(List<InstalledApp> apps)
    {
        // A ListCollectionView over a stable snapshot is safe for virtualized WPF lists.
        // No refresh deferral and no N item-by-item CollectionChanged notifications.
        var view = new ListCollectionView(apps);
        view.Filter = AcceptApplication;
        view.SortDescriptions.Add(new SortDescription(nameof(InstalledApp.DisplayName), ListSortDirection.Ascending));
        return view;
    }

    private bool AcceptApplication(object item)
    {
        if (item is not InstalledApp app) return false;
        if (_sourceFilter is AppSource source && app.Source != source) return false;
        var search = SearchBox.Text.Trim();
        return string.IsNullOrEmpty(search) ||
               app.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               app.Publisher.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               app.Version.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchHint is not null) SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (_view is not null) RefreshFilter();
    }

    private void RefreshFilter()
    {
        _view.Refresh();
        ShowingCount.Text = $"{_view.Cast<object>().Count():N0} shown";
        EmptyState.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        if (_view.IsEmpty) EmptyMessage.Text = _applications.Count == 0 ? "No applications found yet" : "No matching applications found";
    }

    private void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionControls();

    private void UpdateSelectionControls()
    {
        var app = AppList.SelectedItem as InstalledApp;
        NoSelection.Visibility = app is null ? Visibility.Visible : Visibility.Collapsed;
        SelectedDetails.Visibility = app is null ? Visibility.Collapsed : Visibility.Visible;
        SelectedDetails.DataContext = app;
        OpenFolderButton.IsEnabled = app is not null && !string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation);
        CopyDetailsButton.IsEnabled = app is not null;
        string availabilityReason = "Select an application.";
        bool available = app is not null && _uninstaller.TryCreatePlan(app, out _, out availabilityReason);
        UninstallButton.IsEnabled = available && !_uninstallRunning;
        UninstallButton.ToolTip = app is null ? "Select an application." :
            available ? "Run this application's registered interactive uninstaller." : availabilityReason;
        UninstallAvailabilityText.Text = app is null ? "Choose an application to begin." :
            available ? "Runs the original uninstaller · no leftover cleanup in v0.1.1" : availabilityReason;
    }

    private void NavAll_Click(object sender, RoutedEventArgs e) => NavigateToInventory(null, "Installed applications", NavAll);
    private void NavDesktop_Click(object sender, RoutedEventArgs e) => NavigateToInventory(AppSource.Desktop, "Desktop applications", NavDesktop);
    private void NavStore_Click(object sender, RoutedEventArgs e) => NavigateToInventory(AppSource.MicrosoftStore, "Microsoft Store applications", NavStore);

    private void NavigateToInventory(AppSource? source, string heading, Button active)
    {
        SetActiveNavigation(active);
        InventoryPanel.Visibility = Visibility.Visible;
        UpcomingModulePanel.Visibility = Visibility.Collapsed;
        PageHeading.Text = heading;
        PageDescription.Text = source switch
        {
            AppSource.Desktop => "Registered Windows desktop applications from your current Registry views.",
            AppSource.MicrosoftStore => "Current-user Microsoft Store packages (main packages only).",
            _ => "Run an application's official uninstaller, or browse your installed software."
        };
        _sourceFilter = source;
        RefreshFilter();
    }

    private void NavFeature_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var module = button.Name switch
        {
            nameof(NavForced) => "Forced Uninstall",
            nameof(NavBatch) => "Batch Uninstaller",
            nameof(NavScanner) => "Leftover Scanner",
            nameof(NavHunter) => "Hunter Mode",
            nameof(NavBackup) => "Backups & Recovery",
            nameof(NavHistory) => "Activity History",
            nameof(NavSettings) => "Settings",
            _ => "Unknown module"
        };
        SetActiveNavigation(button);
        InventoryPanel.Visibility = Visibility.Collapsed;
        UpcomingModulePanel.Visibility = Visibility.Visible;
        PageHeading.Text = module;
        PageDescription.Text = "Roadmap preview · this advanced module is not implemented in v0.1.1.";
        UpcomingModuleTitle.Text = module;
        UpcomingModuleBody.Text = module switch
        {
            "Forced Uninstall" => "Find a broken installation, verify its identity, and safely remove user-approved remnants when the normal uninstaller is unavailable. Planned for v0.1.4.",
            "Batch Uninstaller" => "Select several applications and run their official uninstallers sequentially, reviewing leftovers after each. Planned for v0.1.5.",
            "Leftover Scanner" => "Search known application locations and Registry records for likely remnants, with confidence scoring and manual review. Initial scanning is planned for v0.1.2; standalone scanning follows later.",
            "Hunter Mode" => "Drag a target onto an open program window to identify its process and corresponding installed application. Planned for v0.1.6.",
            "Backups & Recovery" => "Keep an auditable history and provide reversible cleanup where technically possible. Planned for v0.1.3.",
            "Activity History" => "Review completed uninstall operations, decisions, and recoverable cleanup actions. Planned for v0.1.3.",
            "Settings" => "Configure scan strength, backup preferences, appearance, and GitHub update checks in later builds.",
            _ => "This feature will be added in a future NODE Uninstaller update."
        };
    }

    private void SetActiveNavigation(Button active)
    {
        foreach (var button in new[] { NavAll, NavDesktop, NavStore, NavForced, NavBatch, NavScanner, NavHunter, NavBackup, NavHistory, NavSettings })
            button.Tag = button == active ? "Active" : null;
        // Future-module buttons use Tag to store their module name.
        // Replace lost Tags on each navigation transition to keep them operational.
        NavForced.Tag = NavForced == active ? "Active" : "Forced Uninstall";
        NavBatch.Tag = NavBatch == active ? "Active" : "Batch Uninstaller";
        NavScanner.Tag = NavScanner == active ? "Active" : "Leftover Scanner";
        NavHunter.Tag = NavHunter == active ? "Active" : "Hunter Mode";
        NavBackup.Tag = NavBackup == active ? "Active" : "Backups & Recovery";
        NavHistory.Tag = NavHistory == active ? "Active" : "Activity History";
        NavSettings.Tag = NavSettings == active ? "Active" : "Settings";
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_uninstallRunning || _scanCts is not null || AppList.SelectedItem is not InstalledApp app) return;
        if (!_uninstaller.TryCreatePlan(app, out var plan, out var reason) || plan is null)
        {
            ShowNotice("Uninstaller unavailable", reason);
            return;
        }
        _pendingUninstallApp = app;
        _pendingUninstallPlan = plan;
        UninstallConfirmApp.Text = app.DisplayName;
        UninstallConfirmPublisher.Text = $"Publisher: {app.PublisherLabel}  ·  Source: {app.SourceLabel}";
        UninstallConfirmCommand.Text = plan.Description;
        UninstallConfirmWarning.Text = app.IsMachineWide
            ? "This is registered as a machine-wide application. Windows may ask for administrator permission. Removing shared software or runtimes may affect other applications."
            : "The publisher's uninstaller may ask additional questions or request administrator permission. Other applications may depend on this software.";
        UninstallConfirmOverlay.Visibility = Visibility.Visible;
        ConfirmUninstallButton.Focus();
    }

    private void UninstallCancel_Click(object sender, RoutedEventArgs e)
    {
        UninstallConfirmOverlay.Visibility = Visibility.Collapsed;
        _pendingUninstallApp = null;
        _pendingUninstallPlan = null;
    }

    private async void UninstallConfirm_Click(object sender, RoutedEventArgs e)
    {
        var app = _pendingUninstallApp;
        var plan = _pendingUninstallPlan;
        UninstallCancel_Click(sender, e);
        if (app is null || plan is null || _uninstallRunning) return;

        _uninstallRunning = true;
        _uninstallMonitorCts = new CancellationTokenSource();
        RefreshButton.IsEnabled = false;
        UninstallButton.IsEnabled = false;
        AppList.IsEnabled = false;
        UninstallProgressTitle.Text = $"Running uninstaller: {app.DisplayName}";
        UninstallProgressOverlay.Visibility = Visibility.Visible;
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 195, 102));
        StatusText.Text = $"Waiting for {app.DisplayName}'s uninstaller...";
        try
        {
            var result = await _uninstaller.RunAsync(plan, _uninstallMonitorCts.Token);
            UninstallProgressOverlay.Visibility = Visibility.Collapsed;
            _uninstallRunning = false;
            AppList.IsEnabled = true;
            RefreshButton.IsEnabled = true;
            await RefreshInventoryAsync();
            bool entryStillPresent = _applications.Any(a => a.Id == app.Id);
            var status = !result.Started
                ? "No uninstall process was launched by NODE Uninstaller."
                : !_lastInventorySuccess
                    ? "The post-uninstall inventory refresh failed, so NODE cannot determine whether this application's registered entry remains."
                    : entryStillPresent
                        ? "Windows still lists the application's uninstall record. Its uninstaller may have been cancelled, may be finishing elsewhere, or may have failed."
                        : "The application's registered uninstall entry is no longer in the current inventory. This does NOT verify that all application files are removed.";
            StatusText.Text = result.Started && result.ExitCode is null ? "Monitoring stopped · uninstall may still be running" :
                !_lastInventorySuccess ? "Inventory verification unavailable" : entryStillPresent
                    ? $"Uninstall review · {app.DisplayName} still listed"
                    : $"Uninstall review · {app.DisplayName} entry absent";
            StatusDot.Fill = new SolidColorBrush(!_lastInventorySuccess || entryStillPresent
                ? Color.FromRgb(255, 195, 102) : Color.FromRgb(94, 226, 176));
            ShowNotice(result.Started && result.ExitCode is null ? "Uninstaller monitoring stopped" : "Uninstall operation finished", result.Message + "\n\n" + status +
                "\n\nNODE Uninstaller v0.1.1 does not scan or delete leftover files and Registry entries.");
        }
        catch (Exception ex)
        {
            ShowNotice("Uninstall operation error", ex.Message + "\n\nNo direct cleanup was performed by NODE Uninstaller.");
        }
        finally
        {
            UninstallProgressOverlay.Visibility = Visibility.Collapsed;
            _uninstallRunning = false;
            _uninstallMonitorCts?.Dispose();
            _uninstallMonitorCts = null;
            AppList.IsEnabled = true;
            RefreshButton.IsEnabled = _scanCts is null;
            UpdateSelectionControls();
        }
    }

    private void StopMonitoring_Click(object sender, RoutedEventArgs e)
    {
        // Cancels only WaitForExitAsync; the external uninstall process keeps running.
        _uninstallMonitorCts?.Cancel();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is not InstalledApp app || string.IsNullOrWhiteSpace(app.InstallLocation)) return;
        if (!Directory.Exists(app.InstallLocation))
        {
            ShowNotice("Folder unavailable", "The reported installation folder no longer exists or is inaccessible.");
            return;
        }
        try
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            start.ArgumentList.Add(app.InstallLocation);
            Process.Start(start);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            ShowNotice("Could not open folder", ex.Message);
        }
    }

    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is not InstalledApp app) return;
        var contents = new StringBuilder()
            .AppendLine($"Application: {app.DisplayName}")
            .AppendLine($"Publisher: {app.PublisherLabel}")
            .AppendLine($"Version: {app.VersionLabel}")
            .AppendLine($"Type: {app.SourceLabel}")
            .AppendLine($"Reported size: {app.SizeLabel}")
            .AppendLine($"Install date: {app.InstalledAtLabel}")
            .AppendLine($"Install location: {app.LocationLabel}")
            .AppendLine($"Record ID: {app.IdentifierLabel}")
            .ToString();
        try { Clipboard.SetText(contents); StatusText.Text = $"Copied details for {app.DisplayName}."; }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            ShowNotice("Clipboard unavailable", "Windows clipboard is currently unavailable. Try again.");
        }
    }

    // WPF's borderless windows can maximize over the taskbar unless the native
    // WM_GETMINMAXINFO work-area bounds are supplied for the current monitor.
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref NativeMonitorInfo monitorInfo);

    private bool TryGetWorkArea(out NativeMonitorInfo info)
    {
        info = new NativeMonitorInfo { Size = Marshal.SizeOf<NativeMonitorInfo>() };
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return false;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WindowMessageHook);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmGetMinMaxInfo || lParam == IntPtr.Zero || !TryGetWorkArea(out var monitor))
            return IntPtr.Zero;

        var bounds = Marshal.PtrToStructure<NativeMinMaxInfo>(lParam);
        bounds.MaxPosition.X = monitor.Work.Left - monitor.Monitor.Left;
        bounds.MaxPosition.Y = monitor.Work.Top - monitor.Monitor.Top;
        bounds.MaxSize.X = monitor.Work.Right - monitor.Work.Left;
        bounds.MaxSize.Y = monitor.Work.Bottom - monitor.Work.Top;
        bounds.MaxTrackSize = bounds.MaxSize;
        Marshal.StructureToPtr(bounds, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    private void KeepWindowInsideWorkArea()
    {
        if (WindowState != WindowState.Normal || !TryGetWorkArea(out var monitor)) return;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null) return;

        // Monitor bounds are physical pixels, while WPF Width/Height/Left/Top
        // are device-independent units. Convert for DPI/scaled displays.
        var fromDevice = source.CompositionTarget.TransformFromDevice;
        var start = fromDevice.Transform(new System.Windows.Point(monitor.Work.Left, monitor.Work.Top));
        var end = fromDevice.Transform(new System.Windows.Point(monitor.Work.Right, monitor.Work.Bottom));
        const double margin = 8;
        var areaWidth = Math.Max(480, end.X - start.X - 2 * margin);
        var areaHeight = Math.Max(440, end.Y - start.Y - 2 * margin);
        MinWidth = Math.Min(MinWidth, areaWidth);
        MinHeight = Math.Min(MinHeight, areaHeight);
        Width = Math.Min(Width, areaWidth);
        Height = Math.Min(Height, areaHeight);
        Left = Math.Clamp(Left, start.X + margin, Math.Max(start.X + margin, end.X - margin - Width));
        Top = Math.Clamp(Top, start.Y + margin, Math.Max(start.Y + margin, end.Y - margin - Height));
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (MaximizeButton is not null)
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        if (WindowState == WindowState.Normal && _isLoaded)
            KeepWindowInsideWorkArea();
    }

    private void ShowNotice(string title, string details)
    {
        NoticeTitle.Text = title;
        NoticeMessage.Text = details;
        NoticeOverlay.Visibility = Visibility.Visible;
        NoticeOkButton.Focus();
    }

    private void DismissNotice_Click(object sender, RoutedEventArgs e)
        => NoticeOverlay.Visibility = Visibility.Collapsed;

    private void DragTitleBar(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleMaximize(); return; }
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void IgnoreTitleDrag(object sender, MouseButtonEventArgs e) => e.Handled = true;
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
