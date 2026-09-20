using System;

namespace ViscosityDeterminator.Models
{
    public class Diagram
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Al2O3 { get; set; }
        public double Temperature { get; set; }
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public string ImageFormat { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}