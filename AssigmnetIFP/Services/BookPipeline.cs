using System.Collections.Immutable;
using StudyResourceFinder.Models;
namespace StudyResourceFinder.Services;

public static class BookPipeline
{

    public static IReadOnlyList<Book> FilterBooks(IEnumerable<Book> books, int minimumYear)
    {
        ArgumentNullException.ThrowIfNull(books);
        return books.Where(b => b.FirstPublishYear is int y && y >= minimumYear).ToImmutableArray();
    }

    public static IReadOnlyList<ScoredBook> ScoreBooks(IEnumerable<Book> books, Func<Book, double> scoringPolicy)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(scoringPolicy);
        return books.Select(b => new ScoredBook(b, scoringPolicy(b))).ToImmutableArray();
    }

    public static IReadOnlyList<Recommendation> RankBooks(IEnumerable<ScoredBook> scored)
    {
        ArgumentNullException.ThrowIfNull(scored);
        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Book.Title, StringComparer.Ordinal)
            .Select((s, i) => new Recommendation(i + 1, s.Book, s.Score))
            .ToImmutableArray();
    }

    public static IReadOnlyList<Recommendation> FindResources(
        IEnumerable<Book> books, int minimumYear, Func<Book, double> scoringPolicy)
        => RankBooks(ScoreBooks(FilterBooks(books, minimumYear), scoringPolicy));
}