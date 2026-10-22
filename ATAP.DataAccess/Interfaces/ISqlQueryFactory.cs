// ATAP.DataAccess/Interfaces/ISqlQueryFactory.cs
namespace ATAP.DataAccess.Interfaces;

public interface ISqlQueryFactory
{
    string GetCongestionQuery(bool includeDetails);
    string GetCurrentFlightsQuery();
    string GetAirportsQuery();
    string GetUserByUsernameQuery();
    string CreateUserQuery();
}