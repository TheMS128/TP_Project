using CourseProject.DataBase.DbModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CourseProject.DataBase;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User, IdentityRole, string>(options)
{
    public virtual DbSet<Group> Groups { get; set; }
    public virtual DbSet<Subject> Subjects { get; set; }
    public virtual DbSet<Lecture> Lectures { get; set; }
    public virtual DbSet<Test> Tests { get; set; }
    public virtual DbSet<Question> Questions { get; set; }
    public virtual DbSet<AnswerOption> AnswerOptions { get; set; }
    public virtual DbSet<StudentAnswer> StudentAnswers { get; set; }
    public virtual DbSet<TestAttempt> TestAttempts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<User>()
            .HasMany(u => u.AssignedSubjects)
            .WithMany(s => s.Teachers)
            .UsingEntity(j => j.ToTable("SubjectsUsers"));

        modelBuilder.Entity<TestAttempt>()
            .HasOne(t => t.Student)
            .WithMany(s => s.TestAttempts)
            .HasForeignKey(t => t.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Group)
            .WithMany(g => g.Students)
            .HasForeignKey(u => u.GroupId)
            .OnDelete(DeleteBehavior.SetNull); 
        
        modelBuilder.Entity<Subject>()
            .HasMany(s => s.EnrolledGroups)
            .WithMany(g => g.Subjects)
            .UsingEntity(j => j.ToTable("SubjectsGroups"));
    }
}