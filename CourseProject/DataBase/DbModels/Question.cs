namespace CourseProject.DataBase.DbModels;

public class Question
{
    public int Id { get; set; }
    public string Text { get; set; }
    public string Type { get; set; }
    public int Points { get; set; }
    public int TestId { get; set; }
    public virtual List<AnswerOption> AnswerOptions { get; set; }
    public virtual Test Test { get; set; }
}