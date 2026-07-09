using Xunit;
using CanvasDesktop;

namespace CanvasDesktop.Tests;

public class CameraGlideTests
{
    [Fact]
    public void BeforeStart_IsInactiveAndReportsDone()
    {
        var glide = new CameraGlide(new FakeClock());
        var (x, y, done) = glide.Sample();
        Assert.False(glide.IsActive);
        Assert.True(done);
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void Start_AtTimeZero_ReturnsStartPosition()
    {
        var clock = new FakeClock();
        var glide = new CameraGlide(clock);
        glide.Start(100, 200, 900, 600, durationMs: 200);

        var (x, y, done) = glide.Sample();
        Assert.Equal(100, x, 3);
        Assert.Equal(200, y, 3);
        Assert.False(done);
        Assert.True(glide.IsActive);
    }

    [Fact]
    public void Midway_IsPastLinearMidpoint_DueToEaseOut()
    {
        var clock = new FakeClock();
        var glide = new CameraGlide(clock);
        glide.Start(0, 0, 1000, 0, durationMs: 200);

        clock.Advance(100); // halfway in time
        var (x, _, done) = glide.Sample();

        // Ease-out cubic at t=0.5 is 1-0.5^3 = 0.875, so well past the linear 500.
        Assert.False(done);
        Assert.InRange(x, 800, 900);
    }

    [Fact]
    public void AtOrPastDuration_SnapsToTargetAndDeactivates()
    {
        var clock = new FakeClock();
        var glide = new CameraGlide(clock);
        glide.Start(0, 0, 1000, 500, durationMs: 200);

        clock.Advance(250); // past the end
        var (x, y, done) = glide.Sample();

        Assert.Equal(1000, x, 3);
        Assert.Equal(500, y, 3);
        Assert.True(done);
        Assert.False(glide.IsActive);
    }

    [Fact]
    public void Cancel_StopsGlideAndReportsTarget()
    {
        var clock = new FakeClock();
        var glide = new CameraGlide(clock);
        glide.Start(0, 0, 1000, 0, durationMs: 200);

        clock.Advance(50);
        glide.Cancel();

        Assert.False(glide.IsActive);
        var (x, _, done) = glide.Sample();
        Assert.True(done);
        Assert.Equal(1000, x, 3); // Target reported after cancel
    }

    [Fact]
    public void Target_ReflectsMostRecentStart()
    {
        var glide = new CameraGlide(new FakeClock());
        glide.Start(0, 0, 42, 84, durationMs: 200);
        Assert.Equal((42.0, 84.0), glide.Target);
    }
}
