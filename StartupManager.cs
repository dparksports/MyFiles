using System;
using System.Diagnostics;
using System.IO;

namespace FileLister
{
    public static class StartupManager
    {
        private const string TaskName = "MyFilesAutoStart";

        public static bool IsRegistered()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks", $"/Query /TN \"{TaskName}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void Register()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;

                // /SC ONLOGON triggers at user login
                // /TR command to run
                // /F forces overwrite
                // /RL HIGHEST is optional, but often good for tools. Default is LIMITED.
                // Keeping it simple with default privileges for now as app doesn't request Admin.
                
                string command = $"/Create /SC ONLOGON /TN \"{TaskName}\" /TR \"'{exePath}'\" /F";
                
                var psi = new ProcessStartInfo("schtasks", command)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                
                Process.Start(psi)?.WaitForExit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering task: {ex.Message}");
            }
        }

        public static void Unregister()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks", $"/Delete /TN \"{TaskName}\" /F")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process.Start(psi)?.WaitForExit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing task: {ex.Message}");
            }
        }
    }
}
