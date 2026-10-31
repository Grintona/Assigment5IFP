using System;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public sealed record ScoringWeights(double EditionWeight, double RecencyWeight, int ReferenceYear = 2026);

public static class CurriedScoring
{
    /// <summary>
    /// Curried function: Configuration -> (DomainItem -> Score).
    /// Takes configuration first, returns a function waiting for a Book to produce its score.
    /// </summary>
    public static Func<ScoringWeights, Func<Book, double>> CreateScore =>
        weights =>
        {
            ArgumentNullException.ThrowIfNull(weights);

            return book =>
            {
                ArgumentNullException.ThrowIfNull(book);

                int age = book.FirstPublishYear.HasValue
                    ? Math.Max(0, weights.ReferenceYear - book.FirstPublishYear.Value)
                    : 50;

                double recencyFactor = 100.0 / (age + 1.0);
                return (book.EditionCount * weights.EditionWeight) + (recencyFactor * weights.RecencyWeight);
            };
        };

    /// <summary>
    /// Configured scoring function 1: high edition weight (popularity biased).
    /// Produced via partial application of CreateScore with fixed weights.
    /// </summary>
    public static readonly Func<Book, double> PopularityBiasedScorer =
        CreateScore(new ScoringWeights(EditionWeight: 1.5, RecencyWeight: 0.2));

    /// <summary>
    /// Configured scoring function 2: high recency weight (recency biased).
    /// Produced via partial application of CreateScore with fixed weights.
    /// </summary>
    public static readonly Func<Book, double> RecencyBiasedScorer =
        CreateScore(new ScoringWeights(EditionWeight: 0.1, RecencyWeight: 2.5));
}
