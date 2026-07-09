using System;
using Xunit;
using CanvasDesktop;

namespace CanvasDesktop.Tests;

public class PinTests
{
    private static (Canvas canvas, FakeWindowApi api, WindowManager wm) Create()
    {
        var canvas = new Canvas();
        var api = new FakeWindowApi();
        var wm = new WindowManager(canvas, api, new FakeAppConfig(), new FakeInputRouter(), new FakeClock());
        return (canvas, api, wm);
    }

    // ==================== Canvas ====================

    [Fact]
    public void PinWindow_MarksPinnedAndStoresRect()
    {
        var canvas = new Canvas();
        canvas.SetWindow((IntPtr)1, 100, 200, 800, 600);

        Assert.False(canvas.IsPinned((IntPtr)1));
        canvas.PinWindow((IntPtr)1, 10, 20, 320, 240);

        Assert.True(canvas.IsPinned((IntPtr)1));
    }

    [Fact]
    public void UnpinWindow_ClearsPinAndSetsWorld()
    {
        var canvas = new Canvas();
        canvas.SetWindow((IntPtr)1, 100, 200, 800, 600);
        canvas.PinWindow((IntPtr)1, 10, 20, 320, 240);

        canvas.UnpinWindow((IntPtr)1, 500, 600, 320, 240);

        Assert.False(canvas.IsPinned((IntPtr)1));
        var r = canvas.Windows[(IntPtr)1];
        Assert.Equal(500, r.X);
        Assert.Equal(600, r.Y);
        Assert.Equal(320, r.W);
    }

    [Fact]
    public void PinChanged_FiresOnPinAndUnpin()
    {
        var canvas = new Canvas();
        canvas.SetWindow((IntPtr)1, 0, 0, 100, 100);
        int fires = 0;
        canvas.PinChanged += _ => fires++;

        canvas.PinWindow((IntPtr)1, 0, 0, 100, 100);
        canvas.UnpinWindow((IntPtr)1, 0, 0, 100, 100);

        Assert.Equal(2, fires);
    }

    [Fact]
    public void PinnedWindow_ExcludedFromWorldExtents()
    {
        var canvas = new Canvas();
        canvas.SetWindow((IntPtr)1, 100, 100, 200, 200);
        canvas.SetWindow((IntPtr)2, 9000, 9000, 200, 200);
        canvas.PinWindow((IntPtr)2, 0, 0, 200, 200);

        var ext = canvas.GetWorldExtents();
        Assert.NotNull(ext);
        // The far-away pinned window must not stretch the extents.
        Assert.Equal(300, ext!.Value.maxX);
        Assert.Equal(300, ext.Value.maxY);
    }

    // ==================== WindowManager ====================

    [Fact]
    public void Reproject_PinnedWindow_UsesFixedScreenRect_IgnoringCamera()
    {
        var (canvas, api, wm) = Create();
        canvas.SetWindow((IntPtr)1, 100, 200, 800, 600);
        api.AddWindow((IntPtr)1, 100, 200, 800, 600);
        canvas.PinWindow((IntPtr)1, 40, 50, 320, 240);

        // Pan the camera far away; a pinned window must not move.
        canvas.SetCamera(5000, 5000);
        wm.Reproject();

        var item = api.LastBatch[0];
        Assert.Equal(40, item.Rect.X);
        Assert.Equal(50, item.Rect.Y);
        Assert.Equal(320, item.Rect.W);
        Assert.Equal(240, item.Rect.H);
        Assert.False(item.PosOnly); // pinned moves apply size too
    }

    [Fact]
    public void Reproject_PinnedWindow_NotClippedEvenWhenCameraFarAway()
    {
        var (canvas, api, wm) = Create();
        canvas.SetWindow((IntPtr)1, 100, 200, 800, 600);
        api.AddWindow((IntPtr)1, 100, 200, 800, 600);
        canvas.PinWindow((IntPtr)1, 40, 50, 320, 240);

        canvas.SetCamera(9000, 9000);
        wm.Reproject();

        Assert.DoesNotContain((IntPtr)1, api.ClippedWindows);
    }

    // ==================== WindowPinner ====================

    [Fact]
    public void TogglePin_PinsForegroundWindow_ThenUnpins()
    {
        var (canvas, api, wm) = Create();
        canvas.SetWindow((IntPtr)7, 100, 200, 800, 600);
        api.AddWindow((IntPtr)7, 300, 400, 640, 480);
        api.ForegroundWindow = (IntPtr)7;

        var pinner = new WindowPinner(canvas, wm, api);

        pinner.TogglePinForeground();
        Assert.True(canvas.IsPinned((IntPtr)7));

        pinner.TogglePinForeground();
        Assert.False(canvas.IsPinned((IntPtr)7));
    }

    [Fact]
    public void TogglePin_IgnoresUnmanagedForegroundWindow()
    {
        var (canvas, api, wm) = Create();
        api.AddWindow((IntPtr)9, 0, 0, 100, 100);
        api.ForegroundWindow = (IntPtr)9; // not in canvas

        var pinner = new WindowPinner(canvas, wm, api);
        pinner.TogglePinForeground();

        Assert.False(canvas.IsPinned((IntPtr)9));
    }
}
