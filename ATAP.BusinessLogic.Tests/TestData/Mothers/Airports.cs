using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Tests.TestData.Mothers;

/// <summary>Object Mother для аэропортов — реальные ICAO-коды из сид-данных проекта (db/create_tables.sql).</summary>
public static class Airports
{
    public static AirportDb Sheremetyevo() => new()
    {
        AirportId = 1,
        Name = "Шереметьево",
        IcaoCode = "UUEE",
        Latitude = 55.972,
        Longitude = 37.415
    };

    public static AirportDb Domodedovo() => new()
    {
        AirportId = 2,
        Name = "Домодедово",
        IcaoCode = "UUDD",
        Latitude = 55.409,
        Longitude = 37.902
    };

    public static List<AirportDb> Default() => new() { Sheremetyevo(), Domodedovo() };
}
