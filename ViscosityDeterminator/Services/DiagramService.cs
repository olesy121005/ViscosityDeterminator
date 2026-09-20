using Microsoft.EntityFrameworkCore;
using System.IO;
using ViscosityDeterminator.Data;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class DiagramService
    {
        public void InitializeDatabase()
        {
            using var context = new KnowledgeBaseContext();

            context.Database.EnsureCreated();
        }

        public void AddDiagram(string name, double al2o3, double temperature, string filePath)
        {
            byte[] imageData = File.ReadAllBytes(filePath);

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            string imageFormat = extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".tif" => "image/tiff",
                ".tiff" => "image/tiff",
                _ => "application/octet-stream"
            };

            var diagram = new Diagram
            {
                Name = name,
                Al2O3 = al2o3,
                Temperature = temperature,
                ImageData = imageData,
                ImageFormat = imageFormat,
                CreatedAt = DateTime.Now
            };

            using var context = new KnowledgeBaseContext();
            context.Diagrams.Add(diagram);
            context.SaveChanges();
        }

        public List<Diagram> GetDiagrams()
        {
            using var context = new KnowledgeBaseContext();

            return context.Diagrams
                .AsNoTracking()
                .OrderBy(d => d.Temperature)
                .ToList();
        }
        public Diagram? FindDiagram(double al2o3, double temperature)
        {
            using var context = new KnowledgeBaseContext();

            return context.Diagrams
                .AsNoTracking()
                .FirstOrDefault(d =>
                    Math.Abs(d.Al2O3 - al2o3) < 0.0001 &&
                    Math.Abs(d.Temperature - temperature) < 0.0001);
        }
    }
}
