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

namespace Wolfenshine.ViewModels;

/// <summary>
/// Supplies the initial state for the main Wolfenshine window.
/// </summary>
/// <remarks>
/// The native viewport dimensions establish the original Wolfenstein 3D rendering target.
/// </remarks>
public sealed class MainWindowViewModel : ViewModelBase
{
    public string Title => "Wolfenshine";
    public string StatusText => "Software renderer foundation";
    public int NativeViewportWidth => 320;
    public int NativeViewportHeight => 200;
}
