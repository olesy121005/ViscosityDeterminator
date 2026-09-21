using ViscosityDeterminator.Models;

namespace ViscosityDeterminator.Services
{
    public class ViscosityIsolineRecognitionResult
    {
        public List<DiagramViscosityIsoline> Isolines { get; set; }
            = new();

        public int GreenPixelCount { get; set; }

        public double OverallConfidence { get; set; }
    }
}