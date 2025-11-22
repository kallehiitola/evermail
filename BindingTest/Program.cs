var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/test", (string? from, DateTime? dateFrom) => Results.Json(new { from, dateFrom }));

app.Run();
