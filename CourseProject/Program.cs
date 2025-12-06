using Microsoft.AspNetCore.Identity;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 5;
    options.Password.RequiredUniqueChars = 0;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<User>>();

    string[] roleNames = { "Admin", "Teacher", "Student" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var usersToCreate = new[]
    {
        new { Email = "admin@gmail.com", Pwd = "admin", Role = "Admin", Name = "Главный Администратор" },
        new { Email = "teacher@gmail.com", Pwd = "teacher", Role = "Teacher", Name = "Иван Преподаватель" },
        new { Email = "student@gmail.com", Pwd = "student", Role = "Student", Name = "Петр Студент" }
    };

    foreach (var userData in usersToCreate)
    {
        var user = await userManager.FindByEmailAsync(userData.Email);
        if (user == null)
        {
            var newUser = new User
            {
                UserName = userData.Email,
                Email = userData.Email,
                FullName = userData.Name,
                Description = "Создан автоматически при старте",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(newUser, userData.Pwd);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newUser, userData.Role);
            }
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
