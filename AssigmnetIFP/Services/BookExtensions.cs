using System;
using System.Collections.Generic;
using System.Linq;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public static class BookExtensions
{

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

    public static IEnumerable<Book> WithMinimumEditions(this IEnumerable<Book> books, int minEditions)
    {
        ArgumentNullException.ThrowIfNull(books);

        int threshold = minEditions < 0 ? 0 : minEditions;
        return books.Where(b => b.EditionCount >= threshold);
    }
}
