// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core;
using NUnit.Framework;
using Wolfenshine.Game;
using Wolfenshine.Maps;
using Wolfenshine.Resources;
using Wolfenshine.ViewModels;

namespace Wolfenshine.Tests.ViewModels;

/// <summary>
/// Verifies the initial state exposed by the main window.
/// </summary>
/// <remarks>
/// These checks preserve the original render target while the renderer is developed.
/// </remarks>
public sealed class MainWindowViewModelTests
{
    [Test]
    public void CheckInitialViewportSize()
    {
        var viewModel = new MainWindowViewModel();

        Assert.That(viewModel.NativeViewportWidth, Is.EqualTo(320));
        Assert.That(viewModel.NativeViewportHeight, Is.EqualTo(200));
        Assert.That(viewModel.PresentationViewportWidth, Is.EqualTo(320));
        Assert.That(viewModel.PresentationViewportHeight, Is.EqualTo(240));
    }

    [Test]
    public void CheckInitialWindowTitle()
    {
        var viewModel = new MainWindowViewModel();

        Assert.That(viewModel.Title, Is.EqualTo("Wolfenshine"));
    }

    [Test]
    public void GivenMissingGameDataCheckErrorIsShown()
    {
        var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "missing-wolfenshine-data"));
        var exception = new WolfensteinDataNotFoundException(directory, ["VSWAP.WL6"]);

        var viewModel = new MainWindowViewModel(exception);

        Assert.That(viewModel.HasGameData, Is.False);
        Assert.That(viewModel.StatusText, Does.Contain("not found"));
        Assert.That(viewModel.DataErrorMessage, Does.Contain("VSWAP.WL6"));
    }

    [Test]
    public void GivenLoadedMapsCheckFirstMapIsSelected()
    {
        var map = new WolfensteinMap(0, "E1M1", 1, 1, new ushort[] { 1 }, new ushort[] { 19 });
        var mapSet = new WolfensteinMapSet(0xABCD, new[] { map });
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        var resources = WolfensteinResources.Load(directory);

        var viewModel = new MainWindowViewModel(resources, mapSet);

        Assert.That(viewModel.SelectedMap, Is.SameAs(map));
        Assert.That(viewModel.Camera, Is.Null);
        Assert.That(viewModel.IsSelectingDifficulty, Is.True);
        Assert.That(viewModel.StatusText, Does.Contain("Select difficulty"));
    }

#if DEBUG
    [TestCase(1, 1)]
    [TestCase(-1, 2)]
    public void GivenDebugLevelSkipCheckMapSelectionWraps(int offset, int expectedMapIndex)
    {
        var maps = new[]
        {
            CreateElevatorMap(0, "E1M1"),
            CreateElevatorMap(1, "E1M2"),
            CreateElevatorMap(2, "E1M3")
        };
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        var viewModel = new MainWindowViewModel(
            WolfensteinResources.Load(directory),
            new WolfensteinMapSet(0xABCD, maps));
        StartNormalGame(viewModel);

        viewModel.SkipDebugLevel(offset);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedMap, Is.SameAs(maps[expectedMapIndex]));
            Assert.That(viewModel.Camera, Is.Not.Null);
            Assert.That(viewModel.IsSelectingDifficulty, Is.False);
        });
    }
