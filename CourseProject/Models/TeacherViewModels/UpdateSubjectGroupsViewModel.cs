namespace CourseProject.Models.TeacherViewModels;

public class UpdateSubjectGroupsViewModel
{
    public int SubjectId { get; set; }
    public List<int> SelectedGroupIds { get; set; } = new();
}