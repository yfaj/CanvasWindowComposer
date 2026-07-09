using System;
using System.Windows.Forms;

namespace CanvasDesktop;

/// <summary>
/// Production <see cref="ICameraGlider"/>: eases the main-canvas camera to a
/// target over a short duration instead of snapping, so focusing a window from
/// the taskbar / Alt-Tab / an external app glides the desktop into view.
///
/// Drives a UI-thread <see cref="Timer"/> that samples a <see cref="CameraGlide"/>
/// each frame, applies the camera, and reprojects real windows. While gliding,
/// <see cref="WindowManager.SuspendProjection"/> is set so the throttled
/// camera-changed reproject doesn't double up with our per-frame reproject; the
/// final <see cref="Canvas.Commit"/> snaps windows to crisp integer positions.
///
/// Any mouse button press, drag, or the overview opening cancels the glide so
/// it never fights user input.
/// </summary>
internal sealed class AnimatedCameraGlider : ICameraGlider, IDisposable
{
    private const double DurationMs = 220.0;
    private const int FrameIntervalMs = 15;

    private readonly Canvas _canvas;
    private readonly WindowManager _wm;
    private readonly IScreens _screens;
    private readonly IOverviewController _overview;
    private readonly CameraGlide _glide;
    private readonly Timer _timer;

    public AnimatedCameraGlider(
        Canvas canvas,
        WindowManager wm,
        IScreens screens,
        IInputRouter input,
        IOverviewController overview,
        IClock? clock = null)
    {
        _canvas = canvas;
        _wm = wm;
        _screens = screens;
        _overview = overview;
        _glide = new CameraGlide(clock);

        _timer = new Timer { Interval = FrameIntervalMs };
        _timer.Tick += OnTick;

        input.ButtonDown  += Cancel;
        input.DragStarted += Cancel;
        overview.BeforeModeChanged += OnOverviewModeChanged;
    }

    public void GlideTo(double worldX, double worldY, double worldW, double worldH)
    {
        double zoom = _canvas.Zoom;
        var wa = _screens.PrimaryWorkingArea;
        double targetX = worldX + worldW / 2 - wa.Width / (2 * zoom);
        double targetY = worldY + worldH / 2 - wa.Height / (2 * zoom);

        _wm.SuspendProjection = true;
        _glide.Start(_canvas.CamX, _canvas.CamY, targetX, targetY, DurationMs);
        _timer.Start();
    }

    public void Cancel()
    {
        if (!_glide.IsActive && !_timer.Enabled) return;
        _timer.Stop();
        _glide.Cancel();
        _wm.SuspendProjection = false;
        _canvas.Commit();
    }

    private void OnOverviewModeChanged(OverviewMode from, OverviewMode to)
    {
        if (to != OverviewMode.Hidden)
            Cancel();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var (x, y, done) = _glide.Sample();
        _canvas.SetCamera(x, y);

        if (done)
        {
            _timer.Stop();
            _wm.SuspendProjection = false;
            _canvas.Commit();
            return;
        }

        _wm.Reproject(isAsync: true, isTransient: true);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
