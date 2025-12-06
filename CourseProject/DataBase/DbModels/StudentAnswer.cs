namespace CourseProject.DataBase.DbModels;

public class StudentAnswer
{
    public int Id { get; set; }
    public float ScorePoints { get; set; }
    public int QuestionId { get; set; }

    public virtual Question Question { get; set; }
    public virtual List<AnswerOption> AnswerOptions { get; set; }
}