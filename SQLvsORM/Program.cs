using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ConsoleTables;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

[Table("game")]
public class Game
{
    [Key]
    public int game_id { get; set; }
    public string title { get; set; }
    public DateTime? release_date { get; set; }
    public string developer { get; set; }
    public string publisher { get; set; }
    public string? description { get; set; }
    public decimal? base_price { get; set; }

    public ICollection<AttributeText> AttributeTexts { get; set; }
    public ICollection<AttributeNumber> AttributeNumbers { get; set; }
    public ICollection<AttributeBoolean> AttributeBooleans { get; set; }
    public ICollection<AttributeDate> AttributeDates { get; set; }
}
[Table("attributetext")]
public class AttributeText
{
    public int game_id { get; set; }
    public string attribute_name { get; set; }
    public string attribute_value { get; set; }

    [ForeignKey("game_id")]
    public Game Game { get; set; }
}
[Table("attributenumber")]
public class AttributeNumber
{
    public int game_id { get; set; }
    public string attribute_name { get; set; }
    public decimal attribute_value { get; set; }

    [ForeignKey("game_id")]
    public Game Game { get; set; }
}
[Table("attributeboolean")]
public class AttributeBoolean
{
    public int game_id { get; set; }
    public string attribute_name { get; set; }
    public bool attribute_value { get; set; }

    [ForeignKey("game_id")]
    public Game Game { get; set; }
}
[Table("attributedate")]
public class AttributeDate
{
    public int game_id { get; set; }
    public string attribute_name { get; set; }
    public DateTime attribute_value { get; set; }

    [ForeignKey("game_id")]
    public Game Game { get; set; }
}
public class GameDbContext : DbContext
{
    public DbSet<Game> Games { get; set; }
    public DbSet<AttributeText> AttributeTexts { get; set; }
    public DbSet<AttributeNumber> AttributeNumbers { get; set; }
    public DbSet<AttributeBoolean> AttributeBooleans { get; set; }
    public DbSet<AttributeDate> AttributeDates { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=VGDatabase2;Username=postgres;Password=PikPok666");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // AttributeText - составной ключ
        modelBuilder.Entity<AttributeText>()
            .HasKey(a => new { a.game_id, a.attribute_name });

        // AttributeNumber - составной ключ
        modelBuilder.Entity<AttributeNumber>()
            .HasKey(a => new { a.game_id, a.attribute_name });

        // AttributeBoolean - составной ключ
        modelBuilder.Entity<AttributeBoolean>()
            .HasKey(a => new { a.game_id, a.attribute_name });

        // AttributeDate - составной ключ
        modelBuilder.Entity<AttributeDate>()
            .HasKey(a => new { a.game_id, a.attribute_name });

        // Связи
        modelBuilder.Entity<AttributeText>()
            .HasOne(a => a.Game)
            .WithMany(g => g.AttributeTexts)
            .HasForeignKey(a => a.game_id);

        modelBuilder.Entity<AttributeNumber>()
            .HasOne(a => a.Game)
            .WithMany(g => g.AttributeNumbers)
            .HasForeignKey(a => a.game_id);

        modelBuilder.Entity<AttributeBoolean>()
            .HasOne(a => a.Game)
            .WithMany(g => g.AttributeBooleans)
            .HasForeignKey(a => a.game_id);

        modelBuilder.Entity<AttributeDate>()
            .HasOne(a => a.Game)
            .WithMany(g => g.AttributeDates)
            .HasForeignKey(a => a.game_id);
    }
}

public class GameFullInfoDto
{
    public int game_id { get; set; }
    public string title { get; set; }
    public DateTime? release_date { get; set; }
    public string developer { get; set; }
    public string publisher { get; set; }
    public string description { get; set; }
    public decimal? base_price { get; set; }

    public Dictionary<string, string> TextAttributes { get; set; } = new();
    public Dictionary<string, decimal> NumberAttributes { get; set; } = new();
    public Dictionary<string, bool> BooleanAttributes { get; set; } = new();
    public Dictionary<string, DateTime> DateAttributes { get; set; } = new();
}

