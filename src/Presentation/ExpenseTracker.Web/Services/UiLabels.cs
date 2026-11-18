using System.Globalization;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Web.Services;

/// <summary>Spanish labels for enums and months shown in the UI.</summary>
public static class UiLabels
{
    private static readonly CultureInfo Culture = CreateCulture();

    public static string FrequencyName(Frequency frequency) => frequency switch
    {
        Frequency.Weekly => "Semanal",
        Frequency.Biweekly => "Quincenal",
        Frequency.Monthly => "Mensual",
        Frequency.Quarterly => "Trimestral",
        Frequency.SemiAnnual => "Semestral",
        Frequency.Annual => "Anual",
        _ => frequency.ToString()
    };

    public static string DebtTypeName(DebtType type) => type switch
    {
        DebtType.Loan => "Préstamo",
        DebtType.Mortgage => "Hipoteca",
        DebtType.CreditCard => "Tarjeta de crédito",
        DebtType.CarLoan => "Préstamo de vehículo",
        DebtType.Other => "Otra",
        _ => type.ToString()
    };

    public static string GoalPeriodName(GoalPeriod period) => period switch
    {
        GoalPeriod.Monthly => "Mensual",
        GoalPeriod.Annual => "Anual",
        GoalPeriod.Custom => "Personalizado",
        _ => period.ToString()
    };

    /// <summary>Month and year such as "mar 2029".</summary>
    public static string MonthYear(DateOnly date) => $"{ShortMonthName(date.Month).ToLower(Culture)} {date.Year}";

    /// <summary>Capitalized month name, e.g. "Noviembre".</summary>
    public static string MonthName(int month)
    {
        var name = Culture.DateTimeFormat.GetMonthName(month);
        return name.Length == 0 ? name : char.ToUpper(name[0], Culture) + name[1..];
    }

    /// <summary>Three-letter month name, e.g. "Nov".</summary>
    public static string ShortMonthName(int month) => MonthName(month)[..3];

    /// <summary>Short date such as "15 nov".</summary>
    public static string ShortDate(DateOnly date) => $"{date.Day} {ShortMonthName(date.Month).ToLower(Culture)}";

    private static CultureInfo CreateCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo("es-DO");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("es");
        }
    }
}
