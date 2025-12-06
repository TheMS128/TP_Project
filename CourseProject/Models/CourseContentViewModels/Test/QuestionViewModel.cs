using System.ComponentModel.DataAnnotations;

namespace CourseProject.Models.CourseContentViewModels.Test;

public class QuestionViewModel
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public int SubjectId { get; set; } 
    [Required(ErrorMessage = "Текст вопроса обязателен")] public string Text { get; set; } = null!;
    [Display(Name = "Тип вопроса")] public string Type { get; set; } = "Single"; 
    [Range(1, 100, ErrorMessage = "Баллы должны быть от 1 до 100")] public int Points { get; set; } = 1;
    public List<AnswerOptionViewModel> Answers { get; set; } = new List<AnswerOptionViewModel>();
}