class GameDatabaseSearch
{
    private static string connectionString = "Host=localhost;Database=VGDatabase2;Username=postgres;Password=PikPok666";

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

       /* using (var context = new GameDbContext())
        {
            var games = context.Games
                .Include(g => g.AttributeTexts)
                .Include(g => g.AttributeNumbers)
                .Include(g => g.AttributeBooleans)
                .Include(g => g.AttributeDates)
                .ToList();

            var allTextAttrs = games.SelectMany(g => g.AttributeTexts ?? new List<AttributeText>())
                .Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList();
            var allNumberAttrs = games.SelectMany(g => g.AttributeNumbers ?? new List<AttributeNumber>())
                .Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList();
            var allBoolAttrs = games.SelectMany(g => g.AttributeBooleans ?? new List<AttributeBoolean>())
                .Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList();
            var allDateAttrs = games.SelectMany(g => g.AttributeDates ?? new List<AttributeDate>())
                .Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Games");

                int col = 1;

                worksheet.Cell(1, col++).Value = "ID";
                worksheet.Cell(1, col++).Value = "Title";
                worksheet.Cell(1, col++).Value = "Release Date";
                worksheet.Cell(1, col++).Value = "Developer";
                worksheet.Cell(1, col++).Value = "Publisher";
                worksheet.Cell(1, col++).Value = "Price";

                int startTextCol = col;
                foreach (var attr in allTextAttrs)
                    worksheet.Cell(1, col++).Value = attr;

                int startNumberCol = col;
                foreach (var attr in allNumberAttrs)
                    worksheet.Cell(1, col++).Value = attr;

                int startBoolCol = col;
                foreach (var attr in allBoolAttrs)
                    worksheet.Cell(1, col++).Value = attr;

                int startDateCol = col;
                foreach (var attr in allDateAttrs)
                    worksheet.Cell(1, col++).Value = attr;

                int row = 2;
                foreach (var game in games)
                {
                    col = 1;
                    worksheet.Cell(row, col++).Value = game.game_id;
                    worksheet.Cell(row, col++).Value = game.title;
                    worksheet.Cell(row, col++).Value = game.release_date?.ToString("yyyy-MM-dd") ?? "";
                    worksheet.Cell(row, col++).Value = game.developer;
                    worksheet.Cell(row, col++).Value = game.publisher;
                    worksheet.Cell(row, col++).Value = game.base_price;

                    col = startTextCol;
                    foreach (var attr in allTextAttrs)
                        worksheet.Cell(row, col++).Value = game.AttributeTexts?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value ?? "";

                    col = startNumberCol;
                    foreach (var attr in allNumberAttrs)
                    {
                        var val = game.AttributeNumbers?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value;
                        if (val.HasValue)
                            worksheet.Cell(row, col).Value = val.Value;
                        col++;
                    }

                    col = startBoolCol;
                    foreach (var attr in allBoolAttrs)
                    {
                        var val = game.AttributeBooleans?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value;
                        worksheet.Cell(row, col++).Value = val?.ToString() ?? "";
                    }

                    col = startDateCol;
                    foreach (var attr in allDateAttrs)
                    {
                        var val = game.AttributeDates?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value;
                        if (val.HasValue)
                            worksheet.Cell(row, col).Value = val.Value.ToString("yyyy-MM-dd");
                        col++;
                    }

                    row++;
                }

                worksheet.Columns().AdjustToContents();
                worksheet.SheetView.FreezeRows(1);

                string filename = $"games_export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                workbook.SaveAs(filename);
                Console.WriteLine($"Экспортировано {games.Count} игр в файл: {filename}");
            }
        }*/


