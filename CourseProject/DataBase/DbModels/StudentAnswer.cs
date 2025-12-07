namespace CourseProject.DataBase.DbModels;

public class StudentAnswer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public virtual Question Question { get; set; }
    public int TestAttemptId { get; set; }
    public float PointsScored { get; set; } 
    public virtual TestAttempt TestAttempt { get; set; }
    public virtual List<AnswerOption> SelectedOptions { get; set; } = new();
}