namespace CourseProject.Models.AdminViewModels.Teacher;

using DataBase.DbModels;

public class ManageTeachersViewModel
{
    public List<User> Teachers { get; set; } = new();
    public List<Subject> AllSubjects { get; set; } = new();
}