        while (true)
        {
            try
            {
                Console.Write("\nВведите имя атрибута или 'exit' для выхода: ");
                string attributeName = Console.ReadLine()?.Trim();

                if (attributeName?.ToLower() == "exit")
                    break;

                if (string.IsNullOrEmpty(attributeName))
                {
                    Console.WriteLine("Имя атрибута не может быть пустым!");
                    continue;
                }

                Console.Write("Введите значение атрибута: ");
                string attributeValue = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(attributeValue))
                {
                    Console.WriteLine("Значение атрибута не может быть пустым!");
                    continue;
                }

                Console.Write("Введите тип поиска (1 - Equals, 2 - NotEquals, 3 - GreaterThan, 4 - LessThan, 5 - Between, 6 - Contains, 7 - In, 8 - Before, 9 - After ): ");
                string searchType = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(searchType) || ((searchType != "1") && (searchType != "2") && (searchType != "3") && (searchType != "4") && (searchType != "5") && (searchType != "6") && (searchType != "7") && (searchType != "8") && (searchType != "9")))
                {
                    Console.WriteLine("Пожалуйста выберите тип поиска!");
                    continue;
                }
                string attributeValue2 = "";
                if  (searchType == "5")
                {
                    Console.Write("\nВведите второе имя атрибута: ");
                    attributeValue2 = Console.ReadLine()?.Trim();
                        if (string.IsNullOrEmpty(attributeValue2))
                        {
                            Console.WriteLine("Значение атрибута не может быть пустым!");
                            continue;
                        }
                    try 
                    {
                        if (Convert.ToInt32(attributeValue) > Convert.ToInt32(attributeValue2))
                        { }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Пожалуйста введите корректные значения атрибутов!");
                        continue;
                    }
                    
                }

                SearchGames(attributeName, attributeValue, attributeValue2, searchType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }

