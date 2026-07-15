using ConsoleTables;
using SQLvsORM.Model;
using Microsoft.EntityFrameworkCore;

namespace SQLvsORM.Services
{

    public class GameViewServiceEntityFramework
    {
        private readonly GameDbContext _context;

        public GameViewServiceEntityFramework(GameDbContext context)
        {
            _context = context;
        }

        public void PrintAllGamesToConsole()
        {
            var games = _context.Games
                .Include(g => g.AttributeTexts)
                .Include(g => g.AttributeNumbers)
                .Include(g => g.AttributeBooleans)
                .Include(g => g.AttributeDates)
                .ToList();

            var allTextAttrs = GetDistinctAttributes(games.SelectMany(g => g.AttributeTexts).Select(a => a.attribute_name));
            var allNumberAttrs = GetDistinctAttributes(games.SelectMany(g => g.AttributeNumbers).Select(a => a.attribute_name));
            var allBoolAttrs = GetDistinctAttributes(games.SelectMany(g => g.AttributeBooleans).Select(a => a.attribute_name));
            var allDateAttrs = GetDistinctAttributes(games.SelectMany(g => g.AttributeDates).Select(a => a.attribute_name));

            var columns = new List<string> { "ID", "Title", "Date", "Developer", "Price" };
            columns.AddRange(allTextAttrs);
            columns.AddRange(allNumberAttrs);
            columns.AddRange(allBoolAttrs);
            columns.AddRange(allDateAttrs);

            var table = new ConsoleTable(columns.ToArray());

            foreach (var game in games)
            {
                var row = new List<string>
            {
                game.game_id.ToString(),
                Truncate(game.title, 30),
                game.release_date?.ToString("yyyy-MM-dd") ?? "N/A",
                Truncate(game.developer, 20),
                game.base_price?.ToString("F2") ?? "0.00"
            };

                row.AddRange(allTextAttrs.Select(attr => game.AttributeTexts?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value ?? ""));
                row.AddRange(allNumberAttrs.Select(attr => game.AttributeNumbers?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value.ToString() ?? ""));
                row.AddRange(allBoolAttrs.Select(attr => game.AttributeBooleans?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value.ToString() ?? ""));
                row.AddRange(allDateAttrs.Select(attr => game.AttributeDates?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value.ToString("yyyy-MM-dd") ?? ""));

                table.AddRow(row.ToArray());
            }

            table.Write(Format.Alternative);
        }

        public void PrintGameDetails(int gameId)
        {
            var game = _context.Games
                .Include(g => g.AttributeTexts)
                .Include(g => g.AttributeNumbers)
                .Include(g => g.AttributeBooleans)
                .Include(g => g.AttributeDates)
                .FirstOrDefault(g => g.game_id == gameId);

            if (game == null)
            {
                Console.WriteLine($"Игра с ID {gameId} не найдена.");
                return;
            }

            Console.WriteLine($"\n{new string('═', 80)}");
            Console.WriteLine($"ID: {game.game_id} | {game.title}");
            Console.WriteLine($"Дата: {game.release_date?.ToString("yyyy-MM-dd") ?? "Н/Д"}");
            Console.WriteLine($"Разработчик: {game.developer}");
            Console.WriteLine($"Издатель: {game.publisher}");
            Console.WriteLine($"Цена: ${game.base_price}");

            PrintAttributeGroup("Текстовые атрибуты", game.AttributeTexts?.Select(a => (a.attribute_name, a.attribute_value)));
            PrintAttributeGroup("Числовые атрибуты", game.AttributeNumbers?.Select(a => (a.attribute_name, a.attribute_value.ToString())));
            PrintAttributeGroup("Булевы атрибуты", game.AttributeBooleans?.Select(a => (a.attribute_name, a.attribute_value.ToString())));
            PrintAttributeGroup("Атрибуты-даты", game.AttributeDates?.Select(a => (a.attribute_name, a.attribute_value.ToString("yyyy-MM-dd"))));
        }

        private static void PrintAttributeGroup(string title, IEnumerable<(string name, string value)> attributes)
        {
            if (attributes == null || !attributes.Any()) return;
            Console.WriteLine($"\n{title}:");
            foreach (var (name, value) in attributes)
                Console.WriteLine($"  {name}: {value}");
        }

        private static List<string> GetDistinctAttributes(IEnumerable<string> attributes)
            => attributes.Where(a => a != null).Distinct().OrderBy(n => n).ToList();

        private static string Truncate(string value, int maxLength)
            => value?.Length > maxLength ? value[..(maxLength - 3)] + "..." : value;
    }
}
