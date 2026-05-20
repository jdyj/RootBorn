using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Rootborn.Tests.PlayMode.Network.Support
{
    public readonly struct DedicatedServerClientProcessHarnessConfig
    {
        public DedicatedServerClientProcessHarnessConfig(string serverExecutablePath, string clientExecutablePath, string logDirectory, int port, string saveSlot, string joinIp, int maxPlayers)
            : this(serverExecutablePath, clientExecutablePath, logDirectory, port, saveSlot, joinIp, maxPlayers, string.Empty)
        {
        }

        public DedicatedServerClientProcessHarnessConfig(string serverExecutablePath, string clientExecutablePath, string logDirectory, int port, string saveSlot, string joinIp, int maxPlayers, string clientExtraArguments)
        {
            ServerExecutablePath = string.IsNullOrEmpty(serverExecutablePath) ? string.Empty : serverExecutablePath;
            ClientExecutablePath = string.IsNullOrEmpty(clientExecutablePath) ? string.Empty : clientExecutablePath;
            LogDirectory = string.IsNullOrEmpty(logDirectory) ? string.Empty : logDirectory;
            Port = port;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? string.Empty : saveSlot;
            JoinIp = string.IsNullOrEmpty(joinIp) ? "127.0.0.1" : joinIp;
            MaxPlayers = maxPlayers <= 0 ? 1 : maxPlayers;
            ClientExtraArguments = string.IsNullOrEmpty(clientExtraArguments) ? string.Empty : clientExtraArguments.Trim();
        }

        public string ServerExecutablePath { get; }
        public string ClientExecutablePath { get; }
        public string LogDirectory { get; }
        public int Port { get; }
        public string SaveSlot { get; }
        public string JoinIp { get; }
        public int MaxPlayers { get; }
        public string ClientExtraArguments { get; }
    }

    public static class DedicatedServerClientProcessHarness
    {
        public static string[] ValidatePrerequisites(DedicatedServerClientProcessHarnessConfig config)
        {
            var diagnostics = new List<string>();
            if (string.IsNullOrEmpty(config.ServerExecutablePath) || !File.Exists(config.ServerExecutablePath))
            {
                diagnostics.Add("Missing server executable: " + config.ServerExecutablePath);
            }

            if (string.IsNullOrEmpty(config.ClientExecutablePath) || !File.Exists(config.ClientExecutablePath))
            {
                diagnostics.Add("Missing client executable: " + config.ClientExecutablePath);
            }

            if (config.Port <= 0 || config.Port > 65535)
            {
                diagnostics.Add("Invalid port: " + config.Port);
            }

            if (string.IsNullOrEmpty(config.SaveSlot))
            {
                diagnostics.Add("Missing save slot.");
            }

            if (string.IsNullOrEmpty(config.LogDirectory))
            {
                diagnostics.Add("Missing log directory.");
            }

            return diagnostics.ToArray();
        }

        public static ProcessStartInfo BuildServerStartInfo(DedicatedServerClientProcessHarnessConfig config)
        {
            return BuildStartInfo(
                config.ServerExecutablePath,
                "-mode server -batchmode -nographics -port " + config.Port + " -maxPlayers " + config.MaxPlayers + " -saveSlot " + config.SaveSlot);
        }

        public static ProcessStartInfo BuildClientStartInfo(DedicatedServerClientProcessHarnessConfig config)
        {
            string arguments = "-mode client -joinIp " + config.JoinIp + " -port " + config.Port + " -saveSlot " + config.SaveSlot;
            if (!string.IsNullOrEmpty(config.ClientExtraArguments))
            {
                arguments += " " + config.ClientExtraArguments;
            }

            return BuildStartInfo(config.ClientExecutablePath, arguments);
        }

        public static string RouteRelationshipAutoplayArgs(string screenshotPath)
        {
            return "-directValidationTrace -directValidationAutoplay -directValidationAutoplayRouteRelationship -directValidationScreenshot " + (string.IsNullOrEmpty(screenshotPath) ? string.Empty : screenshotPath);
        }

        public static string ServerLogPath(DedicatedServerClientProcessHarnessConfig config)
        {
            return Path.Combine(config.LogDirectory, "dedicated-server.log");
        }

        public static string ClientLogPath(DedicatedServerClientProcessHarnessConfig config)
        {
            return Path.Combine(config.LogDirectory, "dedicated-client.log");
        }

        public static DedicatedServerClientProcessSession Start(DedicatedServerClientProcessHarnessConfig config)
        {
            var diagnostics = ValidatePrerequisites(config);
            if (diagnostics.Length > 0)
            {
                throw new InvalidOperationException("Dedicated server/client process prerequisites failed:\n" + string.Join("\n", diagnostics));
            }

            Directory.CreateDirectory(config.LogDirectory);
            var server = Process.Start(BuildServerStartInfo(config));
            try
            {
                var client = Process.Start(BuildClientStartInfo(config));
                return new DedicatedServerClientProcessSession(
                    new SystemProcessHandle("server", server, ServerLogPath(config)),
                    new SystemProcessHandle("client", client, ClientLogPath(config)));
            }
            catch
            {
                using (var serverHandle = new SystemProcessHandle("server", server, ServerLogPath(config)))
                {
                    if (!serverHandle.HasExited)
                    {
                        serverHandle.Kill();
                    }
                }

                throw;
            }
        }

        private static ProcessStartInfo BuildStartInfo(string fileName, string arguments)
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string directory = Path.GetDirectoryName(fileName);
            info.WorkingDirectory = string.IsNullOrEmpty(directory) ? Path.GetFullPath(".") : Path.GetFullPath(directory);
            return info;
        }
    }

    public readonly struct DedicatedServerClientReadiness
    {
        public DedicatedServerClientReadiness(bool serverStarted, bool clientStarted, bool clientConnected, bool townSynchronized, bool clientAutoplayCompleted, bool clientAutoplayFailed)
        {
            ServerStarted = serverStarted;
            ClientStarted = clientStarted;
            ClientConnected = clientConnected;
            TownSynchronized = townSynchronized;
            ClientAutoplayCompleted = clientAutoplayCompleted;
            ClientAutoplayFailed = clientAutoplayFailed;
        }

        public bool ServerStarted { get; }
        public bool ClientStarted { get; }
        public bool ClientConnected { get; }
        public bool TownSynchronized { get; }
        public bool ClientAutoplayCompleted { get; }
        public bool ClientAutoplayFailed { get; }
        public bool IsReady => ServerStarted && ClientStarted && ClientConnected && TownSynchronized;
        public bool PlayerFlowEvidenceReady => IsReady && ClientAutoplayCompleted && !ClientAutoplayFailed;
    }

    public static class DedicatedServerClientReadinessProbe
    {
        public static DedicatedServerClientReadiness FromLogs(string serverLog, string clientLog)
        {
            string server = serverLog ?? string.Empty;
            string client = clientLog ?? string.Empty;
            bool serverStarted = server.Contains("Dedicated server started");
            bool clientStarted = client.Contains("Client started");
            bool clientConnected = server.Contains("Dedicated server client connected") && client.Contains("Client connected callback");
            bool townSynchronized = ContainsTownSynchronization(server) && ContainsTownSynchronization(client);
            bool clientAutoplayCompleted = client.Contains("autoplay completed route-relationship");
            bool clientAutoplayFailed = client.Contains("autoplay failed");
            return new DedicatedServerClientReadiness(serverStarted, clientStarted, clientConnected, townSynchronized, clientAutoplayCompleted, clientAutoplayFailed);
        }

        private static bool ContainsTownSynchronization(string log)
        {
            return log.Contains("scene=Town") || log.Contains("scene event type=LoadEventCompleted scene=Town");
        }
    }

    public interface IDedicatedProcessHandle : IDisposable
    {
        string Name { get; }
        bool HasExited { get; }
        void Kill();
    }

    public sealed class DedicatedServerClientProcessSession : IDisposable
    {
        private readonly IDedicatedProcessHandle _server;
        private readonly IDedicatedProcessHandle _client;

        public DedicatedServerClientProcessSession(IDedicatedProcessHandle server, IDedicatedProcessHandle client)
        {
            _server = server;
            _client = client;
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            Stop(_client);
            Stop(_server);
            IsDisposed = true;
        }

        private static void Stop(IDedicatedProcessHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            try
            {
                if (!handle.HasExited)
                {
                    handle.Kill();
                }
            }
            finally
            {
                handle.Dispose();
            }
        }
    }

    public sealed class SystemProcessHandle : IDedicatedProcessHandle
    {
        private readonly Process _process;
        private readonly StreamWriter _logWriter;

        public SystemProcessHandle(string name, Process process, string logPath)
        {
            Name = string.IsNullOrEmpty(name) ? "process" : name;
            _process = process;
            if (!string.IsNullOrEmpty(logPath))
            {
                string directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                _logWriter = new StreamWriter(logPath, false) { AutoFlush = true };
                AttachLogCapture();
            }
        }

        public string Name { get; }
        public bool HasExited => _process == null || _process.HasExited;

        public void Kill()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
            }
        }

        public void Dispose()
        {
            _logWriter?.Dispose();
            _process?.Dispose();
        }

        private void AttachLogCapture()
        {
            if (_process == null || _logWriter == null)
            {
                return;
            }

            _process.OutputDataReceived += (_, args) => WriteLine(args.Data);
            _process.ErrorDataReceived += (_, args) => WriteLine(args.Data);
            try
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (InvalidOperationException)
            {
                // Process may have exited before async capture starts; cleanup still owns the handle.
            }
        }

        private void WriteLine(string line)
        {
            if (line == null || _logWriter == null)
            {
                return;
            }

            lock (_logWriter)
            {
                _logWriter.WriteLine(line);
            }
        }
    }
}
