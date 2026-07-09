namespace CanvasDesktop;

/// <summary>
/// Moves the canvas camera so a world-space rectangle becomes centered on the
/// primary screen. Implementations may animate the motion or apply it instantly.
/// </summary>
internal interface ICameraGlider
{
    /// <summary>Bring the given world rectangle to screen center.</summary>
    void GlideTo(double worldX, double worldY, double worldW, double worldH);

    /// <summary>Abort any in-progress motion, settling windows where they are.</summary>
    void Cancel();
}
