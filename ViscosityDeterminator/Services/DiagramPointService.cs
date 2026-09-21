using OpenCvSharp;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class DiagramPointService
    {
        /// <summary>
        /// Находит положение точки состава на диаграмме.
        ///
        /// Точка может находиться:
        /// 1. непосредственно на существующих линиях сетки;
        /// 2. между двумя существующими линиями сетки.
        ///
        /// Экстраполяция за пределы сохранённой сетки запрещена.
        /// </summary>
        public Point2f? FindPoint(
            Mat image,
            IEnumerable<DiagramGridLine> gridLines,
            double cao,
            double mgo,
            double sio2)
        {
            if (image == null || image.Empty())
                return null;

            var lines =
                gridLines
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.Component))
                    .ToList();

            if (lines.Count == 0)
                return null;


            // =====================================================
            // Находим промежуточные линии для каждого компонента.
            //
            // Если значение точно совпадает с существующей линией,
            // возвращается сама линия.
            //
            // Если значение находится между двумя линиями,
            // строится интерполированная линия.
            // =====================================================

            var caoLine =
                FindInterpolatedLine(
                    lines,
                    "CaO",
                    cao);

            var mgoLine =
                FindInterpolatedLine(
                    lines,
                    "MgO",
                    mgo);

            var sio2Line =
                FindInterpolatedLine(
                    lines,
                    "SiO2",
                    sio2);


            if (caoLine == null ||
                mgoLine == null ||
                sio2Line == null)
            {
                return null;
            }


            // =====================================================
            // Переводим нормализованные координаты в пиксели.
            // =====================================================

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


            // =====================================================
            // Пересекаем линии CaO и MgO.
            // =====================================================

            if (!TryIntersect(
                    ca1,
                    ca2,
                    mg1,
                    mg2,
                    out Point2f point))
            {
                return null;
            }


            // =====================================================
            // Проверяем полученную точку относительно линии SiO2.
            //
            // Для интерполированной точки допустима небольшая
            // погрешность из-за особенностей геометрии диаграммы.
            // =====================================================

            double error =
                DistancePointToLine(
                    point,
                    si1,
                    si2);


            double tolerance =
                Math.Max(
                    image.Width,
                    image.Height) *
                0.025;


            if (error > tolerance)
                return null;


            // =====================================================
            // Дополнительная проверка:
            // точка должна находиться внутри изображения.
            // =====================================================

            if (point.X < 0 ||
                point.X > image.Width ||
                point.Y < 0 ||
                point.Y > image.Height)
            {
                return null;
            }


            return point;
        }


        // =========================================================
        // ПОИСК ЛИНИИ ИЛИ ЕЁ ИНТЕРПОЛЯЦИЯ
        // =========================================================

        private static InterpolatedLine? FindInterpolatedLine(
            List<DiagramGridLine> lines,
            string component,
            double value)
        {
            var componentLines =
                lines
                    .Where(x =>
                        x.Component.Equals(
                            component,
                            StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Value)
                    .ToList();


            if (componentLines.Count == 0)
                return null;


            // =====================================================
            // Сначала проверяем точное совпадение.
            // =====================================================

            const double exactTolerance =
                0.000001;


            var exact =
                componentLines.FirstOrDefault(
                    x =>
                        Math.Abs(
                            x.Value - value) <
                        exactTolerance);


            if (exact != null)
            {
                return new InterpolatedLine
                {
                    X1 = exact.X1,
                    Y1 = exact.Y1,
                    X2 = exact.X2,
                    Y2 = exact.Y2
                };
            }


            // =====================================================
            // Значение должно находиться внутри диапазона.
            //
            // За пределы сетки не выходим.
            // =====================================================

            if (value <
                    componentLines.First().Value ||
                value >
                    componentLines.Last().Value)
            {
                return null;
            }


            // =====================================================
            // Ищем две соседние линии:
            //
            // lower.Value <= value <= upper.Value
            // =====================================================

            DiagramGridLine? lower = null;
            DiagramGridLine? upper = null;


            for (int i = 0;
                 i < componentLines.Count - 1;
                 i++)
            {
                var first =
                    componentLines[i];

                var second =
                    componentLines[i + 1];


                if (value >= first.Value &&
                    value <= second.Value)
                {
                    lower =
                        first;

                    upper =
                        second;

                    break;
                }
            }


            if (lower == null ||
                upper == null)
            {
                return null;
            }


            double denominator =
                upper.Value -
                lower.Value;


            if (Math.Abs(denominator) <
                0.000001)
            {
                return null;
            }


            // =====================================================
            // Коэффициент интерполяции.
            //
            // Например:
            //
            // lower = 40
            // upper = 45
            // value = 42.5
            //
            // t = 0.5
            // =====================================================

            double t =
                (value - lower.Value) /
                denominator;


            // =====================================================
            // Интерполируем обе конечные точки линии.
            // =====================================================

            double x1 =
                Lerp(
                    lower.X1,
                    upper.X1,
                    t);

            double y1 =
                Lerp(
                    lower.Y1,
                    upper.Y1,
                    t);


            double x2 =
                Lerp(
                    lower.X2,
                    upper.X2,
                    t);

            double y2 =
                Lerp(
                    lower.Y2,
                    upper.Y2,
                    t);


            return new InterpolatedLine
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2
            };
        }


        // =========================================================
        // ЛИНЕЙНАЯ ИНТЕРПОЛЯЦИЯ
        // =========================================================

        private static double Lerp(
            double a,
            double b,
            double t)
        {
            return a +
                   (b - a) *
                   t;
        }


        // =========================================================
        // ПЕРЕВОД В ПИКСЕЛИ
        // =========================================================

        private static Point2f ToPixel(
            InterpolatedLine line,
            int width,
            int height)
        {
            return new Point2f(
                (float)(
                    line.X1 *
                    width),

                (float)(
                    line.Y1 *
                    height));
        }


        private static Point2f ToPixel2(
            InterpolatedLine line,
            int width,
            int height)
        {
            return new Point2f(
                (float)(
                    line.X2 *
                    width),

                (float)(
                    line.Y2 *
                    height));
        }


        // =========================================================
        // ПЕРЕСЕЧЕНИЕ ДВУХ ЛИНИЙ
        // =========================================================

        private static bool TryIntersect(
            Point2f a1,
            Point2f a2,
            Point2f b1,
            Point2f b2,
            out Point2f intersection)
        {
            intersection =
                default;


            double dx1 =
                a2.X - a1.X;

            double dy1 =
                a2.Y - a1.Y;


            double dx2 =
                b2.X - b1.X;

            double dy2 =
                b2.Y - b1.Y;


            double denominator =
                dx1 * dy2 -
                dy1 * dx2;


            if (Math.Abs(denominator) <
                0.000001)
            {
                return false;
            }


            double t =
                ((b1.X - a1.X) * dy2 -
                 (b1.Y - a1.Y) * dx2) /
                denominator;


            intersection =
                new Point2f(
                    (float)(
                        a1.X +
                        t * dx1),

                    (float)(
                        a1.Y +
                        t * dy1));


            return true;
        }


        // =========================================================
        // РАССТОЯНИЕ ОТ ТОЧКИ ДО ЛИНИИ
        // =========================================================

        private static double DistancePointToLine(
            Point2f p,
            Point2f a,
            Point2f b)
        {
            double dx =
                b.X - a.X;

            double dy =
                b.Y - a.Y;


            double denominator =
                Math.Sqrt(
                    dx * dx +
                    dy * dy);


            if (denominator <
                0.000001)
            {
                return double.MaxValue;
            }


            return Math.Abs(
                       dy * p.X -
                       dx * p.Y +
                       b.X * a.Y -
                       b.Y * a.X)
                   /
                   denominator;
        }


        // =========================================================
        // ВРЕМЕННОЕ ПРЕДСТАВЛЕНИЕ ЛИНИИ
        // =========================================================

        private class InterpolatedLine
        {
            public double X1 { get; set; }

            public double Y1 { get; set; }

            public double X2 { get; set; }

            public double Y2 { get; set; }
        }


        // =========================================================
        // ОТРИСОВКА ТОЧКИ
        // =========================================================

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
                new Scalar(
                    0,
                    0,
                    255),
                -1,
                LineTypes.AntiAlias);


            Cv2.Circle(
                result,
                new Point(
                    (int)Math.Round(point.X),
                    (int)Math.Round(point.Y)),
                12,
                new Scalar(
                    255,
                    255,
                    255),
                2,
                LineTypes.AntiAlias);


            return result;
        }
    }
}