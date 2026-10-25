using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using NODE.Uninstaller.Models;
using NODE.Uninstaller.Services;

namespace NODE.Uninstaller;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<InstalledApp> _applications = [];
    private readonly AppInventoryService _inventory = new();
    private readonly ICollectionView _view;
    private CancellationTokenSource? _scanCts;
    private AppSource? _sourceFilter;
    private bool _isLoaded;

    public MainWindow()
    {
        InitializeComponent();
        AppList.ItemsSource = _applications;
        _view = CollectionViewSource.GetDefaultView(_applications);
        _view.Filter = AcceptApplication;
        _view.SortDescriptions.Add(new SortDescription(nameof(InstalledApp.DisplayName), ListSortDirection.Ascending));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded) return;
        _isLoaded = true;
        await RefreshInventoryAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _scanCts?.Cancel();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshInventoryAsync();

    private async Task RefreshInventoryAsync()
    {
        if (_scanCts is not null) return;
        using var cancellation = new CancellationTokenSource();
        _scanCts = cancellation;
        RefreshButton.IsEnabled = false;
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 195, 102));
        StatusText.Text = "Scanning desktop and Microsoft Store application records...";

        try
        {
            var result = await _inventory.ScanAsync(cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            var selectedId = (AppList.SelectedItem as InstalledApp)?.Id;

            // Replace the list in a single view update to avoid repeated filter refreshes.
            using (_view.DeferRefresh())
            {
                _applications.Clear();
                foreach (var app in result.Applications) _applications.Add(app);
            }

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
                StatusText.Text = $"Scan complete · {_applications.Count:N0} application records · read-only";
                StatusText.ToolTip = "Application inventory was read without removing files or modifying Windows.";
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
            MessageBox.Show($"Unable to scan installed applications.\n\n{ex.Message}", "NODE Uninstaller",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (ReferenceEquals(_scanCts, cancellation)) _scanCts = null;
            RefreshButton.IsEnabled = true;
        }
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

    private void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var app = AppList.SelectedItem as InstalledApp;
        NoSelection.Visibility = app is null ? Visibility.Visible : Visibility.Collapsed;
        SelectedDetails.Visibility = app is null ? Visibility.Collapsed : Visibility.Visible;
        SelectedDetails.DataContext = app;
        OpenFolderButton.IsEnabled = app is not null && !string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation);
        CopyDetailsButton.IsEnabled = app is not null;
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
            _ => "Discover your software. Removal tools are introduced in upcoming updates."
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
        PageDescription.Text = "Roadmap preview · functionality is not implemented in v0.1.0.";
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

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is not InstalledApp app || string.IsNullOrWhiteSpace(app.InstallLocation)) return;
        if (!Directory.Exists(app.InstallLocation))
        {
            MessageBox.Show("The reported installation folder no longer exists or is inaccessible.", "NODE Uninstaller",
                MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show($"Could not open this installation folder.\n\n{ex.Message}", "NODE Uninstaller",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show("Windows clipboard is currently unavailable. Try again.", "NODE Uninstaller",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void DragTitleBar(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleMaximize(); return; }
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private static void IgnoreTitleDrag(object sender, MouseButtonEventArgs e) => e.Handled = true;
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }
}
