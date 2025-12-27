using System.Security.Claims;
using CourseProject.Controllers;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.CourseContentViewModels.Test;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CourseProject.Tests;

[TestFixture]
public class SubjectsControllerTests
{
    private Mock<UserManager<User>> _mockUserManager;
    private Mock<ApplicationDbContext> _mockContext;
    private Mock<IWebHostEnvironment> _mockEnvironment;
    private SubjectsController _controller;

    [SetUp]
    public void Setup()
    {
        var store = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().Options;
        _mockContext = new Mock<ApplicationDbContext>(options);

        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockEnvironment.Setup(e => e.WebRootPath).Returns(Directory.GetCurrentDirectory());

        _controller = new SubjectsController(_mockContext.Object, _mockUserManager.Object, _mockEnvironment.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _controller?.Dispose();
    }

    [Test]
    public async Task Index_Admin_ReturnsAllSubjects()
    {
        SetupUser("admin1", "Admin");
        var subjects = new List<Subject>
        {
            new Subject { Id = 1, Title = "A" },
            new Subject { Id = 2, Title = "B" }
        };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(subjects).Object);

        var result = await _controller.Index() as ViewResult;
        var model = result.Model as List<Subject>;

        Assert.That(model.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Index_Teacher_ReturnsOnlyAssigned()
    {
        SetupUser("t1", "Teacher");
        var teacher = new User { Id = "t1" };
        var subjects = new List<Subject>
        {
            new Subject { Id = 1, Teachers = new List<User> { teacher } },
            new Subject { Id = 2, Teachers = new List<User>() }
        };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(subjects).Object);

        var result = await _controller.Index() as ViewResult;
        var model = result.Model as List<Subject>;

        Assert.That(model.Count, Is.EqualTo(1));
        Assert.That(model[0].Id, Is.EqualTo(1));
    }

    [Test]
    public async Task Index_Student_ReturnsOnlyEnrolledAndPublished()
    {
        SetupUser("s1", "Student");
        var user = new User { Id = "s1" };
        var group = new Group { Id = 1, Students = new List<User> { user } };

        var subjects = new List<Subject>
        {
            new Subject { Id = 1, Status = ContentStatus.Published, EnrolledGroups = new List<Group> { group } },
            new Subject { Id = 2, Status = ContentStatus.Hidden, EnrolledGroups = new List<Group> { group } },
            new Subject { Id = 3, Status = ContentStatus.Published, EnrolledGroups = new List<Group>() }
        };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(subjects).Object);

        var result = await _controller.Index() as ViewResult;
        var model = result.Model as List<Subject>;

        Assert.That(model.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Details_SubjectNotFound_ReturnsNotFound()
    {
        SetupUser("a1", "Admin");
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject>()).Object);

        var result = await _controller.Details(999);
        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Details_StudentNotEnrolled_ReturnsForbid()
    {
        SetupUser("s1", "Student");
        var group = new Group { Id = 1, Students = new List<User> { new User { Id = "s1" } } };

        var subject = new Subject { Id = 1, EnrolledGroups = new List<Group>() }; 

        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subject }).Object);
        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group> { group }).Object);

        var result = await _controller.Details(1);
        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task Details_StudentEnrolled_ReturnsViewWithFilteredContent()
    {
        SetupUser("s1", "Student");
        var user = new User { Id = "s1" };
        var group = new Group { Id = 1, Students = new List<User> { user } };

        group.Subjects = new List<Subject>(); 

        var subject = new Subject
        {
            Id = 1,
            EnrolledGroups = new List<Group> { group },
            Lectures = new List<Lecture>
            {
                new Lecture { Status = ContentStatus.Published },
                new Lecture { Status = ContentStatus.Hidden }
            }
        };
        group.Subjects.Add(subject);

        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subject }).Object);
        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group> { group }).Object);

        var result = await _controller.Details(1) as ViewResult;
        var model = result.Model as Subject;

        Assert.That(model.Lectures.Count, Is.EqualTo(1)); 
    }

    [Test]
    public async Task DownloadLecture_Student_Hidden_ReturnsForbid()
    {
        SetupUser("s1", "Student");
        var lecture = new Lecture { Id = 1, SubjectId = 1, Status = ContentStatus.Hidden };

        var lecMock = DbSetMock.Create(new List<Lecture> { lecture });
        lecMock.Setup(m => m.FindAsync(It.IsAny<object[]>())).ReturnsAsync(lecture);
        _mockContext.Setup(c => c.Lectures).Returns(lecMock.Object);

        var result = await _controller.DownloadLecture(1);
        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task DownloadLecture_ValidFile_ReturnsFileResult()
    {
        string tempFile = "test_lecture.txt";
        await File.WriteAllTextAsync(tempFile, "content");

        try
        {
            SetupUser("a1", "Admin"); 
            var lecture = new Lecture { Id = 1, FilePath = tempFile, OriginalFileName = "Lec.txt" };

            var lecMock = DbSetMock.Create(new List<Lecture> { lecture });
            lecMock.Setup(m => m.FindAsync(It.IsAny<object[]>())).ReturnsAsync(lecture);
            _mockContext.Setup(c => c.Lectures).Returns(lecMock.Object);

            var result = await _controller.DownloadLecture(1);

            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            Assert.That(fileResult.FileDownloadName, Is.EqualTo("Lec.txt"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public async Task StartTest_HiddenAndNotStaff_ReturnsNotFound()
    {
        SetupUser("s1", "Student");
        var test = new Test { Id = 1, Status = ContentStatus.Hidden };

        _mockContext.Setup(c => c.Tests).Returns(DbSetMock.Create(new List<Test> { test }).Object);

        var result = await _controller.StartTest(1);
        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task StartTest_StudentNoAccess_ReturnsForbid()
    {
        SetupUser("s1", "Student");
        var group = new Group { Id = 1, Students = new List<User> { new User { Id = "s1" } } };
        var test = new Test { Id = 1, SubjectId = 99, Status = ContentStatus.Published };

        _mockContext.Setup(c => c.Tests).Returns(DbSetMock.Create(new List<Test> { test }).Object);
        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group> { group }).Object);
        _mockContext.Setup(c => c.TestAttempts).Returns(DbSetMock.Create(new List<TestAttempt>()).Object);

        var result = await _controller.StartTest(1);
        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task TakeTest_CreatesNewAttempt_IfNoneExists()
    {
        SetupUser("s1", "Student");
        var test = new Test { Id = 1, TimeLimitMinutes = 10, Title = "Test" };

        var attemptsMock = DbSetMock.Create(new List<TestAttempt>());
        _mockContext.Setup(c => c.TestAttempts).Returns(attemptsMock.Object);

        var testsMock = DbSetMock.Create(new List<Test> { test });
        testsMock.Setup(m => m.FindAsync(It.IsAny<object[]>())).ReturnsAsync(test);
        _mockContext.Setup(c => c.Tests).Returns(testsMock.Object);
        _mockContext.Setup(c => c.Questions).Returns(DbSetMock.Create(new List<Question>()).Object);

        var result = await _controller.TakeTest(1) as ViewResult;

        _mockContext.Verify(c => c.TestAttempts.Add(It.IsAny<TestAttempt>()), Times.Once);
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task TakeTest_RedirectsIfMaxAttemptsExceeded()
    {
        SetupUser("s1", "Student");
        var test = new Test { Id = 1, MaxAttempts = 1 };
        var attempt = new TestAttempt { TestId = 1, StudentId = "s1", IsCompleted = true };
        var attemptsMock = DbSetMock.Create(new List<TestAttempt> { attempt });
        _mockContext.Setup(c => c.TestAttempts).Returns(attemptsMock.Object);

        var testsMock = DbSetMock.Create(new List<Test> { test });
        testsMock.Setup(m => m.FindAsync(It.IsAny<object[]>())).ReturnsAsync(test);
        _mockContext.Setup(c => c.Tests).Returns(testsMock.Object);

        var result = await _controller.TakeTest(1);

        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
        var redirect = result as RedirectToActionResult;
        Assert.That(redirect.ActionName, Is.EqualTo(nameof(SubjectsController.StartTest)));
    }

    [Test]
    public async Task SubmitTest_CalculatesScoreCorrectly_SingleChoice()
    {
        SetupUser("s1", "Student");

        var q1 = new Question { Id = 10, Type = "Single", Points = 5, AnswerOptions = new List<AnswerOption>() };
        var optCorrect = new AnswerOption { Id = 100, IsCorrect = true, QuestionId = 10 };
        var optWrong = new AnswerOption { Id = 101, IsCorrect = false, QuestionId = 10 };
        q1.AnswerOptions.Add(optCorrect);
        q1.AnswerOptions.Add(optWrong);

        var test = new Test { Id = 1, TimeLimitMinutes = 0 };
        var attempt = new TestAttempt { Id = 500, StudentId = "s1", IsCompleted = false, Test = test };

        _mockContext.Setup(c => c.TestAttempts).Returns(DbSetMock.Create(new List<TestAttempt> { attempt }).Object);
        _mockContext.Setup(c => c.Questions).Returns(DbSetMock.Create(new List<Question> { q1 }).Object);
        _mockContext.Setup(c => c.AnswerOptions)
            .Returns(DbSetMock.Create(new List<AnswerOption> { optCorrect, optWrong }).Object);
        _mockContext.Setup(c => c.StudentAnswers).Returns(DbSetMock.Create(new List<StudentAnswer>()).Object);

        var model = new SubmitTestViewModel
        {
            TestAttemptId = 500,
            Answers = new List<UserAnswerInputModel>
            {
                new UserAnswerInputModel { QuestionId = 10, SelectedOptionIds = new List<int> { 100 } }
            }
        };

        var result = await _controller.SubmitTest(model);

        Assert.That(attempt.IsCompleted, Is.True);
        Assert.That(attempt.Score, Is.EqualTo(5));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task SubmitTest_TimeLimitExceeded_SetsScoreZero()
    {
        SetupUser("s1", "Student");
        var test = new Test { Id = 1, TimeLimitMinutes = 10 };
        var attempt = new TestAttempt
            { Id = 500, StudentId = "s1", StartTime = DateTime.Now.AddHours(-1), IsCompleted = false, Test = test };

        _mockContext.Setup(c => c.TestAttempts).Returns(DbSetMock.Create(new List<TestAttempt> { attempt }).Object);

        var model = new SubmitTestViewModel { TestAttemptId = 500, Answers = new List<UserAnswerInputModel>() };
        await _controller.SubmitTest(model);

        Assert.That(attempt.Score, Is.EqualTo(0));
        Assert.That(attempt.IsCompleted, Is.True);
    }

    private void SetupUser(string userId, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        _mockUserManager.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);
        _mockUserManager.Setup(u => u.IsInRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync((User u, string r) => r == role);
    }
}