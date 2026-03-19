using System.Windows;
using MailScanner.App.Services;

namespace MailScanner.App;

public partial class MainWindow
{
    private WorkspacePage currentPage = WorkspacePage.Scanner;
    private Visibility previewTabVisibility = Visibility.Collapsed;

    public DebugLogService DebugLog { get; } = DebugLogService.Instance;

    public Visibility ScannerPageVisibility => currentPage == WorkspacePage.Scanner ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ResultsPageVisibility => currentPage == WorkspacePage.Results ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AccountsPageVisibility => currentPage == WorkspacePage.Accounts ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UpdatePageVisibility => currentPage == WorkspacePage.Update ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DebugPageVisibility => currentPage == WorkspacePage.Debug ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreviewTabVisibility
    {
        get => previewTabVisibility;
        set
        {
            previewTabVisibility = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultsListVisibility));
            OnPropertyChanged(nameof(ResultsPreviewVisibility));
        }
    }

    public Visibility ResultsListVisibility => PreviewTabVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ResultsPreviewVisibility => PreviewTabVisibility;

    private void OnScannerNavClicked(object sender, RoutedEventArgs e)
    {
        SetCurrentPage(WorkspacePage.Scanner);
    }

    private void OnOpenDebugClicked(object sender, RoutedEventArgs e)
    {
        SetCurrentPage(WorkspacePage.Debug);
    }

    private void OnResultsNavClicked(object sender, RoutedEventArgs e)
    {
        PreviewTabVisibility = Visibility.Collapsed;
        SetCurrentPage(WorkspacePage.Results);
    }

    private void SetCurrentPage(WorkspacePage page)
    {
        currentPage = page;
        OnPropertyChanged(nameof(ScannerPageVisibility));
        OnPropertyChanged(nameof(ResultsPageVisibility));
        OnPropertyChanged(nameof(AccountsPageVisibility));
        OnPropertyChanged(nameof(UpdatePageVisibility));
        OnPropertyChanged(nameof(DebugPageVisibility));
        UpdateNavigationVisualState();
    }

    private void UpdateNavigationVisualState()
    {
        SetNavigationButtonStyle(RefreshButton, currentPage == WorkspacePage.Scanner);
        SetNavigationButtonStyle(ResultsButton, currentPage == WorkspacePage.Results);
        SetNavigationButtonStyle(AccountButton, currentPage == WorkspacePage.Accounts);
        SetNavigationButtonStyle(UpdateButton, currentPage == WorkspacePage.Update);
        SetNavigationButtonStyle(DebugButton, currentPage == WorkspacePage.Debug);
    }

    private void SetNavigationButtonStyle(System.Windows.Controls.Button button, bool isActive)
    {
        button.Style = (Style)FindResource(isActive ? "NavigationButtonActive" : "NavigationButton");
    }

    private enum WorkspacePage
    {
        Scanner,
        Results,
        Accounts,
        Update,
        Debug
    }
}
