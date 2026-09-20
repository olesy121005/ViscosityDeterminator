using OpenCvSharp;


namespace ViscosityDeterminator.Models
{
    public class DiagramVertices
    {
        public List<Point> Points { get; set; } = new();

        public int Count => Points.Count;
    }
}
