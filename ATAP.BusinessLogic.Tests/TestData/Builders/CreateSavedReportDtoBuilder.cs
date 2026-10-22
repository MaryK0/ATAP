using ATAP.BusinessLogic.DTO;

namespace ATAP.BusinessLogic.Tests.TestData.Builders;

/// <summary>Data Builder для <see cref="CreateSavedReportDto"/> — удобен для граничных значений периода.</summary>
public class CreateSavedReportDtoBuilder
{
    private string _name = "Отчёт";
    private int _airportId = 1;
    private DateOnly _dateFrom = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
    private DateOnly _dateTo = DateOnly.FromDateTime(DateTime.Today);

    public CreateSavedReportDtoBuilder WithName(string name) { _name = name; return this; }
    public CreateSavedReportDtoBuilder WithAirportId(int airportId) { _airportId = airportId; return this; }
    public CreateSavedReportDtoBuilder WithDateFrom(DateOnly date) { _dateFrom = date; return this; }
    public CreateSavedReportDtoBuilder WithDateTo(DateOnly date) { _dateTo = date; return this; }

    /// <summary>Граничный случай: период нулевой длины (from == to) — валиден по бизнес-правилу date_from &lt;= date_to.</summary>
    public CreateSavedReportDtoBuilder WithSingleDayPeriod(DateOnly day) { _dateFrom = _dateTo = day; return this; }

    /// <summary>Граничный (невалидный) случай: конец периода раньше начала.</summary>
    public CreateSavedReportDtoBuilder WithInvertedPeriod()
    {
        _dateFrom = DateOnly.FromDateTime(DateTime.Today);
        _dateTo = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        return this;
    }

    public CreateSavedReportDto Build() => new()
    {
        Name = _name,
        AirportId = _airportId,
        DateFrom = _dateFrom,
        DateTo = _dateTo
    };

    public static implicit operator CreateSavedReportDto(CreateSavedReportDtoBuilder builder) => builder.Build();
}
