internal static class BoxHelpers
{
    /// <summary>
    /// Calculates rectangle area.
    /// </summary>
    public static float Area(this IRect source)
    {
        return source.Width * source.Height;
    }
    public static float Overlap(this IBox a, IBox b)
    {
        var (rect1, rect2) = (PredictionBox.RectangleF(a.X, a.Y, a.W, a.H), PredictionBox.RectangleF(b.X, b.Y, b.W, b.H));

        var intersection = PredictionBox.Intersect(rect1, rect2);

        var intArea = intersection.Area(); // intersection area
        var unionArea = rect1.Area() + rect2.Area() - intArea; // union area
        var overlap = intArea / unionArea; // overlap ratio
        return overlap;
    }
}