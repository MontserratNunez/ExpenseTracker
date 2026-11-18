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

    /// <summary>Capitalized month name, e.g. "Noviembre".</summary>
    public static string MonthName(int month)
    {
        var name = Culture.DateTimeFormat.GetMonthName(month);
        return name.Length == 0 ? name : char.ToUpper(name[0], Culture) + name[1..];
    }

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
