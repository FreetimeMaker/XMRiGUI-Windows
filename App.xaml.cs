using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace XMRiGUI
{
    public partial class App : Application
    {
        private Mutex _instance;
        private bool _ownsInstance;
        private readonly CancellationTokenSource _shutdown = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            using var identity = WindowsIdentity.GetCurrent();
            using var process = Process.GetCurrentProcess();
            var name = $"XMRiGUI-{identity.User.Value}-{process.SessionId}";
            byte command = e.Args.Contains("stop") ? (byte)2 : e.Args.Contains("start") ? (byte)1 :
                e.Args.Contains("--close") ? (byte)3 : (byte)0;
            _instance = new Mutex(true, @"Local\" + name, out _ownsInstance);
            if (!_ownsInstance)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", name, PipeDirection.Out);
                    client.Connect(2500);
                    client.WriteByte(command);
                }
                catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
                {
                    MessageBox.Show("Could not contact the running instance: " + ex.Message, "XMRiGUI");
                    Shutdown(1);
                    return;
                }
                Shutdown();
                return;
            }

            MainWindow window;
            try { window = new MainWindow(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            {
                MessageBox.Show("Could not load settings: " + ex.Message, "XMRiGUI");
                Shutdown(1);
                return;
            }
            MainWindow = window;
            if (command != 3) window.Show();
            window.CheckMinerOnStartup();
            window.HandleCommand(command);
            _ = ListenAsync(name, window, _shutdown.Token);
        }

        private async Task ListenAsync(string name, MainWindow window, CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                try
                {
                    await server.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    deadline.CancelAfter(TimeSpan.FromSeconds(2));
                    var buffer = new byte[1];
                    if (await server.ReadAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false) == 1 && buffer[0] <= 3)
                        await Dispatcher.InvokeAsync(() => window.HandleCommand(buffer[0]), System.Windows.Threading.DispatcherPriority.Normal, cancellation);
                }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _shutdown.Cancel();
            _shutdown.Dispose();
            if (_ownsInstance) _instance.ReleaseMutex();
            _instance?.Dispose();
            base.OnExit(e);
        }
    }
}
