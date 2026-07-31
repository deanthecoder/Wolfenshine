// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using DTC.Core;

namespace Wolfenshine;

/// <summary>
/// Starts the Wolfenshine desktop application.
/// </summary>
/// <remarks>
/// Avalonia startup remains isolated here so the application can later expose test and tooling hosts.
/// </remarks>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var resetRequested = args.Any(argument =>
            argument.Equals("--reset", StringComparison.OrdinalIgnoreCase));
        if (resetRequested)
        {
            var userDataDirectory = WolfenshineUserData.GetDirectory();
            WolfenshineUserData.Reset(userDataDirectory);
            Logger.Instance.Info("Reset saved Wolfenshine preferences and installed game data.");
        }

        var avaloniaArguments = args
            .Where(argument => !argument.Equals("--reset", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(avaloniaArguments);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
