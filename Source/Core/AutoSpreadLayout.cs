using System;
using System.Collections.Generic;

namespace CanvasDesktop;

/// <summary>
/// Pure geometry for the auto-spread feature: given a set of world-space window
/// rects and a target viewport (also world-space), lay them out in a
/// near-square grid so none overlap. Each window keeps its aspect ratio and is
/// shrunk to fit its cell (never upscaled beyond its original size), then
/// centered in the cell. No Win32 / canvas knowledge — the caller applies the
/// results (see <c>WindowSpreader</c>).
/// </summary>
internal static class AutoSpreadLayout
{
    /// <summary>
    /// Compute new positions/sizes for <paramref name="windows"/> tiled inside
    /// <paramref name="viewport"/> with <paramref name="padding"/> world units
    /// of gap around and between cells. Output order matches input order.
    /// </summary>
    public static List<(IntPtr hWnd, double x, double y, double w, double h)> Arrange(
        IReadOnlyList<(IntPtr hWnd, double w, double h)> windows,
        (double x, double y, double w, double h) viewport,
        double padding)
    {
        var result = new List<(IntPtr, double, double, double, double)>(windows.Count);
        int n = windows.Count;
        if (n == 0) return result;

        int cols = (int)Math.Ceiling(Math.Sqrt(n));
        int rows = (int)Math.Ceiling((double)n / cols);

        double cellW = (viewport.w - padding * (cols + 1)) / cols;
        double cellH = (viewport.h - padding * (rows + 1)) / rows;
        cellW = Math.Max(1.0, cellW);
        cellH = Math.Max(1.0, cellH);

        for (int i = 0; i < n; i++)
        {
            int r = i / cols;
            int c = i % cols;

            double cellX = viewport.x + padding + c * (cellW + padding);
            double cellY = viewport.y + padding + r * (cellH + padding);

            var (hWnd, ww, wh) = windows[i];
            double scale = Math.Min(Math.Min(cellW / ww, cellH / wh), 1.0);
            if (double.IsNaN(scale) || scale <= 0) scale = 1.0;

            double newW = ww * scale;
            double newH = wh * scale;
            double newX = cellX + (cellW - newW) / 2.0;
            double newY = cellY + (cellH - newH) / 2.0;

            result.Add((hWnd, newX, newY, newW, newH));
        }

        return result;
    }
}
