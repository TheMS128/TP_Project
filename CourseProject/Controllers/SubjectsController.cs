using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums; 
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

    public SubjectsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
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

        return View(subjects);
    }

    public async Task<IActionResult> Details(int id)
    {
        var subject = await _context.Subjects
            .Include(s => s.Lectures)
            .Include(s => s.Tests)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subject == null) return NotFound();

        if (User.IsInRole("Student"))
        {
            var userId = _userManager.GetUserId(User);
            
            var hasAccess = await _context.Groups
                .Where(g => g.Students.Any(u => u.Id == userId))
                .AnyAsync(g => g.Subjects.Any(s => s.Id == id));

            if (!hasAccess) return Forbid(); 
            
            subject.Lectures = subject.Lectures?.Where(l => l.Status == ContentStatus.Published).ToList();
            subject.Tests = subject.Tests?.Where(t => t.Status == ContentStatus.Published).ToList();
        }

        return View(subject);
    }
}