using OpenCvSharp;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class ViscosityCalculationService
    {
        // Допустимое расстояние до изолинии,
        // при котором считаем, что точка лежит непосредственно
        // на ней.
        private const double ExactMatchTolerancePixels = 8.0;

        public ViscosityCalculationResult Calculate(
            Point2f point,
            IEnumerable<DiagramViscosityIsoline> isolines,
            int imageWidth,
            int imageHeight)
        {
            if (imageWidth <= 0 ||
                imageHeight <= 0)
            {
                return ViscosityCalculationResult.Failed(
                    "Некорректный размер изображения.");
            }

            var validIsolines =
                isolines
                    .Where(x =>
                        x.IsVerified &&
                        x.Viscosity > 0 &&
                        x.Points.Count >= 2)
                    .ToList();

            if (validIsolines.Count == 0)
            {
                return ViscosityCalculationResult.Failed(
                    "На диаграмме нет подтверждённых " +
                    "изолиний вязкости.");
            }

            // =====================================================
            // ПЕРЕВОД ИЗОЛИНИЙ ИЗ НОРМАЛИЗОВАННЫХ КООРДИНАТ
            // В ПИКСЕЛИ И РАСЧЁТ РАССТОЯНИЯ ДО КАЖДОЙ ИЗОЛИНИИ
            // =====================================================

            var candidates =
                new List<IsolineCandidate>();

            foreach (var isoline in validIsolines)
            {
                var points =
                    isoline.Points
                        .Select(
                            p => new Point2f(
                                (float)(p.X * imageWidth),
                                (float)(p.Y * imageHeight)))
                        .ToList();

                if (points.Count < 2)
                    continue;

                double distance =
                    DistancePointToPolyline(
                        point,
                        points);

                candidates.Add(
                    new IsolineCandidate
                    {
                        Isoline = isoline,
                        Distance = distance
                    });
            }

            if (candidates.Count == 0)
            {
                return ViscosityCalculationResult.Failed(
                    "Не удалось получить геометрию изолиний.");
            }

            // =====================================================
            // ТОЧКА НЕПОСРЕДСТВЕННО НА ИЗОЛИНИИ
            // =====================================================

            var exact =
                candidates
                    .Where(x =>
                        x.Distance <=
                        ExactMatchTolerancePixels)
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();

            if (exact != null)
            {
                return ViscosityCalculationResult.Exact(
                    exact.Isoline.Viscosity,
                    exact.Isoline.Id,
                    exact.Distance);
            }

            // =====================================================
            // ИЩЕМ ДВЕ ИЗОЛИНИИ:
            //
            // нижнюю по вязкости
            // и
            // верхнюю по вязкости
            //
            // Они должны окружать искомое значение.
            // =====================================================

            var orderedByDistance =
                candidates
                    .OrderBy(x => x.Distance)
                    .ToList();

            var lowerCandidates =
                orderedByDistance
                    .Where(x =>
                        x.Isoline.Viscosity <
                        GetNearestViscosity(
                            orderedByDistance,
                            x))
                    .ToList();

            // Вместо попытки угадать сторону через одну ближайшую
            // изолинию используем все возможные пары изолиний.
            // Для каждой пары проверяем геометрическую близость
            // точки к обеим линиям.
            var pairs =
                new List<IsolinePairCandidate>();

            for (int i = 0;
                 i < candidates.Count;
                 i++)
            {
                for (int j = i + 1;
                     j < candidates.Count;
                     j++)
                {
                    var first =
                        candidates[i];

                    var second =
                        candidates[j];

                    if (Math.Abs(
                            first.Isoline.Viscosity -
                            second.Isoline.Viscosity)
                        < 0.000001)
                    {
                        continue;
                    }

                    var lower =
                        first.Isoline.Viscosity <
                        second.Isoline.Viscosity
                            ? first
                            : second;

                    var upper =
                        first.Isoline.Viscosity <
                        second.Isoline.Viscosity
                            ? second
                            : first;

                    pairs.Add(
                        new IsolinePairCandidate
                        {
                            Lower = lower,
                            Upper = upper,

                            TotalDistance =
                                lower.Distance +
                                upper.Distance
                        });
                }
            }

            if (pairs.Count == 0)
            {
                return ViscosityCalculationResult.Failed(
                    "Не удалось подобрать две изолинии " +
                    "для определения вязкости.");
            }

            // =====================================================
            // БЕРЁМ ПАРУ, КОТОРАЯ ГЕОМЕТРИЧЕСКИ БЛИЖЕ ВСЕГО
            // =====================================================

            var bestPair =
                pairs
                    .OrderBy(x =>
                        x.TotalDistance)
                    .First();

            double lowerDistance =
                bestPair.Lower.Distance;

            double upperDistance =
                bestPair.Upper.Distance;

            double distanceSum =
                lowerDistance +
                upperDistance;

            if (distanceSum < 0.000001)
            {
                return ViscosityCalculationResult.Failed(
                    "Невозможно выполнить интерполяцию.");
            }

            // =====================================================
            // ЛИНЕЙНАЯ ИНТЕРПОЛЯЦИЯ
            // =====================================================

            double t =
                lowerDistance /
                distanceSum;

            double lowerViscosity =
                bestPair.Lower.Isoline.Viscosity;

            double upperViscosity =
                bestPair.Upper.Isoline.Viscosity;

            double viscosity =
                lowerViscosity +
                (upperViscosity -
                 lowerViscosity) * t;

            return ViscosityCalculationResult.Interpolated(
                viscosity,

                bestPair.Lower.Isoline.Id,
                lowerViscosity,
                lowerDistance,

                bestPair.Upper.Isoline.Id,
                upperViscosity,
                upperDistance);
        }

        // =========================================================
        // РАССТОЯНИЕ ОТ ТОЧКИ ДО ПОЛИЛИНИИ
        // =========================================================

        private static double DistancePointToPolyline(
            Point2f point,
            List<Point2f> polyline)
        {
            if (polyline.Count < 2)
                return double.MaxValue;

            double minimumDistance =
                double.MaxValue;

            for (int i = 0;
                 i < polyline.Count - 1;
                 i++)
            {
                double distance =
                    DistancePointToSegment(
                        point,
                        polyline[i],
                        polyline[i + 1]);

                if (distance < minimumDistance)
                {
                    minimumDistance =
                        distance;
                }
            }

            return minimumDistance;
        }

        // =========================================================
        // РАССТОЯНИЕ ОТ ТОЧКИ ДО ОТРЕЗКА
        // =========================================================

        private static double DistancePointToSegment(
            Point2f point,
            Point2f a,
            Point2f b)
        {
            double dx =
                b.X - a.X;

            double dy =
                b.Y - a.Y;

            double lengthSquared =
                dx * dx +
                dy * dy;

            if (lengthSquared < 0.000001)
            {
                return Distance(
                    point,
                    a);
            }

            double t =
                ((point.X - a.X) * dx +
                 (point.Y - a.Y) * dy)
                / lengthSquared;

            t =
                Math.Clamp(
                    t,
                    0.0,
                    1.0);

            double projectionX =
                a.X +
                t * dx;

            double projectionY =
                a.Y +
                t * dy;

            double diffX =
                point.X -
                projectionX;

            double diffY =
                point.Y -
                projectionY;

            return Math.Sqrt(
                diffX * diffX +
                diffY * diffY);
        }

        // =========================================================
        // РАССТОЯНИЕ МЕЖДУ ТОЧКАМИ
        // =========================================================

        private static double Distance(
            Point2f a,
            Point2f b)
        {
            double dx =
                a.X - b.X;

            double dy =
                a.Y - b.Y;

            return Math.Sqrt(
                dx * dx +
                dy * dy);
        }

        // =========================================================
        // ВСПОМОГАТЕЛЬНЫЙ МЕТОД
        // =========================================================

        private static double GetNearestViscosity(
            List<IsolineCandidate> candidates,
            IsolineCandidate current)
        {
            return candidates
                .Where(x =>
                    !ReferenceEquals(
                        x,
                        current))
                .OrderBy(x =>
                    x.Distance)
                .Select(x =>
                    x.Isoline.Viscosity)
                .FirstOrDefault(
                    current.Isoline.Viscosity);
        }

        // =========================================================
        // ВНУТРЕННИЙ КЛАСС КАНДИДАТА
        // =========================================================

        private class IsolineCandidate
        {
            public DiagramViscosityIsoline Isoline { get; set; } = null!;

            public double Distance { get; set; }
        }

        // =========================================================
        // ВНУТРЕННИЙ КЛАСС ПАРЫ ИЗОЛИНИЙ
        // =========================================================

        private class IsolinePairCandidate
        {
            public IsolineCandidate Lower { get; set; } = null!;

            public IsolineCandidate Upper { get; set; } = null!;

            public double TotalDistance { get; set; }
        }
    }
}