using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ViscosityDeterminator.Models
{
    public class DiagramViscosityIsoline
    {
        [Key]
        public int Id { get; set; }

        public int DiagramId { get; set; }

        /// <summary>
        /// Значение вязкости изолинии, Па·с.
        /// </summary>
        public double Viscosity { get; set; }

        /// <summary>
        /// Геометрия изолинии в виде JSON.
        /// Координаты нормализованы от 0 до 1.
        /// </summary>
        public string Geometry { get; set; } = "[]";

        /// <summary>
        /// Подтверждена ли изолиния пользователем.
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Уверенность автоматического распознавания.
        /// </summary>
        public double Confidence { get; set; }

        [NotMapped]
        public List<DiagramIsolinePoint> Points
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Geometry))
                    return new List<DiagramIsolinePoint>();

                try
                {
                    return JsonSerializer.Deserialize<
                        List<DiagramIsolinePoint>>(Geometry)
                        ?? new List<DiagramIsolinePoint>();
                }
                catch
                {
                    return new List<DiagramIsolinePoint>();
                }
            }

            set
            {
                Geometry = JsonSerializer.Serialize(
                    value ?? new List<DiagramIsolinePoint>());
            }
        }
    }

    public class DiagramIsolinePoint
    {
        public double X { get; set; }

        public double Y { get; set; }

        public DiagramIsolinePoint()
        {
        }

        public DiagramIsolinePoint(
            double x,
            double y)
        {
            X = x;
            Y = y;
        }
    }
}