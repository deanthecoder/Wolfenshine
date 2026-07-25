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
        Assert.That(viewModel.Camera, Is.Not.Null);
        Assert.That(viewModel.StatusText, Does.Contain("E1M1"));
    }

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
        var originalCamera = viewModel.Camera;
        var changedProperty = string.Empty;
        viewModel.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        viewModel.UpdateGame(0.1, new PlayerInput(false, false, false, true));

        Assert.That(viewModel.Camera, Is.Not.SameAs(originalCamera));
        Assert.That(changedProperty, Is.EqualTo(nameof(MainWindowViewModel.Camera)));
    }
}
