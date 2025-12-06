using System.ComponentModel.DataAnnotations;

namespace CourseProject.Models.AdminViewModels.Student;

public class CreateStudentViewModel
{
    [Required(ErrorMessage = "ФИО обязательно")]
    public string FullName { get; set; } = null!;
    [Required(ErrorMessage = "Email обязателен"), EmailAddress]
    public string Email { get; set; } = null!;
    [Required(ErrorMessage = "Пароль обязателен")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;
    public string? Description { get; set; }
    [Display(Name = "Группа")] public int? GroupId { get; set; } 
}