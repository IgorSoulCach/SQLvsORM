using System.Data;
using Npgsql;

namespace SQLvsORM.Features;

public class DataCreator
{
    private readonly string _connectionString;

    private static readonly string[] Developers = {
        "Valve", "Ubisoft", "EA", "Nintendo", "Rockstar Games", "Bethesda",
        "Square Enix", "Capcom", "FromSoftware", "CD Projekt Red", "Blizzard",
        "Riot Games", "Epic Games", "Naughty Dog", "Insomniac Games", "BioWare"
    };

    private static readonly string[] Publishers = {
        "Sony", "Microsoft", "Nintendo", "EA", "Ubisoft", "Take-Two",
        "Bandai Namco", "Square Enix", "Sega", "Capcom", "Devolver Digital"
    };

    private static readonly string[] Platforms = { "PC", "PlayStation", "Xbox", "Nintendo", "Mobile" };
    private static readonly string[] Genres = { "Action", "RPG", "Strategy", "Simulation", "Adventure", "Horror", "Puzzle", "Racing", "Fighting", "Platformer" };
    private static readonly string[] Engines = { "Unity", "Unreal Engine 5", "Godot", "GameMaker", "Source 2", "Frostbite", "Custom" };
    private static readonly string[] Countries = { "USA", "Japan", "Canada", "UK", "Poland", "Germany", "Sweden", "France" };

    private static readonly string[] FirstWords = { "Dark", "Lost", "Final", "Eternal", "Shadow", "Cyber", "Neon", "Pixel", "Void", "Epic", "Savage", "Holy", "Cursed", "Broken", "Rising", "Fallen", "Ancient", "Mystic", "Forgotten", "Crystal" };
    private static readonly string[] SecondWords = { "Souls", "Hearts", "Blades", "Realms", "Kingdoms", "Empires", "Legends", "Warriors", "Heroes", "Dragons", "Knights", "Wizards", "Hunters", "Assassins", "Guardians", "Pirates", "Spirits", "Demons", "Angels", "Gods" };

    private readonly Random _random = new();
    private readonly HashSet<string> _usedTitles = new();

