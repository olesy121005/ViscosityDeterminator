using ViscosityDeterminator.Models;

namespace ViscosityDeterminator
{
    public class DiagramGridLineViewModel
    {
        public DiagramGridLine Model { get; }

        public string DisplayText
        {
            get
            {
                string verified =
                    Model.IsVerified
                        ? "✓"
                        : "○";

                return
                    $"{verified}  " +
                    $"{Model.Component} = " +
                    $"{Model.Value:0.###}";
            }
        }

        public DiagramGridLineViewModel(
            DiagramGridLine model)
        {
            Model = model;
        }
    }
}