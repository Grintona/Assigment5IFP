using System.Collections.Immutable;
using StudyResourceFinder.Models;
namespace StudyResourceFinder.Services;

public static class BookParser
{
    public static bool TryParseBook(BookDto? dto, out Book? book, out string error)
    {
        book = null;
        if (dto is null) { error = "Record is missing."; return false; }
        if (string.IsNullOrWhiteSpace(dto.Title)) { error = "Title is missing."; return false; }
        if (string.IsNullOrWhiteSpace(dto.Key)) { error = $"Key is missing for '{dto.Title}'."; return false; }
        if (dto.EditionCount is null) { error = $"edition_count is missing for '{dto.Title}'."; return false; }
        if (dto.EditionCount < 0) { error = $"edition_count is negative for '{dto.Title}'."; return false; }
        if (dto.FirstPublishYear is < 0) { error = $"first_publish_year is negative for '{dto.Title}'."; return false; }

        book = new Book(
            dto.Title, 
            (dto.Authors ?? new()).ToImmutableArray(), 
            dto.FirstPublishYear,
            dto.EditionCount.Value,
            dto.Key,
            (dto.Languages ?? new()).ToImmutableArray());
        error = "";
        return true;
    }


    public static (ImmutableArray<Book> Valid, ImmutableArray<string> Errors) ParseAll(IEnumerable<BookDto>? dtos)
    {
        ArgumentNullException.ThrowIfNull(dtos);
        var valid = ImmutableArray.CreateBuilder<Book>();
        var errors = ImmutableArray.CreateBuilder<string>();
        foreach (var dto in dtos)
        {
            if (TryParseBook(dto, out var book, out var err) && book is not null) valid.Add(book);
            else errors.Add(err);
        }
        return (valid.ToImmutable(), errors.ToImmutable());
    }

    public static bool TryParseMinYear(string? input, out int year, out string error)
    {
        year = 0;
        if (string.IsNullOrWhiteSpace(input)) { error = "Year is missing."; return false; }
        if (!int.TryParse(input, out year)) { error = "Year must be an integer."; return false; }
        if (year < 0) { error = "Year cannot be negative."; return false; }
        error = "";
        return true;
    }
}