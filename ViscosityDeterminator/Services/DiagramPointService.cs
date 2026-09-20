using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ViscosityDeterminator.Services
{
    public class DiagramPointService
    {
        // =========================================================
        // ДИАПАЗОНЫ ШКАЛ
        // =========================================================

        private const double CaoMin = 30.0;
        private const double CaoMax = 60.0;

        private const double MgoMin = 0.0;
        private const double MgoMax = 30.0;

        private const double Sio2Min = 30.0;
        private const double Sio2Max = 60.0;

        // =========================================================
        // ГЛАВНЫЙ МЕТОД
        // =========================================================

        public Point2f? FindPoint(
            Mat image,
            double cao,
            double mgo,
            double al2o3,
            double sio2)
        {
            if (image == null || image.Empty())
            {
                return null;
            }

            /*
             * Проверяем состав.
             */
            if (cao < CaoMin || cao > CaoMax)
            {
                return null;
            }

            if (mgo < MgoMin || mgo > MgoMax)
            {
                return null;
            }

            if (sio2 < Sio2Min || sio2 > Sio2Max)
            {
                return null;
            }

            /*
             * -----------------------------------------------------
             * 1. Находим реальную черную рамку диаграммы.
             * -----------------------------------------------------
             */

            DiagramBounds bounds =
                FindDiagramBounds(image);

            if (!bounds.IsValid)
            {
                return null;
            }

            /*
             * -----------------------------------------------------
             * 2. Получаем синие линии сетки.
             * -----------------------------------------------------
             */

            List<LineSegmentPoint> blueLines =
                DetectBlueLines(image);

            if (blueLines.Count == 0)
            {
                return null;
            }

            /*
             * -----------------------------------------------------
             * 3. Ищем ТОЛЬКО нужную линию CaO.
             * -----------------------------------------------------
             */

            Line2D? caoLine =
                FindTargetCaoLine(
                    blueLines,
                    bounds,
                    cao,
                    image);

            if (caoLine == null)
            {
                return null;
            }

            /*
             * -----------------------------------------------------
             * 4. Ищем ТОЛЬКО нужную линию MgO.
             * -----------------------------------------------------
             */

            Line2D? mgoLine =
                FindTargetMgoLine(
                    blueLines,
                    bounds,
                    mgo,
                    image);

            if (mgoLine == null)
            {
                return null;
            }

            /*
             * -----------------------------------------------------
             * 5. Ищем ТОЛЬКО нужную линию SiO2.
             * -----------------------------------------------------
             */

            Line2D? sio2Line =
                FindTargetSio2Line(
                    blueLines,
                    bounds,
                    sio2,
                    image);

            /*
             * -----------------------------------------------------
             * 6. Получаем точку пересечения CaO + MgO.
             * -----------------------------------------------------
             */

            if (!TryIntersectLines(
                    caoLine,
                    mgoLine,
                    out Point2f point))
            {
                return null;
            }

            /*
             * -----------------------------------------------------
             * 7. Проверяем SiO2.
             *
             * Если третья линия найдена, точка должна
             * находиться непосредственно на ней.
             * -----------------------------------------------------
             */

            if (sio2Line != null)
            {
                double distance =
                    DistancePointToLine(
                        point,
                        sio2Line);

                /*
                 * Небольшой допуск нужен из-за толщины
                 * синей линии и погрешности Hough.
                 */
                if (distance > 18.0)
                {
                    /*
                     * Пробуем получить более стабильную точку
                     * как среднее трех попарных пересечений.
                     */
                    if (TryIntersectLines(
                            caoLine,
                            sio2Line,
                            out Point2f p1) &&
                        TryIntersectLines(
                            mgoLine,
                            sio2Line,
                            out Point2f p2))
                    {
                        Point2f average =
                            new Point2f(
                                (point.X +
                                 p1.X +
                                 p2.X) / 3.0f,

                                (point.Y +
                                 p1.Y +
                                 p2.Y) / 3.0f);

                        double d1 =
                            Point2f.Distance(
                                point,
                                average);

                        if (d1 < 25.0)
                        {
                            point =
                                average;
                        }
                    }
                }
            }

            /*
             * -----------------------------------------------------
             * 8. Проверяем, что точка действительно находится
             * внутри диаграммы.
             * -----------------------------------------------------
             */

            if (!IsPointInsideBounds(
                    point,
                    bounds))
            {
                return null;
            }

            return point;
        }

        // =========================================================
        // ОПРЕДЕЛЕНИЕ СИНИХ ЛИНИЙ
        // =========================================================

        private List<LineSegmentPoint> DetectBlueLines(
            Mat image)
        {
            using var hsv =
                new Mat();

            using var mask =
                new Mat();

            /*
             * BGR -> HSV
             */
            Cv2.CvtColor(
                image,
                hsv,
                ColorConversionCodes.BGR2HSV);

            /*
             * Синяя сетка.
             */
            Cv2.InRange(
                hsv,
                new Scalar(
                    85,
                    60,
                    40),

                new Scalar(
                    140,
                    255,
                    255),

                mask);

            /*
             * Немного соединяем разрывы.
             */
            using var kernel =
                Cv2.GetStructuringElement(
                    MorphShapes.Rect,
                    new Size(3, 3));

            Cv2.MorphologyEx(
                mask,
                mask,
                MorphTypes.Close,
                kernel);

            int minDimension =
                Math.Min(
                    image.Width,
                    image.Height);

            /*
             * HoughLinesP используется только для получения
             * геометрии синих линий.
             *
             * Важно:
             * мы НЕ рисуем каждый найденный сегмент.
             */
            LineSegmentPoint[] lines =
                Cv2.HoughLinesP(
                    mask,
                    1.0,
                    Math.PI / 720.0,
                    10,
                    minDimension * 0.04,
                    30);

            return lines
                .Where(
                    line =>
                        GetLength(line) >
                        minDimension * 0.04)
                .ToList();
        }

        // =========================================================
        // CAO
        // =========================================================

        private Line2D? FindTargetCaoLine(
            List<LineSegmentPoint> segments,
            DiagramBounds bounds,
            double value,
            Mat image)
        {
            /*
             * CaO = 30 сверху
             * CaO = 60 снизу.
             *
             * Находим ожидаемое положение нужного значения
             * непосредственно на левой шкале.
             */

            double t =
                (value - CaoMin) /
                (CaoMax - CaoMin);

            Point2f expected =
                Lerp(
                    bounds.TopLeft,
                    bounds.BottomLeft,
                    t);

            return FindBestLine(
                segments,
                expected,
                bounds.BottomLeft,
                bounds.TopLeft,
                48.0,
                72.0,
                image);
        }

        // =========================================================
        // MGO
        // =========================================================

        private Line2D? FindTargetMgoLine(
            List<LineSegmentPoint> segments,
            DiagramBounds bounds,
            double value,
            Mat image)
        {
            /*
             * MgO = 0 слева
             * MgO = 30 справа.
             */

            double t =
                (value - MgoMin) /
                (MgoMax - MgoMin);

            Point2f expected =
                Lerp(
                    bounds.BottomLeft,
                    bounds.BottomRight,
                    t);

            return FindBestLine(
                segments,
                expected,
                bounds.BottomLeft,
                bounds.BottomRight,
                -72.0,
                -48.0,
                image);
        }

        // =========================================================
        // SIO2
        // =========================================================

        private Line2D? FindTargetSio2Line(
            List<LineSegmentPoint> segments,
            DiagramBounds bounds,
            double value,
            Mat image)
        {
            /*
             * SiO2 = 60 сверху
             * SiO2 = 30 снизу.
             */

            double t =
                (Sio2Max - value) /
                (Sio2Max - Sio2Min);

            Point2f expected =
                Lerp(
                    bounds.TopRight,
                    bounds.BottomRight,
                    t);

            return FindBestLine(
                segments,
                expected,
                bounds.BottomRight,
                bounds.TopRight,
                -8.0,
                8.0,
                image);
        }

        // =========================================================
        // ПОИСК ЛУЧШЕЙ ЛИНИИ
        // =========================================================

        private Line2D? FindBestLine(
            List<LineSegmentPoint> segments,
            Point2f expectedBoundaryPoint,
            Point2f boundaryStart,
            Point2f boundaryEnd,
            double minAngle,
            double maxAngle,
            Mat image)
        {
            Line2D? bestLine =
                null;

            double bestScore =
                double.MaxValue;

            foreach (LineSegmentPoint segment
                     in segments)
            {
                double angle =
                    NormalizeAngle(
                        GetAngleDegrees(
                            segment));

                /*
                 * Оставляем только нужное семейство.
                 */
                if (angle < minAngle ||
                    angle > maxAngle)
                {
                    continue;
                }

                /*
                 * Проверяем, что середина сегмента
                 * находится внутри диаграммы.
                 */
                Point2f middle =
                    new Point2f(
                        (segment.P1.X +
                         segment.P2.X) / 2.0f,

                        (segment.P1.Y +
                         segment.P2.Y) / 2.0f);

                /*
                 * Если сегмент явно находится за пределами
                 * изображения — пропускаем.
                 */
                if (middle.X < 0 ||
                    middle.X >= image.Width ||
                    middle.Y < 0 ||
                    middle.Y >= image.Height)
                {
                    continue;
                }

                /*
                 * Строим бесконечную линию сегмента.
                 */
                Line2D candidate =
                    CreateLine(
                        segment);

                /*
                 * Пересекаем ее с нужной шкалой.
                 */
                if (!TryIntersectLineWithSegment(
                        candidate,
                        boundaryStart,
                        boundaryEnd,
                        out Point2f intersection))
                {
                    continue;
                }

                /*
                 * Насколько пересечение близко
                 * к ожидаемому значению шкалы.
                 */
                double distance =
                    Point2f.Distance(
                        intersection,
                        expectedBoundaryPoint);

                /*
                 * Чем длиннее исходный Hough-сегмент,
                 * тем больше доверие к нему.
                 *
                 * Но главным остается положение
                 * на шкале.
                 */
                double length =
                    GetLength(
                        segment);

                double score =
                    distance -
                    Math.Min(
                        length * 0.02,
                        10.0);

                if (score < bestScore)
                {
                    bestScore =
                        score;

                    bestLine =
                        candidate;
                }
            }

            return bestLine;
        }

        // =========================================================
        // СОЗДАНИЕ БЕСКОНЕЧНОЙ ЛИНИИ
        // =========================================================

        private Line2D CreateLine(
            LineSegmentPoint segment)
        {
            double x1 =
                segment.P1.X;

            double y1 =
                segment.P1.Y;

            double x2 =
                segment.P2.X;

            double y2 =
                segment.P2.Y;

            /*
             * Ax + By + C = 0
             */

            double A =
                y1 - y2;

            double B =
                x2 - x1;

            double C =
                x1 * y2 -
                x2 * y1;

            double length =
                Math.Sqrt(
                    A * A +
                    B * B);

            if (length >
                0.000001)
            {
                A /= length;
                B /= length;
                C /= length;
            }

            return new Line2D
            {
                A = A,
                B = B,
                C = C
            };
        }

        // =========================================================
        // ПЕРЕСЕЧЕНИЕ ДВУХ ЛИНИЙ
        // =========================================================

        private bool TryIntersectLines(
            Line2D first,
            Line2D second,
            out Point2f point)
        {
            double denominator =
                first.A * second.B -
                second.A * first.B;

            if (Math.Abs(
                    denominator) <
                0.000001)
            {
                point = default;
                return false;
            }

            double x =
                (
                    first.B * second.C -
                    second.B * first.C
                )
                /
                denominator;

            double y =
                (
                    second.A * first.C -
                    first.A * second.C
                )
                /
                denominator;

            point =
                new Point2f(
                    (float)x,
                    (float)y);

            return true;
        }

        // =========================================================
        // ПЕРЕСЕЧЕНИЕ ЛИНИИ С ЧЕРНОЙ ГРАНИЦЕЙ
        // =========================================================

        private bool TryIntersectLineWithSegment(
            Line2D line,
            Point2f start,
            Point2f end,
            out Point2f point)
        {
            Line2D border =
                CreateLine(
                    new LineSegmentPoint(
                        new Point(
                            (int)start.X,
                            (int)start.Y),

                        new Point(
                            (int)end.X,
                            (int)end.Y)));

            if (!TryIntersectLines(
                    line,
                    border,
                    out point))
            {
                return false;
            }

            /*
             * Проверяем, что точка находится
             * непосредственно на отрезке.
             */
            double length =
                Point2f.Distance(
                    start,
                    end);

            double d =
                Point2f.Distance(
                    start,
                    point)
                +
                Point2f.Distance(
                    point,
                    end);

            if (Math.Abs(
                    d -
                    length) >
                5.0)
            {
                return false;
            }

            return true;
        }

        // =========================================================
        // ЧЕРНАЯ ГРАНИЦА
        // =========================================================

        private DiagramBounds FindDiagramBounds(
            Mat image)
        {
            using var gray =
                new Mat();

            using var mask =
                new Mat();

            Cv2.CvtColor(
                image,
                gray,
                ColorConversionCodes.BGR2GRAY);

            /*
             * Берем только действительно темные пиксели.
             */
            Cv2.InRange(
                gray,
                new Scalar(0),
                new Scalar(70),
                mask);

            /*
             * Соединяем небольшие разрывы
             * черной рамки.
             */
            using var kernel =
                Cv2.GetStructuringElement(
                    MorphShapes.Rect,
                    new Size(5, 5));

            Cv2.MorphologyEx(
                mask,
                mask,
                MorphTypes.Close,
                kernel);

            Cv2.FindContours(
                mask,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0)
            {
                return DiagramBounds.Invalid;
            }

            double imageArea =
                image.Width *
                image.Height;

            Point[]? best =
                null;

            double bestArea =
                0;

            foreach (Point[] contour
                     in contours)
            {
                double area =
                    Math.Abs(
                        Cv2.ContourArea(
                            contour));

                if (area <
                    imageArea * 0.05)
                {
                    continue;
                }

                double perimeter =
                    Cv2.ArcLength(
                        contour,
                        true);

                Point[] polygon =
                    Cv2.ApproxPolyDP(
                        contour,
                        perimeter * 0.01,
                        true);

                if (polygon.Length != 4)
                {
                    polygon =
                        Cv2.ApproxPolyDP(
                            contour,
                            perimeter * 0.02,
                            true);
                }

                if (polygon.Length != 4)
                {
                    continue;
                }

                if (!Cv2.IsContourConvex(
                        polygon))
                {
                    continue;
                }

                if (area > bestArea)
                {
                    bestArea =
                        area;

                    best =
                        polygon;
                }
            }

            /*
             * Если напрямую четырехугольник не нашли,
             * пробуем выпуклую оболочку крупнейшего контура.
             */
            if (best == null)
            {
                Point[]? largest =
                    contours
                        .OrderByDescending(
                            c =>
                                Math.Abs(
                                    Cv2.ContourArea(
                                        c)))
                        .FirstOrDefault();

                if (largest != null)
                {
                    Point[] hull =
                        Cv2.ConvexHull(
                            largest);

                    double perimeter =
                        Cv2.ArcLength(
                            hull,
                            true);

                    Point[] polygon =
                        Cv2.ApproxPolyDP(
                            hull,
                            perimeter * 0.02,
                            true);

                    if (polygon.Length == 4)
                    {
                        best =
                            polygon;
                    }
                }
            }

            if (best == null)
            {
                return DiagramBounds.Invalid;
            }

            return OrderBounds(best);
        }

        // =========================================================
        // УПОРЯДОЧИВАНИЕ ВЕРШИН
        // =========================================================

        private DiagramBounds OrderBounds(
            Point[] points)
        {
            if (points.Length != 4)
            {
                return DiagramBounds.Invalid;
            }

            Point2f[] p =
                points
                    .Select(
                        x =>
                            new Point2f(
                                x.X,
                                x.Y))
                    .ToArray();

            /*
             * Для нашей трапеции:
             *
             * TL — минимальная X+Y
             * BR — максимальная X+Y
             * TR — минимальная Y-X
             * BL — максимальная Y-X
             */

            Point2f topLeft =
                p.OrderBy(
                    x =>
                        x.X +
                        x.Y)
                 .First();

            Point2f bottomRight =
                p.OrderByDescending(
                    x =>
                        x.X +
                        x.Y)
                 .First();

            Point2f topRight =
                p.OrderBy(
                    x =>
                        x.Y -
                        x.X)
                 .First();

            Point2f bottomLeft =
                p.OrderByDescending(
                    x =>
                        x.Y -
                        x.X)
                 .First();

            return new DiagramBounds
            {
                TopLeft =
                    topLeft,

                TopRight =
                    topRight,

                BottomLeft =
                    bottomLeft,

                BottomRight =
                    bottomRight,

                IsValid =
                    true
            };
        }

        // =========================================================
        // ПРОВЕРКА ТОЧКИ
        // =========================================================

        private bool IsPointInsideBounds(
            Point2f point,
            DiagramBounds bounds)
        {
            Point2f[] polygon =
            {
                bounds.TopLeft,
                bounds.TopRight,
                bounds.BottomRight,
                bounds.BottomLeft
            };

            double result =
                Cv2.PointPolygonTest(
                    polygon,
                    point,
                    false);

            return result >= 0;
        }

        // =========================================================
        // РАССТОЯНИЕ ТОЧКИ ДО ЛИНИИ
        // =========================================================

        private double DistancePointToLine(
            Point2f point,
            Line2D line)
        {
            return Math.Abs(
                line.A * point.X +
                line.B * point.Y +
                line.C);
        }

        // =========================================================
        // ЛИНЕЙНАЯ ИНТЕРПОЛЯЦИЯ
        // =========================================================

        private Point2f Lerp(
            Point2f first,
            Point2f second,
            double t)
        {
            return new Point2f(
                (float)(
                    first.X +
                    (second.X -
                     first.X) *
                    t),

                (float)(
                    first.Y +
                    (second.Y -
                     first.Y) *
                    t));
        }

        // =========================================================
        // УГОЛ СЕГМЕНТА
        // =========================================================

        private double GetAngleDegrees(
            LineSegmentPoint line)
        {
            double dx =
                line.P2.X -
                line.P1.X;

            double dy =
                line.P2.Y -
                line.P1.Y;

            return
                Math.Atan2(
                    dy,
                    dx)
                *
                180.0 /
                Math.PI;
        }

        // =========================================================
        // НОРМАЛИЗАЦИЯ УГЛА
        // =========================================================

        private double NormalizeAngle(
            double angle)
        {
            while (angle > 90.0)
            {
                angle -= 180.0;
            }

            while (angle <= -90.0)
            {
                angle += 180.0;
            }

            return angle;
        }

        // =========================================================
        // ДЛИНА СЕГМЕНТА
        // =========================================================

        private double GetLength(
            LineSegmentPoint line)
        {
            double dx =
                line.P2.X -
                line.P1.X;

            double dy =
                line.P2.Y -
                line.P1.Y;

            return Math.Sqrt(
                dx * dx +
                dy * dy);
        }

        // =========================================================
        // РИСОВАНИЕ ТОЛЬКО ОДНОЙ ТОЧКИ
        // =========================================================

        public Mat DrawPoint(
            Mat image,
            Point2f point)
        {
            if (image == null ||
                image.Empty())
            {
                throw new ArgumentException(
                    "Изображение пустое.");
            }

            Mat result =
                image.Clone();

            Point center =
                new Point(
                    (int)Math.Round(
                        point.X),

                    (int)Math.Round(
                        point.Y));

            /*
             * Белая внешняя обводка.
             */
            Cv2.Circle(
                result,
                center,
                11,
                new Scalar(
                    255,
                    255,
                    255),
                4,
                LineTypes.AntiAlias);

            /*
             * Красная точка.
             */
            Cv2.Circle(
                result,
                center,
                8,
                new Scalar(
                    0,
                    0,
                    255),
                -1,
                LineTypes.AntiAlias);

            /*
             * Маленький белый центр.
             */
            Cv2.Circle(
                result,
                center,
                2,
                new Scalar(
                    255,
                    255,
                    255),
                -1,
                LineTypes.AntiAlias);

            return result;
        }

        // =========================================================
        // КЛАСС ЛИНИИ
        // =========================================================

        private class Line2D
        {
            public double A
            {
                get;
                set;
            }

            public double B
            {
                get;
                set;
            }

            public double C
            {
                get;
                set;
            }
        }

        // =========================================================
        // ГРАНИЦЫ ДИАГРАММЫ
        // =========================================================

        private class DiagramBounds
        {
            public Point2f TopLeft
            {
                get;
                set;
            }

            public Point2f TopRight
            {
                get;
                set;
            }

            public Point2f BottomLeft
            {
                get;
                set;
            }

            public Point2f BottomRight
            {
                get;
                set;
            }

            public bool IsValid
            {
                get;
                set;
            }

            public static DiagramBounds Invalid
            {
                get
                {
                    return new DiagramBounds
                    {
                        IsValid = false
                    };
                }
            }
        }
    }
}