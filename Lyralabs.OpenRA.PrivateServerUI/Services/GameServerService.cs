﻿using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Lyralabs.OpenRA.PrivateServerUI.Services
{
    public class GameServerService
    {
        private readonly List<GameServerStartInfo> servers = new();
        private readonly string launchScriptDirectory;
        private readonly AppSettings appSettings;

        private volatile int portOffset = 0;

        public GameServerService(AppSettings appSettings)
        {
            this.appSettings = appSettings;
            this.launchScriptDirectory = new FileInfo(this.appSettings.LaunchScriptPath).Directory.FullName;
        }

        public List<GameServerStartInfo> GetRunningServers()
        {
            return this.servers
                .Where(x => x.StopAt > DateTime.Now)
                .ToList();
        }

        public int StartNewInstance(GameServerOptions options)
        {
            options.ListenPort = this.GetFreePort();

            var info = new GameServerStartInfo
            {
                Options = options,
                StartedAt = DateTime.Now,
                StopAt = DateTime.Now.Add(options.RunDuration),
                Thread = new Thread(this.ServerKiller)
            };

            options.EngineDir = this.appSettings.GameDir ?? this.launchScriptDirectory;

            var psi = new ProcessStartInfo()
            {
                FileName = this.appSettings.LaunchScriptPath,
                WorkingDirectory = this.launchScriptDirectory,
                UserName = this.appSettings.User,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false
            };

            this.SetArguments(psi.ArgumentList, options);

            Debug.WriteLine($"launching [PWD={psi.WorkingDirectory}]: {psi.FileName} {String.Join(" ", psi.ArgumentList)}");

            //if (Debugger.IsAttached == false)
            {
                info.Process = Process.Start(psi);
                info.Process.EnableRaisingEvents = true;

                info.Process.BeginOutputReadLine();
                info.Process.BeginErrorReadLine();

                info.Process.OutputDataReceived += (s, e) =>
                {
                    info.ProcessOutput.AppendLine(e.Data);
                    Console.WriteLine(e.Data);
                    Debug.WriteLine(e.Data);
                };
                info.Process.ErrorDataReceived += (s, e) =>
                {
                    info.ProcessOutput.AppendLine(e.Data);
                    Console.WriteLine(e.Data);
                    Debug.WriteLine(e.Data);
                };
            }

            this.servers.Add(info);
            info.Thread.Start(info);

            return options.ListenPort;
        }

        private int GetFreePort()
        {
            return 42000 + this.portOffset++;
        }

        private void SetArguments(Collection<string> arguments, GameServerOptions options)
        {
            var defaults = new GameServerOptions();

            options.GetType().GetProperties()
                .Where(x => x.CanRead == true)
                .Select(x => new
                {
                    Name = x.Name,
                    Value = this.ConvertArgument(x.GetValue(options)),
                    DefaultValue = this.ConvertArgument(x.GetValue(defaults)),
                    ParameterPrefix = x.GetCustomAttributes(typeof(ParameterPrefixAttribute), inherit: false).OfType<ParameterPrefixAttribute>().SingleOrDefault()
                })
                .Where(x => x.ParameterPrefix != null)
                .ToList()
                .ForEach(x =>
                {
                    arguments.Add(String.Concat(x.ParameterPrefix.Prefix, ".", x.Name, "=", x.Value));
                    //arguments.Add(String.Concat(x.ParameterPrefix.Prefix, ".", x.Name, "=", "\"", x.Value, "\""));
                });
        }

        private string ConvertArgument(object value)
        {
            if (value is bool b)
            {
                return b == true ? Boolean.TrueString : Boolean.FalseString;
            }
            else
            {
                return value?.ToString() ?? String.Empty;
            }
        }

        private void ServerKiller(object optionsObject)
        {
            if (optionsObject is GameServerStartInfo info)
            {
                var wait = info.StopAt - DateTime.Now;
                if (wait.TotalSeconds > 1)
                {
                    Thread.Sleep(wait);
                }

                info.Process.Kill(entireProcessTree: true);
                this.servers.Remove(info);
            }
        }
    }
}
