using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.CourseContentViewModels.Lecture;
using CourseProject.Models.CourseContentViewModels.Test;
using CourseProject.Models.SubjectModels;

namespace CourseProject.Controllers;

[Authorize(Roles = "Admin,Teacher")]
public class CourseContentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IWebHostEnvironment _appEnvironment;

    public CourseContentController(ApplicationDbContext context, UserManager<User> userManager, IWebHostEnvironment appEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _appEnvironment = appEnvironment;
    }

    private async Task<bool> HasAccessToSubject(int subjectId)
    {
        if (User.IsInRole("Admin")) return true;
        var userId = _userManager.GetUserId(User);
        return await _context.Subjects.AsNoTracking()
            .AnyAsync(s => s.Id == subjectId && s.Teachers.Any(t => t.Id == userId));
    }

    [HttpGet]
    public async Task<IActionResult> Index(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();

        var subject = await _context.Subjects
            .AsNoTracking()
            .Include(s => s.Lectures)
            .Include(s => s.Tests)
            .Include(s => s.EnrolledGroups)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null) return NotFound();

        var model = new CourseContentModel
        {
            Id = subject.Id,
            Title = subject.Title,
            Status = subject.Status,
            LecturesCount = subject.Lectures?.Count ?? 0,
            TestsCount = subject.Tests?.Count ?? 0,
            AllGroups = await _context.Groups.AsNoTracking().OrderBy(g => g.GroupName).ToListAsync(),
            SelectedGroupIds = subject.EnrolledGroups.Select(g => g.Id).ToList()
        };

        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeSubjectStatus(int subjectId, ContentStatus status)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();

        var subject = await _context.Subjects
            .Include(s => s.Lectures)
            .Include(s => s.Tests).ThenInclude(t => t.Questions)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null) return NotFound();

        if (status == ContentStatus.Published)
        {
            var errors = ValidateSubjectPublishing(subject);
            if (errors.Any())
            {
                TempData["SubjectStatusError"] = $"Нельзя опубликовать предмет: {string.Join(", ", errors)}.";
                return RedirectToAction(nameof(Index), new { subjectId });
            }
        }

        subject.Status = status;
        await _context.SaveChangesAsync();

        TempData["SubjectStatusSuccess"] = $"Статус предмета успешно изменен на: <b>{(status == ContentStatus.Published ? "Опубликован" : "Скрыт")}</b>";
        return RedirectToAction(nameof(Index), new { subjectId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateGroups(ConfigureGroupsModel model)
    {
        if (!await HasAccessToSubject(model.SubjectId)) return Forbid();

        var subject = await _context.Subjects.Include(s => s.EnrolledGroups).FirstOrDefaultAsync(s => s.Id == model.SubjectId);
        if (subject != null)
        {
            subject.EnrolledGroups.Clear();
            if (model.SelectedGroupIds?.Any() == true)
            {
                var groups = await _context.Groups.Where(g => model.SelectedGroupIds.Contains(g.Id)).ToListAsync();
                subject.EnrolledGroups.AddRange(groups);
            }
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { subjectId = model.SubjectId });
    }

    public async Task<IActionResult> ManageLectures(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        var subject = await _context.Subjects.Include(s => s.Lectures).FirstOrDefaultAsync(s => s.Id == subjectId);
        return subject == null ? NotFound() : View("Lecture/ManageLectures", subject);
    }

    [HttpGet]
    public async Task<IActionResult> CreateLecture(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        return View("Lecture/CreateLecture", new LectureViewModel { SubjectId = subjectId });
    }

    [HttpPost]
    public async Task<IActionResult> CreateLecture(LectureViewModel model)
    {
        if (!await HasAccessToSubject(model.SubjectId)) return Forbid();
        if (model.UploadedFile == null) ModelState.AddModelError("UploadedFile", "Выберите файл.");

        if (ModelState.IsValid)
        {
            var fileData = await SaveFileAsync(model.UploadedFile!);
            var lecture = new Lecture
            {
                Title = model.Title,
                SubjectId = model.SubjectId,
                DateAdded = DateTime.Now,
                Status = ContentStatus.Hidden,
                FilePath = fileData.Path,
                OriginalFileName = fileData.OriginalName
            };

            _context.Lectures.Add(lecture);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageLectures), new { subjectId = model.SubjectId });
        }
        return View("Lecture/CreateLecture", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditLecture(int id)
    {
        var lecture = await _context.Lectures.FindAsync(id);
        if (lecture == null) return NotFound();
        if (!await HasAccessToSubject(lecture.SubjectId)) return Forbid();

        return View("Lecture/EditLecture", new LectureViewModel
        {
            Id = lecture.Id,
            SubjectId = lecture.SubjectId,
            Title = lecture.Title,
            ExistingFilePath = lecture.FilePath,
            OriginalFileName = lecture.OriginalFileName,
            Status = lecture.Status
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditLecture(LectureViewModel model)
    {
        var lecture = await _context.Lectures.FindAsync(model.Id);
        if (lecture == null) return NotFound();
        if (!await HasAccessToSubject(lecture.SubjectId)) return Forbid();

        ViewBag.CurrentStatus = lecture.Status;

        if (ModelState.IsValid)
        {
            if (model.UploadedFile != null)
            {
                DeleteFile(lecture.FilePath); 
                var fileData = await SaveFileAsync(model.UploadedFile);
                
                lecture.FilePath = fileData.Path;
                lecture.OriginalFileName = fileData.OriginalName;
                lecture.DateAdded = DateTime.Now;
            }

            lecture.Title = model.Title;
            
            if (model.Status == ContentStatus.Published && string.IsNullOrEmpty(lecture.FilePath))
            {
                ModelState.AddModelError("", "Нельзя опубликовать лекцию без загруженного файла.");
                model.ExistingFilePath = lecture.FilePath;
                model.OriginalFileName = lecture.OriginalFileName;
                return View("Lecture/EditLecture", model);
            }

            lecture.Status = model.Status;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageLectures), new { subjectId = lecture.SubjectId });
        }
        return View("Lecture/EditLecture", model);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeLectureStatus(int id, ContentStatus newStatus)
    {
        var lecture = await _context.Lectures.FindAsync(id);
        if (lecture == null) return NotFound();
        if (!await HasAccessToSubject(lecture.SubjectId)) return Forbid();

        if (newStatus == ContentStatus.Published && string.IsNullOrEmpty(lecture.FilePath))
        {
            TempData["ErrorMessage"] = "Нельзя опубликовать лекцию: файл не загружен.";
            return RedirectToAction(nameof(ManageLectures), new { subjectId = lecture.SubjectId });
        }

        lecture.Status = newStatus;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(ManageLectures), new { subjectId = lecture.SubjectId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteLecture(int id)
    {
        var lecture = await _context.Lectures.FindAsync(id);
        if (lecture != null)
        {
            if (!await HasAccessToSubject(lecture.SubjectId)) return Forbid();
            DeleteFile(lecture.FilePath);
            _context.Lectures.Remove(lecture);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageLectures), new { subjectId = lecture.SubjectId });
        }
        return RedirectToAction(nameof(Index), "Admin"); 
    }

    public async Task<IActionResult> ManageTests(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        var subject = await _context.Subjects
            .Include(s => s.Tests).ThenInclude(t => t.Questions)
            .FirstOrDefaultAsync(s => s.Id == subjectId);
        return subject == null ? NotFound() : View("Test/ManageTests", subject);
    }

    [HttpGet]
    public async Task<IActionResult> CreateTest(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        return View("Test/CreateTest", new TestViewModel { SubjectId = subjectId, Status = ContentStatus.Hidden });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTest(TestViewModel model)
    {
        if (!await HasAccessToSubject(model.SubjectId)) return Forbid();

        if (ModelState.IsValid)
        {
            var test = new Test
            {
                Title = model.Title,
                SubjectId = model.SubjectId,
                DaysToComplete = model.DaysToComplete,
                TimeLimitMinutes = model.TimeLimitMinutes,
                MaxAttempts = model.MaxAttempts,
                Status = ContentStatus.Hidden
            };
            _context.Tests.Add(test);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageTests), new { subjectId = model.SubjectId });
        }
        return View("Test/CreateTest", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditTest(int id)
    {
        var test = await _context.Tests.FindAsync(id);
        if (test == null) return NotFound();
        if (!await HasAccessToSubject(test.SubjectId)) return Forbid();

        return View("Test/EditTest", new TestViewModel
        {
            Id = test.Id,
            SubjectId = test.SubjectId,
            Title = test.Title,
            DaysToComplete = test.DaysToComplete,
            TimeLimitMinutes = test.TimeLimitMinutes,
            MaxAttempts = test.MaxAttempts,
            Status = test.Status
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditTest(TestViewModel model)
    {
        var test = await _context.Tests.FindAsync(model.Id);
        if (test == null) return NotFound();
        if (!await HasAccessToSubject(test.SubjectId)) return Forbid();

        if (ModelState.IsValid)
        {
            test.Title = model.Title;
            test.DaysToComplete = model.DaysToComplete;
            test.TimeLimitMinutes = model.TimeLimitMinutes;
            test.MaxAttempts = model.MaxAttempts;

            if (model.Status == ContentStatus.Published)
            {
                if (!await _context.Questions.AnyAsync(q => q.TestId == test.Id))
                {
                    ModelState.AddModelError("", "Нельзя опубликовать тест без вопросов.");
                    model.Status = ContentStatus.Hidden; 
                    return View("Test/EditTest", model);
                }
            }
            
            test.Status = model.Status;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageTests), new { subjectId = test.SubjectId });
        }
        return View("Test/EditTest", model);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeTestStatus(int testId, ContentStatus newStatus)
    {
        var test = await _context.Tests.Include(t => t.Questions).FirstOrDefaultAsync(t => t.Id == testId);
        if (test == null) return NotFound();
        if (!await HasAccessToSubject(test.SubjectId)) return Forbid();

        if (newStatus == ContentStatus.Published && (test.Questions == null || !test.Questions.Any()))
        {
            TempData["ErrorMessage"] = $"Нельзя опубликовать тест «{test.Title}»: нет вопросов.";
            return RedirectToAction(nameof(ManageTests), new { subjectId = test.SubjectId });
        }

        test.Status = newStatus;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Статус теста обновлен: <b>{(newStatus == ContentStatus.Published ? "Опубликован" : "Скрыт")}</b>";
        return RedirectToAction(nameof(ManageTests), new { subjectId = test.SubjectId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTest(int id)
    {
        var test = await _context.Tests.FindAsync(id);
        if (test != null)
        {
            if (!await HasAccessToSubject(test.SubjectId)) return Forbid();
            _context.Tests.Remove(test);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageTests), new { subjectId = test.SubjectId });
        }
        return RedirectToAction(nameof(Index), "Admin");
    }

    [HttpGet]
    public async Task<IActionResult> ManageQuestions(int testId)
    {
        var test = await _context.Tests.Include(t => t.Questions).ThenInclude(q => q.AnswerOptions).FirstOrDefaultAsync(t => t.Id == testId);
        if (test == null) return NotFound();
        if (!await HasAccessToSubject(test.SubjectId)) return Forbid();

        ViewBag.TestTitle = test.Title;
        ViewBag.SubjectId = test.SubjectId;
        ViewBag.TestId = test.Id;
        return View("Test/Question/ManageQuestions", test.Questions);
    }

    [HttpGet]
    public async Task<IActionResult> CreateQuestion(int testId)
    {
        var test = await _context.Tests.FindAsync(testId);
        if (test == null) return NotFound();
        if (!await HasAccessToSubject(test.SubjectId)) return Forbid();

        return View("Test/Question/CreateQuestion", new QuestionViewModel
        {
            TestId = testId,
            SubjectId = test.SubjectId,
            Answers = new List<AnswerOptionViewModel> { new(), new() }
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuestion(QuestionViewModel model)
    {
        if (!await HasAccessToSubject(model.SubjectId)) return Forbid();
        ValidateQuestionModel(model);

        if (ModelState.IsValid)
        {
            var question = new Question
            {
                Text = model.Text,
                Type = model.Type,
                Points = model.Points,
                TestId = model.TestId,
                AnswerOptions = model.Answers.Select((a, idx) => new AnswerOption { Text = a.Text, IsCorrect = a.IsCorrect, OrderIndex = idx }).ToList()
            };
            _context.Questions.Add(question);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageQuestions), new { testId = model.TestId });
        }
        return View("Test/Question/CreateQuestion", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditQuestion(int id)
    {
        var question = await _context.Questions.Include(q => q.AnswerOptions).Include(q => q.Test).FirstOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();
        if (!await HasAccessToSubject(question.Test.SubjectId)) return Forbid();

        return View("Test/Question/EditQuestion", new QuestionViewModel
        {
            Id = question.Id,
            TestId = question.TestId,
            SubjectId = question.Test.SubjectId,
            Text = question.Text,
            Type = question.Type,
            Points = question.Points,
            Answers = question.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => new AnswerOptionViewModel { Id = a.Id, Text = a.Text, IsCorrect = a.IsCorrect }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditQuestion(QuestionViewModel model)
    {
        if (!await HasAccessToSubject(model.SubjectId)) return Forbid();
        ValidateQuestionModel(model);

        if (ModelState.IsValid)
        {
            var question = await _context.Questions.Include(q => q.AnswerOptions).FirstOrDefaultAsync(q => q.Id == model.Id);
            if (question == null) return NotFound();

            question.Text = model.Text;
            question.Type = model.Type;
            question.Points = model.Points;
            
            _context.AnswerOptions.RemoveRange(question.AnswerOptions);
            question.AnswerOptions = model.Answers.Select((a, idx) => new AnswerOption { Text = a.Text, IsCorrect = a.IsCorrect, OrderIndex = idx, QuestionId = question.Id }).ToList();

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageQuestions), new { testId = model.TestId });
        }
        return View("Test/Question/EditQuestion", model);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var question = await _context.Questions.Include(q => q.Test).FirstOrDefaultAsync(q => q.Id == id);
        if (question != null)
        {
            if (!await HasAccessToSubject(question.Test.SubjectId)) return Forbid();
            var testId = question.TestId;
            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageQuestions), new { testId });
        }
        return NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> TestStats(int testId)
    {
        var test = await _context.Tests.Include(t => t.Subject).FirstOrDefaultAsync(t => t.Id == testId);
        if (test == null) return NotFound();
        if (!await HasAccessToSubject(test.SubjectId)) return Forbid();

        var attempts = await _context.TestAttempts
            .AsNoTracking()
            .Where(ta => ta.TestId == testId && ta.IsCompleted)
            .Include(ta => ta.Student).ThenInclude(s => s.Group)
            .ToListAsync();

        var stats = attempts.GroupBy(a => a.Student)
            .Select(g => new StudentResultViewModel
            {
                StudentName = g.Key.FullName,
                GroupName = g.Key.Group?.GroupName ?? "Без группы",
                AttemptsCount = g.Count(),
                BestScore = g.Max(a => a.Score),
                LastAttemptDate = g.Max(a => a.EndTime)
            })
            .OrderByDescending(s => s.BestScore).ThenBy(s => s.StudentName).ToList();

        return View("Test/TestStats", new TestStatsViewModel
        {
            TestId = test.Id,
            SubjectId = test.SubjectId,
            TestTitle = test.Title,
            SubjectTitle = test.Subject.Title,
            Results = stats
        });
    }

    private List<string> ValidateSubjectPublishing(Subject subject)
    {
        var errors = new List<string>();
        if (subject.Lectures == null || !subject.Lectures.Any()) errors.Add("нет ни одной лекции");
        if (subject.Tests == null || !subject.Tests.Any()) errors.Add("нет ни одного теста");
        else if (!subject.Tests.Any(t => t.Questions != null && t.Questions.Any())) errors.Add("тесты не содержат вопросов");
        return errors;
    }

    private async Task<(string Path, string OriginalName)> SaveFileAsync(IFormFile file)
    {
        string uniqueName = Guid.NewGuid() + Path.GetExtension(file.FileName);
        string relativePath = "/lectures/" + uniqueName;
        string absPath = Path.Combine(_appEnvironment.WebRootPath, "lectures", uniqueName);

        var dir = Path.GetDirectoryName(absPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

        using (var fileStream = new FileStream(absPath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }
        return (relativePath, file.FileName);
    }

    private void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        try
        {
            string absPath = Path.Combine(_appEnvironment.WebRootPath, relativePath.TrimStart('/'));
            if (System.IO.File.Exists(absPath)) System.IO.File.Delete(absPath);
        }
        catch { }
    }

    private void ValidateQuestionModel(QuestionViewModel model)
    {
        var correctCount = model.Answers.Count(a => a.IsCorrect);
        if (model.Type == "Single" && correctCount != 1)
            ModelState.AddModelError("", "Для одиночного выбора должен быть ровно 1 правильный ответ.");
        else if (model.Type == "Multiple" && correctCount < 1)
            ModelState.AddModelError("", "Для множественного выбора нужен хотя бы 1 правильный ответ.");
    }
}