#endif

    [Test]
    public void GivenGameUpdateCheckCameraChangeIsPublished()
    {
        var map = new WolfensteinMap(0, "E1M1", 1, 1, new ushort[] { 1 }, new ushort[] { 19 });
        var mapSet = new WolfensteinMapSet(0xABCD, new[] { map });
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        var viewModel = new MainWindowViewModel(WolfensteinResources.Load(directory), mapSet);
        StartNormalGame(viewModel);
        var originalCamera = viewModel.Camera;
        var changedProperty = string.Empty;
        viewModel.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        viewModel.UpdateGame(0.1, new PlayerInput(false, false, false, true));

        Assert.That(viewModel.Camera, Is.Not.SameAs(originalCamera));
        Assert.That(changedProperty, Is.EqualTo(nameof(MainWindowViewModel.Camera)));
    }

    [Test]
    public void GivenPauseToggledCheckGameplayFreezesUntilResumed()
    {
        var map = new WolfensteinMap(0, "E1M1", 1, 1, new ushort[] { 1 }, new ushort[] { 19 });
        var mapSet = new WolfensteinMapSet(0xABCD, new[] { map });
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        var viewModel = new MainWindowViewModel(WolfensteinResources.Load(directory), mapSet);
        StartNormalGame(viewModel);
        var originalCamera = viewModel.Camera;

        viewModel.TogglePause();
        viewModel.UpdateGame(0.1, new PlayerInput(false, false, false, true));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsPaused, Is.True);
            Assert.That(viewModel.Camera, Is.SameAs(originalCamera));
        });

        viewModel.TogglePause();
        viewModel.UpdateGame(0.1, new PlayerInput(false, false, false, true));
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsPaused, Is.False);
            Assert.That(viewModel.Camera, Is.Not.SameAs(originalCamera));
        });
    }

    [Test]
    public void GivenRendererToggledCheckEnhancedModeCanReturnToAuthenticMode()
    {
        var map = new WolfensteinMap(0, "E1M1", 1, 1, new ushort[] { 1 }, new ushort[] { 19 });
        var mapSet = new WolfensteinMapSet(0xABCD, new[] { map });
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        var viewModel = new MainWindowViewModel(WolfensteinResources.Load(directory), mapSet);
        StartNormalGame(viewModel);

        viewModel.ToggleRenderer();
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsEnhancedRendering, Is.True);
            Assert.That(viewModel.RenderModeText, Is.EqualTo("RENDERER: ENHANCED"));
        });

        viewModel.ToggleRenderer();
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsEnhancedRendering, Is.False);
            Assert.That(viewModel.RenderModeText, Is.EqualTo("RENDERER: AUTHENTIC"));
        });
    }

    [Test]
    public void GivenHardDifficultySelectedCheckHardActorsArePlaced()
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        var objects = new ushort[size * size];
        objects[(2 * size) + 2] = 19;
        objects[(1 * size) + 2] = 180;
        var map = new WolfensteinMap(0, "E1M1", size, size, walls, objects);
        var mapSet = new WolfensteinMapSet(0xABCD, new[] { map });
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        var viewModel = new MainWindowViewModel(WolfensteinResources.Load(directory), mapSet);

        viewModel.UpdateGame(0.0, new PlayerInput(false, true, false, false));
        viewModel.UpdateGame(0.0, default);
        viewModel.UpdateGame(0.0, new PlayerInput(false, false, false, false, Use: true));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSelectingDifficulty, Is.False);
            Assert.That(viewModel.Difficulty, Is.EqualTo(GameDifficulty.Hard));
            Assert.That(viewModel.Actors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void GivenElevatorCompletionCheckNextMapLoadsAndFinalMapWrapsToFirst()
    {
        var firstMap = CreateElevatorMap(0, "E1M1");
        var secondMap = CreateElevatorMap(1, "E1M2");
        var mapSet = new WolfensteinMapSet(0xABCD, new[] { firstMap, secondMap });
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        var viewModel = new MainWindowViewModel(WolfensteinResources.Load(directory), mapSet);
        StartNormalGame(viewModel);

        CompleteLevel(viewModel);
        Assert.That(viewModel.IsShowingLevelStats, Is.True);
        ContinueFromStats(viewModel);
        Assert.That(viewModel.SelectedMap, Is.SameAs(secondMap));

        viewModel.UpdateGame(0.5, default);
        CompleteLevel(viewModel);
        ContinueFromStats(viewModel);
        Assert.That(viewModel.SelectedMap, Is.SameAs(firstMap));
    }

    [Test]
    public void GivenFinalLifeLostCheckDifficultyScreenReturnsAtFirstLevel()
    {
        var firstMap = CreateDogMap(0, "E1M1");
        var secondMap = CreateElevatorMap(1, "E1M2");
        var mapSet = new WolfensteinMapSet(0xABCD, new[] { firstMap, secondMap });
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        var viewModel = new MainWindowViewModel(WolfensteinResources.Load(directory), mapSet);
        StartNormalGame(viewModel);

        for (var update = 0; update < 500 && !viewModel.IsSelectingDifficulty; update++)
            viewModel.UpdateGame(1.0, default);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSelectingDifficulty, Is.True);
            Assert.That(viewModel.IsGameOver, Is.False);
            Assert.That(viewModel.SelectedMap, Is.SameAs(firstMap));
            Assert.That(viewModel.Camera, Is.Null);
        });
    }

    private static WolfensteinMap CreateElevatorMap(int slot, string name)
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)140, size * size).ToArray();
        for (var index = 0; index < size; index++)
        {
            walls[index] = 1;
            walls[((size - 1) * size) + index] = 1;
            walls[index * size] = 1;
            walls[(index * size) + size - 1] = 1;
        }
        walls[(2 * size) + 3] = 21;
        var objects = new ushort[size * size];
        objects[(2 * size) + 2] = 20;
        return new WolfensteinMap(slot, name, size, size, walls, objects);
    }

    private static WolfensteinMap CreateDogMap(int slot, string name)
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var index = 0; index < size; index++)
        {
            walls[index] = 1;
            walls[((size - 1) * size) + index] = 1;
            walls[index * size] = 1;
            walls[(index * size) + size - 1] = 1;
        }
        var objects = new ushort[size * size];
        objects[(2 * size) + 2] = 19;
        objects[(1 * size) + 2] = 134;
        return new WolfensteinMap(slot, name, size, size, walls, objects);
    }

    private static void CompleteLevel(MainWindowViewModel viewModel)
    {
        viewModel.UpdateGame(0.0, new PlayerInput(false, false, false, false, Use: true));
        viewModel.UpdateGame(0.5, default);
    }

    private static void StartNormalGame(MainWindowViewModel viewModel)
    {
        viewModel.UpdateGame(0.0, new PlayerInput(false, false, false, false, Use: true));
        viewModel.UpdateGame(0.5, default);
    }

    private static void ContinueFromStats(MainWindowViewModel viewModel)
    {
        viewModel.UpdateGame(0.0, default);
        viewModel.UpdateGame(0.0, new PlayerInput(false, false, false, false, Attack: true));
        viewModel.UpdateGame(0.0, default);
        viewModel.UpdateGame(0.0, new PlayerInput(false, false, false, false, Attack: true));
    }
}
