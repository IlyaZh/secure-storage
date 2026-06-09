using Microsoft.EntityFrameworkCore;
using SecureStorage.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureLogging();

builder.Services.AddControllers();

builder.ConfigureSettings(builder.Configuration);

builder.ConfigureDatabase(builder.Configuration);
builder.ConfigureAuthentication(builder.Configuration);

builder.RegisterApplicationServices();
builder.ConfigureFeatureFlags();

var app = builder.Build();

app.UseConfiguredLogging();
app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.MigrateDatabaseAsync();

app.Run();
