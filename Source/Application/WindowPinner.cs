using System;

namespace CanvasDesktop;

/// <summary>
/// Toggles "pin" state on the foreground window. A pinned window is anchored to
/// a fixed screen rectangle and no longer tracks the camera — it stays put
/// (PiP-style) while the canvas pans/zooms, e.g. keep a video visible while you
/// navigate. Unpinning drops it back onto the canvas at its current location.
/// </summary>
internal sealed class WindowPinner
{
    private readonly Canvas _canvas;
    private readonly WindowManager _wm;
    private readonly IWindowApi _win32;

    public WindowPinner(Canvas canvas, WindowManager wm, IWindowApi win32)
    {
        _canvas = canvas;
        _wm = wm;
        _win32 = win32;
    }

    /// <summary>Pin the foreground window if unpinned, or unpin it if already pinned.</summary>
    public void TogglePinForeground()
    {
        IntPtr hWnd = _win32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero || !_canvas.HasWindow(hWnd))
            return;
        if (_canvas.GetWindowState(hWnd) != WindowState.Normal)
            return;

        var (sx, sy, sw, sh) = _win32.GetWindowRect(hWnd);

        if (_canvas.IsPinned(hWnd))
        {
            // Drop back onto the canvas where it currently sits on screen.
            var (wx, wy) = _canvas.ScreenToWorld(sx, sy);
            var (ww, wh) = _canvas.ScreenToWorldSize(sw, sh);
            _canvas.UnpinWindow(hWnd, wx, wy, ww, wh);
        }
        else
        {
            _canvas.PinWindow(hWnd, sx, sy, sw, sh);
        }

        _wm.Reproject();
        _canvas.Commit();
    }
}
