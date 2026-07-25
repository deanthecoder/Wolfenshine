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

namespace Wolfenshine.Views;

/// <summary>
/// Hosts Wolfenshine's game viewport and desktop chrome.
/// </summary>
/// <remarks>
/// View-specific behavior belongs here; game and rendering behavior will live outside the window.
/// </remarks>
public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
