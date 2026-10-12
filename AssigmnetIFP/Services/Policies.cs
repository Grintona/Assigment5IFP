using StudyResourceFinder.Models;
namespace StudyResourceFinder.Services;

public static class Policies
{
    public static double Popularity(Book book) => book.EditionCount;

    public static double Recency(Book book) => book.FirstPublishYear ?? throw new InvalidOperationException("Recency policy requires a publication year.");
}