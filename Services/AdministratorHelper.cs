using System.Security.Principal;
using System.Windows;

namespace iMonitor.Services;

public static class AdministratorHelper
{
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static void ShowAdministratorWarning()
    {
        if (!IsRunningAsAdministrator())
        {
            var result = MessageBox.Show(
                "iMonitor requires Administrator privileges to create virtual monitors.\n\n" +
                "Virtual monitor features will not work without elevated permissions.\n" +
                "Device detection and system tray functionality will still work.\n\n" +
                "Would you like to restart iMonitor as Administrator?",
                "Administrator Privileges Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                RestartAsAdministrator();
            }
        }
    }

    public static void RestartAsAdministrator()
    {
        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "",
                Verb = "runas"
            };

            System.Diagnostics.Process.Start(processInfo);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to restart as Administrator: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}