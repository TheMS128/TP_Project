using System.Security.Claims;
using CourseProject.Controllers;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.CourseContentViewModels.Lecture;
using CourseProject.Models.CourseContentViewModels.Test;
using CourseProject.Models.SubjectModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CourseProject.Tests
{
    [TestFixture]
    public class CourseContentControllerTests
    {
        private Mock<ApplicationDbContext> _mockContext;
        private Mock<UserManager<User>> _mockUserManager;
        private Mock<IWebHostEnvironment> _mockEnvironment;
        private CourseContentController _controller;

        private List<User> _users;
        private List<Subject> _subjects;
        private List<Lecture> _lectures;
        private List<Test> _tests;
        private List<Question> _questions;
        private List<Group> _groups;
        private List<TestAttempt> _attempts;
        private List<AnswerOption> _answers;

        private string _testWebRootPath;

        [SetUp]
        public void Setup()
        {
            _users = new List<User>();
            _subjects = new List<Subject>();
            _lectures = new List<Lecture>();
            _tests = new List<Test>();
            _questions = new List<Question>();
            _groups = new List<Group>();
            _attempts = new List<TestAttempt>();
            _answers = new List<AnswerOption>();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>().Options;
            _mockContext = new Mock<ApplicationDbContext>(options);

            _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(_users).Object);
            _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(_subjects).Object);
            _mockContext.Setup(c => c.Lectures).Returns(DbSetMock.Create(_lectures).Object);
            _mockContext.Setup(c => c.Tests).Returns(DbSetMock.Create(_tests).Object);
            _mockContext.Setup(c => c.Questions).Returns(DbSetMock.Create(_questions).Object);
            _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(_groups).Object);
            _mockContext.Setup(c => c.TestAttempts).Returns(DbSetMock.Create(_attempts).Object);
            _mockContext.Setup(c => c.AnswerOptions).Returns(DbSetMock.Create(_answers).Object);

            _testWebRootPath = Path.Combine(Path.GetTempPath(), "CourseTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_testWebRootPath);
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockEnvironment.Setup(m => m.WebRootPath).Returns(_testWebRootPath);

            var userStore = new Mock<IUserStore<User>>();
            _mockUserManager = new Mock<UserManager<User>>(userStore.Object, null, null, null, null, null, null, null, null);

            _controller = new CourseContentController(_mockContext.Object, _mockUserManager.Object, _mockEnvironment.Object);
            _controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            if (Directory.Exists(_testWebRootPath)) Directory.Delete(_testWebRootPath, true);
        }

        private void SetUser(string userId, string role)
        {
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userPrincipal }
            };

            _mockUserManager.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);
        }

        private void SeedData()
        {
            var t1 = new User { Id = "t1", UserName = "t1" };
            var admin = new User { Id = "admin", UserName = "admin" };
            _users.AddRange(new[] { t1, admin });

            var g1 = new Group { Id = 10, GroupName = "G1" };
            _groups.Add(g1);

            var s1 = new Subject
            {
                Id = 1,
                Title = "Subject 1",
                Teachers = new List<User> { t1 }, 
                Lectures = new List<Lecture>(),
                Tests = new List<Test>(),
                EnrolledGroups = new List<Group> { g1 }
            };

            var s2 = new Subject
            {
                Id = 2,
                Title = "Subject 2",
                Teachers = new List<User>(),
                Lectures = new List<Lecture>(),
                Tests = new List<Test>()
            };

            _subjects.AddRange(new[] { s1, s2 });
        }

        [Test]
        public async Task Index_TeacherWithAccess_ReturnsView()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var result = await _controller.Index(1);

            Assert.That(result, Is.InstanceOf<ViewResult>());
            var model = ((ViewResult)result).Model as CourseContentModel;
            Assert.That(model.Id, Is.EqualTo(1));
        }

        [Test]
        public async Task Index_TeacherNoAccess_ReturnsForbid()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var result = await _controller.Index(2); 

            Assert.That(result, Is.InstanceOf<ForbidResult>());
        }

        [Test]
        public async Task ChangeSubjectStatus_PublishValid_Success()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var subject = _subjects.First(s => s.Id == 1);
            
            var lecture = new Lecture { Id = 100, Title = "L1" };
            var test = new Test { Id = 200, Title = "T1", Questions = new List<Question>() };
            var question = new Question { Id = 300, Text = "Q1" };
            
            subject.Lectures.Add(lecture);
            subject.Tests.Add(test);
            test.Questions.Add(question);

            var result = await _controller.ChangeSubjectStatus(1, ContentStatus.Published);

            Assert.That(subject.Status, Is.EqualTo(ContentStatus.Published));
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
            
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect.ActionName, Is.EqualTo("Index"));
        }

        [Test]
        public async Task UpdateGroups_UpdatesList()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var model = new ConfigureGroupsModel { SubjectId = 1, SelectedGroupIds = new List<int>() }; 
            var result = await _controller.UpdateGroups(model);
            var subject = _subjects.First(s => s.Id == 1);
            Assert.That(subject.EnrolledGroups, Is.Empty);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Test]
        public async Task CreateLecture_ValidFile_Saves()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("test.pdf");
            fileMock.Setup(f => f.Length).Returns(100);

            var model = new LectureViewModel
            {
                SubjectId = 1,
                Title = "New Lec",
                UploadedFile = fileMock.Object
            };

            var lecturesMock = Mock.Get(_mockContext.Object.Lectures);

            var result = await _controller.CreateLecture(model);

            lecturesMock.Verify(m => m.Add(It.Is<Lecture>(l => l.Title == "New Lec" && l.SubjectId == 1)), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
        }

        [Test]
        public async Task EditLecture_UpdatesData()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var lecture = new Lecture { Id = 10, SubjectId = 1, Title = "Old", FilePath = "old.pdf" };
            _lectures.Add(lecture);

            var model = new LectureViewModel { Id = 10, SubjectId = 1, Title = "New" }; 

            var result = await _controller.EditLecture(model);

            Assert.That(lecture.Title, Is.EqualTo("New"));
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Test]
        public async Task DeleteLecture_RemovesData()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var lecture = new Lecture { Id = 10, SubjectId = 1, FilePath = "path" };
            _lectures.Add(lecture);
            var lecturesMock = Mock.Get(_mockContext.Object.Lectures);

            var result = await _controller.DeleteLecture(10);

            lecturesMock.Verify(m => m.Remove(lecture), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Test]
        public async Task CreateTest_Valid_Saves()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var model = new TestViewModel { SubjectId = 1, Title = "Quiz", TimeLimitMinutes = 10 };
            var testsMock = Mock.Get(_mockContext.Object.Tests);

            var result = await _controller.CreateTest(model);

            testsMock.Verify(m => m.Add(It.Is<Test>(t => t.Title == "Quiz")), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Test]
        public async Task EditTest_UpdatesData()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var test = new Test { Id = 20, SubjectId = 1, Title = "Old" };
            _tests.Add(test);

            var model = new TestViewModel { Id = 20, SubjectId = 1, Title = "New" };

            var result = await _controller.EditTest(model);

            Assert.That(test.Title, Is.EqualTo("New"));
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Test]
        public async Task ChangeTestStatus_PublishEmpty_ReturnsError()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var test = new Test { Id = 20, SubjectId = 1, Status = ContentStatus.Hidden, Questions = new List<Question>() };
            _tests.Add(test);

            var result = await _controller.ChangeTestStatus(20, ContentStatus.Published);

            Assert.That(test.Status, Is.EqualTo(ContentStatus.Hidden));
            Assert.That(_controller.TempData["ErrorMessage"], Is.Not.Null);
        }

        [Test]
        public async Task CreateQuestion_Valid_Saves()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var test = new Test { Id = 20, SubjectId = 1 };
            _tests.Add(test);

            var model = new QuestionViewModel
            {
                TestId = 20,
                SubjectId = 1,
                Text = "2+2",
                Type = "Single",
                Answers = new List<AnswerOptionViewModel>
                {
                    new AnswerOptionViewModel { Text = "4", IsCorrect = true },
                    new AnswerOptionViewModel { Text = "5", IsCorrect = false }
                }
            };

            var questionsMock = Mock.Get(_mockContext.Object.Questions);

            var result = await _controller.CreateQuestion(model);

            questionsMock.Verify(m => m.Add(It.Is<Question>(q => q.Text == "2+2" && q.AnswerOptions.Count == 2)), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Test]
        public async Task CreateQuestion_Invalid_ReturnsView()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var model = new QuestionViewModel
            {
                TestId = 20,
                SubjectId = 1,
                Type = "Single",
                Answers = new List<AnswerOptionViewModel>
                {
                    new AnswerOptionViewModel { IsCorrect = false },
                    new AnswerOptionViewModel { IsCorrect = false }
                }
            };

            var result = await _controller.CreateQuestion(model);

            Assert.That(result, Is.InstanceOf<ViewResult>());
            Assert.That(_controller.ModelState.IsValid, Is.False);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Test]
        public async Task TestStats_ReturnsData()
        {
            SeedData();
            SetUser("t1", "Teacher");

            var test = new Test { Id = 20, SubjectId = 1, Title = "T", Subject = _subjects[0] };
            _tests.Add(test);

            var student = new User { Id = "s1", FullName = "Stud" };
            var attempt = new TestAttempt { TestId = 20, Student = student, IsCompleted = true, Score = 5 };
            _attempts.Add(attempt);

            var result = await _controller.TestStats(20) as ViewResult;

            Assert.That(result, Is.Not.Null);
            var model = result.Model as TestStatsViewModel;
            Assert.That(model.Results.Count, Is.EqualTo(1));
            Assert.That(model.Results[0].BestScore, Is.EqualTo(5));
        }
    }
}