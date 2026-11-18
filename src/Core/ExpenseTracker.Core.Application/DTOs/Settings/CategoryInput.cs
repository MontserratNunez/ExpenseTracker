using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Core.Application.DTOs.Settings;

public class CategoryInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El color es obligatorio.")]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "El color debe tener el formato #RRGGBB.")]
    [Display(Name = "Color")]
    public string Color { get; set; } = "#6C757D";

    [StringLength(50, ErrorMessage = "El ícono no puede superar {1} caracteres.")]
    [Display(Name = "Ícono")]
    public string? Icon { get; set; }

    [Display(Name = "Es de ahorro")]
    public bool IsSavings { get; set; }
}
