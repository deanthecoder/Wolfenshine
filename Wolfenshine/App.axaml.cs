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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DTC.Core;
using Wolfenshine.Maps;
using Wolfenshine.Resources;
using Wolfenshine.ViewModels;
using Wolfenshine.Views;

namespace Wolfenshine;

/// <summary>
/// Configures the Wolfenshine application and its desktop lifetime.
/// </summary>
/// <remarks>
/// Application composition lives here while game and rendering systems remain independent of Avalonia.
/// </remarks>
public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Logger.Instance.SysInfo();
            Logger.Instance.Info("Starting Wolfenshine.");
            MainWindowViewModel viewModel;
            try
            {
                var resources = WolfensteinResourceLocator.LoadDefault();
                var maps = WolfensteinMapLoader.Load(resources);
                var wallTextures = WolfensteinVSwapLoader.LoadWallTextures(resources);
                var sprites = WolfensteinVSwapLoader.LoadSprites(resources);
                var palette = WolfensteinPaletteLoader.Load(resources);
                var hudGraphics = WolfensteinGraphicsLoader.LoadHudGraphics(resources);
                viewModel = new MainWindowViewModel(
                    resources,
                    maps,
                    wallTextures,
                    palette,
                    sprites.PistolReady,
                    sprites,
                    hudGraphics);
                Logger.Instance.Info(
                    $"Loaded {viewModel.StaticObjects.Count} static world objects for {viewModel.SelectedMap.Name}.");
                Logger.Instance.Info(
                    $"Placed {viewModel.Actors.Count} inert actors for {viewModel.SelectedMap.Name} on normal difficulty.");
            }
            catch (WolfensteinDataNotFoundException exception)
            {
                viewModel = new MainWindowViewModel(exception);
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
