using System;
using System.Collections.Generic;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public static class DeferredExecution
{
    /// <summary>
    /// Evaluates scores lazily using deferred execution (yield return).
    /// Processing is postponed until elements are iterated.
    /// </summary>
    public static IEnumerable<ScoredBook> StreamScoreBooks(
        IEnumerable<Book> books,
        Func<Book, double> scoringPolicy)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(scoringPolicy);

        foreach (var book in books)
        {
            double score = scoringPolicy(book);
            yield return new ScoredBook(book, score);
        }
    }
}
