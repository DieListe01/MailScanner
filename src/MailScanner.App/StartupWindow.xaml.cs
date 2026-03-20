using System.Windows;

namespace MailScanner.App;

public partial class StartupWindow : Window
{
    public string VersionLabel { get; }

    public StartupWindow(string versionLabel)
    {
        VersionLabel = versionLabel;
        DataContext = this;
        InitializeComponent();
    }
}
