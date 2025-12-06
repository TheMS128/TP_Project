using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.TeacherViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseProject.Controllers;

[Authorize(Roles = "Admin, Teacher")]
public class TeacherController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IWebHostEnvironment _appEnvironment;

    public TeacherController(ApplicationDbContext context, UserManager<User> userManager,
        IWebHostEnvironment appEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _appEnvironment = appEnvironment;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var subjects = await _context.Subjects
            .Where(s => s.Teachers.Any(t => t.Id == user.Id))
            .ToListAsync();

        if (User.IsInRole("Admin"))
        {
            subjects = await _context.Subjects.ToListAsync();
        }

        return View(subjects);
    }

    public async Task<IActionResult> ManageSubject(int id)
    {
        var subject = await _context.Subjects
            .Include(s => s.Lectures)
            .Include(s => s.Tests)
            .Include(s => s.EnrolledGroups)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subject == null) return NotFound();

        if (!User.IsInRole("Admin"))
        {
            var user = await _userManager.GetUserAsync(User);
            var isAssigned = await _context.Subjects
                .AnyAsync(s => s.Id == id && s.Teachers.Any(t => t.Id == user.Id));
            if (!isAssigned) return Forbid();
        }

        var model = new SubjectManageViewModel
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            Description = subject.Description,
            Status = subject.Status, 
            Lectures = subject.Lectures ?? new List<Lecture>(),
            Tests = subject.Tests ?? new List<Test>(),
            EnrolledGroups = subject.EnrolledGroups ?? new List<Group>()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeSubjectStatus(int subjectId, ContentStatus newStatus)
    {
        var subject = await _context.Subjects.FindAsync(subjectId);
        if (subject == null) return NotFound();
        if (!User.IsInRole("Admin"))
        {
            var user = await _userManager.GetUserAsync(User);
            var isAssigned = await _context.Subjects
                .AnyAsync(s => s.Id == subjectId && s.Teachers.Any(t => t.Id == user.Id));
            if (!isAssigned) return Forbid();
        }

        subject.Status = newStatus;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(ManageSubject), new { id = subjectId });
    }

    [HttpGet]
    public async Task<IActionResult> ConfigureGroups(int subjectId)
    {
        var subject = await _context.Subjects
            .Include(s => s.EnrolledGroups)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null) return NotFound();

        var allGroups = await _context.Groups.OrderBy(g => g.GroupName).ToListAsync();

        var model = new SubjectAccessViewModel
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            AvailableGroups = allGroups,
            SelectedGroupIds = subject.EnrolledGroups.Select(g => g.Id).ToList()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSubjectGroups(UpdateSubjectGroupsViewModel model)
    {
        var subject = await _context.Subjects
            .Include(s => s.EnrolledGroups)
            .FirstOrDefaultAsync(s => s.Id == model.SubjectId);

        if (subject != null)
        {
            subject.EnrolledGroups.Clear();

            if (model.SelectedGroupIds != null && model.SelectedGroupIds.Any())
            {
                var groupsToAdd = await _context.Groups
                    .Where(g => model.SelectedGroupIds.Contains(g.Id))
                    .ToListAsync();

                subject.EnrolledGroups.AddRange(groupsToAdd);
            }

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageSubject), new { id = model.SubjectId });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeLectureState(int lectureId, ContentStatus newStatus)
    {
        var lecture = await _context.Lectures.FindAsync(lectureId);
        if (lecture == null) return NotFound();

        lecture.Status = newStatus;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(ManageSubject), new { id = lecture.SubjectId });
    }

    [HttpGet]
    public IActionResult CreateLecture(int subjectId)
    {
        return View(new CreateLectureViewModel { SubjectId = subjectId });
    }

    [HttpPost]
    public async Task<IActionResult> CreateLecture(CreateLectureViewModel model, string actionType)
    {
        if (ModelState.IsValid)
        {
            string path = "/files/lectures/"; 
            string uniqueFileName = "no_file";

            if (model.UploadedFile != null)
            {
                string uploadsFolder = Path.Combine(_appEnvironment.WebRootPath, "files", "lectures");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                uniqueFileName = Guid.NewGuid().ToString() + "_" + model.UploadedFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.UploadedFile.CopyToAsync(fileStream);
                }
            }

            ContentStatus status = actionType switch
            {
                "Publish" => ContentStatus.Published,
                "Hide" => ContentStatus.Hidden,
                _ => ContentStatus.Draft
            };

            var lecture = new Lecture
            {
                Title = model.Title,
                SubjectId = model.SubjectId,
                DateAdded = DateTime.Now,
                FilePath = path + uniqueFileName,
                Status = status
            };

            _context.Lectures.Add(lecture);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ManageSubject), new { id = model.SubjectId });
        }

        return View(model);
    }
}