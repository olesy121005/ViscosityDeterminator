using ViscosityDeterminator.Data;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class DiagramViscosityIsolineService
    {
        private readonly KnowledgeBaseContext _context;

        public DiagramViscosityIsolineService(
            KnowledgeBaseContext context)
        {
            _context = context;
        }

        public List<DiagramViscosityIsoline> GetIsolines(
            int diagramId)
        {
            return _context.DiagramViscosityIsolines
                .Where(x => x.DiagramId == diagramId)
                .OrderBy(x => x.Viscosity)
                .ToList();
        }

        public DiagramViscosityIsoline? GetIsoline(
            int id)
        {
            return _context.DiagramViscosityIsolines
                .FirstOrDefault(x => x.Id == id);
        }

        public void ReplaceIsolines(
            int diagramId,
            IEnumerable<DiagramViscosityIsoline> isolines)
        {
            var oldIsolines =
                _context.DiagramViscosityIsolines
                    .Where(x => x.DiagramId == diagramId)
                    .ToList();

            _context.DiagramViscosityIsolines.RemoveRange(
                oldIsolines);

            foreach (var source in isolines)
            {
                var isoline =
                    new DiagramViscosityIsoline
                    {
                        DiagramId = diagramId,
                        Viscosity = source.Viscosity,
                        Geometry = source.Geometry,
                        IsVerified = source.IsVerified,
                        Confidence = source.Confidence
                    };

                _context.DiagramViscosityIsolines.Add(
                    isoline);
            }

            _context.SaveChanges();
        }

        public void AddIsoline(
            DiagramViscosityIsoline isoline)
        {
            _context.DiagramViscosityIsolines.Add(
                isoline);

            _context.SaveChanges();
        }

        public void UpdateIsoline(
            DiagramViscosityIsoline isoline)
        {
            _context.DiagramViscosityIsolines.Update(
                isoline);

            _context.SaveChanges();
        }

        public void DeleteIsoline(
            int id)
        {
            var isoline =
                _context.DiagramViscosityIsolines
                    .FirstOrDefault(x => x.Id == id);

            if (isoline == null)
                return;

            _context.DiagramViscosityIsolines.Remove(
                isoline);

            _context.SaveChanges();
        }
    }
}