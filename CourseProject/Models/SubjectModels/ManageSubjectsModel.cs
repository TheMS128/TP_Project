using CourseProject.DataBase.DbModels;

namespace CourseProject.Models.SubjectModels;

public class ManageSubjectsModel
{
    public List<Subject> Subjects { get; set; } = new();
    public List<User> AllTeachers { get; set; } = new();
    public List<Group> AllGroups { get; set; } = new();
}