using System.Collections.Immutable;
using StudyResourceFinder.Models;
namespace StudyResourceFinder.Services;

public static class BookParser
{
    public static Result<Book, DomainError> ParseBook(BookDto? dto)
    {
        if (dto is null)
            return Result<Book, DomainError>.Fail(new("MissingRecord", "The book record is missing."));
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<Book, DomainError>.Fail(new("MissingTitle", "Title is required."));
        if (string.IsNullOrWhiteSpace(dto.Key))
            return Result<Book, DomainError>.Fail(new("MissingKey", $"Key is required for '{dto.Title}'."));
        if (dto.EditionCount is null)
            return Result<Book, DomainError>.Fail(new("MissingEditionCount", $"edition_count is required for '{dto.Title}'."));
        if (dto.EditionCount < 0)
            return Result<Book, DomainError>.Fail(new("NegativeEditionCount", $"edition_count cannot be negative for '{dto.Title}'."));
        if (dto.FirstPublishYear is < 0)
            return Result<Book, DomainError>.Fail(new("NegativePublishYear", $"first_publish_year cannot be negative for '{dto.Title}'."));

        return Result<Book, DomainError>.Ok(new Book(
            dto.Title,
            (dto.Authors ?? new()).ToImmutableArray(),
            dto.FirstPublishYear,
            dto.EditionCount.Value,
            dto.Key,
            (dto.Languages ?? new()).ToImmutableArray()));
    }

    public static Result<ImmutableArray<Book>, DomainError> ParseAllResult(IEnumerable<BookDto?>? dtos)
    {
        if (dtos is null)
            return Result<ImmutableArray<Book>, DomainError>.Fail(new("MissingRecords", "Book records are required."));

        var valid = ImmutableArray.CreateBuilder<Book>();
        foreach (var dto in dtos)
        {
            var parsed = ParseBook(dto);
            if (parsed is Result<Book, DomainError>.Failure failure)
                return Result<ImmutableArray<Book>, DomainError>.Fail(failure.Error);
            valid.Add(((Result<Book, DomainError>.Success)parsed).Value);
        }

        return Result<ImmutableArray<Book>, DomainError>.Ok(valid.ToImmutable());
    }

    public static bool TryParseBook(BookDto? dto, out Book? book, out string error)
    {
        book = null;
        if (dto is null) { error = "Record is missing."; return false; }
        if (string.IsNullOrWhiteSpace(dto.Title)) { error = "Title is missing."; return false; }
        if (string.IsNullOrWhiteSpace(dto.Key)) { error = $"Key is missing for '{dto.Title}'."; return false; }
        if (dto.EditionCount is null) { error = $"edition_count is missing for '{dto.Title}'."; return false; }
        if (dto.EditionCount < 0) { error = $"edition_count is negative for '{dto.Title}'."; return false; }
        if (dto.FirstPublishYear is < 0) { error = $"first_publish_year is negative for '{dto.Title}'."; return false; }

        book = ((Result<Book, DomainError>.Success)ParseBook(dto)).Value;
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