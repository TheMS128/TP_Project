using System.ComponentModel.DataAnnotations;

namespace CourseProject.Models.CourseContentViewModels.Test;

public class AnswerOptionViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Текст ответа обязателен")] public string Text { get; set; } = null!;
    public bool IsCorrect { get; set; }
}