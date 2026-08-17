using FlightPlanAgent.Models;

namespace FlightPlanAgent.Data;

// Small, hand-picked set of Scottish airfields for the demo.
// Coordinates/elevations are approximate — good enough for a nav-log demo, not for real navigation.
public static class ScottishAirfields
{
    public static readonly List<Airfield> All = new()
    {
        new("EGPT", "Perth (Scone)",      56.4189, -3.3719,  394, "03/21 grass, 09/27 grass", "PPR, home of Tayside Aviation"),
        new("EGPG", "Cumbernauld",        55.9339, -3.9439,  85,  "08/26 hard 1024m",         "Busy GA field, PPR at weekends"),
        new("EGPJ", "Fife (Glenrothes)",  56.1994, -3.1836,  240, "13/31 hard 780m",           "Small strip, radio only"),
        new("EGPN", "Dundee",             56.4525, -3.0258,  16,  "09/27 hard 1400m",          "ATC field, controlled airspace"),
        new("EGPE", "Inverness",          57.5425, -4.0475,  31,  "05/23 hard 2000m",          "ATC field"),
        new("EGPD", "Aberdeen",           57.2019, -2.1978,  65,  "16/34 hard 1829m",          "Busy controlled/offshore traffic"),
        new("EGPK", "Prestwick",          55.5094, -4.5864,  65,  "12/30 hard 2986m",          "Large ATC field, good weather record"),
        new("EGEO", "Oban (Connel)",      56.4636, -5.3839,  138, "01/19 hard 1024m",          "Scenic west coast approach"),
        new("EGPI", "Islay",              55.6819, -6.2564,  17,  "13/31 hard 1470m",          "Coastal, can be breezy"),
        new("EGPR", "Barra",              57.0228, -7.4431,  6,   "beach runway, tide-dependent","Only scheduled beach airport in the world"),
        new("EGPC", "Wick John O'Groats", 58.4539, -3.0930,  38,  "13/31 hard 1600m",          "Exposed, can be very windy"),
        new("EGPO", "Stornoway",          58.2156, -6.3311,  20,  "18/36 hard 1740m",          "ATC field, Outer Hebrides"),
    };

    public static Airfield? Find(string icao) =>
        All.FirstOrDefault(a => a.Icao.Equals(icao, StringComparison.OrdinalIgnoreCase));
}
