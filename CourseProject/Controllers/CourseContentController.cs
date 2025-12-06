using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.CourseContentViewModels.Lecture;
using CourseProject.Models.CourseContentViewModels.Test;

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

        var subject = await _context.Subjects.FindAsync(subjectId);
        if (subject == null) return NotFound();

        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View(subject);
    }

    [HttpGet]
    public async Task<IActionResult> ManageLectures(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();

        var subject = await _context.Subjects
            .Include(s => s.Lectures)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null) return NotFound();

        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View("Lecture/ManageLectures", subject);
    }

    [HttpGet]
    public async Task<IActionResult> ManageTests(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();

        var subject = await _context.Subjects
            .Include(s => s.Tests)
            .ThenInclude(t => t.Questions)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null) return NotFound();

        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View("Test/ManageTests", subject);
    }

    [HttpGet]
    public async Task<IActionResult> CreateLecture(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        return View("Lecture/CreateLecture", new LectureViewModel { SubjectId = subjectId, IsPublished = true });
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
                IsPublished = model.IsPublished,
                Status = model.IsPublished ? ContentStatus.Published : ContentStatus.Draft,

                FilePath = path, OriginalFileName = model.UploadedFile.FileName
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
            IsPublished = lecture.IsPublished,
            ExistingFilePath = lecture.FilePath,
            OriginalFileName = lecture.OriginalFileName
        };
        return View("Lecture/EditLecture", model);
    }
    
    [HttpPost]
    public async Task<IActionResult> ChangeLectureStatus(int id, bool isPublished)
    {
        var lecture = await _context.Lectures.FindAsync(id);
        if (lecture == null) return NotFound();
        
        if (!await HasAccessToSubject(lecture.SubjectId)) return Forbid();

        lecture.IsPublished = isPublished;
        lecture.Status = isPublished ? ContentStatus.Published : ContentStatus.Draft; 

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(ManageLectures), new { subjectId = lecture.SubjectId });
    }

    [HttpPost]
    public async Task<IActionResult> EditLecture(LectureViewModel model)
    {
        var lecture = await _context.Lectures.FindAsync(model.Id);

        if (ModelState.IsValid)
        {
            lecture.Title = model.Title;
            lecture.IsPublished = model.IsPublished;

            if (model.UploadedFile != null)
            {
                if (!string.IsNullOrEmpty(lecture.FilePath))
                {
                    var oldPath = _appEnvironment.WebRootPath + lecture.FilePath;
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

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageLectures), new { subjectId = lecture.SubjectId });
        }

        return View("Lecture/EditLecture", model);
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

    [HttpGet]
    public async Task<IActionResult> CreateTest(int subjectId)
    {
        if (!await HasAccessToSubject(subjectId)) return Forbid();
        return View("Test/CreateTest", new TestViewModel { SubjectId = subjectId, IsPublished = true });
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
                IsPublished = model.IsPublished,
                Status = model.IsPublished ? ContentStatus.Published : ContentStatus.Draft
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
            test.IsPublished = model.IsPublished;
            test.Status = model.IsPublished ? ContentStatus.Published : ContentStatus.Draft;

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