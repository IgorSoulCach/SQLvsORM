using System.Data;
using SQLvsORM.Services;
using SQLvsORM.Model;
using Npgsql;

namespace SQLvsORM;

class Program
{
    private const string ConnectionString = "Host=localhost;Database=VGDatabase2;Username=postgres;Password=PikPok666";

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var searchService = new SearchService(ConnectionString);

        while (true)
        {
            try
            {
                Console.Write("\nВведите имя атрибута или 'exit' для выхода (пустой ввод - показать все): ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    ShowAllGames();
                    continue;
                }

                string attributeName = input.Trim();
                if (attributeName.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                Console.Write("Введите значение атрибута: ");
                string? valueInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(valueInput))
                {
                    Console.WriteLine("Значение атрибута не может быть пустым!");
                    continue;
                }
                string attributeValue = valueInput.Trim();

                Console.Write("Введите тип поиска (1-Equals, 2-NotEquals, 3-GreaterThan, 4-LessThan, 5-Between, 6-Contains, 7-In, 8-Before, 9-After): ");
                if (!Enum.TryParse(Console.ReadLine()?.Trim(), out SearchType searchType) || !Enum.IsDefined(searchType))
                {
                    Console.WriteLine("Неверный тип поиска!");
                    continue;
                }

                string attributeValue2 = "";
                if (searchType == SearchType.Between)
                {
                    Console.Write("Введите второе значение: ");
                    string? secondInput = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(secondInput))
                    {
                        Console.WriteLine("Второе значение не может быть пустым!");
                        continue;
                    }
                    attributeValue2 = secondInput.Trim();
                }

                var result = searchService.Search(attributeName, attributeValue, attributeValue2, searchType);
                PrintResults(result, attributeName, attributeValue, attributeValue2, searchType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }

    static void ShowAllGames()
    {
        using var context = new GameDbContext();
        var viewService = new GameViewServiceEntityFramework(context);
        viewService.PrintAllGamesToConsole();
    }

    static void PrintResults(DataTable dt, string attributeName, string attributeValue, string attributeValue2, SearchType searchType)
    {
        var symbols = new Dictionary<SearchType, string>
        {
            { SearchType.Equals, "=" },
            { SearchType.NotEquals, "!=" },
            { SearchType.GreaterThan, ">" },
            { SearchType.LessThan, "<" },
            { SearchType.Between, " - " },
            { SearchType.Contains, " contains " },
            { SearchType.In, " in " },
            { SearchType.Before, " before " },
            { SearchType.After, " after " }
        };

        string symbol = symbols.GetValueOrDefault(searchType, "?");
        string searchDescription = searchType == SearchType.Between
            ? $"'{attributeName}' '{attributeValue}'{symbol}'{attributeValue2}'"
            : $"'{attributeName}' {symbol} '{attributeValue}'";

        if (dt.Rows.Count == 0)
        {
            Console.WriteLine($"\nИгр с {searchDescription} не найдено.");
            return;
        }

        Console.WriteLine($"\nНайдено игр: {dt.Rows.Count}");
        Console.WriteLine(new string('═', 120));
        Console.WriteLine($"{"ID",-5} {"Название",-30} {"Дата",-12} {"Разработчик",-18} {"Цена",-8} {attributeName,-20}");
        Console.WriteLine(new string('═', 120));

        foreach (DataRow row in dt.Rows)
        {
            string title = Truncate(row["title"].ToString() ?? "", 28);
            string developer = Truncate(row["developer"].ToString() ?? "", 16);
            string attrValue = GetAttributeValue(row["game_id"].ToString()!, attributeName);

            Console.WriteLine($"{row["game_id"],-5} {title,-30} {row["release_date"],-12} {developer,-18} ${row["base_price"],-8} {Truncate(attrValue, 18),-20}");
        }

        Console.WriteLine(new string('═', 120));
    }

    static string GetAttributeValue(string gameId, string attributeName)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        string query = @"
            SELECT attribute_value::TEXT FROM AttributeText WHERE game_id = @gameId AND attribute_name = @attrName
            UNION ALL
            SELECT attribute_value::TEXT FROM AttributeNumber WHERE game_id = @gameId AND attribute_name = @attrName
            UNION ALL
            SELECT attribute_value::TEXT FROM AttributeBoolean WHERE game_id = @gameId AND attribute_name = @attrName
            UNION ALL
            SELECT attribute_value::TEXT FROM AttributeDate WHERE game_id = @gameId AND attribute_name = @attrName
            LIMIT 1";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@gameId", int.Parse(gameId));
        cmd.Parameters.AddWithValue("@attrName", attributeName);

        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? "N/A";
    }

    static string Truncate(string value, int maxLength)
        => value.Length > maxLength ? value[..(maxLength - 3)] + "..." : value;
}