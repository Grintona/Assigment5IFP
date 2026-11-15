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
