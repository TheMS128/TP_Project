using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.Models.AdminViewModels.Group;
using CourseProject.Models.AdminViewModels.Student;
using CourseProject.Models.AdminViewModels.Subject;
using CourseProject.Models.AdminViewModels.Teacher;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CourseProject.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> ManageStudents(string searchString)
    {
        var studentRoleId = await _context.Roles
            .Where(r => r.Name == "Student")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var query = _context.Users
            .Include(u => u.Group)
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == studentRoleId && ur.UserId == u.Id))
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            searchString = searchString.Trim().ToLower();

            query = query.Where(s => s.FullName.ToLower().Contains(searchString)
                                     || s.Email.ToLower().Contains(searchString)
                                     || (s.Description != null && s.Description.ToLower().Contains(searchString))
                                     || (s.Group != null && s.Group.GroupName.ToLower().Contains(searchString)));
        }

        var students = await query.ToListAsync();
        var allGroups = await _context.Groups.OrderBy(g => g.GroupName).ToListAsync();
        var model = new ManageStudentsViewModel
        {
            Students = students,
            AllGroups = allGroups
        };

        ViewData["SearchString"] = searchString;
        return View("Students/ManageStudents", model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateStudent()
    {
        ViewBag.Groups = new SelectList(await _context.Groups.OrderBy(g => g.GroupName).ToListAsync(), "Id",
            "GroupName");
        return View("Students/CreateStudent");
    }

    [HttpPost]
    public async Task<IActionResult> CreateStudent(CreateStudentViewModel model)
    {
        if (ModelState.IsValid)
        {
            var student = new User
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Description = model.Description ?? "Студент",
                GroupId = model.GroupId,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(student, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(student, "Student");
                return RedirectToAction(nameof(ManageStudents));
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        }

        ViewBag.Groups = new SelectList(await _context.Groups.OrderBy(g => g.GroupName).ToListAsync(), "Id",
            "GroupName", model.GroupId);
        return View("Students/CreateStudent", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditStudent(string id)
    {
        var student = await _context.Users.FindAsync(id);
        if (student == null) return NotFound();

        var model = new EditStudentViewModel
        {
            Id = student.Id,
            FullName = student.FullName,
            Email = student.Email,
            Description = student.Description,
            GroupId = student.GroupId
        };

        ViewBag.Groups = new SelectList(await _context.Groups.OrderBy(g => g.GroupName).ToListAsync(), "Id",
            "GroupName", student.GroupId);
        return View("Students/EditStudent", model);
    }

    [HttpPost]
    public async Task<IActionResult> EditStudent(EditStudentViewModel model)
    {
        if (ModelState.IsValid)
        {
            var student = await _context.Users.FindAsync(model.Id);
            if (student != null)
            {
                student.FullName = model.FullName;
                student.Email = model.Email;
                student.UserName = model.Email;
                student.Description = model.Description;
                student.GroupId = model.GroupId;

                var result = await _userManager.UpdateAsync(student);
                if (result.Succeeded)
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(ManageStudents));
                }

                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            }
        }

        ViewBag.Groups = new SelectList(await _context.Groups.OrderBy(g => g.GroupName).ToListAsync(), "Id",
            "GroupName", model.GroupId);
        return View("Students/EditStudent", model);
    }

    [HttpGet]
    public async Task<IActionResult> AddStudentToGroup(int groupId)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) return NotFound();

        var studentRoleId = await _context.Roles
            .Where(r => r.Name == "Student")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var freeStudents = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == studentRoleId && ur.UserId == u.Id))
            .Where(s => s.Group == null)
            .ToListAsync();

        var model = new AddStudentToGroupViewModel
        {
            GroupId = group.Id,
            GroupName = group.GroupName,
            AvailableStudents = freeStudents
        };

        return View("Students/AddStudentToGroup", model);
    }

    [HttpPost]
    public async Task<IActionResult> AddStudentToGroup(AddStudentToGroupViewModel model)
    {
        var student = await _context.Users.FirstOrDefaultAsync(s => s.Id == model.SelectedStudentId);
        if (student != null)
        {
            var group = await _context.Groups.FindAsync(model.GroupId);
            if (group != null)
            {
                student.Group = group;
                await _context.SaveChangesAsync();
            }
        }

        return RedirectToAction(nameof(ManageGroups));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStudentGroup(string studentId, int? selectedGroupId)
    {
        var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);

        if (student != null)
        {
            student.GroupId = selectedGroupId;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageStudents));
    }

    public async Task<IActionResult> ManageTeachers(string searchString)
    {
        var teacherRoleId = await _context.Roles
            .Where(r => r.Name == "Teacher")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var query = _context.Users
            .Include(u => u.AssignedSubjects)
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == teacherRoleId && ur.UserId == u.Id))
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            searchString = searchString.Trim().ToLower();

            query = query.Where(t => t.FullName.ToLower().Contains(searchString)
                                     || t.Email.ToLower().Contains(searchString)
                                     || (t.Description != null && t.Description.ToLower().Contains(searchString)));
        }

        var allSubjects = await _context.Subjects.ToListAsync();
        var teachers = await query.ToListAsync();

        var model = new ManageTeachersViewModel
        {
            Teachers = teachers,
            AllSubjects = allSubjects
        };

        ViewData["SearchString"] = searchString;
        return View("Teachers/ManageTeachers", model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateTeacher()
    {
        ViewBag.Subjects = await _context.Subjects.OrderBy(s => s.Title).ToListAsync();
        return View("Teachers/CreateTeacher");
    }

    [HttpPost]
    public async Task<IActionResult> CreateTeacher(CreateTeacherViewModel model)
    {
        if (ModelState.IsValid)
        {
            var teacher = new User
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Description = model.Description ?? "Преподаватель",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(teacher, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(teacher, "Teacher");

                if (model.SelectedSubjectIds != null && model.SelectedSubjectIds.Any())
                {
                    var subjects = await _context.Subjects
                        .Where(s => model.SelectedSubjectIds.Contains(s.Id))
                        .ToListAsync();
                    teacher.AssignedSubjects = new List<Subject>(subjects);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(ManageTeachers));
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        }

        ViewBag.Subjects = await _context.Subjects.OrderBy(s => s.Title).ToListAsync();
        return View("Teachers/CreateTeacher", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditTeacher(string id)
    {
        var teacher = await _context.Users
            .Include(u => u.AssignedSubjects)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (teacher == null) return NotFound();

        var model = new EditTeacherViewModel
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            Email = teacher.Email,
            Description = teacher.Description,
            SelectedSubjectIds = teacher.AssignedSubjects?.Select(s => s.Id).ToList() ?? new List<int>()
        };

        ViewBag.Subjects = await _context.Subjects.OrderBy(s => s.Title).ToListAsync();
        return View("Teachers/EditTeacher", model);
    }

    [HttpPost]
    public async Task<IActionResult> EditTeacher(EditTeacherViewModel model)
    {
        if (ModelState.IsValid)
        {
            var teacher = await _context.Users
                .Include(u => u.AssignedSubjects)
                .FirstOrDefaultAsync(u => u.Id == model.Id);

            if (teacher != null)
            {
                teacher.FullName = model.FullName;
                teacher.Email = model.Email;
                teacher.UserName = model.Email;
                teacher.Description = model.Description;

                teacher.AssignedSubjects.Clear();
                if (model.SelectedSubjectIds != null && model.SelectedSubjectIds.Any())
                {
                    var subjects = await _context.Subjects
                        .Where(s => model.SelectedSubjectIds.Contains(s.Id))
                        .ToListAsync();
                    teacher.AssignedSubjects.AddRange(subjects);
                }

                var result = await _userManager.UpdateAsync(teacher);
                if (result.Succeeded)
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(ManageTeachers));
                }

                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            }
        }

        ViewBag.Subjects = await _context.Subjects.OrderBy(s => s.Title).ToListAsync();
        return View("Teachers/EditTeacher", model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateTeacherSubjects(string teacherId, List<int> selectedSubjectIds)
    {
        var teacher = await _context.Users
            .Include(u => u.AssignedSubjects)
            .FirstOrDefaultAsync(u => u.Id == teacherId);

        if (teacher != null)
        {
            teacher.AssignedSubjects.Clear();

            if (selectedSubjectIds != null && selectedSubjectIds.Any())
            {
                var subjectsToAdd = await _context.Subjects
                    .Where(s => selectedSubjectIds.Contains(s.Id))
                    .ToListAsync();

                teacher.AssignedSubjects.AddRange(subjectsToAdd);
            }

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageTeachers));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user != null)
        {
            var isStudent = await _userManager.IsInRoleAsync(user, "Student");
            var isTeacher = await _userManager.IsInRoleAsync(user, "Teacher");

            await _userManager.DeleteAsync(user);
            if (isStudent)
            {
                return RedirectToAction(nameof(ManageStudents));
            }

            if (isTeacher)
            {
                return RedirectToAction(nameof(ManageTeachers));
            }
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ManageGroups(string searchString)
    {
        var groupsQuery = _context.Groups
            .Include(g => g.Students)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            var normalizedSearch = searchString.Trim().ToLower();
            groupsQuery = groupsQuery.Where(g => g.GroupName.ToLower().Contains(normalizedSearch));
        }

        var studentRoleId = await _context.Roles
            .Where(r => r.Name == "Student")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var allStudents = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == studentRoleId && ur.UserId == u.Id))
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var model = new ManageGroupsViewModel
        {
            Groups = await groupsQuery.ToListAsync(),
            AllStudents = allStudents
        };

        ViewData["SearchString"] = searchString;
        return View("Groups/ManageGroups", model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateGroup()
    {
        var studentRoleId = await _context.Roles
            .Where(r => r.Name == "Student")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var allStudents = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == studentRoleId && ur.UserId == u.Id))
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var model = new CreateGroupViewModel
        {
            AvailableStudents = allStudents
        };

        return View("Groups/CreateGroup", model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup(CreateGroupViewModel model)
    {
        if (ModelState.IsValid)
        {
            if (await _context.Groups.AnyAsync(g => g.GroupName == model.GroupName))
            {
                ModelState.AddModelError("GroupName", "Группа с таким названием уже существует.");
                var studentRoleId = await _context.Roles.Where(r => r.Name == "Student").Select(r => r.Id)
                    .FirstOrDefaultAsync();
                model.AvailableStudents = await _context.Users
                    .Where(u => _context.Set<IdentityUserRole<string>>()
                        .Any(ur => ur.RoleId == studentRoleId && ur.UserId == u.Id))
                    .OrderBy(s => s.FullName).ToListAsync();
                return View("Groups/CreateGroup", model);
            }

            var group = new Group { GroupName = model.GroupName };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            if (model.SelectedStudentIds != null && model.SelectedStudentIds.Any())
            {
                var studentsToAdd = await _context.Users
                    .Where(u => model.SelectedStudentIds.Contains(u.Id))
                    .ToListAsync();

                foreach (var student in studentsToAdd)
                {
                    student.GroupId = group.Id;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(ManageGroups));
        }

        var roleId = await _context.Roles.Where(r => r.Name == "Student").Select(r => r.Id).FirstOrDefaultAsync();
        model.AvailableStudents = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == roleId && ur.UserId == u.Id))
            .OrderBy(s => s.FullName).ToListAsync();
        return View("Groups/CreateGroup", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditGroup(int id)
    {
        var group = await _context.Groups
            .Include(g => g.Students)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();

        var studentRoleId = await _context.Roles
            .Where(r => r.Name == "Student")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var allStudents = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == studentRoleId && ur.UserId == u.Id))
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var model = new EditGroupViewModel
        {
            Id = group.Id,
            GroupName = group.GroupName,
            AvailableStudents = allStudents,
            SelectedStudentIds = group.Students.Select(s => s.Id).ToList()
        };

        return View("Groups/EditGroup", model);
    }

    [HttpPost]
    public async Task<IActionResult> EditGroup(EditGroupViewModel model)
    {
        if (ModelState.IsValid)
        {
            var group = await _context.Groups
                .Include(g => g.Students)
                .FirstOrDefaultAsync(g => g.Id == model.Id);

            if (group != null)
            {
                group.GroupName = model.GroupName;
                var currentStudentIds = group.Students.Select(s => s.Id).ToList();
                var newSelectedIds = model.SelectedStudentIds ?? new List<string>();

                foreach (var student in group.Students)
                {
                    if (!newSelectedIds.Contains(student.Id))
                    {
                        student.GroupId = null;
                    }
                }

                if (newSelectedIds.Any())
                {
                    var studentsToAdd = await _context.Users
                        .Where(u => newSelectedIds.Contains(u.Id) && u.GroupId != group.Id)
                        .ToListAsync();

                    foreach (var student in studentsToAdd)
                    {
                        student.GroupId = group.Id;
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ManageGroups));
            }
        }

        var roleId = await _context.Roles.Where(r => r.Name == "Student").Select(r => r.Id).FirstOrDefaultAsync();
        model.AvailableStudents = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == roleId && ur.UserId == u.Id))
            .OrderBy(s => s.FullName).ToListAsync();
        return View("Groups/EditGroup", model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateGroupStudents(int groupId, List<string> selectedStudentIds)
    {
        var currentGroupStudents = await _context.Users
            .Where(u => u.GroupId == groupId)
            .ToListAsync();

        foreach (var student in currentGroupStudents)
        {
            student.GroupId = null;
        }

        if (selectedStudentIds != null && selectedStudentIds.Any())
        {
            var studentsToAdd = await _context.Users
                .Where(u => selectedStudentIds.Contains(u.Id))
                .ToListAsync();

            foreach (var student in studentsToAdd)
            {
                student.GroupId = groupId;
            }
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(ManageGroups));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var group = await _context.Groups
            .Include(g => g.Students)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group != null)
        {
            if (group.Students != null)
            {
                foreach (var student in group.Students)
                {
                    student.GroupId = null;
                }
            }

            _context.Groups.Remove(group);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageGroups));
    }

    public async Task<IActionResult> ManageSubjects(string searchString)
    {
        var subjectsQuery = _context.Subjects
            .Include(s => s.Teachers)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            var normalizedSearch = searchString.Trim().ToLower();

            subjectsQuery = subjectsQuery.Where(s =>
                s.Title.ToLower().Contains(normalizedSearch)
                || (s.Description != null && s.Description.ToLower().Contains(normalizedSearch))
            );
        }

        var teacherRoleId = await _context.Roles
            .Where(r => r.Name == "Teacher")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var allTeachers = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == teacherRoleId && ur.UserId == u.Id))
            .ToListAsync();

        var model = new ManageSubjectsViewModel
        {
            Subjects = await subjectsQuery.ToListAsync(),
            AllTeachers = allTeachers
        };

        ViewData["SearchString"] = searchString;
        return View("Subject/ManageSubjects", model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateSubject()
    {
        var teacherRoleId = await _context.Roles
            .Where(r => r.Name == "Teacher")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var allTeachers = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == teacherRoleId && ur.UserId == u.Id))
            .OrderBy(t => t.FullName)
            .ToListAsync();

        var model = new CreateSubjectViewModel
        {
            AvailableTeachers = allTeachers
        };

        return View("Subject/CreateSubject", model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubject(CreateSubjectViewModel model)
    {
        if (ModelState.IsValid)
        {
            var subject = new Subject
            {
                Title = model.Title,
                Description = model.Description
            };

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            if (model.SelectedTeacherIds != null && model.SelectedTeacherIds.Any())
            {
                var teachersToAdd = await _context.Users
                    .Where(u => model.SelectedTeacherIds.Contains(u.Id))
                    .ToListAsync();

                subject.Teachers = new List<User>();
                subject.Teachers.AddRange(teachersToAdd);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(ManageSubjects));
        }

        var roleId = await _context.Roles.Where(r => r.Name == "Teacher").Select(r => r.Id).FirstOrDefaultAsync();
        model.AvailableTeachers = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == roleId && ur.UserId == u.Id))
            .OrderBy(t => t.FullName).ToListAsync();
        return View("Subject/CreateSubject", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditSubject(int id)
    {
        var subject = await _context.Subjects
            .Include(s => s.Teachers)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subject == null) return NotFound();

        var teacherRoleId = await _context.Roles
            .Where(r => r.Name == "Teacher")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var allTeachers = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == teacherRoleId && ur.UserId == u.Id))
            .OrderBy(t => t.FullName)
            .ToListAsync();

        var model = new EditSubjectViewModel
        {
            Id = subject.Id,
            Title = subject.Title,
            Description = subject.Description,
            AvailableTeachers = allTeachers,
            SelectedTeacherIds = subject.Teachers.Select(t => t.Id).ToList()
        };
        return View("Subject/EditSubject", model);
    }

    [HttpPost]
    public async Task<IActionResult> EditSubject(EditSubjectViewModel model)
    {
        if (ModelState.IsValid)
        {
            var subject = await _context.Subjects
                .Include(s => s.Teachers)
                .FirstOrDefaultAsync(s => s.Id == model.Id);

            if (subject != null)
            {
                subject.Title = model.Title;
                subject.Description = model.Description;
                subject.Teachers.Clear();

                if (model.SelectedTeacherIds != null && model.SelectedTeacherIds.Any())
                {
                    var teachersToAdd = await _context.Users
                        .Where(u => model.SelectedTeacherIds.Contains(u.Id))
                        .ToListAsync();

                    subject.Teachers.AddRange(teachersToAdd);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ManageSubjects));
            }
        }

        var roleId = await _context.Roles.Where(r => r.Name == "Teacher").Select(r => r.Id).FirstOrDefaultAsync();
        model.AvailableTeachers = await _context.Users
            .Where(u => _context.Set<IdentityUserRole<string>>()
                .Any(ur => ur.RoleId == roleId && ur.UserId == u.Id))
            .OrderBy(t => t.FullName).ToListAsync();

        return View("Subject/EditSubject", model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSubjectTeachers(int subjectId, List<string> selectedTeacherIds)
    {
        var subject = await _context.Subjects
            .Include(s => s.Teachers)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject != null)
        {
            subject.Teachers.Clear();

            if (selectedTeacherIds != null && selectedTeacherIds.Any())
            {
                var newTeachers = await _context.Users
                    .Where(u => selectedTeacherIds.Contains(u.Id))
                    .ToListAsync();

                subject.Teachers.AddRange(newTeachers);
            }

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageSubjects));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSubject(int id)
    {
        var subject = await _context.Subjects.FindAsync(id);
        if (subject != null)
        {
            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageSubjects));
    }
}