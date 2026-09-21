using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ViscosityDeterminator.Models
{
    public class DiagramGridLine
    {
        [Key]
        public int Id { get; set; }

        public int DiagramId { get; set; }

        /// <summary>
        /// CaO, MgO или SiO2.
        /// </summary>
        [Required]
        public string Component { get; set; } = string.Empty;

        /// <summary>
        /// Значение линии, например 25 для MgO=25%.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Нормализованные координаты начала линии.
        /// Диапазон 0..1.
        /// </summary>
        public double X1 { get; set; }

        public double Y1 { get; set; }

        /// <summary>
        /// Нормализованные координаты конца линии.
        /// </summary>
        public double X2 { get; set; }

        public double Y2 { get; set; }

        /// <summary>
        /// true — линия проверена пользователем.
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Уверенность автоматического распознавания.
        /// 0..1.
        /// </summary>
        public double Confidence { get; set; }

        [NotMapped]
        public OpenCvSharp.Point2f P1 =>
            new OpenCvSharp.Point2f(
                (float)X1,
                (float)Y1);

        [NotMapped]
        public OpenCvSharp.Point2f P2 =>
            new OpenCvSharp.Point2f(
                (float)X2,
                (float)Y2);
    }
}