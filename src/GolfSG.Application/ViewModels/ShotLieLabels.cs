using GolfSG.Core.Models;

namespace GolfSG.Application.ViewModels;

public static class ShotLieLabels
{
    public static ShotLie Parse(string text)
    {
        return text switch
        {
            "Kortklippet" => ShotLie.FairwayCut,
            "Fairway cut" => ShotLie.FairwayCut,
            "Fairway" => ShotLie.Fairway,
            "Tee" => ShotLie.Tee,
            "Sand" => ShotLie.Sand,
            "Problemlie" => ShotLie.Recovery,
            "Recovery" => ShotLie.Recovery,
            "Green" => ShotLie.Green,
            "I hul" => ShotLie.Holed,
            "Holed" => ShotLie.Holed,
            _ => ShotLie.Rough
        };
    }

    public static string Format(ShotLie lie)
    {
        return lie switch
        {
            ShotLie.FairwayCut => "Kortklippet",
            ShotLie.Fairway => "Fairway",
            ShotLie.Tee => "Tee",
            ShotLie.Sand => "Sand",
            ShotLie.Recovery => "Problemlie",
            ShotLie.Green => "Green",
            ShotLie.Holed => "I hul",
            _ => "Rough"
        };
    }

    public static string FormatAroundGreenStart(ShotLie lie)
    {
        return lie switch
        {
            ShotLie.Fairway or ShotLie.FairwayCut => "Kortklippet",
            ShotLie.Sand => "Sand",
            ShotLie.Recovery => "Problemlie",
            _ => "Rough"
        };
    }
}
