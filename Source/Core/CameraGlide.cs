using System;

namespace CanvasDesktop;

/// <summary>
/// Pure, time-driven interpolation of the canvas camera between two world-space
/// positions with an ease-out curve. No Win32 / timer knowledge — a driver
/// samples it each frame (see <c>AnimatedCameraGlider</c>) and applies the
/// result to the <see cref="Canvas"/>. Deterministic via an injected
/// <see cref="IClock"/> so it can be unit-tested by advancing the clock.
/// </summary>
internal sealed class CameraGlide
{
    private readonly IClock _clock;

    private double _startX, _startY;
    private double _endX, _endY;
    private long _startTick;
    private double _durationMs;
    private bool _active;

    public CameraGlide(IClock? clock = null)
    {
        _clock = clock ?? SystemClock.Instance;
    }

    public bool IsActive => _active;

    /// <summary>Target the glide is heading toward (valid while and after a run).</summary>
    public (double x, double y) Target => (_endX, _endY);

    /// <summary>Begin gliding from one world position to another over the given duration.</summary>
    public void Start(double fromX, double fromY, double toX, double toY, double durationMs)
    {
        _startX = fromX;
        _startY = fromY;
        _endX = toX;
        _endY = toY;
        _durationMs = Math.Max(1.0, durationMs);
        _startTick = _clock.TickCount64;
        _active = true;
    }

    public void Cancel() => _active = false;

    /// <summary>
    /// Current interpolated camera position for the present clock time.
    /// <paramref name="done"/> is true once the glide has reached its target;
    /// after that the glide is inactive and always reports the target.
    /// </summary>
    public (double x, double y, bool done) Sample()
    {
        if (!_active)
            return (_endX, _endY, true);

        double elapsed = _clock.TickCount64 - _startTick;
        double t = Math.Clamp(elapsed / _durationMs, 0.0, 1.0);
        double e = EaseOutCubic(t);

        double x = _startX + (_endX - _startX) * e;
        double y = _startY + (_endY - _startY) * e;

        bool done = t >= 1.0;
        if (done)
        {
            _active = false;
            return (_endX, _endY, true);
        }
        return (x, y, false);
    }

    private static double EaseOutCubic(double t)
    {
        double u = 1.0 - t;
        return 1.0 - u * u * u;
    }
}
