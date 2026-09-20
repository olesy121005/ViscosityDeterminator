using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace ViscosityDeterminator.Models
{
    public class DiagramGrid
    {
        public int Id { get; set; }

        public int DiagramId { get; set; }

        public string Component { get; set; } = "";

        public double Value { get; set; }

        public double X1 { get; set; }
        public double Y1 { get; set; }

        public double X2 { get; set; }
        public double Y2 { get; set; }
    }
}
