namespace ATAP.Api.Infrastructure;

/// <summary>
/// Правила периода (контракт и README): <c>dateFrom</c> не позже <c>dateTo</c>, период не дальше
/// 31 дня; обе даты включаются. Репозитории принимают полуоткрытый интервал [начало; конец), поэтому
/// конец периода преобразуется в полночь следующих суток.
/// </summary>
public static class PeriodRules
{
    public const int MaxDays = 31;

    public static (string Slug, string Title)? Validate(DateOnly from, DateOnly to)
    {
        if (from > to)
            return ("invalid-period", "Дата начала не может быть позже даты окончания");
        if (to.DayNumber - from.DayNumber > MaxDays)
            return ("period-too-long", "Период не может быть больше 31 дня");
        return null;
    }

    public static (DateTime Start, DateTime EndExclusive) ToInterval(DateOnly from, DateOnly to) =>
        (from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
         to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
