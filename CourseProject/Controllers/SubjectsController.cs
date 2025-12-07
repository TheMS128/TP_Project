using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.CourseContentViewModels.Test;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseProject.Controllers;

[Authorize]
public class SubjectsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IWebHostEnvironment _appEnvironment;

    public SubjectsController(ApplicationDbContext context, UserManager<User> userManager,
        IWebHostEnvironment appEnvironment)
    {
        _context = context;
        _userManager = userManager;
        _appEnvironment = appEnvironment;
    }

    // Вспомогательный метод для проверки прав учителя на просмотр
    private async Task<bool> IsTeacherOfSubject(int subjectId)
    {
        if (User.IsInRole("Admin")) return true;
        if (User.IsInRole("Teacher"))
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Subjects
                .AnyAsync(s => s.Id == subjectId && s.Teachers.Any(t => t.Id == userId));
        }
        return false;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        List<Subject> subjects = new();

        if (User.IsInRole("Admin"))
        {
            subjects = await _context.Subjects
                .Include(s => s.Lectures)
                .OrderBy(s => s.Title)
                .ToListAsync();
        }
        else if (User.IsInRole("Teacher"))
        {
            subjects = await _context.Subjects
                .Where(s => s.Teachers.Any(t => t.Id == userId))
                .Include(s => s.Lectures)
                .OrderBy(s => s.Title)
                .ToListAsync();
        }
        else if (User.IsInRole("Student"))
        {
            subjects = await _context.Subjects
                .Where(s => s.EnrolledGroups.Any(g => g.Students.Any(u => u.Id == userId)))
                .Where(s => s.Status == ContentStatus.Published)
                .Include(s => s.Lectures)
                .OrderBy(s => s.Title)
                .ToListAsync();
        }

        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View(subjects);
    }

    public async Task<IActionResult> Details(int id)
    {
        var subject = await _context.Subjects
            .Include(s => s.Lectures)
            .Include(s => s.Tests)
            .Include(s => s.EnrolledGroups)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subject == null) return NotFound();

        if (User.IsInRole("Student"))
        {
            var userId = _userManager.GetUserId(User);
            var hasAccess = await _context.Groups
                .Where(g => g.Students.Any(u => u.Id == userId))
                .AnyAsync(g => g.Subjects.Any(s => s.Id == id));

            if (!hasAccess) return Forbid();

            // Студенты видят только опубликованный контент
            subject.Lectures = subject.Lectures?.Where(l => l.Status == ContentStatus.Published).ToList();
            subject.Tests = subject.Tests?.Where(t => t.Status == ContentStatus.Published).ToList();
        }

        ViewBag.IsStudent = User.IsInRole("Student");
        // Флаг для View, чтобы показать кнопку "Управление", которая ведет на CourseContentController
        ViewBag.CanEdit = await IsTeacherOfSubject(id);

        return View(subject);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadLecture(int id)
    {
        var lecture = await _context.Lectures.FindAsync(id);
        if (lecture == null) return NotFound();

        var userId = _userManager.GetUserId(User);
        bool hasAccess = false;

        if (User.IsInRole("Admin")) hasAccess = true;
        else if (User.IsInRole("Teacher")) hasAccess = await IsTeacherOfSubject(lecture.SubjectId);
        else if (User.IsInRole("Student"))
        {
            if (lecture.Status != ContentStatus.Published) return Forbid();
            hasAccess = await _context.Groups
                .Where(g => g.Students.Any(u => u.Id == userId))
                .AnyAsync(g => g.Subjects.Any(s => s.Id == lecture.SubjectId));
        }

        if (!hasAccess) return Forbid();

        string absPath = Path.Combine(_appEnvironment.WebRootPath, lecture.FilePath.TrimStart('/'));
        if (lecture.FilePath.StartsWith("/")) absPath = _appEnvironment.WebRootPath + lecture.FilePath;
        else absPath = Path.Combine(_appEnvironment.WebRootPath, lecture.FilePath);

        if (!System.IO.File.Exists(absPath)) return NotFound("Файл не найден на сервере.");

        byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(absPath);
        string fileName = lecture.OriginalFileName ?? "Lecture" + Path.GetExtension(lecture.FilePath);

        return File(fileBytes, "application/octet-stream", fileName);
    }

    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> StartTest(int testId)
    {
        var userId = _userManager.GetUserId(User);
        var test = await _context.Tests
            .Include(t => t.Subject)
            .FirstOrDefaultAsync(t => t.Id == testId);

        if (test == null || test.Status != ContentStatus.Published) return NotFound();

        bool hasAccess = await _context.Groups
            .Where(g => g.Students.Any(u => u.Id == userId))
            .AnyAsync(g => g.Subjects.Any(s => s.Id == test.SubjectId));

        if (!hasAccess) return Forbid();

        int attemptsUsed = await _context.TestAttempts
            .CountAsync(ta => ta.TestId == testId && ta.StudentId == userId && ta.IsCompleted);

        var activeAttempt = await _context.TestAttempts
            .FirstOrDefaultAsync(ta => ta.TestId == testId && ta.StudentId == userId && !ta.IsCompleted);

        var pastAttempts = await _context.TestAttempts
            .Where(ta => ta.TestId == testId && ta.StudentId == userId && ta.IsCompleted)
            .OrderByDescending(ta => ta.EndTime)
            .ToListAsync();

        ViewBag.AttemptsUsed = attemptsUsed;
        ViewBag.CanTake = test.MaxAttempts == null || attemptsUsed < test.MaxAttempts;
        ViewBag.HasActiveAttempt = activeAttempt != null;
        ViewBag.PastAttempts = pastAttempts;

        return View(test);
    }

    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> TakeTest(int testId)
    {
        var userId = _userManager.GetUserId(User);

        var attempt = await _context.TestAttempts
            .FirstOrDefaultAsync(ta => ta.TestId == testId && ta.StudentId == userId && !ta.IsCompleted);

        if (attempt == null)
        {
            var test = await _context.Tests.FindAsync(testId);
            if (test == null) return NotFound();

            int used = await _context.TestAttempts.CountAsync(ta =>
                ta.TestId == testId && ta.StudentId == userId && ta.IsCompleted);
            if (test.MaxAttempts != null && used >= test.MaxAttempts)
                return RedirectToAction(nameof(StartTest), new { testId });

            attempt = new TestAttempt
            {
                TestId = testId,
                StudentId = userId,
                StartTime = DateTime.Now,
                IsCompleted = false,
                Score = 0
            };
            _context.TestAttempts.Add(attempt);
            await _context.SaveChangesAsync();
        }

        var questions = await _context.Questions
            .Where(q => q.TestId == testId)
            .Include(q => q.AnswerOptions)
            .Select(q => new QuestionTakeViewModel
            {
                QuestionId = q.Id,
                Text = q.Text,
                Type = q.Type,
                Points = q.Points,
                Options = q.AnswerOptions.Select(o => new AnswerOptionTakeViewModel
                {
                    Id = o.Id,
                    Text = o.Text
                }).OrderBy(x => Guid.NewGuid()).ToList()
            })
            .ToListAsync();

        var testInfo = await _context.Tests.FindAsync(testId);

        var model = new TakeTestViewModel
        {
            TestId = testId,
            TestAttemptId = attempt.Id,
            Title = testInfo.Title,
            TimeLimitMinutes = testInfo.TimeLimitMinutes ?? 0,
            StartTime = attempt.StartTime,
            Questions = questions
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitTest(SubmitTestViewModel model)
    {
        var userId = _userManager.GetUserId(User);

        var attempt = await _context.TestAttempts
            .Include(ta => ta.Test)
            .FirstOrDefaultAsync(ta => ta.Id == model.TestAttemptId && ta.StudentId == userId);

        if (attempt == null || attempt.IsCompleted)
            return RedirectToAction(nameof(Index));

        attempt.EndTime = DateTime.Now;
        attempt.IsCompleted = true;

        if (attempt.Test.TimeLimitMinutes > 0)
        {
            var limit = TimeSpan.FromMinutes(attempt.Test.TimeLimitMinutes.Value);
            if ((attempt.EndTime.Value - attempt.StartTime) > limit.Add(TimeSpan.FromMinutes(1)))
            {
                attempt.Score = 0;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(TestResult), new { attemptId = attempt.Id });
            }
        }

        float totalScore = 0;

        foreach (var userAnswer in model.Answers)
        {
            var question = await _context.Questions
                .Include(q => q.AnswerOptions)
                .FirstOrDefaultAsync(q => q.Id == userAnswer.QuestionId);

            if (question == null) continue;

            var selectedOptions = await _context.AnswerOptions
                .Where(o => userAnswer.SelectedOptionIds.Contains(o.Id))
                .ToListAsync();

            var studentAnswerDb = new StudentAnswer
            {
                TestAttemptId = attempt.Id,
                QuestionId = question.Id,
                SelectedOptions = selectedOptions
            };

            bool isCorrect = false;
            var correctIds = question.AnswerOptions.Where(o => o.IsCorrect).Select(o => o.Id).ToList();
            var userIds = userAnswer.SelectedOptionIds;

            if (question.Type == "Single")
            {
                if (userIds.Count == 1 && correctIds.Contains(userIds.First()))
                    isCorrect = true;
            }
            else
            {
                if (!correctIds.Except(userIds).Any() && !userIds.Except(correctIds).Any())
                    isCorrect = true;
            }

            if (isCorrect)
            {
                studentAnswerDb.PointsScored = question.Points;
                totalScore += question.Points;
            }
            else
            {
                studentAnswerDb.PointsScored = 0;
            }

            _context.StudentAnswers.Add(studentAnswerDb);
        }

        attempt.Score = totalScore;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(TestResult), new { attemptId = attempt.Id });
    }

    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> TestResult(int attemptId)
    {
        var userId = _userManager.GetUserId(User);
        var result = await _context.TestAttempts
            .Include(ta => ta.Test)
            .FirstOrDefaultAsync(ta => ta.Id == attemptId && ta.StudentId == userId);

        if (result == null) return NotFound();

        return View(result);
    }
}