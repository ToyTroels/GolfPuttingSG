namespace GolfSG.Core.Models;

public class GolfShot
{
    public int Id { get; set; }

    public int RoundId { get; set; }

    public int HoleNumber { get; set; }

    public int Par { get; set; }

    public int ShotNumber { get; set; }

    public double StartDistanceToPin { get; set; }

    public DistanceUnit StartDistanceUnit { get; set; }

    public ShotLie StartLie { get; set; }

    public double StartDistanceToGreenEdgeYards { get; set; }

    public double EndDistanceToPin { get; set; }

    public DistanceUnit EndDistanceUnit { get; set; }

    public ShotLie EndLie { get; set; }

    public double EndDistanceToGreenEdgeYards { get; set; }

    public int PenaltyStrokes { get; set; }

    public bool Holed { get; set; }

    public bool IsTeeShot { get; set; }
}
