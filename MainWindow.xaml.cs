using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using XMRiGUI.Models;
using XMRiGUI.Services;
using Forms = System.Windows.Forms;

namespace XMRiGUI
{
    public partial class MainWindow : Window
    {
        private readonly MinerService _miner = new();
        private readonly ConfigService _storage = new();
        private readonly XmriguiConfig _config;
        private readonly ProfileView[] _profiles = new ProfileView[3];
        private readonly DispatcherTimer[] _restarts = new DispatcherTimer[3];
        private readonly int[] _attempts = new int[3];
        private readonly bool[] _active = new bool[3];
        private readonly ProfileConfig[] _launchConfigs = new ProfileConfig[3];
        private readonly System.Collections.Generic.List<string>[] _arguments = new System.Collections.Generic.List<string>[3];
        private readonly Forms.NotifyIcon _tray;
        private readonly DispatcherTimer _statsTimer;
        private bool _quitting;
        private readonly CancellationTokenSource _minerSetupCancellation = new();
        private Task<bool> _minerSetup;
        private readonly int[] _startGeneration = new int[3];
        private readonly bool[] _waitingForMiner = new bool[3];

        public MainWindow()
        {
            InitializeComponent();
            _config = _storage.LoadConfig();
            for (int i = 0; i < _profiles.Length; i++)
            {
                int index = i;
                var view = _profiles[i] = new ProfileView();
                view.LoadProfile(_config.profiles[i], _miner.GetXmrigPath());
                view.MineRequested += active => { if (active) Start(index); else Stop(index); };
                view.SaveRequested += () => { if (view.TryApply(out _)) Persist(); };
                view.RenameRequested += () => Rename(index);
                view.DuplicateRequested += () => Duplicate(index);
                MainTabs.Items.Add(new TabItem { Header = view.Config.name, Content = view });
                var timer = _restarts[i] = new DispatcherTimer();
                timer.Tick += (_, _) => { timer.Stop(); if (_active[index]) Launch(index); };
            }
            var settings = new StackPanel { Margin = new Thickness(20), DataContext = _config };
            foreach (var (label, key) in new[] { ("Minimize to tray", "minimize_to_tray"), ("Close to tray", "close_to_tray") })
            {
                var option = new CheckBox { Content = label, Margin = new Thickness(0, 0, 0, 8) };
                option.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, key);
                settings.Children.Add(option);
            }
            var saveSettings = new Button { Content = "Save", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 3, 10, 3) };
            saveSettings.Click += (_, _) => Persist();
            settings.Children.Add(saveSettings);
            MainTabs.Items.Add(new TabItem { Header = "Settings", Content = settings });
            MainTabs.SelectedIndex = 0;
            _miner.OutputReceived += (i, line) => _profiles[i].AppendLog(line);
            _miner.Exited += MinerExited;
            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statsTimer.Tick += (_, _) => { foreach (var profile in _profiles) profile.RefreshStats(); };
            _statsTimer.Start();

