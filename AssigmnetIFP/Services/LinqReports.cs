using System;
using System.Collections.Generic;
using System.Linq;
using StudyResourceFinder.Models;
using StudyResourceFinder.Native;

namespace StudyResourceFinder.Services;

public sealed record AuthorReportItem(
    string Author,
    int BookCount,
    int TotalEditions,
    int EarliestYear,
    int LatestYear);

public sealed record BookPriorityReportItem(
    int Rank,
    string Title,
    string PrimaryAuthor,
    int PublishYear,
    int Decade,
    double CurriedScore,
    ulong NativePriority);

public sealed record DecadeSummary(
    int Decade,
    int BookCount,
    double AverageNativePriority);

public static class LinqReports
{
    /// <summary>
    /// Report 1: Author Publishing & Edition Aggregation Report.
    /// Purpose: Analyzes author productivity and total edition reach.
    /// Filters: Books with first publish year >= minYear, filtered using custom extension WithMinimumEditions.
    /// Query stages:
    /// 1. WithMinimumEditions (custom extension)
    /// 2. Where (year filter and non-empty authors)
    /// 3. SelectMany (flatten authors to author-book pairs)
    /// 4. GroupBy (group by author name)
    /// 5. Select (aggregate Count, Sum of editions, Min year, Max year)
    /// 6. OrderByDescending (primary: TotalEditions)
    /// 7. ThenBy (tie-breaker: Author name ordinal)
    /// </summary>
    public static IReadOnlyList<AuthorReportItem> GenerateAuthorReport(
        IEnumerable<Book> books,
        int minimumYear,
        int minEditions = 0)
    {
        ArgumentNullException.ThrowIfNull(books);

        return books
            .WithMinimumEditions(minEditions)
            .Where(b => b.FirstPublishYear is int y && y >= minimumYear && !b.Authors.IsDefaultOrEmpty)
            .SelectMany(b => b.Authors, (b, a) => new { Author = a, Book = b })
            .GroupBy(x => x.Author)
            .Select(g => new AuthorReportItem(
                Author: g.Key,
                BookCount: g.Count(),
                TotalEditions: g.Sum(x => x.Book.EditionCount),
                EarliestYear: g.Min(x => x.Book.FirstPublishYear!.Value),
                LatestYear: g.Max(x => x.Book.FirstPublishYear!.Value)))
            .OrderByDescending(r => r.TotalEditions)
            .ThenBy(r => r.Author, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Report 2: Curried & Native Priority Ranking Report.
    /// Purpose: Evaluates candidate study books combining a curried functional score
    /// and a low-level native assembly priority score, with decade-level aggregation.
    /// Filters: Books with first publish year >= minYear.
    /// Query stages:
    /// 1. Where (year filter)
    /// 2. Select (calculates age via Book.GetAge(), curried score, and calls NativeAdapter.CalculatePrioritySafe)
    /// 3. OrderByDescending (primary: CurriedScore)
    /// 4. ThenBy (tie-breaker: Title ordinal)
    /// 5. Select with index (assigns Rank)
    /// </summary>
    public static (IReadOnlyList<BookPriorityReportItem> Items, IReadOnlyList<DecadeSummary> DecadeSummaries) GeneratePriorityReport(
        IEnumerable<Book> books,
        int minimumYear,
        Func<Book, double> curriedScorer,
        int referenceYear = 2026)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(curriedScorer);

        var evaluated = books
            .Where(b => b.FirstPublishYear is int y && y >= minimumYear)
            .Select(b =>
            {
                int age = b.GetAge(referenceYear);
                // Call real native assembly library through adapter
                // ratingsCount estimated as editions * 5
                int ratingsCount = Math.Max(0, b.EditionCount * 5);
                var nativeResult = NativeAdapter.CalculatePrioritySafe(b.EditionCount, ratingsCount, age);
                ulong nativeScore = nativeResult is Result<ulong, string>.Success success ? success.Value : 0UL;
                double curriedScore = curriedScorer(b);
                int year = b.FirstPublishYear!.Value;
                int decade = (year / 10) * 10;
                string primaryAuthor = b.Authors.IsDefaultOrEmpty ? "Unknown" : b.Authors[0];

                return new
                {
                    Book = b,
                    PrimaryAuthor = primaryAuthor,
                    Year = year,
                    Decade = decade,
                    CurriedScore = curriedScore,
                    NativePriority = nativeScore
                };
            })
            .OrderByDescending(x => x.CurriedScore)
            .ThenBy(x => x.Book.Title, StringComparer.Ordinal)
            .Select((x, index) => new BookPriorityReportItem(
                Rank: index + 1,
                Title: x.Book.Title,
                PrimaryAuthor: x.PrimaryAuthor,
                PublishYear: x.Year,
                Decade: x.Decade,
                CurriedScore: x.CurriedScore,
                NativePriority: x.NativePriority))
            .ToList();

        var decadeSummaries = evaluated
            .GroupBy(item => item.Decade)
            .Select(g => new DecadeSummary(
                Decade: g.Key,
                BookCount: g.Count(),
                AverageNativePriority: g.Count() > 0 ? g.Average(item => (double)item.NativePriority) : 0.0))
            .OrderBy(d => d.Decade)
            .ToList();

        return (evaluated, decadeSummaries);
    }
}
