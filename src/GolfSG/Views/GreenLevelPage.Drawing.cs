namespace GolfSG.Views;

public sealed partial class GreenLevelPage
{
    private sealed class LevelDrawing : IDrawable
    {
        public double Right { get; set; }
        public double Forward { get; set; }
        public void Draw(ICanvas canvas, RectF rect)
        {
            float cx = rect.Center.X, cy = rect.Center.Y;
            float radius = Math.Min(rect.Width, rect.Height) / 2 - 18;
            canvas.FillColor = GolfTheme.Colors.SoftGreen;
            canvas.FillCircle(cx, cy, radius);
            canvas.StrokeColor = GolfTheme.Colors.PrimaryGreen;
            canvas.StrokeSize = 2;
            canvas.DrawCircle(cx, cy, radius);
            canvas.DrawCircle(cx, cy, 14);
            canvas.DrawLine(cx - radius, cy, cx + radius, cy);
            canvas.DrawLine(cx, cy - radius, cx, cy + radius);
            var dx = Right / 5;
            var dy = -Forward / 5;
            var magnitude = Math.Sqrt(dx * dx + dy * dy);
            if (magnitude > 1) { dx /= magnitude; dy /= magnitude; }
            canvas.FillColor = GolfTheme.Colors.PrimaryGreen;
            canvas.FillCircle(cx + (float)dx * (radius - 12), cy + (float)dy * (radius - 12), 10);
        }
    }
}
