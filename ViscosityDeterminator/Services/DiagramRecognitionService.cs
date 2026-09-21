using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ViscosityDeterminator.Services
{
    public class DiagramRecognitionService
    {
        public Mat LoadImage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "Путь к файлу не указан.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    "Файл изображения не найден.",
                    filePath);

            var image =
                Cv2.ImRead(
                    filePath,
                    ImreadModes.Color);

            if (image.Empty())
            {
                image.Dispose();

                throw new InvalidOperationException(
                    "OpenCV не смог загрузить изображение.");
            }

            return image;
        }

        public GridRecognitionResult FindGrid(Mat image)
        {
            if (image == null || image.Empty())
                throw new ArgumentException(
                    "Изображение отсутствует.");

            using var hsv = new Mat();

            Cv2.CvtColor(
                image,
                hsv,
                ColorConversionCodes.BGR2HSV);

            using var mask = new Mat();

            // Синие линии сетки.
            Cv2.InRange(
                hsv,
                new Scalar(85, 60, 40),
                new Scalar(140, 255, 255),
                mask);

            int minDimension =
                Math.Min(
                    image.Width,
                    image.Height);

            var detected =
                Cv2.HoughLinesP(
                    mask,
                    1.0,
                    Math.PI / 720.0,
                    10,
                    minDimension * 0.04,
                    30);

            var lines =
                new List<RecognizedGridLine>();

            foreach (var segment in detected)
            {
                var p1 =
                    new Point2f(
                        segment.P1.X,
                        segment.P1.Y);

                var p2 =
                    new Point2f(
                        segment.P2.X,
                        segment.P2.Y);

                double length =
                    Point2f.Distance(
                        p1,
                        p2);

                if (length <
                    minDimension * 0.04)
                {
                    continue;
                }

                double angle =
                    GetAngle(
                        p1,
                        p2);

                angle =
                    NormalizeAngle(angle);

                string? component = null;

                // CaO
                if (angle >= 42 &&
                    angle <= 78)
                {
                    component = "CaO";
                }

                // MgO
                else if (angle >= -78 &&
                         angle <= -42)
                {
                    component = "MgO";
                }

                // SiO2
                else if (Math.Abs(angle) <= 10)
                {
                    component = "SiO2";
                }

                if (component == null)
                    continue;

                lines.Add(
                    new RecognizedGridLine
                    {
                        Component = component,
                        P1 = p1,
                        P2 = p2,
                        Confidence =
                            CalculateConfidence(
                                length,
                                minDimension)
                    });
            }

            return new GridRecognitionResult
            {
                Lines = MergeSimilarLines(lines)
            };
        }

        public Mat DrawDetectedGrid(
            Mat image,
            GridRecognitionResult grid)
        {
            var result =
                image.Clone();

            foreach (var line in grid.Lines)
            {
                var color =
                    line.Component switch
                    {
                        "CaO" =>
                            new Scalar(
                                0, 0, 255),

                        "MgO" =>
                            new Scalar(
                                0, 255, 0),

                        "SiO2" =>
                            new Scalar(
                                0, 255, 255),

                        _ =>
                            new Scalar(
                                255, 255, 255)
                    };

                Cv2.Line(
                    result,
                    new Point(
                        (int)line.P1.X,
                        (int)line.P1.Y),
                    new Point(
                        (int)line.P2.X,
                        (int)line.P2.Y),
                    color,
                    2,
                    LineTypes.AntiAlias);
            }

            return result;
        }

        private static double GetAngle(
            Point2f p1,
            Point2f p2)
        {
            return Math.Atan2(
                       p2.Y - p1.Y,
                       p2.X - p1.X)
                   * 180.0 /
                   Math.PI;
        }

        private static double NormalizeAngle(
            double angle)
        {
            while (angle > 90)
                angle -= 180;

            while (angle < -90)
                angle += 180;

            return angle;
        }

        private static double CalculateConfidence(
            double length,
            double imageSize)
        {
            return Math.Clamp(
                (length / imageSize) * 2.0,
                0.0,
                1.0);
        }

        private static List<RecognizedGridLine>
            MergeSimilarLines(
                List<RecognizedGridLine> lines)
        {
            var result =
                new List<RecognizedGridLine>();

            foreach (var line in lines)
            {
                bool duplicate = false;

                foreach (var existing in result)
                {
                    if (existing.Component !=
                        line.Component)
                    {
                        continue;
                    }

                    double angle1 =
                        NormalizeAngle(
                            GetAngle(
                                line.P1,
                                line.P2));

                    double angle2 =
                        NormalizeAngle(
                            GetAngle(
                                existing.P1,
                                existing.P2));

                    if (Math.Abs(
                            angle1 - angle2) > 5)
                    {
                        continue;
                    }

                    double distance =
                        DistanceBetweenLines(
                            line,
                            existing);

                    if (distance < 15)
                    {
                        duplicate = true;

                        if (line.Confidence >
                            existing.Confidence)
                        {
                            existing.P1 =
                                line.P1;

                            existing.P2 =
                                line.P2;

                            existing.Confidence =
                                line.Confidence;
                        }

                        break;
                    }
                }

                if (!duplicate)
                    result.Add(line);
            }

            return result;
        }

        private static double DistanceBetweenLines(
            RecognizedGridLine a,
            RecognizedGridLine b)
        {
            double d1 =
                DistancePointToLine(
                    a.P1,
                    b.P1,
                    b.P2);

            double d2 =
                DistancePointToLine(
                    a.P2,
                    b.P1,
                    b.P2);

            return Math.Min(
                d1,
                d2);
        }

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

            if (denominator < 0.0001)
                return double.MaxValue;

            return Math.Abs(
                       dy * p.X -
                       dx * p.Y +
                       b.X * a.Y -
                       b.Y * a.X)
                   / denominator;
        }
    }

    public class GridRecognitionResult
    {
        public List<RecognizedGridLine> Lines { get; set; }
            = new();
    }

    public class RecognizedGridLine
    {
        public string Component { get; set; }
            = string.Empty;

        public Point2f P1 { get; set; }

        public Point2f P2 { get; set; }

        public double Confidence { get; set; }
    }
}