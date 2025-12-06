using CourseProject.DataBase.DbModels;

namespace CourseProject.Models.SubjectModels;

public class ConfigureGroupsModel
{
    public int SubjectId { get; set; }
    public string SubjectTitle { get; set; }
    public List<Group> AllGroups { get; set; } = new();
    public List<int> SelectedGroupIds { get; set; } = new();
}