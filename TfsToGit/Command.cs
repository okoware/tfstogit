using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace TfsToGit
{
    internal static class Command
    {
        public static void ExecuteTfCommand(string arguments, string workingDirectory = null)
        {
            void errorProcessor(int exitCode)
            {
                switch (exitCode)
                {
                    case 0:
                    case 100:
                        break;
                    default:
                        throw new Exception($"ERROR CODE [{exitCode}].  Failed to execute: {arguments}");
                }
            }

            ExecuteCommand(Properties.Settings.Default.TfsExecutablePath, arguments, workingDirectory, errorProcessor);
        }

        public static void ExecuteRobocopyCommand(string arguments, string workingDirectory = null)
        {
            void errorProcessor(int exitCode)
            {
                // https://ss64.com/nt/robocopy-exit.html
                if (exitCode < 8) return;
                throw new Exception($"ERROR CODE [{exitCode}].  Failed to execute: {arguments}");
            }

            ExecuteCommand(Properties.Settings.Default.RobocopyPath, arguments, workingDirectory, processError: errorProcessor);
        }

        public static void ExecuteGitCommand(string arguments, string workingDirectory, Action<ProcessStartInfo> configureProcess = null)
        {
            void errorProcessor(int exitCode)
            {
                if (exitCode == 0) return;
                throw new Exception($"ERROR CODE [{exitCode}].  Failed to execute: {arguments}");
            }

            ExecuteCommand(Properties.Settings.Default.GitPath, arguments, workingDirectory, processError: errorProcessor, configureProcess: configureProcess);
        }

        private static void ExecuteCommand(string processPath, string arguments, string workingDirectory = null, Action<int> processError = null, Action<ProcessStartInfo> configureProcess = null)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = processPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
                configureProcess?.Invoke(process.StartInfo);

                var output = new StringBuilder();
                var error = new StringBuilder();
                var isTfsServicesUnavailble = false;

                using (var outputWaitHandle = new AutoResetEvent(false))
                using (var errorWaitHandle = new AutoResetEvent(false))
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            outputWaitHandle.Set();
                        }
                        else
                        {
                            Write(Console.Out, ConsoleColor.Yellow, e.Data);
                            output.AppendLine(e.Data);
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            errorWaitHandle.Set();
                        }
                        else
                        {
                            // HACK
                            isTfsServicesUnavailble = isTfsServicesUnavailble || e.Data.Contains("TF400324: Team Foundation services are not available from server");
                            Write(Console.Error, ConsoleColor.Red, e.Data);
                            error.AppendLine(e.Data);
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var timeout = TimeSpan.FromMinutes(7);
                    if (process.WaitForExit((int)timeout.TotalMilliseconds) &&
                        outputWaitHandle.WaitOne(timeout) &&
                        errorWaitHandle.WaitOne(timeout))
                    {
                        processError?.Invoke(process.ExitCode);

                        if (isTfsServicesUnavailble)
                        {
                            throw new Exception($"Team Foundation services are not available from server: {arguments}");
                        }
                    }
                    else
                    {
                        throw new Exception($"Process hung: {arguments}");

                    }
                }
            }
        }

        private static void Write(TextWriter writer, ConsoleColor color, string message)
        {
            try
            {
                Console.ForegroundColor = color;
                writer.WriteLine(message);
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
}
