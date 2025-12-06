using CourseProject.DataBase.DbModels;

namespace CourseProject.Models.TeacherViewModels;

public class SubjectAccessViewModel
{
    public int SubjectId { get; set; }
    public string SubjectTitle { get; set; }
    public List<Group> AvailableGroups { get; set; } = new();
    public List<int> SelectedGroupIds { get; set; } = new(); 
}