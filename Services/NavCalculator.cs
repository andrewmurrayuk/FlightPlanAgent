namespace FlightPlanAgent.Services;

// Approximate PA28-161 Warrior II figures. Adjust to your own POH / actual aircraft numbers —
// these are illustrative for the demo, not performance data to fly with.
public static class Pa28WarriorPerformance
{
    public const double CruiseTasKt = 108.0;      // ~75% power cruise
    public const double FuelBurnGphUsg = 8.5;      // US gallons per hour
    public const double UsableFuelUsg = 46.0;      // usable fuel, PA28-161
    public const double ClimbRateFpm = 700.0;
}

public record LegResult(
    double DistanceNm,
    double TrueHeadingDeg,
    double EstTimeEnrouteMinutes,
    double FuelBurnUsg,
    double FuelRemainingUsg,
    string Notes
);

public class NavCalculator
{
    private const double EarthRadiusNm = 3440.065;

    public LegResult CalculateLeg(
        double fromLat, double fromLon,
        double toLat, double toLon,
        double cruiseTasKt = Pa28WarriorPerformance.CruiseTasKt,
        double windDirDeg = 0,
        double windSpeedKt = 0)
    {
        var (distanceNm, trueHeadingDeg) = GreatCircle(fromLat, fromLon, toLat, toLon);

        // Simple wind correction: headwind/tailwind component only, for a quick demo nav log.
        var windAngleRad = DegreesToRadians(windDirDeg - trueHeadingDeg);
        var headwindComponent = windSpeedKt * Math.Cos(windAngleRad);
        var groundSpeedKt = Math.Max(cruiseTasKt - headwindComponent, 20); // floor to avoid absurd ETEs

        var timeHours = distanceNm / groundSpeedKt;
        var timeMinutes = timeHours * 60.0;
        var fuelBurn = timeHours * Pa28WarriorPerformance.FuelBurnGphUsg;
        var fuelRemaining = Pa28WarriorPerformance.UsableFuelUsg - fuelBurn;

        var notes = fuelRemaining < Pa28WarriorPerformance.UsableFuelUsg * 0.25
            ? "Fuel remaining below typical 25% reserve threshold — review fuel plan."
            : "Fuel margin looks reasonable for a direct routing.";

        return new LegResult(
            Math.Round(distanceNm, 1),
            Math.Round((trueHeadingDeg + 360) % 360, 0),
            Math.Round(timeMinutes, 0),
            Math.Round(fuelBurn, 1),
            Math.Round(fuelRemaining, 1),
            notes
        );
    }

    private (double distanceNm, double trueHeadingDeg) GreatCircle(double lat1, double lon1, double lat2, double lon2)
    {
        var phi1 = DegreesToRadians(lat1);
        var phi2 = DegreesToRadians(lat2);
        var deltaPhi = DegreesToRadians(lat2 - lat1);
        var deltaLambda = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                Math.Cos(phi1) * Math.Cos(phi2) *
                Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distanceNm = EarthRadiusNm * c;

        var y = Math.Sin(deltaLambda) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);
        var bearingRad = Math.Atan2(y, x);
        var bearingDeg = RadiansToDegrees(bearingRad);

        return (distanceNm, bearingDeg);
    }

    private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;
    private static double RadiansToDegrees(double rad) => rad * 180.0 / Math.PI;
}
