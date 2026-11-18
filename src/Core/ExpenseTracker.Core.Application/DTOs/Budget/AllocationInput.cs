using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Budget;

public class AllocationInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una categoría.")]
    public int CategoryId { get; set; }

    public AllocationType AllocationType { get; set; } = AllocationType.FixedAmount;

    /// <summary>An amount (FixedAmount) or a percentage from 0 to 100 (PercentageOfIncome).</summary>
    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal Value { get; set; }

    /// <summary>For percentages: the source to take the percentage from. Null means total recurring income.</summary>
    public int? IncomeSourceId { get; set; }
}
