using System;
using System.Collections.Generic;

namespace CanvasDesktop;

/// <summary>
/// Lays every managed window out in a non-overlapping grid inside the current
/// viewport (Alt+G), so you can arrange a cluttered canvas without dragging
/// windows around by hand. Pinned, minimized and maximized windows are left
/// alone. Delegates the geometry to <see cref="AutoSpreadLayout"/>.
/// </summary>
internal sealed class WindowSpreader
{
    private const double PaddingScreenPx = 24.0;

    private readonly Canvas _canvas;
    private readonly WindowManager _wm;
    private readonly IScreens _screens;

    public WindowSpreader(Canvas canvas, WindowManager wm, IScreens screens)
    {
        _canvas = canvas;
        _wm = wm;
        _screens = screens;
    }

    public void SpreadCurrentViewport()
    {
        var byZ = new List<(IntPtr hWnd, long z, double w, double h)>();
        foreach (var (hWnd, r) in _canvas.Windows)
        {
            if (r.State != WindowState.Normal || r.Pinned) continue;
            byZ.Add((hWnd, r.ZOrder, r.W, r.H));
        }
        if (byZ.Count == 0) return;

        byZ.Sort((a, b) => a.z.CompareTo(b.z));

        var input = new List<(IntPtr, double, double)>(byZ.Count);
        foreach (var e in byZ)
            input.Add((e.hWnd, e.w, e.h));

        // Map the primary working area (excludes the taskbar) into world space
        // at the current camera so windows land in the visible viewport.
        double zoom = _canvas.Zoom;
        var wa = _screens.PrimaryWorkingArea;
        var (vx, vy) = _canvas.ScreenToWorld(wa.X, wa.Y);
        var viewport = (vx, vy, wa.Width / zoom, wa.Height / zoom);

        var layout = AutoSpreadLayout.Arrange(input, viewport, PaddingScreenPx / zoom);
        foreach (var (hWnd, x, y, w, h) in layout)
            _canvas.SetWindow(hWnd, x, y, w, h);

        _wm.Reproject();
        _canvas.Commit();
    }
}