    static void SearchGames(string attributeName, string attributeValue, string attributeValue2, string searchType)
    {
        string sqlQueryEquals = @"
            SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            LEFT JOIN AttributeText at_text ON g.game_id = at_text.game_id 
                AND at_text.attribute_name = @attrName
            LEFT JOIN AttributeNumber at_num ON g.game_id = at_num.game_id 
                AND at_num.attribute_name = @attrName
            LEFT JOIN AttributeBoolean at_bool ON g.game_id = at_bool.game_id 
                AND at_bool.attribute_name = @attrName
            LEFT JOIN AttributeDate at_date ON g.game_id = at_date.game_id 
                AND at_date.attribute_name = @attrName
            WHERE 
                at_text.attribute_value = @attrValue OR 
                CAST(at_num.attribute_value AS TEXT) = @attrValue OR 
                CAST(at_bool.attribute_value AS TEXT) = @attrValue OR 
                CAST(at_date.attribute_value AS TEXT) = @attrValue
            ORDER BY g.title";

        string sqlQueryNotEquals = @"
            SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            LEFT JOIN AttributeText at_text ON g.game_id = at_text.game_id 
                AND at_text.attribute_name = @attrName
            LEFT JOIN AttributeNumber at_num ON g.game_id = at_num.game_id 
                AND at_num.attribute_name = @attrName
            LEFT JOIN AttributeBoolean at_bool ON g.game_id = at_bool.game_id 
                AND at_bool.attribute_name = @attrName
            LEFT JOIN AttributeDate at_date ON g.game_id = at_date.game_id 
                AND at_date.attribute_name = @attrName
            WHERE 
                at_text.attribute_value != @attrValue OR 
                CAST(at_num.attribute_value AS TEXT) != @attrValue OR 
                CAST(at_bool.attribute_value AS TEXT) != @attrValue OR 
                CAST(at_date.attribute_value AS TEXT) != @attrValue
            ORDER BY g.title";

        string sqlQueryGreaterThan = @"
            SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id 
                AND at_num.attribute_name = @attrName
            WHERE 
                at_num.attribute_value > CAST(@attrValue AS DECIMAL)
            ORDER BY g.title";

        string sqlQueryLessThan = @"
            SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id 
                AND at_num.attribute_name = @attrName
            WHERE 
                at_num.attribute_value > CAST(@attrValue AS DECIMAL)
            ORDER BY g.title";

        string sqlQueryContains = @"SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            INNER JOIN AttributeText at_text ON g.game_id = at_text.game_id 
                AND at_text.attribute_name = @attrName
            WHERE 
                at_text.attribute_value LIKE '%' || @attrValue || '%'
            ORDER BY g.title";

        string sqlQueryIn = @"SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            INNER JOIN AttributeText at_text ON g.game_id = at_text.game_id 
                AND at_text.attribute_name = @attrName
            WHERE 
                at_text.attribute_value = ANY(STRING_TO_ARRAY(@attrValue, ','))
            ORDER BY g.title";
        
        string sqlQueryBetween = @"SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id 
                AND at_num.attribute_name = @attrName
            WHERE 
                at_num.attribute_value BETWEEN CAST(@attrValue AS DECIMAL) AND CAST(@attrValue2 AS DECIMAL)
            ORDER BY g.title";

        string sqlQueryBefore = @"SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            INNER JOIN AttributeDate at_date ON g.game_id = at_date.game_id 
                AND at_date.attribute_name = @attrName
            WHERE 
                at_date.attribute_value < CAST (@attrValue AS DATE)
            ORDER BY g.title";

        string sqlQueryAfter = @"SELECT DISTINCT 
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            FROM Game g
            INNER JOIN AttributeDate at_date ON g.game_id = at_date.game_id 
                AND at_date.attribute_name = @attrName
            WHERE 
                at_date.attribute_value > CAST (@attrValue AS DATE)
            ORDER BY g.title";


        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            string sqlQuery;
            string symbolForOutput;

            if (searchType == "1")
                {
                    sqlQuery = sqlQueryEquals;
                    symbolForOutput = "=";
                }
            else if (searchType == "2")
                {
                    sqlQuery = sqlQueryNotEquals;
                    symbolForOutput = "!=";
                }
            else if (searchType == "3")
                {
                    sqlQuery = sqlQueryGreaterThan;
                    symbolForOutput = ">";
                }
            else if (searchType == "4")
            {
                    sqlQuery = sqlQueryLessThan;
                    symbolForOutput = "<";
                }
            else if (searchType == "5")
            {
                sqlQuery = sqlQueryBetween;
                symbolForOutput = " - ";
            }
            else if (searchType == "6")
            {
                sqlQuery = sqlQueryContains;
                symbolForOutput = " contains";
            }
            else if (searchType == "7")
            {
                sqlQuery = sqlQueryIn;
                symbolForOutput = " in ";
            }
            else if (searchType == "8")
            {
                sqlQuery = sqlQueryBefore;
                symbolForOutput = " before ";
            }
            else 
            {
                sqlQuery = sqlQueryAfter;
                symbolForOutput = " after ";
            }

            using (var cmd = new NpgsqlCommand(sqlQuery, conn))
            {
                cmd.Parameters.AddWithValue("@attrName", attributeName);
                cmd.Parameters.AddWithValue("@attrValue", attributeValue);
                cmd.Parameters.AddWithValue("@attrValue2", attributeValue2);

                using (var reader = cmd.ExecuteReader())
                {
                    DataTable dt = new DataTable();
                    dt.Load(reader);

                    if (dt.Rows.Count == 0)
                    {
                        if (searchType == "5")
                        {
                            Console.WriteLine($"\nИгр с атрибутом '{attributeName}' '{attributeValue}' '{symbolForOutput}' '{attributeValue2}' не найдено.");
                            return;
                        }
                        else
                        {
                            Console.WriteLine($"\nИгр с атрибутом '{attributeName}' '{symbolForOutput}' '{attributeValue}' не найдено.");
                            return;
                        }
                    }

                    Console.WriteLine($"\nНайдено игр: {dt.Rows.Count}");
                    Console.WriteLine(new string('═', 100));
                    Console.WriteLine($"{"ID",-5} {"Название",-35} {"Дата",-12} {"Разработчик",-20} {"Цена",-8}");
                    Console.WriteLine(new string('═', 100));

                    foreach (DataRow row in dt.Rows)
                    {
                        string title = row["title"].ToString();
                        string developer = row["developer"].ToString();

                        Console.WriteLine(
                            $"{row["game_id"],-5} " +
                            $"{title.Substring(0, Math.Min(33, title.Length)),-35} " +
                            $"{row["release_date"],-12} " +
                            $"{developer.Substring(0, Math.Min(18, developer.Length)),-20} " +
                            $"${row["base_price"],-8}"
                        );
                    }

                    Console.WriteLine(new string('═', 100));
                }
            }
        }
    }
}
