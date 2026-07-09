using System;
using System.Collections.Generic;
using Xunit;
using CanvasDesktop;

namespace CanvasDesktop.Tests;

public class AutoSpreadLayoutTests
{
    private static List<(IntPtr, double, double)> Wins(int n, double w = 400, double h = 300)
    {
        var list = new List<(IntPtr, double, double)>();
        for (int i = 0; i < n; i++) list.Add(((IntPtr)(i + 1), w, h));
        return list;
    }

    [Fact]
    public void Arrange_EmptyInput_ReturnsEmpty()
    {
        var r = AutoSpreadLayout.Arrange(new List<(IntPtr, double, double)>(), (0, 0, 1920, 1080), 20);
        Assert.Empty(r);
    }

    [Fact]
    public void Arrange_PreservesInputOrder()
    {
        var r = AutoSpreadLayout.Arrange(Wins(4), (0, 0, 1920, 1080), 20);
        Assert.Equal((IntPtr)1, r[0].hWnd);
        Assert.Equal((IntPtr)4, r[3].hWnd);
    }

    [Fact]
    public void Arrange_FourWindows_MakesTwoByTwoGrid_NoOverlap()
    {
        var r = AutoSpreadLayout.Arrange(Wins(4), (0, 0, 1920, 1080), 20);
        Assert.Equal(4, r.Count);

        // No two output rects overlap.
        for (int i = 0; i < r.Count; i++)
            for (int j = i + 1; j < r.Count; j++)
            {
                var a = r[i];
                var b = r[j];
                bool overlap = a.x < b.x + b.w && b.x < a.x + a.w &&
                               a.y < b.y + b.h && b.y < a.y + a.h;
                Assert.False(overlap, $"rects {i} and {j} overlap");
            }
    }

    [Fact]
    public void Arrange_KeepsWindowsWithinViewport()
    {
        var vp = (100.0, 200.0, 1600.0, 900.0);
        var r = AutoSpreadLayout.Arrange(Wins(5), vp, 20);
        foreach (var w in r)
        {
            Assert.True(w.x >= vp.Item1 - 0.001);
            Assert.True(w.y >= vp.Item2 - 0.001);
            Assert.True(w.x + w.w <= vp.Item1 + vp.Item3 + 0.001);
            Assert.True(w.y + w.h <= vp.Item2 + vp.Item4 + 0.001);
        }
    }

    [Fact]
    public void Arrange_ShrinksLargeWindowsToFitCell_PreservingAspectRatio()
    {
        // One 800x600 window into a small viewport must shrink but keep 4:3.
        var r = AutoSpreadLayout.Arrange(
            new List<(IntPtr, double, double)> { ((IntPtr)1, 800, 600) },
            (0, 0, 400, 400), 20);
        var w = r[0];
        Assert.True(w.w < 800);
        Assert.True(w.h < 600);
        Assert.Equal(800.0 / 600.0, w.w / w.h, 3);
    }

    [Fact]
    public void Arrange_DoesNotUpscaleSmallWindows()
    {
        // A tiny window in a huge cell keeps its original size (no upscaling).
        var r = AutoSpreadLayout.Arrange(
            new List<(IntPtr, double, double)> { ((IntPtr)1, 200, 150) },
            (0, 0, 1920, 1080), 20);
        Assert.Equal(200, r[0].w, 3);
        Assert.Equal(150, r[0].h, 3);
    }
}
