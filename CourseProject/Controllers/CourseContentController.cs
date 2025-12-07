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

    public CourseContentController(ApplicationDbContext context, UserManager<User> userManager,
        IWebHostEnvironment appEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _appEnvironment = appEnvironment;
    }

    private async Task<bool> HasAccessToSubject(int subjectId)
    {
        if (User.IsInRole("Admin")) return true;
        var userId = _userManager.GetUserId(User);
        return await _context.Subjects
            .Where(s => s.Id == subjectId)
            .AnyAsync(s => s.Teachers.Any(t => t.Id == userId));
    }

    [HttpGet]
    public async Task<IActionResult> Index(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();

        var subject = await _context.Subjects
            .Include(s => s.Lectures)
            .Include(s => s.Tests)
            .Include(s => s.EnrolledGroups)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null) return NotFound();

        var allGroups = await _context.Groups.OrderBy(g => g.GroupName).ToListAsync();

        var model = new CourseContentModel
        {
            Id = subject.Id,
            Title = subject.Title,
            Status = subject.Status,
            LecturesCount = subject.Lectures?.Count ?? 0,
            TestsCount = subject.Tests?.Count ?? 0,
            AllGroups = allGroups,
            SelectedGroupIds = subject.EnrolledGroups.Select(g => g.Id).ToList()
        };

        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View(model);
    }

    // --- УПРАВЛЕНИЕ СТАТУСОМ ПРЕДМЕТА (Перенесено сюда) ---

    [HttpPost]
    public async Task<IActionResult> ChangeSubjectStatus(int subjectId, ContentStatus status)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();

        var subject = await _context.Subjects
            .Include(s => s.Tests)
            .ThenInclude(t => t.Questions)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null) return NotFound();

        if (status != ContentStatus.Draft)
        {
            // Проверка: хотя бы 1 вопрос в любом из тестов
            bool hasQuestions = subject.Tests != null &&
                                subject.Tests.Any(t => t.Questions != null && t.Questions.Any());

            if (!hasQuestions)
            {
                TempData["ErrorMessage"] =
                    "Нельзя опубликовать предмет: должен быть создан хотя бы один вопрос в тестах.";
                return RedirectToAction(nameof(Index), new { subjectId = subjectId });
            }
        }

        subject.Status = status;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { subjectId = subjectId });
    }

    // --- УПРАВЛЕНИЕ ГРУППАМИ ПРЕДМЕТА (Перенесено сюда) ---

    [HttpGet]
    public async Task<IActionResult> ConfigureGroups(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();

        var subject = await _context.Subjects
            .Include(s => s.EnrolledGroups)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null) return NotFound();

        var model = new ConfigureGroupsModel
        {
            SubjectId = subject.Id,
            SubjectTitle = subject.Title,
            AllGroups = await _context.Groups.OrderBy(g => g.GroupName).ToListAsync(),
            SelectedGroupIds = subject.EnrolledGroups.Select(g => g.Id).ToList()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateGroups(ConfigureGroupsModel model)
    {
        if (!await HasAccessToSubject(model.SubjectId)) return Forbid();

        var subject = await _context.Subjects
            .Include(s => s.EnrolledGroups)
            .FirstOrDefaultAsync(s => s.Id == model.SubjectId);

        if (subject != null)
        {
            subject.EnrolledGroups.Clear();
            if (model.SelectedGroupIds != null && model.SelectedGroupIds.Any())
            {
                var groups = await _context.Groups
                    .Where(g => model.SelectedGroupIds.Contains(g.Id))
                    .ToListAsync();
                subject.EnrolledGroups.AddRange(groups);
            }

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index), new { subjectId = model.SubjectId });
    }

    // --- ЛЕКЦИИ ---

    [HttpGet]
    public async Task<IActionResult> ManageLectures(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        var subject = await _context.Subjects.Include(s => s.Lectures).FirstOrDefaultAsync(s => s.Id == subjectId);
        if (subject == null) return NotFound();
        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View("Lecture/ManageLectures", subject);
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

        if (model.UploadedFile == null)
            ModelState.AddModelError("UploadedFile", "Выберите файл.");

        if (ModelState.IsValid)
        {
            string uniqueName = Guid.NewGuid() + Path.GetExtension(model.UploadedFile.FileName);
            string path = "/lectures/" + uniqueName;
            string absPath = Path.Combine(_appEnvironment.WebRootPath, "lectures", uniqueName);

            using (var fileStream = new FileStream(absPath, FileMode.Create))
            {
                await model.UploadedFile.CopyToAsync(fileStream);
            }

            var lecture = new Lecture
            {
                Title = model.Title,
                SubjectId = model.SubjectId,
                DateAdded = DateTime.Now,
                Status = ContentStatus.Hidden,
                FilePath = path,
                OriginalFileName = model.UploadedFile.FileName
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

        var model = new LectureViewModel
        {
            Id = lecture.Id,
            SubjectId = lecture.SubjectId,
            Title = lecture.Title,
            ExistingFilePath = lecture.FilePath,
            OriginalFileName = lecture.OriginalFileName,
            Status = lecture.Status 
        };
        return View("Lecture/EditLecture", model);
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
                if (!string.IsNullOrEmpty(lecture.FilePath))
                {
                    var oldPath = Path.Combine(_appEnvironment.WebRootPath, lecture.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                string uniqueName = Guid.NewGuid() + Path.GetExtension(model.UploadedFile.FileName);
                string path = "/lectures/" + uniqueName;
                string absPath = Path.Combine(_appEnvironment.WebRootPath, "lectures", uniqueName);

                using (var fileStream = new FileStream(absPath, FileMode.Create))
                {
                    await model.UploadedFile.CopyToAsync(fileStream);
                }

                lecture.FilePath = path;
                lecture.OriginalFileName = model.UploadedFile.FileName;
                lecture.DateAdded = DateTime.Now;
            }

            lecture.Title = model.Title;
            bool hasFile = !string.IsNullOrEmpty(lecture.FilePath);

            if (model.Status == ContentStatus.Published) 
            {
                if (!hasFile)
                {
                    ModelState.AddModelError("", "Нельзя опубликовать лекцию без загруженного файла.");

                    model.ExistingFilePath = lecture.FilePath;
                    model.OriginalFileName = lecture.OriginalFileName;
                    return View("Lecture/EditLecture", model);
                }

                lecture.Status = ContentStatus.Published;
            }
            else 
            {
                lecture.Status = ContentStatus.Hidden;
            }

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

        // 1. Проверка при попытке опубликовать
        if (newStatus == ContentStatus.Published)
        {
            if (string.IsNullOrEmpty(lecture.FilePath))
            {
                TempData["ErrorMessage"] = "Нельзя опубликовать лекцию: файл не загружен.";
                return RedirectToAction(nameof(ManageLectures), new { subjectId = lecture.SubjectId });
            }
        }
    
        if (newStatus != ContentStatus.Published && newStatus != ContentStatus.Hidden)
        {
            newStatus = ContentStatus.Hidden; 
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

            try
            {
                string absPath = _appEnvironment.WebRootPath + lecture.FilePath;
                if (System.IO.File.Exists(absPath)) System.IO.File.Delete(absPath);
            }
            catch
            {
            }

            int sId = lecture.SubjectId;
            _context.Lectures.Remove(lecture);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageLectures), new { subjectId = sId });
        }

        return RedirectToAction("Index", "Admin");
    }

    // --- ТЕСТЫ ---

    [HttpGet]
    public async Task<IActionResult> ManageTests(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        var subject = await _context.Subjects
            .Include(s => s.Tests).ThenInclude(t => t.Questions)
            .FirstOrDefaultAsync(s => s.Id == subjectId);
        if (subject == null) return NotFound();
        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View("Test/ManageTests", subject);
    }

    [HttpGet]
    public async Task<IActionResult> CreateTest(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        return View("Test/CreateTest", new TestViewModel { SubjectId = subjectId, IsPublished = false });
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
                // ПРИ СОЗДАНИИ ВСЕГДА ЧЕРНОВИК
                IsPublished = false,
                Status = ContentStatus.Draft
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

        var model = new TestViewModel
        {
            Id = test.Id,
            SubjectId = test.SubjectId,
            Title = test.Title,
            DaysToComplete = test.DaysToComplete,
            TimeLimitMinutes = test.TimeLimitMinutes,
            MaxAttempts = test.MaxAttempts,
            IsPublished = test.IsPublished
        };
        return View("Test/EditTest", model);
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

            if (model.IsPublished)
            {
                // Проверка: тест должен иметь вопросы
                bool hasQuestions = await _context.Questions.AnyAsync(q => q.TestId == test.Id);

                if (!hasQuestions)
                {
                    ModelState.AddModelError("",
                        "Нельзя опубликовать тест без вопросов. Добавьте хотя бы один вопрос.");
                    // Сбрасываем галочку во View
                    model.IsPublished = false;
                    return View("Test/EditTest", model);
                }

                test.IsPublished = true;
                test.Status = ContentStatus.Published;
            }
            else
            {
                test.IsPublished = false;
                test.Status = ContentStatus.Draft;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageTests), new { subjectId = test.SubjectId });
        }

        return View("Test/EditTest", model);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTest(int id)
    {
        var test = await _context.Tests.FindAsync(id);
        if (test != null)
        {
            if (!await HasAccessToSubject(test.SubjectId)) return Forbid();
            int sId = test.SubjectId;
            _context.Tests.Remove(test);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageTests), new { subjectId = sId });
        }

        return RedirectToAction("Index", "Admin");
    }

    // --- ВОПРОСЫ (Без изменений, кроме проверок доступа) ---

    [HttpGet]
    public async Task<IActionResult> ManageQuestions(int testId)
    {
        var test = await _context.Tests
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == testId);

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

        var model = new QuestionViewModel
        {
            TestId = testId,
            SubjectId = test.SubjectId,
            Answers = new List<AnswerOptionViewModel>
            {
                new AnswerOptionViewModel(),
                new AnswerOptionViewModel()
            }
        };

        return View("Test/Question/CreateQuestion", model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuestion(QuestionViewModel model)
    {
        if (!await HasAccessToSubject(model.SubjectId)) return Forbid();

        ValidateQuestion(model);

        if (ModelState.IsValid)
        {
            var question = new Question
            {
                Text = model.Text,
                Type = model.Type,
                Points = model.Points,
                TestId = model.TestId,
                AnswerOptions = model.Answers.Select((a, index) => new AnswerOption
                {
                    Text = a.Text,
                    IsCorrect = a.IsCorrect,
                    OrderIndex = index
                }).ToList()
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
        var question = await _context.Questions
            .Include(q => q.AnswerOptions)
            .Include(q => q.Test)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (question == null) return NotFound();
        if (!await HasAccessToSubject(question.Test.SubjectId)) return Forbid();

        var model = new QuestionViewModel
        {
            Id = question.Id,
            TestId = question.TestId,
            SubjectId = question.Test.SubjectId,
            Text = question.Text,
            Type = question.Type,
            Points = question.Points,
            Answers = question.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => new AnswerOptionViewModel
            {
                Id = a.Id,
                Text = a.Text,
                IsCorrect = a.IsCorrect
            }).ToList()
        };

        return View("Test/Question/EditQuestion", model);
    }

    [HttpPost]
    public async Task<IActionResult> EditQuestion(QuestionViewModel model)
    {
        if (!await HasAccessToSubject(model.SubjectId)) return Forbid();

        ValidateQuestion(model);

        if (ModelState.IsValid)
        {
            var question = await _context.Questions
                .Include(q => q.AnswerOptions)
                .FirstOrDefaultAsync(q => q.Id == model.Id);

            if (question == null) return NotFound();

            question.Text = model.Text;
            question.Type = model.Type;
            question.Points = model.Points;

            _context.AnswerOptions.RemoveRange(question.AnswerOptions);

            question.AnswerOptions = model.Answers.Select((a, index) => new AnswerOption
            {
                Text = a.Text,
                IsCorrect = a.IsCorrect,
                OrderIndex = index,
                QuestionId = question.Id
            }).ToList();

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageQuestions), new { testId = model.TestId });
        }

        return View("Test/Question/EditQuestion", model);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var question = await _context.Questions
            .Include(q => q.Test)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (question != null)
        {
            if (!await HasAccessToSubject(question.Test.SubjectId)) return Forbid();

            int testId = question.TestId;
            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageQuestions), new { testId = testId });
        }

        return NotFound();
    }

    private void ValidateQuestion(QuestionViewModel model)
    {
        var correctCount = model.Answers.Count(a => a.IsCorrect);
        if (model.Type == "Single" && correctCount != 1)
            ModelState.AddModelError("", "Для одиночного выбора должен быть ровно 1 правильный ответ.");
        else if (model.Type == "Multiple" && correctCount < 1)
            ModelState.AddModelError("", "Для множественного выбора нужен хотя бы 1 правильный ответ.");
    }
}