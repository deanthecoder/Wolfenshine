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
using Wolfenshine.Resources;

namespace Wolfenshine.ViewModels;

/// <summary>
/// Supplies the initial state for the main Wolfenshine window.
/// </summary>
/// <remarks>
/// The native viewport dimensions establish the original Wolfenstein 3D rendering target.
/// </remarks>
public sealed class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
        : this(new WolfensteinDataNotFoundException(
            WolfensteinResourceLocator.GetDefaultDirectory(),
            WolfensteinResources.FileNames.Values.ToArray()))
    {
    }

    public MainWindowViewModel(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Resources = resources;
        StatusText = $"Wolfenstein 3D data loaded from {resources.Directory.FullName}";
    }

    public MainWindowViewModel(WolfensteinDataNotFoundException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        StatusText = "Wolfenstein 3D data files were not found";
        DataErrorMessage =
            $"Copy the original .WL6 files into:\n{exception.Directory.FullName}\n\n" +
            $"Missing: {string.Join(", ", exception.MissingFileNames)}";
    }

    public string Title => "Wolfenshine";
    public WolfensteinResources Resources { get; }
    public string StatusText { get; }
    public string DataErrorMessage { get; }
    public bool HasGameData => Resources != null;
    public int NativeViewportWidth => 320;
    public int NativeViewportHeight => 200;
}
