namespace ATAP.BlazorUI.ViewModels;

public sealed class FlightMapPoint
{
    public string Callsign  { get; init; } = "";
    public double Latitude  { get; init; }
    public double Longitude { get; init; }
    public int    Altitude  { get; init; }
}
