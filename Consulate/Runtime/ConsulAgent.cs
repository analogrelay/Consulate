using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NLog;

namespace Consulate.Runtime
{
    public class ConsulAgent
    {
        private static readonly string DefaultConsulPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "consul.exe");
        private static readonly Logger ConsulAgentTrace = LogManager.GetLogger(typeof(ConsulAgent).FullName + ":EXEC");

        private string _consulPath;
        private Process _consulProcess;
        private ConsulConfig _config;
        private IEnumerable<IntPtr> _consulHwnds;
        private Logger _logger;

        public int ProcessId { get { return _consulProcess.Id; } }
        public bool IsRunning { get { return _consulProcess != null && !_consulProcess.HasExited; } }

        public ConsulAgent(string consulPath, ConsulConfig config, Process consulProcess)
        {
            _consulPath = consulPath;
            _config = config;
            _logger = LogManager.GetLogger(typeof(ConsulAgent).FullName + ":PID" + consulProcess.Id);
            _consulProcess = consulProcess;
            _consulProcess.Exited += (_, __) =>
            {
                _logger.Info("Shutdown");
            };
            _consulProcess.ErrorDataReceived += (s, a) =>
            {
                _logger.Error(a.Data);
            };
            _consulProcess.OutputDataReceived += (s2, a2) =>
            {
                _logger.Info(a2.Data);
            };
            _consulProcess.BeginErrorReadLine();
            _consulProcess.BeginOutputReadLine();
        }

        public void WaitForExit()
        {
            _consulProcess.WaitForExit();
        }

        public void Shutdown()
        {
            // Use Consul RPC to shut our agent down
            Exec(_consulPath, "leave -rpc-addr " + _config.RpcAddr);
        }

        /// <summary>
        /// Starts the consul agent in server mode and returns a ConsulServer to manage it
        /// </summary>
        public static ConsulAgent LaunchServer(bool bootstrap, ConsulConfig config) { return LaunchServer(DefaultConsulPath, bootstrap, config, Enumerable.Empty<IPAddress>()); }

        /// <summary>
        /// Starts the consul agent in server mode and returns a ConsulServer to manage it
        /// </summary>
        public static ConsulAgent LaunchServer(bool bootstrap, ConsulConfig config, IEnumerable<IPAddress> join) { return LaunchServer(DefaultConsulPath, bootstrap, config, join); }

        /// <summary>
        /// Starts the consul agent in server mode and returns a ConsulServer to manage it
        /// </summary>
        public static ConsulAgent LaunchServer(string consulExePath, bool bootstrap, ConsulConfig config) { return LaunchServer(consulExePath, bootstrap, config, Enumerable.Empty<IPAddress>()); }

        /// <summary>
        /// Starts the consul agent in server mode and returns a ConsulServer to manage it
        /// </summary>
        public static ConsulAgent LaunchServer(string consulExePath, bool bootstrap, ConsulConfig config, IEnumerable<IPAddress> join)
        {
            // Write the config file
            var configFile = Path.Combine(Path.GetTempPath(), "consul-config.json");
            File.WriteAllText(configFile, config.ToJson());

            // Build args
            string args = "agent -server -config-file \"" + configFile + "\"";
            if (bootstrap)
            {
                args += " -bootstrap";
            }
            if (join.Any())
            {
                args += String.Join(" ", join.Select(a => " -join " + a.ToString()));
            }

            // Launch!
            return Launch(consulExePath, config, args);
        }

        public static Task<int> Exec(string consulExePath, string args)
        {
            ProcessStartInfo psi = CreateStartInfo(consulExePath, args);
            ConsulAgentTrace.Info("Launching: {0} {1}", consulExePath, args);
            var process = Process.Start(psi);
            process.ErrorDataReceived += (s, a) =>
            {
                ConsulAgentTrace.Error(a.Data);
            };
            process.OutputDataReceived += (s2, a2) =>
            {
                ConsulAgentTrace.Info(a2.Data);
            };
            process.EnableRaisingEvents = true;
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            process.Exited += (_, __) => tcs.TrySetResult(process.ExitCode);
            return tcs.Task;
        }

        private static ConsulAgent Launch(string consulExePath, ConsulConfig config, string args)
        {
            ConsulAgentTrace.Info("Launching: {0} {1}", consulExePath, args);
            return new ConsulAgent(consulExePath, config, Process.Start(CreateStartInfo(consulExePath, args)));
        }

        private static ProcessStartInfo CreateStartInfo(string consulExePath, string args)
        {
            return new ProcessStartInfo(consulExePath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false
            };
        }
    }
}
