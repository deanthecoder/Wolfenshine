// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Controls;
using Avalonia.Input;

namespace Wolfenshine.Views;

/// <summary>
/// Hosts the Debug-only textured level overview.
/// </summary>
/// <remarks>
/// The main window owns this auxiliary view and toggles it with the M shortcut.
/// </remarks>
public sealed partial class MapWindow : Window
{
    public MapWindow() => InitializeComponent();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.M)
            return;
        Close();
        e.Handled = true;
    }
}
