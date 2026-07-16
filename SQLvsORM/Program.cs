using SQLvsORM.Services;
using Microsoft.EntityFrameworkCore;
using SQLvsORM.Model;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Game Database API", Version = "v0.1" });
});

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql("Host=localhost;Database=VGDatabase2;Username=postgres;Password=PikPok666"));

builder.Services.AddScoped<SearchServiceSQL>(_ =>
    new SearchServiceSQL("Host=localhost;Database=VGDatabase2;Username=postgres;Password=PikPok666"));

builder.Services.AddScoped<SearchServiceEF>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Game Database API v0.1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.Run();