using System.Collections.Immutable;
using StudyResourceFinder.Models;
namespace StudyResourceFinder.Services;

public static class BookPipeline
{
    public static Result<Book, DomainError> FindByKey(IEnumerable<Book>? books, string? key)
    {
        if (books is null)
            return Result<Book, DomainError>.Fail(new("MissingBooks", "Books are required for lookup."));
        if (string.IsNullOrWhiteSpace(key))
            return Result<Book, DomainError>.Fail(new("MissingLookupKey", "A lookup key is required."));

        var book = books.SingleOrDefault(candidate => candidate.Key == key);
        return book is null
            ? Result<Book, DomainError>.Fail(new("BookNotFound", $"No book was found for key '{key}'."))
            : Result<Book, DomainError>.Ok(book);
    }

    public static Result<IReadOnlyList<Recommendation>, DomainError> FindResourcesResult(
        IEnumerable<Book>? books, int minimumYear, Func<Book, double>? scoringPolicy)
    {
        return ValidatedSearch.CreateResult(books, minimumYear, scoringPolicy)
            .Bind(search => FilterBooksResult(search))
            .Bind(filtered => ScoreBooksResult(filtered, scoringPolicy!))
            .Map(RankBooks);
    }

    private static Result<IReadOnlyList<Book>, DomainError> FilterBooksResult(ValidatedSearch search)
    {
        var filtered = FilterBooks(search.Books, search.MinimumYear);
        return Result<IReadOnlyList<Book>, DomainError>.Ok(filtered);
    }

    public static Result<IReadOnlyList<ScoredBook>, DomainError> ScoreBooksResult(
        IEnumerable<Book> books, Func<Book, double> scoringPolicy)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(scoringPolicy);

        var scored = ImmutableArray.CreateBuilder<ScoredBook>();
        foreach (var book in books)
        {
            double score;
            try
            {
                score = scoringPolicy(book);
            }
            catch (InvalidOperationException ex)
            {
                return Result<IReadOnlyList<ScoredBook>, DomainError>.Fail(
                    new("ScoringRejected", $"The score for '{book.Title}' could not be calculated: {ex.Message}"));
            }

            if (double.IsNaN(score) || double.IsInfinity(score))
            {
                return Result<IReadOnlyList<ScoredBook>, DomainError>.Fail(
                    new("InvalidScore", $"The score for '{book.Title}' must be finite."));
            }

            scored.Add(new ScoredBook(book, score));
        }

        return Result<IReadOnlyList<ScoredBook>, DomainError>.Ok(scored.ToImmutable());
    }

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

    public static IReadOnlyList<Recommendation> FindResources(ValidatedSearch search)
    {
        ArgumentNullException.ThrowIfNull(search);
        return FindResources(search.Books, search.MinimumYear, search.ScoringPolicy);
    }
}