using Microsoft.EntityFrameworkCore;
using SecureStorage.Data;
using SecureStorage.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("Database");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'Database' not found.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISecretService, SecretService>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
