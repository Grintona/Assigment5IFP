using StudyResourceFinder.Models;
namespace StudyResourceFinder.Services;

public static class PrintBooks
{
    public static void Print(string title, IReadOnlyList<Recommendation> items)
    {
        Console.WriteLine($"--- {title} ---");
        if (items.Count == 0)
        {
            Console.WriteLine("(no results)");
            return;
        }
        foreach (var r in items)
        {
            string authors = r.Book.Authors.IsDefaultOrEmpty
                ? "unknown"
                : string.Join(", ", r.Book.Authors);
            Console.WriteLine($"{r.Rank}. {r.Book.Title} | {authors} | year {r.Book.FirstPublishYear} | score {r.Score}");
        }
        Console.WriteLine();
    }
}