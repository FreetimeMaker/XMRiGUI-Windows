using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.RegularExpressions;
using XMRiGUI.Models;

namespace XMRiGUI
{
    public partial class ProfileView : UserControl
    {
        public event Action<ProfileConfig> StartRequested;
        public event Action StopRequested;

        public ProfileView()
        {
            InitializeComponent();
        }

        private void MinerSwitch_Click(object sender, RoutedEventArgs e)
        {
            var config = DataContext as ProfileConfig;
            if (config == null) return;

            if (MinerSwitch.IsChecked == true)
            {
                StartRequested?.Invoke(config);
            }
            else
            {
                StopRequested?.Invoke();
            }
        }

        public void UpdateStatus(bool running)
        {
            Dispatcher.Invoke(() =>
            {
                MinerSwitch.IsChecked = running;
                StatusText.Text = running ? "LÄUFT" : "GESTOPPT";
                StatusText.Foreground = running ? Brushes.LimeGreen : Brushes.Red;
                if (!running) HashrateText.Text = "0.00 H/s";
            });
        }

        public void AppendLog(string text)
        {
            Dispatcher.Invoke(() =>
            {
                LogOutput.AppendText(text + Environment.NewLine);
                LogOutput.ScrollToEnd();

                var match = Regex.Match(text, @"speed 10s/60s/15m\s+([\d\.]+)\s+([\d\.]+)\s+([\d\.]+)");
                if (match.Success)
                {
                    HashrateText.Text = match.Groups[1].Value + " H/s";
                }
            });
        }
    }
}
