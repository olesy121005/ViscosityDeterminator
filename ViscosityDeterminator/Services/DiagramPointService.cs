using OpenCvSharp;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class DiagramPointService
    {
        public Point2f? FindPoint(
            Mat image,
            IEnumerable<DiagramGridLine> gridLines,
            double cao,
            double mgo,
            double sio2)
        {
            var lines =
                gridLines.ToList();

            var caoLine =
                FindExactLine(
                    lines,
                    "CaO",
                    cao);

            var mgoLine =
                FindExactLine(
                    lines,
                    "MgO",
                    mgo);

            var sio2Line =
                FindExactLine(
                    lines,
                    "SiO2",
                    sio2);

            if (caoLine == null ||
                mgoLine == null ||
                sio2Line == null)
            {
                return null;
            }

            var ca1 =
                ToPixel(
                    caoLine,
                    image.Width,
                    image.Height);

            var ca2 =
                ToPixel2(
                    caoLine,
                    image.Width,
                    image.Height);

            var mg1 =
                ToPixel(
                    mgoLine,
                    image.Width,
                    image.Height);

            var mg2 =
                ToPixel2(
                    mgoLine,
                    image.Width,
                    image.Height);

            var si1 =
                ToPixel(
                    sio2Line,
                    image.Width,
                    image.Height);

            var si2 =
                ToPixel2(
                    sio2Line,
                    image.Width,
                    image.Height);

            if (!TryIntersect(
                    ca1,
                    ca2,
                    mg1,
                    mg2,
                    out Point2f point))
            {
                return null;
            }

            // Точка должна находиться на третьей линии.
            double error =
                DistancePointToLine(
                    point,
                    si1,
                    si2);

            // Допустимая погрешность в пикселях.
            double tolerance =
                Math.Max(
                    image.Width,
                    image.Height) * 0.025;

            if (error > tolerance)
                return null;

            return point;
        }

        private static DiagramGridLine? FindExactLine(
            List<DiagramGridLine> lines,
            string component,
            double value)
        {
            return lines.FirstOrDefault(
                x =>
                    x.Component.Equals(
                        component,
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    Math.Abs(x.Value - value) < 0.001);
        }

        private static Point2f ToPixel(
            DiagramGridLine line,
            int width,
            int height)
        {
            return new Point2f(
                (float)(line.X1 * width),
                (float)(line.Y1 * height));
        }

        private static Point2f ToPixel2(
            DiagramGridLine line,
            int width,
            int height)
        {
            return new Point2f(
                (float)(line.X2 * width),
                (float)(line.Y2 * height));
        }

        private static bool TryIntersect(
            Point2f a1,
            Point2f a2,
            Point2f b1,
            Point2f b2,
            out Point2f intersection)
        {
            intersection = default;

            double dx1 = a2.X - a1.X;
            double dy1 = a2.Y - a1.Y;

            double dx2 = b2.X - b1.X;
            double dy2 = b2.Y - b1.Y;

            double denominator =
                dx1 * dy2 -
                dy1 * dx2;

            if (Math.Abs(denominator) < 0.000001)
                return false;

            double t =
                ((b1.X - a1.X) * dy2 -
                 (b1.Y - a1.Y) * dx2)
                / denominator;

            intersection =
                new Point2f(
                    (float)(a1.X + t * dx1),
                    (float)(a1.Y + t * dy1));

            return true;
        }

        private static double DistancePointToLine(
            Point2f p,
            Point2f a,
            Point2f b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            double denominator =
                Math.Sqrt(
                    dx * dx +
                    dy * dy);

            if (denominator < 0.000001)
                return double.MaxValue;

            return Math.Abs(
                dy * p.X -
                dx * p.Y +
                b.X * a.Y -
                b.Y * a.X)
                / denominator;
        }

        public Mat DrawPoint(
            Mat source,
            Point2f point)
        {
            var result =
                source.Clone();

            Cv2.Circle(
                result,
                new Point(
                    (int)Math.Round(point.X),
                    (int)Math.Round(point.Y)),
                9,
                new Scalar(0, 0, 255),
                -1,
                LineTypes.AntiAlias);

            Cv2.Circle(
                result,
                new Point(
                    (int)Math.Round(point.X),
                    (int)Math.Round(point.Y)),
                12,
                new Scalar(255, 255, 255),
                2,
                LineTypes.AntiAlias);

            return result;
        }
    }
}