namespace CourseProject.Models.CourseContentViewModels.Test;

public class UserAnswerInputModel
{
    public int QuestionId { get; set; }
    public List<int> SelectedOptionIds { get; set; } = new();
}