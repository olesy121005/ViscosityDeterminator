using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using ViscosityDeterminator.Data;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class DiagramGridService
    {
        private readonly KnowledgeBaseContext _db;

        public DiagramGridService(KnowledgeBaseContext db)
        {
            _db = db;
        }

        public List<DiagramGridLine> GetLines(int diagramId)
        {
            return _db.DiagramGridLines
                .Where(x => x.DiagramId == diagramId)
                .OrderBy(x => x.Component)
                .ThenBy(x => x.Value)
                .ToList();
        }

        public List<DiagramGridLine> GetLines(
            int diagramId,
            string component)
        {
            return _db.DiagramGridLines
                .Where(x =>
                    x.DiagramId == diagramId &&
                    x.Component == component)
                .OrderBy(x => x.Value)
                .ToList();
        }

        public DiagramGridLine? GetLine(
            int diagramId,
            string component,
            double value)
        {
            return _db.DiagramGridLines
                .FirstOrDefault(x =>
                    x.DiagramId == diagramId &&
                    x.Component == component &&
                    Math.Abs(x.Value - value) < 0.001);
        }

        public void ReplaceLines(
            int diagramId,
            IEnumerable<DiagramGridLine> lines)
        {
            var oldLines = _db.DiagramGridLines
                .Where(x => x.DiagramId == diagramId)
                .ToList();

            _db.DiagramGridLines.RemoveRange(oldLines);

            foreach (var line in lines)
            {
                line.Id = 0;
                line.DiagramId = diagramId;

                _db.DiagramGridLines.Add(line);
            }

            _db.SaveChanges();
        }

        public void AddLine(DiagramGridLine line)
        {
            _db.DiagramGridLines.Add(line);
            _db.SaveChanges();
        }

        public void UpdateLine(DiagramGridLine line)
        {
            _db.DiagramGridLines.Update(line);
            _db.SaveChanges();
        }

        public void DeleteLine(int id)
        {
            var line = _db.DiagramGridLines
                .FirstOrDefault(x => x.Id == id);

            if (line == null)
                return;

            _db.DiagramGridLines.Remove(line);
            _db.SaveChanges();
        }

        public static Point2f ToPixel(
            DiagramGridLine line,
            int width,
            int height)
        {
            return new Point2f(
                (float)(line.X1 * width),
                (float)(line.Y1 * height));
        }

        public static Point2f ToPixel2(
            DiagramGridLine line,
            int width,
            int height)
        {
            return new Point2f(
                (float)(line.X2 * width),
                (float)(line.Y2 * height));
        }
    }
}