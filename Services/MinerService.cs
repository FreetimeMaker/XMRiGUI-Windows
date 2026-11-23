using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using XMRiGUI.Models;

namespace XMRiGUI.Services
{
    public class MinerService
    {
        private readonly Dictionary<int, Process> _processes = new();
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        public event Action<int, string> OutputReceived;
        public event Action<int, int> Exited;

        public static string InstalledXmrigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XMRiGUI", "miner", "xmrig.exe");

        public string GetXmrigPath()
        {
            var root = Path.Combine(AppContext.BaseDirectory, "xmrig.exe");
            var bundled = Path.Combine(AppContext.BaseDirectory, "miner", "xmrig.exe");
            return File.Exists(root) ? root : File.Exists(bundled) ? bundled : InstalledXmrigPath;
        }

        public void Start(int index, ProfileConfig config, IReadOnlyList<string> extra)
        {
            if (_processes.ContainsKey(index)) throw new InvalidOperationException("The previous miner is still stopping.");
            var path = GetXmrigPath();
            if (!File.Exists(path)) throw new FileNotFoundException("Miner not found: " + path);
            var info = new ProcessStartInfo(path)
            {
                WorkingDirectory = Path.GetDirectoryName(path), UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            var args = info.ArgumentList;
            args.Add("--no-color");
            if (!config.default_args)
            {
                args.Add("--url=" + config.pool.Trim());
                args.Add("--user=" + config.wallet);
                args.Add("--pass=" + config.password);
                args.Add("--donate-level=" + config.donate);
                if (!string.IsNullOrEmpty(config.algo)) args.Add("--algo=" + config.algo);
                if (config.threads > 0)
                {
                    args.Add("--threads=" + config.threads);
                    args.Add("--randomx-init=" + config.threads);
                }
                if (!config.cpu) args.Add("--no-cpu");
                if (config.cuda) args.Add("--cuda");
                if (config.opencl) args.Add("--opencl");
                if (config.tls) args.Add("--tls");
            }
            foreach (var arg in extra) args.Add(arg);

            var process = new Process { StartInfo = info };
            try
            {
                process.Start();
            }
            catch
            {
                process.Dispose();
                throw;
            }
            _processes.Add(index, process);
            process.OutputDataReceived += (_, e) => Publish(e.Data);
            process.ErrorDataReceived += (_, e) => Publish(e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _ = ObserveExit(index, process);

            void Publish(string line)
            {
                if (line == null) return;
                _dispatcher.InvokeAsync(() =>
                {
                    if (_processes.GetValueOrDefault(index) == process) OutputReceived?.Invoke(index, line);
                });
            }
        }

        private async Task ObserveExit(int index, Process process)
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                if (_processes.GetValueOrDefault(index) != process) return;
                _processes.Remove(index);
                Exited?.Invoke(index, process.ExitCode);
            });
            process.Dispose();
        }

        public void Stop(int index)
        {
            if (!_processes.TryGetValue(index, out var process)) return;
            try { process.Kill(); }
            catch (InvalidOperationException) when (process.HasExited) { }
            _processes.Remove(index);
        }
    }
}
