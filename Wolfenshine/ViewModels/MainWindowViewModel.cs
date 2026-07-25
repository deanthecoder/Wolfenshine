// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.ViewModels;
using Wolfenshine.Game;
using Wolfenshine.Graphics;
using Wolfenshine.Maps;
using Wolfenshine.Rendering;
using Wolfenshine.Resources;

namespace Wolfenshine.ViewModels;

/// <summary>
/// Supplies the initial state for the main Wolfenshine window.
/// </summary>
/// <remarks>
/// The native viewport remains 320 x 200, while its presentation size accounts for DOS-era non-square pixels.
/// </remarks>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly GameSession m_gameSession;
    private RaycastCamera m_camera;

    public MainWindowViewModel()
        : this(new WolfensteinDataNotFoundException(
            WolfensteinResourceLocator.GetDefaultDirectory(),
            WolfensteinResources.FileNames.Values.ToArray()))
    {
    }

    public MainWindowViewModel(
        WolfensteinResources resources,
        WolfensteinMapSet maps,
        WolfensteinWallTextures wallTextures = null,
        WolfensteinPalette palette = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(maps);
        Resources = resources;
        Maps = maps;
        WallTextures = wallTextures;
        Palette = palette;
        SelectedMap = maps.Maps.FirstOrDefault();
        if (SelectedMap != null)
        {
            m_camera = RaycastCamera.FromPlayerStart(SelectedMap);
            m_gameSession = new GameSession(SelectedMap, m_camera);
        }
        StatusText = SelectedMap == null
            ? "Wolfenstein 3D data loaded, but it contains no maps"
            : $"{SelectedMap.Name} · arrows move and turn · Space opens doors";
    }

    public MainWindowViewModel(WolfensteinDataNotFoundException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        StatusText = "Wolfenstein 3D data files were not found";
        DataErrorMessage =
            $"Copy the required original game and palette files into:\n{exception.Directory.FullName}\n\n" +
            $"Missing: {string.Join(", ", exception.MissingFileNames)}";
    }

    public string Title => "Wolfenshine";
    public WolfensteinResources Resources { get; }
    public WolfensteinMapSet Maps { get; }
    public WolfensteinMap SelectedMap { get; }
    public RaycastCamera Camera => m_camera;
    public WolfensteinDoors Doors => m_gameSession?.Doors;
    public WolfensteinWallTextures WallTextures { get; }
    public WolfensteinPalette Palette { get; }
    public string StatusText { get; }
    public string DataErrorMessage { get; }
    public bool HasGameData => Resources != null;
    public int NativeViewportWidth => 320;
    public int NativeViewportHeight => 200;
    public int PresentationViewportWidth => 320;
    public int PresentationViewportHeight => 240;

    public void UpdateGame(double elapsedSeconds, PlayerInput input)
    {
        if (m_gameSession?.Update(elapsedSeconds, input) == true)
            SetField(ref m_camera, m_gameSession.Camera, nameof(Camera));
    }
}
