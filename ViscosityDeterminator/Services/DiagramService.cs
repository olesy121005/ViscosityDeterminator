using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using System.IO;
using ViscosityDeterminator.Data;
using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class DiagramService
    {
        public void InitializeDatabase()
        {
            using var context =
                new KnowledgeBaseContext();

            context.Database.EnsureCreated();
        }


        // =========================================================
        // ДОБАВЛЕНИЕ ДИАГРАММЫ ИЗ ФАЙЛА
        // =========================================================

        public Diagram AddDiagram(
            string name,
            double al2o3,
            double temperature,
            string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Файл изображения диаграммы не найден.",
                    filePath);
            }

            byte[] imageData =
                File.ReadAllBytes(filePath);

            string extension =
                Path.GetExtension(filePath)
                    .ToLowerInvariant();

            string imageFormat =
                extension switch
                {
                    ".png" =>
                        "image/png",

                    ".jpg" =>
                        "image/jpeg",

                    ".jpeg" =>
                        "image/jpeg",

                    ".bmp" =>
                        "image/bmp",

                    ".tif" =>
                        "image/tiff",

                    ".tiff" =>
                        "image/tiff",

                    _ =>
                        "application/octet-stream"
                };

            var diagram =
                new Diagram
                {
                    Name =
                        name,

                    Al2O3 =
                        al2o3,

                    Temperature =
                        temperature,

                    ImageData =
                        imageData,

                    ImageFormat =
                        imageFormat,

                    CreatedAt =
                        DateTime.Now
                };

            using var context =
                new KnowledgeBaseContext();

            context.Diagrams.Add(
                diagram);

            context.SaveChanges();

            return diagram;
        }


        // =========================================================
        // ДОБАВЛЕНИЕ ДИАГРАММЫ ИЗ MAT
        // =========================================================

        public Diagram AddDiagram(
            string name,
            double al2o3,
            double temperature,
            Mat image)
        {
            if (image == null ||
                image.Empty())
            {
                throw new ArgumentException(
                    "Изображение диаграммы пустое.",
                    nameof(image));
            }

            Cv2.ImEncode(
                ".png",
                image,
                out byte[] imageData);

            var diagram =
                new Diagram
                {
                    Name =
                        name,

                    Al2O3 =
                        al2o3,

                    Temperature =
                        temperature,

                    ImageData =
                        imageData,

                    ImageFormat =
                        "image/png",

                    CreatedAt =
                        DateTime.Now
                };

            using var context =
                new KnowledgeBaseContext();

            context.Diagrams.Add(
                diagram);

            context.SaveChanges();

            return diagram;
        }


        // =========================================================
        // ПОЛУЧИТЬ ВСЕ ДИАГРАММЫ
        // =========================================================

        public List<Diagram> GetDiagrams()
        {
            using var context =
                new KnowledgeBaseContext();

            return context.Diagrams
                .AsNoTracking()
                .OrderBy(d => d.Temperature)
                .ToList();
        }


        // =========================================================
        // ПОЛУЧИТЬ ДИАГРАММУ ПО ID
        // =========================================================

        public Diagram? GetDiagram(
            int id)
        {
            using var context =
                new KnowledgeBaseContext();

            return context.Diagrams
                .AsNoTracking()
                .FirstOrDefault(
                    d => d.Id == id);
        }


        // =========================================================
        // ПОИСК ДИАГРАММЫ
        // =========================================================

        public Diagram? FindDiagram(
            double al2o3,
            double temperature)
        {
            using var context =
                new KnowledgeBaseContext();

            return context.Diagrams
                .AsNoTracking()
                .FirstOrDefault(
                    d =>
                        Math.Abs(
                            d.Al2O3 - al2o3) < 0.0001
                        &&
                        Math.Abs(
                            d.Temperature - temperature) < 0.0001);
        }


        // =========================================================
        // ИЗМЕНЕНИЕ ПАРАМЕТРОВ ДИАГРАММЫ
        // =========================================================

        public void UpdateDiagramInfo(
            int diagramId,
            string name,
            double al2o3,
            double temperature)
        {
            using var context =
                new KnowledgeBaseContext();

            var diagram =
                context.Diagrams
                    .FirstOrDefault(
                        d => d.Id == diagramId);

            if (diagram == null)
            {
                throw new InvalidOperationException(
                    "Диаграмма не найдена в базе знаний.");
            }

            diagram.Name =
                name;

            diagram.Al2O3 =
                al2o3;

            diagram.Temperature =
                temperature;

            context.SaveChanges();
        }
    }
}