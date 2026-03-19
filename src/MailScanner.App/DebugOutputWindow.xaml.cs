using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TextBox = System.Windows.Controls.TextBox;

namespace MailScanner
{
    public partial class DebugOutputWindow : Window
    {
        private static DebugOutputWindow? _instance;
        
        public static DebugOutputWindow Instance
        {
            get
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new DebugOutputWindow();
                }
                return _instance;
            }
        }
        
        private DebugOutputWindow()
        {
            InitializeComponent();
            UpdateTimestamp();
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (e.ClickCount == 2 && ResizeMode == ResizeMode.CanResize)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            DragMove();
        }

        private void OnMinimizeWindowClicked(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnToggleMaximizeWindowClicked(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnCloseWindowClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        public void LogGeneral(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}\n";
            GeneralOutput.AppendText(logEntry);
            GeneralOutput.ScrollToEnd();
            UpdateStatus("Allgemein", message);
        }
        
        public void LogRegistry(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}\n";
            RegistryOutput.AppendText(logEntry);
            RegistryOutput.ScrollToEnd();
            UpdateStatus("Registry", message);
        }
        
        public void LogSettings(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}\n";
            SettingsOutput.AppendText(logEntry);
            SettingsOutput.ScrollToEnd();
            UpdateStatus("Settings", message);
        }
        
        public void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] ERROR: {message}\n";
            ErrorOutput.AppendText(logEntry);
            ErrorOutput.ScrollToEnd();
            UpdateStatus("Fehler", message);
            
            // Auto-switch to error tab
            DebugTabs.SelectedItem = DebugTabs.Items[3];
        }
        
        private void UpdateStatus(string category, string message)
        {
            StatusText.Text = $"[{category}] {message}";
            UpdateTimestamp();
        }
        
        private void UpdateTimestamp()
        {
            TimestampText.Text = $"Letzte Aktualisierung: {DateTime.Now:HH:mm:ss}";
        }
        
        private void OnCopyClicked(object sender, RoutedEventArgs e)
        {
            var currentTab = DebugTabs.SelectedIndex;
            TextBox? activeTextBox = null;
            
            switch (currentTab)
            {
                case 0: activeTextBox = GeneralOutput; break;
                case 1: activeTextBox = RegistryOutput; break;
                case 2: activeTextBox = SettingsOutput; break;
                case 3: activeTextBox = ErrorOutput; break;
            }
            
            if (activeTextBox != null)
            {
                System.Windows.Clipboard.SetText(activeTextBox.Text);
                UpdateStatus("Kopiert", "Aktiver Debug-Tab wurde in die Zwischenablage kopiert.");
            }
        }
        
        private void OnClearClicked(object sender, RoutedEventArgs e)
        {
            var currentTab = DebugTabs.SelectedIndex;
            TextBox? activeTextBox = null;
            
            switch (currentTab)
            {
                case 0: activeTextBox = GeneralOutput; break;
                case 1: activeTextBox = RegistryOutput; break;
                case 2: activeTextBox = SettingsOutput; break;
                case 3: activeTextBox = ErrorOutput; break;
            }
            
            if (activeTextBox != null)
            {
                activeTextBox.Clear();
                UpdateStatus("Gelöscht", $"Tab {currentTab + 1} wurde geleert");
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            _instance = null;
            base.OnClosed(e);
        }
    }
}
