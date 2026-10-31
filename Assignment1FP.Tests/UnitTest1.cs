using System.Collections.Immutable;
using StudyResourceFinder.Models;
using StudyResourceFinder.Services;
using StudyResourceFinder.Native;
using Xunit;
using System.IO;

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

    // 4a. INVALID INPUT: user input
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

    // 4b. MISSING INPUT: records with missing fields and null
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

    [Fact]
    public void ValidatedSearchFactory_AcceptsValidInput()
    {
        var result = ValidatedSearch.Create(CreateBooks(), 2000, Policies.Popularity);

        var valid = Assert.IsType<ValidationResult<ValidatedSearch>.Valid>(result);
        Assert.Equal(2000, valid.Value.MinimumYear);
        Assert.Equal(6, valid.Value.Books.Length);
        Assert.Equal(Policies.Popularity, valid.Value.ScoringPolicy);
    }

    [Fact]
    public void ValidatedSearchFactory_RejectsNegativeYear()
    {
        var result = ValidatedSearch.Create(CreateBooks(), -1, Policies.Popularity);

        var invalid = Assert.IsType<ValidationResult<ValidatedSearch>.Invalid>(result);
        Assert.Contains("Minimum year cannot be negative.", invalid.Reasons);
    }

    [Fact]
    public void ValidatedSearchFactory_RejectsMissingBooksAndPolicy()
    {
        var result = ValidatedSearch.Create(null, 2000, null);

        var invalid = Assert.IsType<ValidationResult<ValidatedSearch>.Invalid>(result);
        Assert.Contains("Books are required.", invalid.Reasons);
        Assert.Contains("A scoring policy is required.", invalid.Reasons);
    }

    [Fact]
    public void ValidatedSearchFactory_RejectsInvalidBookValues()
    {
        var invalidBook = new Book(
            " ",
            ImmutableArray<string>.Empty,
            -1,
            -1,
            " ",
            ImmutableArray<string>.Empty);

        var result = ValidatedSearch.Create(
            new[] { invalidBook }, 2000, Policies.Popularity);

        var invalid = Assert.IsType<ValidationResult<ValidatedSearch>.Invalid>(result);
        Assert.Contains("Title is missing for book at index 0.", invalid.Reasons);
        Assert.Contains("Key is missing for book at index 0.", invalid.Reasons);
        Assert.Contains("Edition count cannot be negative for book at index 0.", invalid.Reasons);
        Assert.Contains("Publication year cannot be negative for book at index 0.", invalid.Reasons);
    }

    [Fact]
    public void WorkflowCases_AreReachableAndPatternMatched()
    {
        var creation = ValidatedSearch.Create(CreateBooks(), 2000, Policies.Popularity);
        var valid = Assert.IsType<ValidationResult<ValidatedSearch>.Valid>(creation);
        var pending = new BookWorkflowState.Pending(valid.Value);

        var transition = Assert.IsType<ValidationResult<BookWorkflowState>.Valid>(
            BookWorkflow.Transition(pending));
        var completed = Assert.IsType<BookWorkflowState.Completed>(transition.Value);
        Assert.Equal("Popular Book", completed.Recommendations[0].Book.Title);

        var rejected = new BookWorkflowState.Rejected("missing validation");
        var rejectedResult = Assert.IsType<ValidationResult<BookWorkflowState>.Invalid>(
            BookWorkflow.Transition(rejected));
        Assert.Contains("missing validation", rejectedResult.Reasons[0]);
    }

    [Fact]
    public void RepeatedWorkflowOperation_IsRejected()
    {
        var creation = Assert.IsType<ValidationResult<ValidatedSearch>.Valid>(
            ValidatedSearch.Create(CreateBooks(), 2000, Policies.Popularity));
        var completed = Assert.IsType<ValidationResult<BookWorkflowState>.Valid>(
            BookWorkflow.Transition(new BookWorkflowState.Pending(creation.Value)));

        var repeated = Assert.IsType<ValidationResult<BookWorkflowState>.Invalid>(
            BookWorkflow.Transition(completed.Value));
        Assert.Contains("already been completed", repeated.Reasons[0]);
    }

    [Fact]
    public void FailedTransition_PreservesOriginalState()
    {
        var creation = Assert.IsType<ValidationResult<ValidatedSearch>.Valid>(
            ValidatedSearch.Create(CreateBooks(), 2000, Policies.Popularity));
        var pending = new BookWorkflowState.Pending(creation.Value);

        var failed = BookWorkflow.Transition(new BookWorkflowState.Rejected("invalid sequence"));

        Assert.IsType<ValidationResult<BookWorkflowState>.Invalid>(failed);
        Assert.Equal(creation.Value, pending.Search);
        Assert.Equal(2000, pending.Search.MinimumYear);
    }

    [Fact]
    public void Result_MapTransformsSuccess()
    {
        var result = Result<int, DomainError>.Ok(4).Map(value => value * 2);

        var success = Assert.IsType<Result<int, DomainError>.Success>(result);
        Assert.Equal(8, success.Value);
    }

    [Fact]
    public void Result_BindContinuesWithResultOperation()
    {
        var result = Result<int, DomainError>.Ok(4)
            .Bind(value => Result<string, DomainError>.Ok($"book-{value}"));

        Assert.Equal("book-4", Assert.IsType<Result<string, DomainError>.Success>(result).Value);
    }

    [Fact]
    public void Result_FailureSkipsMapAndBind()
    {
        var calls = 0;
        var failure = Result<int, DomainError>.Fail(new("Invalid", "invalid value"));

        var mapped = failure.Map(value => { calls++; return value + 1; });
        var bound = failure.Bind(value =>
        {
            calls++;
            return Result<string, DomainError>.Ok(value.ToString());
        });

        Assert.Equal(0, calls);
        Assert.IsType<Result<int, DomainError>.Failure>(mapped);
        Assert.IsType<Result<string, DomainError>.Failure>(bound);
    }

    [Fact]
    public void ResultChain_ProducesCompleteRecommendation()
    {
        var result = BookPipeline.FindResourcesResult(CreateBooks(), 2000, Policies.Popularity);

        var success = Assert.IsType<Result<IReadOnlyList<Recommendation>, DomainError>.Success>(result);
        Assert.Equal("Popular Book", success.Value[0].Book.Title);
        Assert.Equal(20, success.Value[0].Score);
    }

    [Fact]
    public void ResultChain_ReportsThreeDistinctDomainFailures()
    {
        var missingBooks = Assert.IsType<Result<IReadOnlyList<Recommendation>, DomainError>.Failure>(
            BookPipeline.FindResourcesResult(null, 2000, Policies.Popularity));
        var invalidYear = Assert.IsType<Result<IReadOnlyList<Recommendation>, DomainError>.Failure>(
            BookPipeline.FindResourcesResult(CreateBooks(), -1, Policies.Popularity));
        var missingPolicy = Assert.IsType<Result<IReadOnlyList<Recommendation>, DomainError>.Failure>(
            BookPipeline.FindResourcesResult(CreateBooks(), 2000, null));

        Assert.Equal("MissingBooks", missingBooks.Error.Code);
        Assert.Equal("InvalidMinimumYear", invalidYear.Error.Code);
        Assert.Equal("MissingPolicy", missingPolicy.Error.Code);
    }

    [Fact]
    public void ResultChain_RejectsUnusableScoreAndSkipsRanking()
    {
        var rankingCalls = 0;
        var result = ValidatedSearch.CreateResult(CreateBooks(), 2000, _ => double.NaN)
            .Bind(search => BookPipeline.ScoreBooksResult(
                BookPipeline.FilterBooks(search.Books, search.MinimumYear), search.ScoringPolicy))
            .Map(scored =>
            {
                rankingCalls++;
                return BookPipeline.RankBooks(scored);
            });

        var failure = Assert.IsType<Result<IReadOnlyList<Recommendation>, DomainError>.Failure>(result);
        Assert.Equal("InvalidScore", failure.Error.Code);
        Assert.Equal(0, rankingCalls);
    }

    [Fact]
    public void Lookup_PresentAndAbsentAreExplicit()
    {
        var present = BookPipeline.FindByKey(CreateBooks(), "/boundary");
        var absent = BookPipeline.FindByKey(CreateBooks(), "/does-not-exist");

        var zero = MakeBook("Zero editions", 2020, 0, "/zero");
        var zeroResult = BookPipeline.FindByKey(new[] { zero }, "/zero");

        Assert.Equal("Boundary Book", Assert.IsType<Result<Book, DomainError>.Success>(present).Value.Title);
        Assert.Equal("BookNotFound", Assert.IsType<Result<Book, DomainError>.Failure>(absent).Error.Code);
        Assert.Equal(0, Assert.IsType<Result<Book, DomainError>.Success>(zeroResult).Value.EditionCount);
    }

    [Fact]
    public async Task MissingFile_ReturnsOperationalFailureWithoutStateChange()
    {
        var result = await OpenLibraryClient.LoadFromFileResult(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));

        var failure = Assert.IsType<Result<IReadOnlyList<BookDto>, DomainError>.Failure>(result);
        Assert.Equal("FileNotFound", failure.Error.Code);
    }

    [Fact]
    public async Task MalformedFile_ReturnsFailureWithoutPartialUpdate()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        try
        {
            var result = await OpenLibraryClient.LoadFromFileResult(path);

            var failure = Assert.IsType<Result<IReadOnlyList<BookDto>, DomainError>.Failure>(result);
            Assert.Equal("MalformedJson", failure.Error.Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingRequiredDto_ReturnsStructuredFailure()
    {
        var result = BookParser.ParseBook(new BookDto { Title = "Book", Key = "/book" });

        var failure = Assert.IsType<Result<Book, DomainError>.Failure>(result);
        Assert.Equal("MissingEditionCount", failure.Error.Code);
    }

    [Fact]
    public void RepeatedWorkflow_ReturnsStateFailureAndKeepsCompletedState()
    {
        var completed = new BookWorkflowState.Completed(
            BookPipeline.FindResources(CreateBooks(), 2000, Policies.Popularity));
        var result = BookWorkflow.TransitionResult(completed);

        var failure = Assert.IsType<Result<BookWorkflowState, DomainError>.Failure>(result);
        Assert.Equal("InvalidWorkflowState", failure.Error.Code);
        Assert.Equal(4, completed.Recommendations.Count);
    }
[Fact]
    public void CalculatePrioritySafe_ReturnsExpectedValue()
    {
        // (12 * 10) + (55 / 2) - 15 = 120 + 27 - 15 = 132
        var result = NativeAdapter.CalculatePrioritySafe(12, 55, 15);

        Assert.True(result.IsSuccess);
        Assert.IsType<Result<ulong, string>.Success>(result);
        
        var success = (Result<ulong, string>.Success)result;
        Assert.Equal(132UL, success.Value);
    }
}