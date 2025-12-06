namespace CourseProject.Models.AdminViewModels.Subject;

using DataBase.DbModels;

public class ManageSubjectsViewModel
{
    public List<Subject>? Subjects { get; set; }
    public List<User>? AllTeachers { get; set; }
}