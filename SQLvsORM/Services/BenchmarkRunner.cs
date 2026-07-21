using System.Text;
using System.Text.Json;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace SQLvsORM.Tools;

public class NBomberBenchmark
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public NBomberBenchmark(string baseUrl)
    {
        _baseUrl = baseUrl;
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    public void Run()
    {
        var queries = new (string name, object body)[]
        {
            ("Все игры", new { attributeName = "", attributeValue = "", searchType = 1, attributeValue2 = "" }),
            ("metacritic_score > 80", new { attributeName = "metacritic_score", attributeValue = "80", searchType = 3, attributeValue2 = "" }),
            ("main_genre = RPG", new { attributeName = "main_genre", attributeValue = "RPG", searchType = 1, attributeValue2 = "" }),
            ("playtime_hours 10-50", new { attributeName = "playtime_hours", attributeValue = "10", searchType = 7, attributeValue2 = "50" }),
            ("has_online = true", new { attributeName = "has_online", attributeValue = "true", searchType = 1, attributeValue2 = "" }),
            ("main_genre contains Action", new { attributeName = "main_genre", attributeValue = "Action", searchType = 8, attributeValue2 = "" }),
            ("size_gb < 5", new { attributeName = "size_gb", attributeValue = "5", searchType = 4, attributeValue2 = "" }),
            ("age_rating >= 16", new { attributeName = "age_rating", attributeValue = "16", searchType = 5, attributeValue2 = "" }),
            ("main_genre != Horror", new { attributeName = "main_genre", attributeValue = "Horror", searchType = 2, attributeValue2 = "" }),
            ("original_release after 2020", new { attributeName = "original_release", attributeValue = "2020-01-01", searchType = 12, attributeValue2 = "" }),
        };

        Console.WriteLine("\n╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║        NBOMBER: SQL (прямые запросы)            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝\n");

        RunScenarios(queries, "search", "SQL");

        Console.WriteLine("\n╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║        NBOMBER: EF (Entity Framework)           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝\n");

        RunScenarios(queries, "search-ef", "EF");
    }

    private void RunScenarios((string name, object body)[] queries, string endpoint, string label)
    {
        foreach (var (name, body) in queries)
        {
            var json = JsonSerializer.Serialize(body);

            var scenario = Scenario.Create($"{label}_{name}", async _ =>
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = Http.CreateRequest("POST", $"{_baseUrl}/api/games/{endpoint}")
                    .WithBody(content);
                var response = await Http.Send(_httpClient, request);
                return response.StatusCode == "OK" ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.KeepConstant(2, TimeSpan.FromSeconds(15))
            );

            NBomberRunner.RegisterScenarios(scenario).Run();
        }
    }
}