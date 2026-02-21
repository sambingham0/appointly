using appointly;
using appointly.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// database connection
builder.Services.AddSqlite<AppointlyContext>("Data Source=appointly.db");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Initialize the database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppointlyContext>();
    db.Database.EnsureCreated();

    // Simple test data
    // if (!db.Users.Any())
    // {
    //     db.Users.Add(new User
    //     {
    //         Id = "test-user-1",
    //         DisplayName = "Test User",
    //         Email = "test@example.com"
    //     });

    //     db.SaveChanges();
    // }
}

app.Run();
