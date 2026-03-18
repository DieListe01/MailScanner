using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MailScanner.App.Controls
{
    public partial class FilePathPickerControl : System.Windows.Controls.UserControl
    {
        public FilePathPickerControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register(nameof(Path), typeof(string), typeof(FilePathPickerControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnPathChanged));

        public string Path
        {
            get => (string)GetValue(PathProperty);
            set => SetValue(PathProperty, value);
        }

        private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilePathPickerControl control && e.NewValue is string newPath)
            {
                control.PathTextBox.Text = newPath ?? string.Empty;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "SQLite database file (*.db)|*.db|All files (*.*)|*.*",
                Title = "Select SQLite database file",
                CheckFileExists = false,
                CheckPathExists = true
            };
            if (!string.IsNullOrWhiteSpace(Path))
            {
                dialog.FileName = System.IO.Path.GetFileName(Path);
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(Path);
            }
            if (dialog.ShowDialog(GetOwnerWindow()) == System.Windows.Forms.DialogResult.OK)
            {
                Path = dialog.FileName;
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
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (Directory.Exists(directory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{Path}\"");
                }
                else if (File.Exists(Path))
                {
                    // file exists but parent may not? just open containing folder
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{Path}\"");
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