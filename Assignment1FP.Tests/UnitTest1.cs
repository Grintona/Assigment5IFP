using System.Collections.Immutable;
using StudyResourceFinder.Models;
using StudyResourceFinder.Services;
using Xunit;

namespace StudyResourceFinder.Tests;

public class PipelineTests
{
    private static Book MakeBook(string title, int? year, int editions, string key) =>
        new(title, ImmutableArray.Create("Author " + title), year, editions, key, ImmutableArray.Create("eng"));

    private static List<Book> CreateBooks() => new()
    {
        MakeBook("Old Book",      1999, 5,  "/old"),
        MakeBook("Boundary Book", 2000, 10, "/boundary"),
        MakeBook("Popular Book",  2010, 20, "/popular"),
        MakeBook("Mid Book",      2015, 8,  "/mid"),
        MakeBook("Recent Book",   2022, 2,  "/recent"),
        MakeBook("Unknown Year",  null, 7,  "/unknown"),
    };

    private static string[] Titles(IEnumerable<Recommendation> items) =>
        items.Select(r => r.Book.Title).ToArray();

    // 1. NORMAL WORKFLOW
    [Fact]
    public void NormalWorkflow_ProducesDocumentedResult()
    {
        var result = BookPipeline.FindResources(CreateBooks(), 2000, Policies.Popularity);

        Assert.Equal(
            new[] { "Popular Book", "Boundary Book", "Mid Book", "Recent Book" },
            Titles(result));
        Assert.Equal(new[] { 20.0, 10.0, 8.0, 2.0 }, result.Select(r => r.Score).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(r => r.Rank).ToArray());
    }

    // 2. EMPTY DATASET
    [Fact]
    public void EmptyDataset_ReturnsEmptyResult()
    {
        var (valid, errors) = BookParser.ParseAll(new List<BookDto>());

        Assert.Empty(errors);   
        Assert.Empty(BookPipeline.FindResources(valid, 2000, Policies.Popularity));
    }

    // 3. BOUNDARY VALUE
    [Fact]
    public void BoundaryYear_IsIncluded_AndOneYearEarlierIsExcluded()
    {
        var result = BookPipeline.FilterBooks(CreateBooks(), 2000);

        Assert.Contains(result, b => b.Title == "Boundary Book");      
        Assert.DoesNotContain(result, b => b.Title == "Old Book");    
        Assert.DoesNotContain(result, b => b.Title == "Unknown Year"); 
    }

    // 4a. INVALID INPUT: ввод пользователя
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    public void InvalidYear_IsRejected(string? input)
    {
        bool ok = BookParser.TryParseMinYear(input, out _, out string error);

        Assert.False(ok);
        Assert.NotEmpty(error);
    }

    // 4b. MISSING INPUT: записи с отсутствующими полями и null
    [Fact]
    public void MissingRequiredFields_AreRejected_NoFabricatedResult()
    {
        var cases = new BookDto?[]
        {
            null,
            new() { Title = null, Key = "/k", EditionCount = 1 },
            new() { Title = "T",  Key = null, EditionCount = 1 },
            new() { Title = "T",  Key = "/k", EditionCount = null },
        };

        foreach (var dto in cases)
        {
            Assert.False(BookParser.TryParseBook(dto, out var book, out string error));
            Assert.Null(book);
            Assert.NotEmpty(error);
        }
        Assert.Throws<ArgumentNullException>(() => BookPipeline.FilterBooks(null!, 2000));
        Assert.Throws<ArgumentNullException>(() => BookPipeline.ScoreBooks(CreateBooks(), null!));
    }

    // 5. TWO POLICIES
    [Fact]
    public void TwoPolicies_DifferOnSameInput()
    {
        var books = CreateBooks();

        var popular = BookPipeline.FindResources(books, 2000, Policies.Popularity);
        var recent = BookPipeline.FindResources(books, 2000, Policies.Recency);

        Assert.Equal(
            new[] { "Popular Book", "Boundary Book", "Mid Book", "Recent Book" }, Titles(popular));
        Assert.Equal(
            new[] { "Recent Book", "Mid Book", "Popular Book", "Boundary Book" }, Titles(recent));
    }

    // 6. PURITY AND STAGES
    [Fact]
    public void Stages_DoNotModifyCallerData()
    {
        var books = CreateBooks();
        var booksBefore = books.ToList();
        var authorsBefore = books.Select(b => b.Authors.ToArray()).ToList();

        var filtered = BookPipeline.FilterBooks(books, 2000);      
        var filteredBefore = filtered.ToList();
        var scored = BookPipeline.ScoreBooks(filtered, Policies.Popularity); 
        var scoredBefore = scored.ToList();
        BookPipeline.RankBooks(scored);                            

        Assert.Equal(booksBefore, books);
        Assert.Equal(filteredBefore, filtered);
        Assert.Equal(scoredBefore, scored);
        for (int i = 0; i < books.Count; i++)
        {
            Assert.Equal(authorsBefore[i], books[i].Authors.ToArray());
        }
        Assert.Equal(20.0, scored.Single(s => s.Book.Title == "Popular Book").Score);
    }
}