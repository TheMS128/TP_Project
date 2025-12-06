using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;

namespace CourseProject.Models.SubjectModels;

public class CourseContentModel
{
    public int Id { get; set; }
    public string Title { get; set; }
    public ContentStatus Status { get; set; }
    public int LecturesCount { get; set; }
    public int TestsCount { get; set; }
    public List<Group> AllGroups { get; set; } = new();
    public List<int> SelectedGroupIds { get; set; } = new();
}