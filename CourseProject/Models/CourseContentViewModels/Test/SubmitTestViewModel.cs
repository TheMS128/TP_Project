namespace CourseProject.Models.CourseContentViewModels.Test;

public class SubmitTestViewModel
{
    public int TestAttemptId { get; set; }
    public List<UserAnswerInputModel> Answers { get; set; } = new();
}