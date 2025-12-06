using CourseProject.DataBase.DbModels;

namespace CourseProject.Models.AdminViewModels.Student;

public class AddStudentToGroupViewModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = null!;
    public string SelectedStudentId { get; set; } = null!;
    public List<User> AvailableStudents { get; set; } = new();
}