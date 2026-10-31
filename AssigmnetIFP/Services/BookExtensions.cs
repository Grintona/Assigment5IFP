using System;
using System.Collections.Generic;
using System.Linq;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public static class BookExtensions
{
    /// <summary>
    /// Computes the age of the book relative to a reference year.
    /// Returns 0 if the book has no publication year or if the age is negative.
    /// Belongs on Book because age is an intrinsic domain property derived from a book's publication year.
    /// </summary>
    public static int GetAge(this Book book, int referenceYear = 2026)
    {
        ArgumentNullException.ThrowIfNull(book);

        if (!book.FirstPublishYear.HasValue)
        {
            return 0;
        }

        int age = referenceYear - book.FirstPublishYear.Value;
        return age > 0 ? age : 0;
    }

    /// <summary>
    /// Reusable query pipeline operator that filters books with at least minEditions.
    /// Belongs on IEnumerable&lt;Book&gt; as an idiomatic sequence filter in book query pipelines.
    /// </summary>
    public static IEnumerable<Book> WithMinimumEditions(this IEnumerable<Book> books, int minEditions)
    {
        ArgumentNullException.ThrowIfNull(books);

        int threshold = minEditions < 0 ? 0 : minEditions;
        return books.Where(b => b.EditionCount >= threshold);
    }
}
