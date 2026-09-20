using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class DiagramRecognitionService
    {
        private const double AngleTolerance = 3.0 * Math.PI / 180.0;
        private const double FamilyAngleTolerance = 15.0 * Math.PI / 180.0;
        private const double PositionTolerance = 5.0;
        private const double StepTolerance = 0.22;
        private const double MinLineLengthRatio = 0.06;

        public Mat LoadImage(string filePath)
        {
            var image = Cv2.ImRead(filePath, ImreadModes.Color);

            if (image.Empty())
                throw new InvalidOperationException("Не удалось загрузить изображение диаграммы.");

            return image;
        }

        public DiagramGrid FindGrid(Mat image)
        {
            var result = new DiagramGrid();

            if (image == null || image.Empty())
                return result;

            using var gray = new Mat();
            using var blurred = new Mat();
            using var edges = new Mat();

            Cv2.CvtColor(
                image,
                gray,
                ColorConversionCodes.BGR2GRAY);

            Cv2.GaussianBlur(
                gray,
                blurred,
                new Size(3, 3),
                0);

            Cv2.Canny(
                blurred,
                edges,
                30,
                100);

            int minDimension =
                Math.Min(
                    image.Width,
                    image.Height);

            var detected =
                Cv2.HoughLinesP(
                    edges,
                    1,
                    Math.PI / 360,
                    20,
                    minDimension * MinLineLengthRatio,
                    8);

            if (detected.Length < 6)
                return result;

            var candidates =
                detected
                    .Where(x =>
                        GetLineLength(x) >=
                        minDimension *
                        MinLineLengthRatio)
                    .ToList();

            var groups =
                BuildDirectionGroups(
                    candidates);

            if (groups.Count < 2)
                return result;

            var families =
                new List<GridFamily>();

            foreach (var group in groups)
            {
                var family =
                    AnalyzeFamily(group);

                if (family != null)
                    families.Add(family);
            }

            if (families.Count < 2)
                return result;

            families =
                families
                    .OrderByDescending(
                        x => x.Score)
                    .ToList();

            var selected =
                SelectFamilies(
                    families);

            if (selected.Count < 2)
                return result;

            var boundary =
                FindBoundary(
                    selected,
                    image.Width,
                    image.Height);

            if (boundary.Count < 3)
                return result;

            foreach (var family in selected)
            {
                foreach (var line in family.Lines)
                {
                    if (ExtendLineToBoundary(
                            line.Line,
                            boundary,
                            out var clipped))
                    {
                        result.Lines.Add(clipped);
                    }
                }
            }

            result.Lines =
                RemoveDuplicateLines(
                    result.Lines);

            return result;
        }

        private List<List<LineSegmentPoint>> BuildDirectionGroups(
            List<LineSegmentPoint> lines)
        {
            var groups =
                new List<List<LineSegmentPoint>>();

            foreach (var line in
                     lines.OrderByDescending(
                         GetLineLength))
            {
                double angle =
                    NormalizeAngle(line);

                List<LineSegmentPoint> best = null;
                double bestDifference =
                    double.MaxValue;

                foreach (var group in groups)
                {
                    double difference =
                        AngleDifference(
                            angle,
                            GetGroupAngle(group));

                    if (difference <=
                            AngleTolerance &&
                        difference <
                            bestDifference)
                    {
                        bestDifference =
                            difference;

                        best =
                            group;
                    }
                }

                if (best == null)
                {
                    groups.Add(
                        new List<LineSegmentPoint>
                        {
                            line
                        });
                }
                else
                {
                    best.Add(line);
                }
            }

            return groups
                .Where(x => x.Count >= 3)
                .OrderByDescending(
                    x => x.Count)
                .Take(6)
                .ToList();
        }

        private GridFamily AnalyzeFamily(
            List<LineSegmentPoint> lines)
        {
            if (lines.Count < 3)
                return null;

            double angle =
                GetGroupAngle(lines);

            double nx =
                -Math.Sin(angle);

            double ny =
                Math.Cos(angle);

            var data =
                lines
                    .Select(
                        line =>
                            new GridLine
                            {
                                Line = line,
                                Position =
                                    GetLinePosition(
                                        line,
                                        nx,
                                        ny),
                                Length =
                                    GetLineLength(line)
                            })
                    .OrderBy(
                        x => x.Position)
                    .ToList();

            var clusters =
                MergeClusters(data);

            if (clusters.Count < 3)
                return null;

            var sequence =
                FindBestSequence(
                    clusters);

            if (sequence.Count < 3)
                return null;

            double step =
                CalculateStep(
                    sequence);

            if (step <= 0)
                return null;

            double regularity =
                CalculateRegularity(
                    sequence,
                    step);

            double continuity =
                CalculateContinuity(
                    sequence,
                    step);

            if (regularity < 0.65 ||
                continuity < 0.70)
                return null;

            double outerScore =
                CalculateOuterScore(
                    sequence,
                    clusters,
                    step);

            double span =
                sequence.Last().Position -
                sequence.First().Position;

            double score =
                sequence.Count * 20 +
                regularity * 50 +
                continuity * 30 +
                outerScore * 20 +
                span * 0.05;

            return new GridFamily
            {
                Angle = angle,
                Lines = sequence,
                Step = step,
                Regularity = regularity,
                Continuity = continuity,
                OuterScore = outerScore,
                Score = score
            };
        }

        private List<GridLine> FindBestSequence(
            List<GridLine> clusters)
        {
            if (clusters.Count < 3)
                return new List<GridLine>();

            List<GridLine> best =
                new List<GridLine>();

            for (int start = 0;
                 start < clusters.Count - 2;
                 start++)
            {
                for (int second = start + 1;
                     second < clusters.Count - 1;
                     second++)
                {
                    double initialStep =
                        clusters[second].Position -
                        clusters[start].Position;

                    if (initialStep < 15)
                        continue;

                    var sequence =
                        new List<GridLine>
                        {
                            clusters[start],
                            clusters[second]
                        };

                    double step =
                        initialStep;

                    for (int i = second + 1;
                         i < clusters.Count;
                         i++)
                    {
                        double distance =
                            clusters[i].Position -
                            sequence.Last().Position;

                        double ratio =
                            distance / step;

                        int nearestMultiple =
                            Math.Max(
                                1,
                                (int)Math.Round(
                                    ratio));

                        double expected =
                            step *
                            nearestMultiple;

                        double error =
                            Math.Abs(
                                distance -
                                expected);

                        if (error <=
                            step *
                            StepTolerance)
                        {
                            sequence.Add(
                                clusters[i]);

                            step =
                                CalculateStep(
                                    sequence);
                        }
                        else if (
                            distance >
                            step * 1.8)
                        {
                            break;
                        }
                    }

                    if (sequence.Count >
                        best.Count)
                    {
                        best =
                            sequence;
                    }
                    else if (
                        sequence.Count ==
                        best.Count &&
                        sequence.Count >= 3)
                    {
                        double currentRegularity =
                            CalculateRegularity(
                                sequence,
                                CalculateStep(
                                    sequence));

                        double bestRegularity =
                            CalculateRegularity(
                                best,
                                CalculateStep(
                                    best));

                        if (currentRegularity >
                            bestRegularity)
                        {
                            best =
                                sequence;
                        }
                    }
                }
            }

            return best;
        }

        private double CalculateStep(
            List<GridLine> lines)
        {
            if (lines.Count < 2)
                return 0;

            double sum = 0;

            for (int i = 1;
                 i < lines.Count;
                 i++)
            {
                sum +=
                    lines[i].Position -
                    lines[i - 1].Position;
            }

            return
                sum /
                (lines.Count - 1);
        }

        private double CalculateRegularity(
            List<GridLine> lines,
            double step)
        {
            if (lines.Count < 2 ||
                step <= 0)
                return 0;

            double error = 0;

            for (int i = 1;
                 i < lines.Count;
                 i++)
            {
                double actual =
                    lines[i].Position -
                    lines[i - 1].Position;

                error +=
                    Math.Abs(
                        actual -
                        step);
            }

            double normalized =
                error /
                ((lines.Count - 1) *
                 step);

            return Math.Clamp(
                1 -
                normalized,
                0,
                1);
        }

        private double CalculateContinuity(
            List<GridLine> lines,
            double step)
        {
            if (lines.Count < 3 ||
                step <= 0)
                return 0;

            int valid = 0;

            for (int i = 1;
                 i < lines.Count;
                 i++)
            {
                double distance =
                    lines[i].Position -
                    lines[i - 1].Position;

                if (Math.Abs(
                        distance -
                        step) <=
                    step *
                    StepTolerance)
                {
                    valid++;
                }
            }

            return
                (double)valid /
                (lines.Count - 1);
        }

        private double CalculateOuterScore(
            List<GridLine> sequence,
            List<GridLine> all,
            double step)
        {
            if (sequence.Count < 3 ||
                step <= 0)
                return 0;

            double first =
                sequence.First().Position;

            double last =
                sequence.Last().Position;

            var before =
                all.Where(
                        x =>
                            x.Position <
                            first -
                            step * 0.45)
                    .ToList();

            var after =
                all.Where(
                        x =>
                            x.Position >
                            last +
                            step * 0.45)
                    .ToList();

            double score = 1;

            if (before.Count > 0)
            {
                double closest =
                    before.Max(
                        x => x.Position);

                double distance =
                    first -
                    closest;

                if (distance <
                    step * 0.8)
                {
                    score -= 0.4;
                }
            }

            if (after.Count > 0)
            {
                double closest =
                    after.Min(
                        x => x.Position);

                double distance =
                    closest -
                    last;

                if (distance <
                    step * 0.8)
                {
                    score -= 0.4;
                }
            }

            return Math.Clamp(
                score,
                0,
                1);
        }

        private List<GridFamily> SelectFamilies(
            List<GridFamily> families)
        {
            if (families.Count <= 2)
                return families;

            double bestScore =
                double.MinValue;

            List<GridFamily> best =
                new List<GridFamily>();

            for (int i = 0;
                 i < families.Count;
                 i++)
            {
                for (int j = i + 1;
                     j < families.Count;
                     j++)
                {
                    if (!DifferentDirections(
                            families[i],
                            families[j]))
                        continue;

                    double score =
                        families[i].Score +
                        families[j].Score;

                    var current =
                        new List<GridFamily>
                        {
                            families[i],
                            families[j]
                        };

                    for (int k = 0;
                         k < families.Count;
                         k++)
                    {
                        if (k == i ||
                            k == j)
                            continue;

                        if (!DifferentDirections(
                                families[k],
                                families[i]) ||
                            !DifferentDirections(
                                families[k],
                                families[j]))
                            continue;

                        double compatibility =
                            StepCompatibility(
                                families[i],
                                families[j],
                                families[k]);

                        double candidateScore =
                            score +
                            families[k].Score +
                            compatibility *
                            50;

                        if (candidateScore >
                            bestScore)
                        {
                            bestScore =
                                candidateScore;

                            best =
                                new List<GridFamily>
                                {
                                    families[i],
                                    families[j],
                                    families[k]
                                };
                        }
                    }

                    if (score > bestScore &&
                        best.Count < 3)
                    {
                        bestScore =
                            score;

                        best =
                            current;
                    }
                }
            }

            return best;
        }

        private double StepCompatibility(
            GridFamily first,
            GridFamily second,
            GridFamily third)
        {
            double average =
                (first.Step +
                 second.Step +
                 third.Step) /
                3;

            if (average <= 0)
                return 0;

            double deviation =
                (Math.Abs(
                    first.Step -
                    average) +
                 Math.Abs(
                    second.Step -
                    average) +
                 Math.Abs(
                    third.Step -
                    average)) /
                (3 * average);

            return Math.Clamp(
                1 -
                deviation * 2,
                0,
                1);
        }

        private bool DifferentDirections(
            GridFamily first,
            GridFamily second)
        {
            return
                AngleDifference(
                    first.Angle,
                    second.Angle) >
                FamilyAngleTolerance;
        }

        private List<Point> FindBoundary(
            List<GridFamily> families,
            int width,
            int height)
        {
            var intersections =
                new List<Point2f>();

            for (int i = 0;
                 i < families.Count;
                 i++)
            {
                for (int j = i + 1;
                     j < families.Count;
                     j++)
                {
                    var first =
                        families[i];

                    var second =
                        families[j];

                    var firstOuter =
                        new[]
                        {
                            first.Lines.First().Line,
                            first.Lines.Last().Line
                        };

                    var secondOuter =
                        new[]
                        {
                            second.Lines.First().Line,
                            second.Lines.Last().Line
                        };

                    foreach (var line1 in firstOuter)
                    {
                        foreach (var line2 in secondOuter)
                        {
                            if (!TryGetIntersection(
                                    line1,
                                    line2,
                                    out Point2f point))
                                continue;

                            if (point.X <
                                    -width * 0.1 ||
                                point.X >
                                    width * 1.1 ||
                                point.Y <
                                    -height * 0.1 ||
                                point.Y >
                                    height * 1.1)
                                continue;

                            intersections.Add(point);
                        }
                    }
                }
            }

            intersections =
                RemoveClosePoints(
                    intersections,
                    15);

            if (intersections.Count < 3)
                return new List<Point>();

            var points =
                intersections
                    .Select(
                        p =>
                            new Point(
                                (int)Math.Round(
                                    p.X),
                                (int)Math.Round(
                                    p.Y)))
                    .ToArray();

            var hull =
                Cv2.ConvexHull(
                    points);

            return hull.ToList();
        }

        private bool ExtendLineToBoundary(
            LineSegmentPoint line,
            List<Point> boundary,
            out LineSegmentPoint result)
        {
            result = default;

            if (boundary.Count < 3)
                return false;

            double x1 =
                line.P1.X;

            double y1 =
                line.P1.Y;

            double x2 =
                line.P2.X;

            double y2 =
                line.P2.Y;

            double dx =
                x2 - x1;

            double dy =
                y2 - y1;

            double length =
                Math.Sqrt(
                    dx * dx +
                    dy * dy);

            if (length < 1)
                return false;

            dx /= length;
            dy /= length;

            double cx =
                (x1 + x2) / 2;

            double cy =
                (y1 + y2) / 2;

            double extension = 5000;

            double sx =
                cx -
                dx *
                extension;

            double sy =
                cy -
                dy *
                extension;

            double ex =
                cx +
                dx *
                extension;

            double ey =
                cy +
                dy *
                extension;

            var points =
                new List<Point2f>();

            for (int i = 0;
                 i < boundary.Count;
                 i++)
            {
                Point a =
                    boundary[i];

                Point b =
                    boundary[
                        (i + 1) %
                        boundary.Count];

                if (TryGetSegmentIntersection(
                        sx,
                        sy,
                        ex,
                        ey,
                        a.X,
                        a.Y,
                        b.X,
                        b.Y,
                        out Point2f point))
                {
                    points.Add(point);
                }
            }

            points =
                RemoveClosePoints(
                    points,
                    2);

            if (points.Count < 2)
                return false;

            Point2f first =
                points[0];

            Point2f second =
                points[1];

            double maxDistance = 0;

            for (int i = 0;
                 i < points.Count;
                 i++)
            {
                for (int j = i + 1;
                     j < points.Count;
                     j++)
                {
                    double distance =
                        Distance(
                            points[i],
                            points[j]);

                    if (distance >
                        maxDistance)
                    {
                        maxDistance =
                            distance;

                        first =
                            points[i];

                        second =
                            points[j];
                    }
                }
            }

            result =
                new LineSegmentPoint(
                    new Point(
                        (int)Math.Round(
                            first.X),
                        (int)Math.Round(
                            first.Y)),
                    new Point(
                        (int)Math.Round(
                            second.X),
                        (int)Math.Round(
                            second.Y)));

            return true;
        }

        private bool TryGetSegmentIntersection(
            double x1,
            double y1,
            double x2,
            double y2,
            double x3,
            double y3,
            double x4,
            double y4,
            out Point2f intersection)
        {
            double denominator =
                (x1 - x2) *
                (y3 - y4) -
                (y1 - y2) *
                (x3 - x4);

            if (Math.Abs(
                    denominator) <
                0.00001)
            {
                intersection =
                    default;

                return false;
            }

            double t =
                ((x1 - x3) *
                 (y3 - y4) -
                 (y1 - y3) *
                 (x3 - x4)) /
                denominator;

            double u =
                -((x1 - x2) *
                  (y1 - y3) -
                  (y1 - y2) *
                  (x1 - x3)) /
                denominator;

            if (t < -0.001 ||
                t > 1.001 ||
                u < -0.001 ||
                u > 1.001)
            {
                intersection =
                    default;

                return false;
            }

            intersection =
                new Point2f(
                    (float)(
                        x1 +
                        t *
                        (x2 - x1)),
                    (float)(
                        y1 +
                        t *
                        (y2 - y1)));

            return true;
        }

        private bool TryGetIntersection(
            LineSegmentPoint first,
            LineSegmentPoint second,
            out Point2f intersection)
        {
            double x1 = first.P1.X;
            double y1 = first.P1.Y;
            double x2 = first.P2.X;
            double y2 = first.P2.Y;
            double x3 = second.P1.X;
            double y3 = second.P1.Y;
            double x4 = second.P2.X;
            double y4 = second.P2.Y;

            double denominator =
                (x1 - x2) *
                (y3 - y4) -
                (y1 - y2) *
                (x3 - x4);

            if (Math.Abs(
                    denominator) <
                0.00001)
            {
                intersection =
                    default;

                return false;
            }

            double px =
                ((x1 * y2 -
                  y1 * x2) *
                 (x3 - x4) -
                 (x1 - x2) *
                 (x3 * y4 -
                  y3 * x4)) /
                denominator;

            double py =
                ((x1 * y2 -
                  y1 * x2) *
                 (y3 - y4) -
                 (y1 - y2) *
                 (x3 * y4 -
                  y3 * x4)) /
                denominator;

            intersection =
                new Point2f(
                    (float)px,
                    (float)py);

            return true;
        }

        private List<GridLine> MergeClusters(
            List<GridLine> lines)
        {
            var result =
                new List<GridLine>();

            foreach (var line in lines)
            {
                var nearest =
                    result
                        .OrderBy(
                            x =>
                                Math.Abs(
                                    x.Position -
                                    line.Position))
                        .FirstOrDefault();

                if (nearest != null &&
                    Math.Abs(
                        nearest.Position -
                        line.Position) <=
                    PositionTolerance)
                {
                    if (line.Length >
                        nearest.Length)
                    {
                        nearest.Line =
                            line.Line;

                        nearest.Position =
                            line.Position;

                        nearest.Length =
                            line.Length;
                    }
                }
                else
                {
                    result.Add(
                        new GridLine
                        {
                            Line =
                                line.Line,
                            Position =
                                line.Position,
                            Length =
                                line.Length
                        });
                }
            }

            return result
                .OrderBy(
                    x =>
                        x.Position)
                .ToList();
        }

        private List<LineSegmentPoint>
            MergeCollinearFragments(
                List<LineSegmentPoint> lines)
        {
            var result =
                new List<LineSegmentPoint>();

            foreach (var line in
                     lines.OrderByDescending(
                         GetLineLength))
            {
                bool merged = false;

                for (int i = 0;
                     i < result.Count;
                     i++)
                {
                    if (!AreSameLine(
                            result[i],
                            line))
                        continue;

                    result[i] =
                        MergeTwoLines(
                            result[i],
                            line);

                    merged = true;
                    break;
                }

                if (!merged)
                    result.Add(line);
            }

            return result;
        }

        private LineSegmentPoint MergeTwoLines(
            LineSegmentPoint first,
            LineSegmentPoint second)
        {
            var points =
                new[]
                {
                    first.P1,
                    first.P2,
                    second.P1,
                    second.P2
                };

            Point a =
                points[0];

            Point b =
                points[1];

            double maxDistance = 0;

            for (int i = 0;
                 i < points.Length;
                 i++)
            {
                for (int j = i + 1;
                     j < points.Length;
                     j++)
                {
                    double distance =
                        Math.Sqrt(
                            Math.Pow(
                                points[i].X -
                                points[j].X,
                                2) +
                            Math.Pow(
                                points[i].Y -
                                points[j].Y,
                                2));

                    if (distance >
                        maxDistance)
                    {
                        maxDistance =
                            distance;

                        a =
                            points[i];

                        b =
                            points[j];
                    }
                }
            }

            return new LineSegmentPoint(
                a,
                b);
        }

        private bool AreSameLine(
            LineSegmentPoint first,
            LineSegmentPoint second)
        {
            double difference =
                AngleDifference(
                    NormalizeAngle(first),
                    NormalizeAngle(second));

            if (difference >
                5.0 *
                Math.PI /
                180.0)
                return false;

            double angle =
                NormalizeAngle(first);

            double nx =
                -Math.Sin(angle);

            double ny =
                Math.Cos(angle);

            double firstPosition =
                GetLinePosition(
                    first,
                    nx,
                    ny);

            double secondPosition =
                GetLinePosition(
                    second,
                    nx,
                    ny);

            return
                Math.Abs(
                    firstPosition -
                    secondPosition) <
                PositionTolerance;
        }

        private List<LineSegmentPoint>
            RemoveDuplicateLines(
                List<LineSegmentPoint> lines)
        {
            var result =
                new List<LineSegmentPoint>();

            foreach (var line in lines)
            {
                if (!result.Any(
                        x =>
                            AreSameLine(
                                x,
                                line)))
                {
                    result.Add(line);
                }
            }

            return result;
        }

        private List<Point2f> RemoveClosePoints(
            List<Point2f> points,
            double minDistance)
        {
            var result =
                new List<Point2f>();

            foreach (var point in points)
            {
                if (!result.Any(
                        x =>
                            Distance(
                                x,
                                point) <
                            minDistance))
                {
                    result.Add(point);
                }
            }

            return result;
        }

        private double GetGroupAngle(
            List<LineSegmentPoint> lines)
        {
            double x = 0;
            double y = 0;

            foreach (var line in lines)
            {
                double angle =
                    NormalizeAngle(line);

                double weight =
                    GetLineLength(line);

                x +=
                    Math.Cos(
                        angle * 2) *
                    weight;

                y +=
                    Math.Sin(
                        angle * 2) *
                    weight;
            }

            double result =
                Math.Atan2(
                    y,
                    x) /
                2;

            if (result < 0)
                result += Math.PI;

            return result;
        }

        private double NormalizeAngle(
            LineSegmentPoint line)
        {
            double angle =
                Math.Atan2(
                    line.P2.Y -
                    line.P1.Y,
                    line.P2.X -
                    line.P1.X);

            if (angle < 0)
                angle += Math.PI;

            if (angle >= Math.PI)
                angle -= Math.PI;

            return angle;
        }

        private double AngleDifference(
            double first,
            double second)
        {
            double difference =
                Math.Abs(
                    first -
                    second);

            return Math.Min(
                difference,
                Math.PI -
                difference);
        }

        private double GetLinePosition(
            LineSegmentPoint line,
            double nx,
            double ny)
        {
            double cx =
                (line.P1.X +
                 line.P2.X) /
                2.0;

            double cy =
                (line.P1.Y +
                 line.P2.Y) /
                2.0;

            return
                cx * nx +
                cy * ny;
        }

        private double GetLineLength(
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

        private double Distance(
            Point2f first,
            Point2f second)
        {
            double dx =
                first.X -
                second.X;

            double dy =
                first.Y -
                second.Y;

            return Math.Sqrt(
                dx * dx +
                dy * dy);
        }

        public Mat DrawDetectedGrid(
            Mat image,
            DiagramGrid grid)
        {
            var result =
                image.Clone();

            if (grid == null)
                return result;

            foreach (var line in grid.Lines)
            {
                Cv2.Line(
                    result,
                    line.P1,
                    line.P2,
                    new Scalar(
                        255,
                        0,
                        0),
                    2,
                    LineTypes.AntiAlias);
            }

            return result;
        }

        private class GridLine
        {
            public LineSegmentPoint Line { get; set; }
            public double Position { get; set; }
            public double Length { get; set; }
        }

        private class GridFamily
        {
            public double Angle { get; set; }
            public List<GridLine> Lines { get; set; }
            public double Step { get; set; }
            public double Regularity { get; set; }
            public double Continuity { get; set; }
            public double OuterScore { get; set; }
            public double Score { get; set; }
        }
    }
}