    public DataCreator(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Seed(int totalGames = 200000)
    {
        Console.WriteLine($"Начинаю генерацию {totalGames} игр...");
        var startTime = DateTime.Now;

        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using (var cmd = new NpgsqlCommand("TRUNCATE AttributeDate, AttributeBoolean, AttributeNumber, AttributeText, Game CASCADE; ALTER SEQUENCE game_game_id_seq RESTART WITH 1;", conn))
        {
            cmd.ExecuteNonQuery();
        }

        int batchSize = 100;
        int gameCounter = 0;

        for (int batch = 0; batch < totalGames; batch += batchSize)
        {
            int currentBatchSize = Math.Min(batchSize, totalGames - batch);

            using var transaction = conn.BeginTransaction();

            for (int i = 0; i < currentBatchSize; i++)
            {
                gameCounter++;
                var game = GenerateUniqueGame(gameCounter);

                using (var cmd = new NpgsqlCommand(
                    "INSERT INTO Game (title, release_date, developer, publisher, base_price) VALUES (@t, @d, @dev, @pub, @p) RETURNING game_id",
                    conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@t", game.Title);
                    cmd.Parameters.AddWithValue("@d", game.ReleaseDate);
                    cmd.Parameters.AddWithValue("@dev", game.Developer);
                    cmd.Parameters.AddWithValue("@pub", game.Publisher);
                    cmd.Parameters.AddWithValue("@p", game.BasePrice);

                    int gameId = (int)cmd.ExecuteScalar();

                    InsertTextAttributes(conn, transaction, gameId);
                    InsertNumberAttributes(conn, transaction, gameId);
                    InsertBooleanAttributes(conn, transaction, gameId);
                    InsertDateAttributes(conn, transaction, gameId, game.ReleaseDate);
                }
            }

            transaction.Commit();

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            Console.WriteLine($"Прогресс: {gameCounter}/{totalGames} ({(double)gameCounter / totalGames * 100:F1}%) | Время: {elapsed:F0} сек");
        }

        var totalTime = (DateTime.Now - startTime).TotalSeconds;
        Console.WriteLine($"\nГотово! {totalGames} игр за {totalTime:F0} сек");
    }

    private void InsertTextAttributes(NpgsqlConnection conn, NpgsqlTransaction transaction, int gameId)
    {
        var attrs = new Dictionary<string, string>
        {
            ["main_platform"] = PickRandom(Platforms),
            ["main_genre"] = PickRandom(Genres)
        };
        if (_random.NextDouble() < 0.5) attrs["game_engine"] = PickRandom(Engines);
        if (_random.NextDouble() < 0.3) attrs["developer_country"] = PickRandom(Countries);

        foreach (var attr in attrs)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO AttributeText (game_id, attribute_name, attribute_value) VALUES (@id, @name, @val)",
                conn, transaction);
            cmd.Parameters.AddWithValue("@id", gameId);
            cmd.Parameters.AddWithValue("@name", attr.Key);
            cmd.Parameters.AddWithValue("@val", attr.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void InsertNumberAttributes(NpgsqlConnection conn, NpgsqlTransaction transaction, int gameId)
    {
        var attrs = new Dictionary<string, decimal>
        {
            ["playtime_hours"] = _random.Next(1, 500),
            ["size_gb"] = (decimal)Math.Round(_random.NextDouble() * 150 + 0.1, 2)
        };
        if (_random.NextDouble() < 0.6) attrs["metacritic_score"] = _random.Next(30, 101);
        if (_random.NextDouble() < 0.3) attrs["max_players"] = _random.Next(1, 101);

        foreach (var attr in attrs)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO AttributeNumber (game_id, attribute_name, attribute_value) VALUES (@id, @name, @val)",
                conn, transaction);
            cmd.Parameters.AddWithValue("@id", gameId);
            cmd.Parameters.AddWithValue("@name", attr.Key);
            cmd.Parameters.AddWithValue("@val", attr.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void InsertBooleanAttributes(NpgsqlConnection conn, NpgsqlTransaction transaction, int gameId)
    {
        var attrs = new Dictionary<string, bool>
        {
            ["has_online"] = _random.NextDouble() < 0.4,
            ["has_controller_support"] = _random.NextDouble() < 0.8
        };

        foreach (var attr in attrs)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO AttributeBoolean (game_id, attribute_name, attribute_value) VALUES (@id, @name, @val)",
                conn, transaction);
            cmd.Parameters.AddWithValue("@id", gameId);
            cmd.Parameters.AddWithValue("@name", attr.Key);
            cmd.Parameters.AddWithValue("@val", attr.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void InsertDateAttributes(NpgsqlConnection conn, NpgsqlTransaction transaction, int gameId, DateTime releaseDate)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO AttributeDate (game_id, attribute_name, attribute_value) VALUES (@id, 'original_release', @val)",
            conn, transaction);
        cmd.Parameters.AddWithValue("@id", gameId);
        cmd.Parameters.AddWithValue("@val", releaseDate);
        cmd.ExecuteNonQuery();
    }

    private Game GenerateUniqueGame(int counter)
    {
        string title;
        do
        {
            title = $"{PickRandom(FirstWords)} {PickRandom(SecondWords)} {counter}";
        } while (!_usedTitles.Add(title));

        return new Game
        {
            Title = title,
            ReleaseDate = new DateTime(2000, 1, 1).AddDays(_random.Next(8000)),
            Developer = PickRandom(Developers),
            Publisher = PickRandom(Publishers),
            BasePrice = _random.NextDouble() < 0.15 ? 0 : Math.Round((decimal)(_random.NextDouble() * 69.99), 2)
        };
    }

    private string PickRandom(string[] arr) => arr[_random.Next(arr.Length)];

    private class Game
    {
        public string Title { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string Developer { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
    }
}