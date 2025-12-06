using CourseProject.DataBase.Enums;

namespace CourseProject.DataBase.DbModels;

public class Lecture
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string FilePath { get; set; }
    public DateTime DateAdded { get; set; }
    public bool IsPublished { get; set; }
    public int SubjectId { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public virtual Subject Subject { get; set; }
}