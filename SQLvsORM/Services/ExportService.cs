using ClosedXML.Excel;
using SQLvsORM.Model;
using Microsoft.EntityFrameworkCore;

namespace SQLvsORM.Services
{
    
    public class ExportService
    {
        private readonly GameDbContext _context;

        public ExportService(GameDbContext context)
        {
            _context = context;
        }

        public void ExportToExcel()
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

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Games");

            int col = 1;
            worksheet.Cell(1, col++).Value = "ID";
            worksheet.Cell(1, col++).Value = "Title";
            worksheet.Cell(1, col++).Value = "Release Date";
            worksheet.Cell(1, col++).Value = "Developer";
            worksheet.Cell(1, col++).Value = "Publisher";
            worksheet.Cell(1, col++).Value = "Price";

            foreach (var attr in allTextAttrs) worksheet.Cell(1, col++).Value = attr;
            foreach (var attr in allNumberAttrs) worksheet.Cell(1, col++).Value = attr;
            foreach (var attr in allBoolAttrs) worksheet.Cell(1, col++).Value = attr;
            foreach (var attr in allDateAttrs) worksheet.Cell(1, col++).Value = attr;

            int row = 2;
            foreach (var game in games)
            {
                col = 1;
                worksheet.Cell(row, col++).Value = game.game_id;
                worksheet.Cell(row, col++).Value = game.title;
                worksheet.Cell(row, col++).Value = game.release_date?.ToString("yyyy-MM-dd");
                worksheet.Cell(row, col++).Value = game.developer;
                worksheet.Cell(row, col++).Value = game.publisher;
                worksheet.Cell(row, col++).Value = game.base_price;

                FillAttributeColumn(worksheet, row, ref col, allTextAttrs, attr => game.AttributeTexts?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value);
                FillAttributeColumn(worksheet, row, ref col, allNumberAttrs, attr => game.AttributeNumbers?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value.ToString());
                FillAttributeColumn(worksheet, row, ref col, allBoolAttrs, attr => game.AttributeBooleans?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value.ToString());
                FillAttributeColumn(worksheet, row, ref col, allDateAttrs, attr => game.AttributeDates?.FirstOrDefault(a => a.attribute_name == attr)?.attribute_value.ToString("yyyy-MM-dd"));
                row++;
            }

            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(1);

            string filename = $"games_export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            workbook.SaveAs(filename);
            Console.WriteLine($"Экспортировано {games.Count} игр в файл: {filename}");
        }

        private static List<string> GetDistinctAttributes(IEnumerable<string> attributes)
            => attributes.Where(a => a != null).Distinct().OrderBy(n => n).ToList();

        private static void FillAttributeColumn(IXLWorksheet worksheet, int row, ref int col, List<string> attributes, Func<string, string> valueGetter)
        {
            foreach (var attr in attributes)
                worksheet.Cell(row, col++).Value = valueGetter(attr) ?? "";
        }
    }
}
