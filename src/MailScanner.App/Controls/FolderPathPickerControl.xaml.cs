using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace MailScanner.App.Controls
{
    public partial class FolderPathPickerControl : System.Windows.Controls.UserControl
    {
        public FolderPathPickerControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register(nameof(Path), typeof(string), typeof(FolderPathPickerControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnPathChanged));

        public string Path
        {
            get => (string)GetValue(PathProperty);
            set => SetValue(PathProperty, value);
        }

        private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FolderPathPickerControl control && e.NewValue is string newPath)
            {
                control.PathTextBox.Text = newPath ?? string.Empty;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select folder",
                ShowNewFolderButton = true
            };
            if (!string.IsNullOrWhiteSpace(Path))
            {
                dialog.SelectedPath = Path;
            }
            if (dialog.ShowDialog(GetOwnerWindow()) == WinForms.DialogResult.OK)
            {
                Path = dialog.SelectedPath;
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Path))
            {
                System.Windows.Clipboard.SetText(Path);
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Path)) return;
            try
            {
                if (Directory.Exists(Path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{Path}\"");
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Folder does not exist.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Could not open folder: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private System.Windows.Forms.IWin32Window GetOwnerWindow()
        {
            var window = Window.GetWindow(this);
            return new WindowWrapper(new System.Windows.Interop.WindowInteropHelper(window).Handle);
        }

        private class WindowWrapper : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _handle;
            public WindowWrapper(IntPtr handle) => _handle = handle;
            public IntPtr Handle => _handle;
        }
    }
}