            var menu = new Forms.ContextMenuStrip();
            for (int i = 0; i < _profiles.Length; i++)
            {
                int index = i;
                menu.Items.Add("Toggle " + _profiles[i].Config.name, null, (_, _) => Dispatcher.Invoke(() =>
                {
                    if (_active[index] || _waitingForMiner[index]) Stop(index); else Start(index);
                }));
            }
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Show", null, (_, _) => Dispatcher.Invoke(ShowWindow));
            menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(() => { _quitting = true; Close(); }));
            _tray = new Forms.NotifyIcon { Icon = SystemIcons.Application, ContextMenuStrip = menu, Text = "XMRiGUI", Visible = true };
            _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow);
        }

        public void CheckMinerOnStartup() => _ = EnsureMinerAsync();

        private Task<bool> EnsureMinerAsync()
        {
            var path = _miner.GetXmrigPath();
            if (File.Exists(path))
            {
                foreach (var profile in _profiles) profile.SetMinerPath(path);
                if (_minerSetup?.IsCompletedSuccessfully == true && !_minerSetup.Result)
                    MinerSetupStatus.Visibility = Visibility.Collapsed;
                return Task.FromResult(true);
            }
            if (_minerSetup == null || _minerSetup.IsCompleted)
                _minerSetup = InstallMinerAsync();
            return _minerSetup;
        }

        private async Task<bool> InstallMinerAsync()
        {
            MinerSetupStatus.Text = "XMRig not found. Downloading the latest Windows x64 release from GitHub…";
            MinerSetupStatus.Visibility = Visibility.Visible;
            var cancellation = _minerSetupCancellation.Token;
            try
            {
                var version = await MinerInstaller.InstallLatestAsync(MinerService.InstalledXmrigPath, cancellation);
                if (cancellation.IsCancellationRequested) return false;
                foreach (var profile in _profiles) profile.SetMinerPath(_miner.GetXmrigPath());
                MinerSetupStatus.Text = $"XMRig {version} installed.";
                return true;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException
                or OperationCanceledException or System.Text.Json.JsonException or System.Collections.Generic.KeyNotFoundException
                or InvalidOperationException or FormatException)
            {
                Trace.TraceError("Could not install XMRig: {0}", ex);
                string reason = ex switch
                {
                    InvalidDataException => "The release data or ZIP could not be verified.",
                    HttpRequestException { StatusCode: { } code } => $"GitHub returned HTTP {(int)code}.",
                    HttpRequestException => "Check your connection to GitHub.",
                    OperationCanceledException => "The download timed out.",
                    UnauthorizedAccessException or IOException => "Check that the miner folder is writable.",
                    _ => "GitHub returned unexpected release data."
                };
                MinerSetupStatus.Text = $"Could not install XMRig. {reason} Select Mine or restart XMRiGUI to retry.";
                return false;
            }
        }

        public void HandleCommand(byte command)
        {
            if (command == 1)
            {
                for (int i = 0; i < _profiles.Length; i++) Start(i);
            }
            else if (command == 2)
            {
                for (int i = 0; i < _profiles.Length; i++) Stop(i);
            }
            else if (command == 3) Hide();
            else ShowWindow();
        }

        private bool Persist()
        {
            try { _storage.SaveConfig(_config); return true; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceError("Could not save settings: {0}", ex);
                MessageBox.Show(this, "Check that the settings folder is writable and has enough free space.", "Could not save settings", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async void Start(int index)
        {
            if (_active[index] || _waitingForMiner[index]) return;
            var view = _profiles[index];
            if (!view.TryApply(out var args) || !Persist())
            {
                view.SetStatus(false, "Status: Check your settings.");
                return;
            }
            var config = view.Config.Copy();
            var generation = ++_startGeneration[index];
            _waitingForMiner[index] = true;
            if (!File.Exists(_miner.GetXmrigPath())) view.SetStatus(true, "Waiting for XMRig installation…");
            bool ready = await EnsureMinerAsync();
            if (generation != _startGeneration[index]) return;
            _waitingForMiner[index] = false;
            if (!ready)
            {
                view.SetStatus(false, "XMRig is not installed.");
                return;
            }
            _arguments[index] = args;
            _launchConfigs[index] = config;
            _attempts[index] = 0;
            _active[index] = true;
            view.BeginSession();
            Launch(index);
        }

        private void Launch(int index)
        {
            try
            {
                _miner.Start(index, _launchConfigs[index], _arguments[index]);
                _profiles[index].BeginAttempt(_launchConfigs[index]);
            }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                _active[index] = false;
                Trace.TraceError("Could not start XMRig: {0}", ex);
                _profiles[index].SetStatus(false, ex switch
                {
                    FileNotFoundException => "Start failed: XMRig could not be found.",
                    System.ComponentModel.Win32Exception error => $"Start failed: Windows could not launch XMRig (error {error.NativeErrorCode}).",
                    InvalidOperationException => "Start failed: The previous miner may still be stopping.",
                    _ => "Start failed: The XMRig executable could not be opened."
                });
            }
        }

        private void MinerExited(int index, int code)
        {
            if (_active[index] && !_quitting && _profiles[index].Config.auto_restart && _attempts[index] < 5)
            {
                int attempt = ++_attempts[index], delay = 1 << attempt;
                _profiles[index].WaitForRetry(code, attempt, delay);
                _restarts[index].Interval = TimeSpan.FromSeconds(delay);
                _restarts[index].Start();
            }
            else
            {
                _active[index] = false;
                _profiles[index].SetStatus(false,
                    $"Miner exited (code {code}); restart disabled or limit reached.");
            }
        }

        private bool Stop(int index)
        {
            _restarts[index].Stop();
            _startGeneration[index]++;
            _waitingForMiner[index] = false;
            try { _miner.Stop(index); }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Trace.TraceError("Could not stop XMRig: {0}", ex);
                _profiles[index].SetStatus(true, $"Stop failed: Windows could not stop XMRig (error {ex.NativeErrorCode}).");
                return false;
            }
            _active[index] = false;
            _profiles[index].SetStatus(false, "Status: Stopped.");
            return true;
        }

        private void Rename(int index)
        {
            var input = new TextBox { Text = _profiles[index].Config.name, MaxLength = 80, MinWidth = 250 };
            if (!Prompt("Rename profile", "Name:", input) || string.IsNullOrWhiteSpace(input.Text)) return;
            _profiles[index].Config.name = input.Text.Trim();
            UpdateName(index);
            Persist();
        }

        private void Duplicate(int source)
        {
            var targets = Enumerable.Range(0, _profiles.Length).Where(i => i != source).ToArray();
            var input = new ComboBox { ItemsSource = targets.Select(i => $"{_profiles[i].Config.name} (slot {i + 1})"), SelectedIndex = 0, MinWidth = 250 };
            if (!Prompt("Duplicate profile", "Replace target profile:", input) || input.SelectedIndex < 0) return;
            int target = targets[input.SelectedIndex];
            if (!Prompt("Replace profile", "Overwrite this profile’s settings and stop its miner?", null, "Replace")) return;
            if (!_profiles[source].TryApply(out _) || !Stop(target)) return;
            var config = _profiles[source].Config.Copy();
            config.enabled = false;
            config.name += " (copy)";
            _config.profiles[target] = config;
            _profiles[target].LoadProfile(config, _miner.GetXmrigPath());
            UpdateName(target);
            Persist();
        }

        private bool Prompt(string title, string label, Control input, string confirm = "OK")
        {
            var panel = new StackPanel { Margin = new Thickness(16) };
            var dialog = new Window { Title = title, Owner = this, Content = panel, SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });
            if (input != null) panel.Children.Add(input);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = confirm, IsDefault = input != null, MinWidth = 75, Margin = new Thickness(0, 0, 5, 0) };
            ok.Click += (_, _) => dialog.DialogResult = true;
            buttons.Children.Add(ok);
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 75 };
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            dialog.Loaded += (_, _) => { (input ?? cancel).Focus(); if (input is TextBox box) box.SelectAll(); };
            return dialog.ShowDialog() == true;
        }

        private void UpdateName(int i)
        {
            ((TabItem)MainTabs.Items[i]).Header = _profiles[i].Config.name;
            _tray.ContextMenuStrip.Items[i].Text = "Toggle " + _profiles[i].Config.name;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _config?.minimize_to_tray == true) Hide();
            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_quitting && _config.close_to_tray)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                bool stopped = true;
                for (int i = 0; i < _profiles.Length; i++) stopped &= Stop(i);
                if (!stopped)
                {
                    e.Cancel = true;
                    _quitting = false;
                    ShowWindow();
                }
                else
                {
                    _minerSetupCancellation.Cancel();
                    _minerSetupCancellation.Dispose();
                    _statsTimer.Stop();
                    _tray.ContextMenuStrip.Dispose();
                    _tray.Dispose();
                }
            }
            base.OnClosing(e);
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
    }
}
