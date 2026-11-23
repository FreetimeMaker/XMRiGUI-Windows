using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using XMRiGUI.Models;
using XMRiGUI.Services;

namespace XMRiGUI
{
    public partial class ProfileView : UserControl
    {
        private readonly Queue<(double Time, double Speed)> _samples = new();
        private readonly Queue<string> _logs = new(), _diagnostics = new();
        private readonly HashSet<string> _secrets = new();
        private readonly Stopwatch _runtime = new();
        private string _binary;
        private double _sum, _speed;
        private long _count, _accepted, _rejected, _acceptedBase, _rejectedBase;
        public event Action<bool> MineRequested;
        public event Action SaveRequested, RenameRequested, DuplicateRequested;
        public ProfileConfig Config => (ProfileConfig)DataContext;

        public ProfileView()
        {
            InitializeComponent();
        }

        public void LoadProfile(ProfileConfig config, string binary)
        {
            DataContext = config;
            _binary = binary;
            Pool.Text = config.pool ?? "";
            Wallet.Text = config.wallet ?? "";
            Password.Text = config.password ?? "";
            Threads.Text = config.threads.ToString(CultureInfo.InvariantCulture);
            Donate.Text = config.donate.ToString(CultureInfo.InvariantCulture);
            Arguments.Text = config.args ?? "";
            var coins = new Dictionary<string, string>
            {
                ["rx/0"] = "Monero", ["kawpow"] = "Ravencoin", ["cn/upx2"] = "Uplexa",
                ["argon2/chukwa"] = "Chukwa", ["argon2/chukwav2"] = "Chukwa v2", ["cn/ccx"] = "CCX",
                ["rx/keva"] = "Keva", ["astrobwt"] = "Dero", ["cn-pico/tlo"] = "Talleo",
                ["rx/sfx"] = "Safex", ["rx/arq"] = "ArQmA", ["argon2/ninja"] = "NINJA",
                ["gr"] = "Raptoreum", ["rx/wow"] = "Wownero", ["panthera"] = "Scala",
                ["cn-heavy/xhv"] = "Haven Protocol", ["rx/v"] = "MoneroV", ["rx/epic"] = "Epic Cash",
                ["rx/graft"] = "Graft", ["rx/loki"] = "Oxen", ["rx/xtl"] = "Stellite"
            };
            var algo = config.algo ?? "rx/0";
            coins.TryAdd(algo, string.IsNullOrEmpty(algo) ? "Automatic" : algo);
            Coin.ItemsSource = coins;
            Coin.SelectedValue = algo;
            ClearErrors();
            RefreshDiagnostics();
            RefreshStats();
        }

        private void ClearErrors()
        {
            Errors.Text = "";
            foreach (var field in new[] { Pool, Wallet, Threads, Donate, Arguments })
            {
                field.ClearValue(BorderBrushProperty);
                field.ToolTip = null;
            }
        }

        public bool TryApply(out List<string> extra)
        {
            ClearErrors();
            extra = null;
            void Error(TextBox field, string message)
            {
                field.BorderBrush = Brushes.Firebrick;
                field.ToolTip = message;
                Errors.Text += message + Environment.NewLine;
            }
            if (!Config.default_args && !ProfileTools.ValidPool(Pool.Text)) Error(Pool, "Pool: use host:port or a stratum+tcp/ssl URL with a valid port.");
            if (!Config.default_args && string.IsNullOrWhiteSpace(Wallet.Text)) Error(Wallet, "User or wallet is required.");
            if (!int.TryParse(Threads.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int threads)) Error(Threads, "Threads: enter an integer from 0 to 2147483647.");
            if (!int.TryParse(Donate.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int donate) || donate > 100) Error(Donate, "Donate: enter an integer from 0 to 100.");
            try { extra = ProfileTools.SplitArguments(Arguments.Text); }
            catch (FormatException)
            {
                Advanced.IsExpanded = true;
                Error(Arguments, "Additional arguments: invalid quoting or escaping.");
            }
            if (Errors.Text.Length > 0) return false;
            Config.pool = Pool.Text.Trim();
            Config.wallet = Wallet.Text.Trim();
            Config.password = Password.Text;
            Config.threads = threads;
            Config.donate = donate;
            Config.args = Arguments.Text;
            Config.algo = (string)Coin.SelectedValue;
            return true;
        }

        public void BeginSession()
        {
            _runtime.Restart();
            _samples.Clear();
            _diagnostics.Clear();
            _count = _accepted = _rejected = _acceptedBase = _rejectedBase = 0;
            _sum = _speed = 0;
            RefreshDiagnostics();
            DrawChart();
        }

        public void SetStatus(bool active, string status)
        {
            MineSwitch.IsChecked = active;
            StatusText.Text = status;
            if (!active)
            {
                _runtime.Stop();
                _speed = 0;
            }
            RefreshStats();
        }

        public void BeginAttempt(ProfileConfig runningConfig)
        {
            _acceptedBase = _accepted;
            _rejectedBase = _rejected;
            _secrets.UnionWith(ProfileTools.Secrets(runningConfig));
            SetStatus(true, "Status: Running.");
        }

        public void WaitForRetry(int code, int attempt, int delay)
        {
            _speed = 0;
            AddPoint(0);
            SetStatus(true, $"Miner exited ({code}). Restart {attempt}/5 in {delay}s.");
        }

