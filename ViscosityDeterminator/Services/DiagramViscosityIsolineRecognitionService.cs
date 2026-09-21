using OpenCvSharp;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class DiagramViscosityIsolineRecognitionService
    {
        /// <summary>
        /// Распознаёт зелёные изолинии на диаграмме.
        /// Значения вязкости автоматически не назначаются.
        /// Они будут указаны пользователем в редакторе.
        /// </summary>
        public ViscosityIsolineRecognitionResult FindIsolines(Mat image)
        {
            if (image == null || image.Empty())
                throw new ArgumentException("Изображение отсутствует.");

            using var hsv = new Mat();

            Cv2.CvtColor(
                image,
                hsv,
                ColorConversionCodes.BGR2HSV);

            // =========================================================
            // 1. Маска зелёного цвета
            // =========================================================

            using var greenMask = new Mat();

            Cv2.InRange(
                hsv,
                new Scalar(35, 70, 40),
                new Scalar(90, 255, 255),
                greenMask);

            // Небольшое удаление одиночного шума.
            using var kernelSmall = Cv2.GetStructuringElement(
                MorphShapes.Ellipse,
                new Size(3, 3));

            Cv2.MorphologyEx(
                greenMask,
                greenMask,
                MorphTypes.Open,
                kernelSmall);

            int greenPixels = Cv2.CountNonZero(greenMask);

            var result = new ViscosityIsolineRecognitionResult
            {
                GreenPixelCount = greenPixels
            };

            // =========================================================
            // 2. Поиск длинных зелёных прямолинейных участков
            // =========================================================

            using var lineMask = greenMask.Clone();

            var detectedLines = Cv2.HoughLinesP(
                lineMask,
                1,
                Math.PI / 180.0,
                80,
                minLineLength: image.Width * 0.18,
                maxLineGap: 35);

            var straightCandidates =
                new List<(Point2f P1, Point2f P2, double Length, double Angle)>();

            foreach (var line in detectedLines)
            {
                double dx = line.P2.X - line.P1.X;
                double dy = line.P2.Y - line.P1.Y;

                double length = Math.Sqrt(
                    dx * dx +
                    dy * dy);

                if (length < image.Width * 0.18)
                    continue;

                double angle =
                    Math.Atan2(dy, dx) *
                    180.0 /
                    Math.PI;

                // Наши изолинии преимущественно идут
                // почти горизонтально с небольшим наклоном.
                //
                // Разрешаем достаточно широкий диапазон,
                // поскольку нижние изолинии могут изгибаться.
                if (Math.Abs(angle) > 25)
                    continue;

                straightCandidates.Add(
                    (
                        new Point2f(line.P1.X, line.P1.Y),
                        new Point2f(line.P2.X, line.P2.Y),
                        length,
                        angle
                    ));
            }

            // =========================================================
            // 3. Объединяем похожие прямые
            // =========================================================

            var mergedLines = MergeLines(
                straightCandidates,
                image.Width,
                image.Height);

            foreach (var line in mergedLines)
            {
                var points = new List<DiagramIsolinePoint>
                {
                    ToNormalized(
                        line.P1,
                        image.Width,
                        image.Height),

                    ToNormalized(
                        line.P2,
                        image.Width,
                        image.Height)
                };

                result.Isolines.Add(
                    new DiagramViscosityIsoline
                    {
                        Geometry = "[]",
                        IsVerified = false,
                        Confidence = line.Confidence,
                        Viscosity = 0
                    });

                result.Isolines[^1].Points = points;
            }

            // =========================================================
            // 4. Поиск изогнутых / замкнутых изолиний
            // =========================================================

            using var contourMask = greenMask.Clone();

            Cv2.FindContours(
                contourMask,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxNone);

            foreach (var contour in contours)
            {
                if (contour.Length < 30)
                    continue;

                double perimeter =
                    Cv2.ArcLength(contour, false);

                if (perimeter < image.Width * 0.20)
                    continue;

                var rect = Cv2.BoundingRect(contour);

                if (rect.Width < image.Width * 0.12 &&
                    rect.Height < image.Height * 0.05)
                {
                    continue;
                }

                // Если контур уже фактически представляет
                // найденную длинную прямую, второй раз его
                // добавлять не нужно.
                if (LooksLikeAlreadyDetectedLine(
                        rect,
                        mergedLines))
                {
                    continue;
                }

                // Уменьшаем количество точек,
                // сохраняя форму изолинии.
                double epsilon = perimeter * 0.003;

                Point[] approximated =
                    Cv2.ApproxPolyDP(
                        contour,
                        epsilon,
                        false);

                if (approximated.Length < 5)
                    continue;

                var points = new List<DiagramIsolinePoint>();

                foreach (var p in approximated)
                {
                    points.Add(
                        ToNormalized(
                            new Point2f(p.X, p.Y),
                            image.Width,
                            image.Height));
                }

                result.Isolines.Add(
                    new DiagramViscosityIsoline
                    {
                        IsVerified = false,
                        Confidence = CalculateContourConfidence(
                            contour,
                            image.Width,
                            image.Height),
                        Viscosity = 0
                    });

                result.Isolines[^1].Points = points;
            }

            // =========================================================
            // 5. Сортировка
            // =========================================================

            result.Isolines = result.Isolines
                .OrderBy(x => GetAverageY(x))
                .ToList();

            if (result.Isolines.Count > 0)
            {
                result.OverallConfidence =
                    result.Isolines.Average(
                        x => x.Confidence);
            }

            return result;
        }

        private List<MergedLine> MergeLines(
            List<(Point2f P1, Point2f P2, double Length, double Angle)> lines,
            int width,
            int height)
        {
            var result = new List<MergedLine>();

            foreach (var line in lines
                         .OrderByDescending(x => x.Length))
            {
                bool duplicate = false;

                foreach (var existing in result)
                {
                    double angleDifference =
                        Math.Abs(
                            NormalizeAngle(
                                line.Angle -
                                existing.Angle));

                    if (angleDifference > 8)
                        continue;

                    double distance1 =
                        DistancePointToLine(
                            line.P1,
                            existing.P1,
                            existing.P2);

                    double distance2 =
                        DistancePointToLine(
                            line.P2,
                            existing.P1,
                            existing.P2);

                    if (distance1 > height * 0.025 &&
                        distance2 > height * 0.025)
                    {
                        continue;
                    }

                    duplicate = true;

                    // Расширяем существующую линию,
                    // если новая выходит за её пределы.
                    existing.P1 = new Point2f(
                        Math.Min(
                            existing.P1.X,
                            line.P1.X),
                        Math.Min(
                            existing.P1.Y,
                            line.P1.Y));

                    existing.P2 = new Point2f(
                        Math.Max(
                            existing.P2.X,
                            line.P2.X),
                        Math.Max(
                            existing.P2.Y,
                            line.P2.Y));

                    existing.Confidence =
                        Math.Min(
                            0.99,
                            existing.Confidence + 0.01);

                    break;
                }

                if (!duplicate)
                {
                    result.Add(
                        new MergedLine
                        {
                            P1 = line.P1,
                            P2 = line.P2,
                            Confidence = 0.85
                        });
                }
            }

            return result;
        }

        private bool LooksLikeAlreadyDetectedLine(
            Rect rect,
            List<MergedLine> lines)
        {
            foreach (var line in lines)
            {
                float minX =
                    Math.Min(line.P1.X, line.P2.X);

                float maxX =
                    Math.Max(line.P1.X, line.P2.X);

                float minY =
                    Math.Min(line.P1.Y, line.P2.Y);

                float maxY =
                    Math.Max(line.P1.Y, line.P2.Y);

                var lineRect = new Rect(
                    (int)minX,
                    (int)minY,
                    Math.Max(
                        1,
                        (int)(maxX - minX)),
                    Math.Max(
                        1,
                        (int)(maxY - minY)));

                var intersection =
                    RectIntersection(
                        rect,
                        lineRect);

                if (intersection.Width <= 0 ||
                    intersection.Height <= 0)
                {
                    continue;
                }

                double intersectionArea =
                    intersection.Width *
                    intersection.Height;

                double contourArea =
                    rect.Width *
                    rect.Height;

                if (contourArea > 0 &&
                    intersectionArea / contourArea > 0.5)
                {
                    return true;
                }
            }

            return false;
        }

        private double CalculateContourConfidence(
            Point[] contour,
            int width,
            int height)
        {
            double perimeter =
                Cv2.ArcLength(contour, false);

            double imageSize =
                Math.Sqrt(
                    width * width +
                    height * height);

            double confidence =
                perimeter /
                (imageSize * 0.5);

            return Math.Clamp(
                confidence,
                0.55,
                0.95);
        }

        private double GetAverageY(
            DiagramViscosityIsoline isoline)
        {
            var points = isoline.Points;

            if (points.Count == 0)
                return double.MaxValue;

            return points.Average(p => p.Y);
        }

        private DiagramIsolinePoint ToNormalized(
            Point2f point,
            int width,
            int height)
        {
            return new DiagramIsolinePoint(
                Math.Clamp(
                    point.X / width,
                    0,
                    1),

                Math.Clamp(
                    point.Y / height,
                    0,
                    1));
        }

        private double DistancePointToLine(
            Point2f p,
            Point2f a,
            Point2f b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            double denominator =
                Math.Sqrt(dx * dx + dy * dy);

            if (denominator < 0.0001)
                return Math.Sqrt(
                    Math.Pow(p.X - a.X, 2) +
                    Math.Pow(p.Y - a.Y, 2));

            return Math.Abs(
                dy * p.X -
                dx * p.Y +
                b.X * a.Y -
                b.Y * a.X) /
                denominator;
        }

        private double NormalizeAngle(double angle)
        {
            while (angle > 90)
                angle -= 180;

            while (angle < -90)
                angle += 180;

            return angle;
        }

        private Rect RectIntersection(
            Rect a,
            Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);

            int x2 = Math.Min(
                a.X + a.Width,
                b.X + b.Width);

            int y2 = Math.Min(
                a.Y + a.Height,
                b.Y + b.Height);

            return new Rect(
                x1,
                y1,
                Math.Max(0, x2 - x1),
                Math.Max(0, y2 - y1));
        }

        private class MergedLine
        {
            public Point2f P1 { get; set; }

            public Point2f P2 { get; set; }

            public double Angle { get; set; }

            public double Confidence { get; set; }
        }
    }
}