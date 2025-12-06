namespace CourseProject.DataBase.DbModels;

public class AnswerOption
{
    public int Id { get; set; }
    public string Text { get; set; }
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
    public int QuestionId { get; set; }
    
    public virtual Question Question { get; set; }
}