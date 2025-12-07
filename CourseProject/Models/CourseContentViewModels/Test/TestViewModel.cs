using System.ComponentModel.DataAnnotations;
using CourseProject.DataBase.Enums;

namespace CourseProject.Models.CourseContentViewModels.Test;

public class TestViewModel
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    [Required(ErrorMessage = "Название теста обязательно")] public string Title { get; set; } = null!;
    [Display(Name = "Дней на выполнение")] public int? DaysToComplete { get; set; }
    [Display(Name = "Лимит времени (минуты)")] public int? TimeLimitMinutes { get; set; }
    [Display(Name = "Максимум попыток")] public int? MaxAttempts { get; set; }
    [Display(Name = "Статус")] public ContentStatus Status { get; set; } = ContentStatus.Hidden;
}