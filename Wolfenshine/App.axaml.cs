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
using Avalonia.Threading;
using DTC.Core;
using Wolfenshine.Audio;
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
            WolfensteinResources audioResources = null;
            IReadOnlyList<WolfensteinMusicTrack> musicTracks = null;
            var settings = new WolfenshineSettings();
            try
            {
                var resources = WolfensteinResourceLocator.LoadDefault();
                var maps = WolfensteinMapLoader.Load(resources);
                var wallTextures = WolfensteinVSwapLoader.LoadWallTextures(resources);
                var sprites = WolfensteinVSwapLoader.LoadSprites(resources);
                var palette = WolfensteinPaletteLoader.Load();
                var hudGraphics = WolfensteinGraphicsLoader.LoadHudGraphics(resources);
                var intermissionGraphics = WolfensteinGraphicsLoader.LoadIntermissionGraphics(resources);
                var difficultyGraphics = WolfensteinGraphicsLoader.LoadDifficultyGraphics(resources);
                try
                {
                    musicTracks = WolfensteinMusicLoader.Load(resources);
                    audioResources = resources;
                }
                catch (Exception exception)
                {
                    Logger.Instance.Warn($"Music loading failed; continuing without audio: {exception.Message}");
                }
                viewModel = new MainWindowViewModel(
                    resources,
                    maps,
                    wallTextures,
                    palette,
                    sprites.PistolReady,
                    sprites,
                    hudGraphics,
                    null,
                    settings,
                    intermissionGraphics,
                    difficultyGraphics);
                Logger.Instance.Info("Waiting for the player to select a difficulty.");
            }
            catch (WolfensteinDataNotFoundException exception)
            {
                settings.Dispose();
                viewModel = new MainWindowViewModel(exception);
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            if (audioResources != null)
                _ = InitializeAudioAsync(viewModel, audioResources, musicTracks);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeAudioAsync(
        MainWindowViewModel viewModel,
        WolfensteinResources resources,
        IReadOnlyList<WolfensteinMusicTrack> musicTracks)
    {
        try
        {
            var sounds = await WolfensteinSoundLoader.LoadAsync(resources).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
                viewModel.AttachAudioPlayer(new WolfensteinAudioPlayer(sounds, musicTracks)));
        }
        catch (Exception exception)
        {
            Logger.Instance.Warn($"Sound initialization failed; continuing without audio: {exception.Message}");
        }
    }
}
