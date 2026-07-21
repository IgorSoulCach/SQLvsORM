using SQLvsORM.Services;
using Microsoft.EntityFrameworkCore;
using SQLvsORM.Model;
using SQLvsORM.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Game Database API", Version = "v0.1" });
});

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql("Host=localhost;Database=VGDatabase2;Username=postgres;Password=PikPok666;Timeout=30000;CommandTimeout=30000"));

builder.Services.AddScoped<SearchServiceSQL>(_ =>
    new SearchServiceSQL("Host=localhost;Database=VGDatabase2;Username=postgres;Password=PikPok666;Timeout=300;CommandTimeout=300"));

builder.Services.AddScoped<SearchServiceEF>();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
app.UseResponseCompression();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Game Database API v0.1");
    c.RoutePrefix = "swagger";
});

app.UseSwaggerUI(config =>
{
    config.ConfigObject.AdditionalItems["syntaxHighlight"] = new Dictionary<string, object>
    {
        ["activated"] = false
    };
});

app.MapControllers();
Task.Run(async () =>
{
    await Task.Delay(3000); // Ждём пока API поднимется
    var nbomber = new NBomberBenchmark("https://127.0.0.1:51798");
    nbomber.Run();
});

app.Run();