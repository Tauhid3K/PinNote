using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PinNote.Services
{
    public class SingleInstanceService : IDisposable
    {
        private const string PipeNamePrefix = "PinNote_IPC_";
        private readonly string _identifier;
        private readonly Mutex _mutex;
        private readonly bool _isFirstInstance;
        private readonly CancellationTokenSource _cts = new();
        private bool _isDisposed;

        public event Action<string>? CommandReceived;

        public bool IsFirstInstance => _isFirstInstance;

        public SingleInstanceService(string? customIdentifier = null)
        {
            _identifier = !string.IsNullOrWhiteSpace(customIdentifier)
                ? customIdentifier
                : Environment.UserName.Replace('\\', '_').Replace('/', '_');

            string mutexName = $"PinNote_SingleInstance_Mutex_{_identifier}";
            _mutex = new Mutex(true, mutexName, out _isFirstInstance);
        }

        public void StartServer()
        {
            if (!_isFirstInstance) return;

            Task.Run(async () =>
            {
                if (OperatingSystem.IsWindows())
                {
                    await RunWindowsNamedPipeServerAsync(_cts.Token);
                }
                else
                {
                    await RunLinuxSocketServerAsync(_cts.Token);
                }
            });
        }

        public static async Task<bool> SendCommandToRunningInstanceAsync(string command, string? customIdentifier = null)
        {
            string identifier = !string.IsNullOrWhiteSpace(customIdentifier)
                ? customIdentifier
                : Environment.UserName.Replace('\\', '_').Replace('/', '_');

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    string pipeName = $"{PipeNamePrefix}{identifier}";
                    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                    await client.ConnectAsync(1000);
                    using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
                    await writer.WriteLineAsync(command);
                    return true;
                }
                else
                {
                    string socketPath = GetLinuxSocketPath(identifier);
                    if (!File.Exists(socketPath)) return false;

                    using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    var endpoint = new UnixDomainSocketEndPoint(socketPath);
                    await socket.ConnectAsync(endpoint);
                    byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                    await socket.SendAsync(data, SocketFlags.None);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to send command to running instance: {ex.Message}");
                return false;
            }
        }

        private async Task RunWindowsNamedPipeServerAsync(CancellationToken ct)
        {
            string pipeName = $"{PipeNamePrefix}{_identifier}";

            while (!ct.IsCancellationRequested && !_isDisposed)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string? line = await reader.ReadLineAsync(ct);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        CommandReceived?.Invoke(line.Trim());
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NamedPipeServer error: {ex.Message}");
                    await Task.Delay(200, ct);
                }
            }
        }

        private async Task RunLinuxSocketServerAsync(CancellationToken ct)
        {
            string socketPath = GetLinuxSocketPath(_identifier);
            try
            {
                if (File.Exists(socketPath))
                {
                    File.Delete(socketPath);
                }
            }
            catch { }

            Socket? serverSocket = null;
            try
            {
                serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var endpoint = new UnixDomainSocketEndPoint(socketPath);
                serverSocket.Bind(endpoint);
                serverSocket.Listen(5);

                while (!ct.IsCancellationRequested && !_isDisposed)
                {
                    try
                    {
                        using var client = await serverSocket.AcceptAsync(ct);
                        var buffer = new byte[1024];
                        int bytesRead = await client.ReceiveAsync(buffer, SocketFlags.None, ct);
                        if (bytesRead > 0)
                        {
                            string cmd = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                            if (!string.IsNullOrWhiteSpace(cmd))
                            {
                                CommandReceived?.Invoke(cmd);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"LinuxSocketServer accept error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LinuxSocketServer bind error: {ex.Message}");
            }
            finally
            {
                serverSocket?.Dispose();
                try
                {
                    if (File.Exists(socketPath)) File.Delete(socketPath);
                }
                catch { }
            }
        }

        private static string GetLinuxSocketPath(string identifier)
        {
            string tmpDir = Path.GetTempPath();
            return Path.Combine(tmpDir, $"pinnote_{identifier}.sock");
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cts.Cancel();
            _cts.Dispose();

            if (_isFirstInstance)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { }

                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        string socketPath = GetLinuxSocketPath(_identifier);
                        if (File.Exists(socketPath)) File.Delete(socketPath);
                    }
                    catch { }
                }
            }

            _mutex.Dispose();
        }
    }
}
