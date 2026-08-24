using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using XMRiGUI.Services;
using XMRiGUI.Models;
using System.Drawing;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace XMRiGUI
{
    public partial class MainWindow : Window
    {
        private readonly MinerService _minerService;
        private readonly ConfigService _configService;
        private readonly UpdateService _updateService;
        private XmriguiConfig _config;
        private NotifyIcon _notifyIcon;
        private bool _isExitForced = false;

        public MainWindow()
        {
            InitializeComponent();
            _minerService = new MinerService();
            _configService = new ConfigService();
            _updateService = new UpdateService();

            _minerService.OutputReceived += MinerService_OutputReceived;
            _minerService.StatusChanged += MinerService_StatusChanged;

            SetupProfileViews();
            LoadSettings();
            SetupTrayIcon();
            CheckForUpdates();
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application; // Replace with app icon later
            _notifyIcon.Text = "XMRiGUI Miner";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Anzeigen", null, (s, e) => ShowWindow());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Beenden", null, (s, e) => ForceExit());

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private async void CheckForUpdates()
        {
            string newVersion = await _updateService.CheckForUpdateAsync();
            if (!string.IsNullOrEmpty(newVersion))
            {
                var result = MessageBox.Show($"Eine neue Version (v{newVersion}) ist verfügbar! Möchtest du zur Download-Seite?",
                    "Update verfügbar", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("https://github.com/FreetimeMaker/XMRiGUI/releases");
                }
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _config.minimize_to_tray)
            {
                this.Hide();
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExitForced && _config.close_to_tray)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                _notifyIcon.Dispose();
                _minerService.Stop(0);
                _minerService.Stop(1);
                _minerService.Stop(2);
            }
            base.OnClosing(e);
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ForceExit()
        {
            _isExitForced = true;
            this.Close();
        }

        private void SetupProfileViews()
        {
            Profile1View.StartRequested += (cfg) => StartMiner(0, cfg);
            Profile1View.StopRequested += () => _minerService.Stop(0);

            Profile2View.StartRequested += (cfg) => StartMiner(1, cfg);
            Profile2View.StopRequested += () => _minerService.Stop(1);

            Profile3View.StartRequested += (cfg) => StartMiner(2, cfg);
            Profile3View.StopRequested += () => _minerService.Stop(2);
        }

        private void LoadSettings()
        {
            _config = _configService.LoadConfig();
            this.DataContext = _config;

            // Sidebar selection
            SidebarList.SelectedIndex = 0;
        }

        private void MinerService_OutputReceived(int profileIndex, string text)
        {
            GetProfileView(profileIndex)?.AppendLog(text);
        }

        private void MinerService_StatusChanged(int profileIndex, bool running)
        {
            GetProfileView(profileIndex)?.UpdateStatus(running);
        }

        private ProfileView GetProfileView(int index)
        {
            switch (index)
            {
                case 0: return Profile1View;
                case 1: return Profile2View;
                case 2: return Profile3View;
                default: return null;
            }
        }

        private void StartMiner(int index, ProfileConfig cfg)
        {
            _configService.SaveConfig(_config);
            _minerService.Start(index, cfg);
        }

        private void SidebarList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabs == null) return;
            var item = SidebarList.SelectedItem as ListBoxItem;
            if (item != null && int.TryParse(item.Tag.ToString(), out int index))
            {
                MainTabs.SelectedIndex = index;
            }
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            _configService.SaveConfig(_config);
            MessageBox.Show("Alle Einstellungen wurden in AppData gespeichert.");
        }
    }
}
