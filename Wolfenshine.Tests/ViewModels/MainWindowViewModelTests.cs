// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using NUnit.Framework;
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
}
