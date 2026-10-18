using System.Collections.Immutable;
using StudyResourceFinder.Services;

namespace StudyResourceFinder.Models;

public sealed record ValidatedSearch
{
    public ImmutableArray<Book> Books { get; }
    public int MinimumYear { get; }
    public Func<Book, double> ScoringPolicy { get; }

    private ValidatedSearch(
        ImmutableArray<Book> books,
        int minimumYear,
        Func<Book, double> scoringPolicy)
    {
        Books = books;
        MinimumYear = minimumYear;
        ScoringPolicy = scoringPolicy;
    }

    public static ValidationResult<ValidatedSearch> Create(
        IEnumerable<Book>? books,
        int minimumYear,
        Func<Book, double>? scoringPolicy)
    {
        var reasons = ImmutableArray.CreateBuilder<string>();
        ImmutableArray<Book> materialized = ImmutableArray<Book>.Empty;

        if (books is null)
        {
            reasons.Add("Books are required.");
        }
        else
        {
            materialized = books.ToImmutableArray();
            for (int index = 0; index < materialized.Length; index++)
            {
                var book = materialized[index];
                if (book is null)
                {
                    reasons.Add($"Book at index {index} cannot be null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(book.Title))
                {
                    reasons.Add($"Title is missing for book at index {index}.");
                }

                if (string.IsNullOrWhiteSpace(book.Key))
                {
                    reasons.Add($"Key is missing for book at index {index}.");
                }

                if (book.EditionCount < 0)
                {
                    reasons.Add($"Edition count cannot be negative for book at index {index}.");
                }

                if (book.FirstPublishYear is < 0)
                {
                    reasons.Add($"Publication year cannot be negative for book at index {index}.");
                }
            }
        }

        if (minimumYear < 0)
        {
            reasons.Add("Minimum year cannot be negative.");
        }

        if (scoringPolicy is null)
        {
            reasons.Add("A scoring policy is required.");
        }

        return reasons.Count > 0
            ? new ValidationResult<ValidatedSearch>.Invalid(reasons.ToImmutable())
            : new ValidationResult<ValidatedSearch>.Valid(
                new ValidatedSearch(materialized, minimumYear, scoringPolicy!));
    }
}