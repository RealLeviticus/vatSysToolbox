using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Win32;

namespace vatSysToolbox;

/// <summary>
/// Sets how Windows scales vatSys.
/// </summary>
public static class DpiAwareness
{
    /// <summary>
    /// Makes sure that Windows runs vatSys as a DPI-unaware application, so that WPF content has the correct scale.
    /// If the setting changes, this method restarts vatSys.
    /// </summary>
    public static void EnsureDpiAwareness()
    {
        if (!TryGetVatSysExecutablePath(out var vatSysExecutablePath))
            return;

        const string registryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        const string dpiValue = "DPIUNAWARE";

        using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: false);

        // If already set, exit early
        if (key?.GetValue(vatSysExecutablePath) is string existingValue && existingValue.Contains(dpiValue))
            return;

        // Set the registry key
        using var writableKey = Registry.CurrentUser.OpenSubKey(registryPath, writable: true)
                                ?? Registry.CurrentUser.CreateSubKey(registryPath);

        writableKey!.SetValue(vatSysExecutablePath, dpiValue, RegistryValueKind.String);

        // Restart vatSys to apply the DPI setting
        RestartVatSys();
    }

    static void RestartVatSys()
    {
        if (!TryGetVatSysExecutablePath(out var vatSysExecutablePath))
            return;

        System.Diagnostics.Process.Start(vatSysExecutablePath);
        Environment.Exit(0);
    }

    static bool TryGetVatSysInstallationPath([NotNullWhen(true)] out string? installationPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Sawbe\vatSys");
        installationPath = key?.GetValue("Path") as string;
        return !string.IsNullOrEmpty(installationPath) && Directory.Exists(installationPath);
    }

    static bool TryGetVatSysExecutablePath([NotNullWhen(true)] out string? executablePath)
    {
        try
        {
            if (!TryGetVatSysInstallationPath(out var installationPath))
            {
                executablePath = null;
                return false;
            }

            executablePath = Path.Combine(installationPath, "bin", "vatSys.exe");
            return File.Exists(executablePath);
        }
        catch
        {
            executablePath = null;
            return false;
        }
    }
}
