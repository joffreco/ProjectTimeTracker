using Microsoft.Win32;

namespace ProjectTimeTracker.Infrastructure;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ProjectTimeTracker";

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (key is null)
        {
            return false;
        }

        object? value = key.GetValue(ValueName);
        return value is string s && !string.IsNullOrWhiteSpace(s);
    }

    public static void Enable(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return;
        }

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                                ?? throw new InvalidOperationException("Cannot open HKCU Run key.");
        key.SetValue(ValueName, $"\"{executablePath}\" --tray", RegistryValueKind.String);
    }

    public static void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

