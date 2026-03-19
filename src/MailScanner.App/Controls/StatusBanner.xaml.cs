using System;
using System.Windows;
using System.Windows.Controls;

namespace MailScanner.App.Controls
{
    public enum BannerState
    {
        None,
        Error,
        Warning,
        Success,
        Info
    }

    public partial class StatusBanner : System.Windows.Controls.UserControl
    {
        public StatusBanner()
        {
            InitializeComponent();
        }

        public static readonly System.Windows.DependencyProperty MessageProperty =
            System.Windows.DependencyProperty.Register("Message", typeof(string), typeof(StatusBanner), new System.Windows.PropertyMetadata(string.Empty));

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly System.Windows.DependencyProperty StateProperty =
            System.Windows.DependencyProperty.Register("State", typeof(BannerState), typeof(StatusBanner), new System.Windows.PropertyMetadata(BannerState.None, OnStateChanged));

        public BannerState State
        {
            get => (BannerState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        private static void OnStateChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is StatusBanner banner)
            {
                banner.UpdateState((BannerState)e.NewValue);
            }
        }

        // Brush properties for visual appearance
        public static readonly System.Windows.DependencyProperty BackgroundBrushProperty =
            System.Windows.DependencyProperty.Register("BackgroundBrush", typeof(System.Windows.Media.Brush), typeof(StatusBanner), new System.Windows.PropertyMetadata(System.Windows.Media.Brushes.Transparent));

        public System.Windows.Media.Brush BackgroundBrush
        {
            get => (System.Windows.Media.Brush)GetValue(BackgroundBrushProperty);
            set => SetValue(BackgroundBrushProperty, value);
        }

        public static readonly new System.Windows.DependencyProperty BorderBrushProperty =
            System.Windows.DependencyProperty.Register("BorderBrush", typeof(System.Windows.Media.Brush), typeof(StatusBanner), new System.Windows.PropertyMetadata(System.Windows.Media.Brushes.Transparent));

        public new System.Windows.Media.Brush BorderBrush
        {
            get => (System.Windows.Media.Brush)GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public static readonly System.Windows.DependencyProperty IconBrushProperty =
            System.Windows.DependencyProperty.Register("IconBrush", typeof(System.Windows.Media.Brush), typeof(StatusBanner), new System.Windows.PropertyMetadata(System.Windows.Media.Brushes.Black));

        public System.Windows.Media.Brush IconBrush
        {
            get => (System.Windows.Media.Brush)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }

        public static readonly System.Windows.DependencyProperty IconDataProperty =
            System.Windows.DependencyProperty.Register("IconData", typeof(System.Windows.Media.Geometry), typeof(StatusBanner), new System.Windows.PropertyMetadata(null));

        public System.Windows.Media.Geometry IconData
        {
            get => (System.Windows.Media.Geometry)GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        public static readonly System.Windows.DependencyProperty TextBrushProperty =
            System.Windows.DependencyProperty.Register("TextBrush", typeof(System.Windows.Media.Brush), typeof(StatusBanner), new System.Windows.PropertyMetadata(System.Windows.Media.Brushes.Black));

        public System.Windows.Media.Brush TextBrush
        {
            get => (System.Windows.Media.Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        private void UpdateState(BannerState state)
        {
            switch (state)
            {
                case BannerState.Error:
                    Message = Message ?? "Fehler";
                    BackgroundBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 235, 235)); // Light red
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 199, 206)); // Red border
                    IconBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(183, 28, 28)); // Dark red
                    IconData = System.Windows.Media.Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z");
                    TextBrush = System.Windows.Media.Brushes.Black;
                    break;
                case BannerState.Warning:
                    Message = Message ?? "Warnung";
                    BackgroundBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 250, 235)); // Light yellow
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 170)); // Amber border
                    IconBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(183, 130, 0)); // Dark amber
                    IconData = System.Windows.Media.Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z");
                    TextBrush = System.Windows.Media.Brushes.Black;
                    break;
                case BannerState.Success:
                    Message = Message ?? "Erfolg";
                    BackgroundBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 250, 235)); // Light green
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(199, 250, 199)); // Green border
                    IconBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 94, 32)); // Dark green
                    IconData = System.Windows.Media.Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z");
                    TextBrush = System.Windows.Media.Brushes.Black;
                    break;
                case BannerState.Info:
                    Message = Message ?? "Information";
                    BackgroundBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 245, 255)); // Light blue
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(199, 225, 255)); // Blue border
                    IconBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13, 71, 161)); // Dark blue
                    IconData = System.Windows.Media.Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z");
                    TextBrush = System.Windows.Media.Brushes.Black;
                    break;
                case BannerState.None:
                default:
                    Message = string.Empty;
                    BackgroundBrush = System.Windows.Media.Brushes.Transparent;
                    BorderBrush = System.Windows.Media.Brushes.Transparent;
                    IconBrush = System.Windows.Media.Brushes.Transparent;
                    IconData = null;
                    TextBrush = System.Windows.Media.Brushes.Black;
                    break;
            }
        }

        // Convenience methods to set common states
        public void ShowError(string message = null)
        {
            State = BannerState.Error;
            if (message != null)
                Message = message;
        }

        public void ShowWarning(string message = null)
        {
            State = BannerState.Warning;
            if (message != null)
                Message = message;
        }

        public void ShowSuccess(string message = null)
        {
            State = BannerState.Success;
            if (message != null)
                Message = message;
        }

        public void ShowInfo(string message = null)
        {
            State = BannerState.Info;
            if (message != null)
                Message = message;
        }

        public void Clear()
        {
            State = BannerState.None;
            Message = string.Empty;
        }
    }
}