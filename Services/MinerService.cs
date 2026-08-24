using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using XMRiGUI.Models;

namespace XMRiGUI.Services
{
    public class MinerService
    {
        private readonly Dictionary<int, Process> _processes = new Dictionary<int, Process>();
        public event Action<int, string> OutputReceived;
        public event Action<int, bool> StatusChanged;

        public bool IsRunning(int profileIndex) => _processes.ContainsKey(profileIndex) && !_processes[profileIndex].HasExited;

        public string GetBundledXmrigPath()
        {
            // Erst im Hauptverzeichnis suchen, dann im miner/ Unterordner
            string rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xmrig.exe");
            if (File.Exists(rootPath)) return rootPath;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "miner", "xmrig.exe");
        }

        public void Start(int profileIndex, ProfileConfig config)
        {
            if (IsRunning(profileIndex)) return;

            string xmrigPath = GetBundledXmrigPath();
            if (!File.Exists(xmrigPath))
            {
                OutputReceived?.Invoke(profileIndex, "Error: Integrated miner not found at " + xmrigPath);
                return;
            }

            string arguments = GenerateArguments(config);
            string workingDir = Path.GetDirectoryName(xmrigPath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = xmrigPath,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            process.OutputDataReceived += (s, e) => { if (e.Data != null) OutputReceived?.Invoke(profileIndex, e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) OutputReceived?.Invoke(profileIndex, "ERROR: " + e.Data); };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _processes[profileIndex] = process;
                StatusChanged?.Invoke(profileIndex, true);

                Task.Run(() =>
                {
                    process.WaitForExit();
                    _processes.Remove(profileIndex);
                    StatusChanged?.Invoke(profileIndex, false);
                });
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke(profileIndex, "Start error: " + ex.Message);
            }
        }

        public void Stop(int profileIndex)
        {
            if (_processes.TryGetValue(profileIndex, out var process))
            {
                try { process.Kill(); } catch { }
            }
        }

        private string GenerateArguments(ProfileConfig config)
        {
            var args = new List<string>();
            args.Add("--url=" + config.pool);
            args.Add("--user=" + config.wallet);
            args.Add("--pass=" + config.password);
            args.Add("--donate-level=" + config.donate);

            if (!string.IsNullOrEmpty(config.algo))
                args.Add("--algo=" + config.algo);

            if (config.threads > 0)
                args.Add("--threads=" + config.threads);

            if (!config.cpu) args.Add("--no-cpu");
            if (config.cuda) args.Add("--cuda");
            if (config.opencl) args.Add("--opencl");
            if (config.tls) args.Add("--tls");

            return string.Join(" ", args);
        }
    }
}
