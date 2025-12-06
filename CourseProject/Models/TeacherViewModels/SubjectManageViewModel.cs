using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;

namespace CourseProject.Models.TeacherViewModels;

public class SubjectManageViewModel
{
    public int SubjectId { get; set; }
    public string SubjectTitle { get; set; }
    public string Description { get; set; }
    public ContentStatus Status { get; set; } 
    public List<Lecture> Lectures { get; set; } = new();
    public List<Test> Tests { get; set; } = new();
    public List<Group> EnrolledGroups { get; set; } = new();
}