using CourseProject.DataBase.DbModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CourseProject.DataBase
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole, string>
    {
        public DbSet<Group> Groups { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Lecture> Lectures { get; set; }
        public DbSet<Test> Tests { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<AnswerOption> AnswerOptions { get; set; }
        public DbSet<StudentAnswer> StudentAnswers { get; set; }
        public DbSet<TestAttempt> TestAttempts { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestAttempt>()
                .HasOne(t => t.Student) 
                .WithMany(s => s.TestAttempts) 
                .HasForeignKey(t => t.StudentId) 
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}