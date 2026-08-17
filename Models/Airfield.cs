namespace FlightPlanAgent.Models;

public record Airfield(
    string Icao,
    string Name,
    double LatDeg,
    double LonDeg,
    int ElevationFt,
    string RunwayInfo,
    string Notes
);