        public void AppendLog(string text)
        {
            _logs.Enqueue(text);
            if (_logs.Count > 500)
            {
                var removed = _logs.Dequeue();
                LogOutput.Select(0, removed.Length + Environment.NewLine.Length);
                LogOutput.SelectedText = "";
            }
            LogOutput.AppendText(text + Environment.NewLine);
            LogOutput.ScrollToEnd();

            var speed = Regex.Match(text, @"speed 10s/60s/15m\s+([0-9.]+).*?\s+([kMGT]?)H/s\b");
            if (speed.Success && double.TryParse(speed.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value))
            {
                value *= speed.Groups[2].Value switch
                {
                    "k" => 1e3, "M" => 1e6, "G" => 1e9, "T" => 1e12, _ => 1
                };
                if (double.IsFinite(value))
                {
                    _speed = value;
                    _sum += value;
                    _count++;
                    AddPoint(value);
                }
            }
            var shares = Regex.Match(text, @"(?:accepted|rejected)\s+\((\d+)/(\d+)\)");
            if (shares.Success && int.TryParse(shares.Groups[1].Value, out int accepted) && int.TryParse(shares.Groups[2].Value, out int rejected))
            {
                _accepted = _acceptedBase + accepted;
                _rejected = _rejectedBase + rejected;
            }
            if (Regex.IsMatch(text, @"xmrig|huge ?pages|permission|msr|access denied", RegexOptions.IgnoreCase))
            {
                _diagnostics.Enqueue(text);
                if (_diagnostics.Count > 9) _diagnostics.Dequeue();
                RefreshDiagnostics();
            }
            if (Regex.IsMatch(text, @"connection refused|connection reset|timeout|no active pools|dns error|connect error", RegexOptions.IgnoreCase))
                StatusText.Text = "Status: XMRig is reconnecting…";
            else if (Regex.IsMatch(text, @"new job|\b(?:accepted|ready)\b", RegexOptions.IgnoreCase))
                StatusText.Text = "Status: Mining.";
            RefreshStats();
        }

        public void RefreshStats()
        {
            var runtime = _runtime.Elapsed;
            double rejected = _accepted + _rejected == 0 ? 0 : (double)_rejected / (_accepted + _rejected);
            InfoText.Text = $"Speed: {_speed:0.##} H/s | Shares: {_accepted}/{_rejected}";
            SessionText.Text = $"Runtime: {(long)runtime.TotalHours:00}:{runtime.Minutes:00}:{runtime.Seconds:00} | Average (samples): {(_count == 0 ? 0 : _sum / _count):0.0} H/s | Rejected: {rejected:P1}";
        }

        private void AddPoint(double value)
        {
            if (_samples.Count == 600) _samples.Dequeue();
            _samples.Enqueue((_runtime.Elapsed.TotalSeconds, value));
            DrawChart();
        }

        private void DrawChart()
        {
            Trace.Points.Clear();
            var max = _samples.Count == 0 ? 1 : Math.Max(1, _samples.Max(s => s.Speed));
            var end = _samples.Count == 0 ? 0 : _samples.Last().Time;
            var start = _samples.Count == 0 ? 0 : _samples.Peek().Time;
            var duration = Math.Max(1, end - start);
            foreach (var (time, speed) in _samples)
                Trace.Points.Add(new Point(1 + (time - start) / duration * Math.Max(0, Chart.ActualWidth - 2), 1 + (1 - speed / max) * Math.Max(0, Chart.ActualHeight - 2)));
            if (Trace.Points.Count == 1) Trace.Points.Add(new Point(3, Trace.Points[0].Y));
            ChartScale.Text = $"Hashrate (H/s) · 0–{max:G4}";
            ChartTime.Text = $"Elapsed minutes: {start / 60:0.00}–{end / 60:0.00}";
        }

        public void SetMinerPath(string binary)
        {
            _binary = binary;
            RefreshDiagnostics();
        }

        private void RefreshDiagnostics() => DiagnosticsText.Text = "Binary: " + _binary + Environment.NewLine +
            (_diagnostics.Count == 0 ? "Version / Huge Pages / permissions: not reported yet" : string.Join(Environment.NewLine, _diagnostics));

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Title = "Export redacted log", FileName = "xmrigui-log.txt", Filter = "Text files (*.txt)|*.txt" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            var draft = new ProfileConfig { pool = Pool.Text, wallet = Wallet.Text, password = Password.Text, args = Arguments.Text };
            try
            {
                var text = DiagnosticsText.Text + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, _logs);
                File.WriteAllText(dialog.FileName, ProfileTools.Redact(text, _secrets.Concat(ProfileTools.Secrets(Config)).Concat(ProfileTools.Secrets(draft))));
                MessageBox.Show("Log exported. Redaction is best-effort; review the file before sharing.", "XMRiGUI");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceError("Could not export log: {0}", ex);
                MessageBox.Show("Choose a writable location with enough free space for the log.", "Export failed");
            }
        }

        private void Chart_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();
        private void Mine_Click(object sender, RoutedEventArgs e) => MineRequested?.Invoke(MineSwitch.IsChecked == true);
        private void Save_Click(object sender, RoutedEventArgs e) => SaveRequested?.Invoke();
        private void Rename_Click(object sender, RoutedEventArgs e) => RenameRequested?.Invoke();
        private void Duplicate_Click(object sender, RoutedEventArgs e) => DuplicateRequested?.Invoke();
        private void Source_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/FreetimeMaker/XMRiGUI-Windows") { UseShellExecute = true });
    }
}
