using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StudyResourceFinder.Models;
using StudyResourceFinder.Native;
using StudyResourceFinder.Services;
using Xunit;

namespace StudyResourceFinder.Tests;

public class Assignment8Tests
{
    private static Book MakeBook(string title, int? year, int editions, string key, string[]? authors = null) =>
        new(
            title,
            authors is null ? ImmutableArray.Create("Author " + title) : authors.ToImmutableArray(),
            year,
            editions,
            key,
            ImmutableArray.Create("eng"));

    private static List<Book> SampleBooks() => new()
    {
        MakeBook("Alpha Book", 2010, 20, "/alpha", new[] { "Author One", "Author Two" }),
        MakeBook("Beta Book",  2015, 10, "/beta",  new[] { "Author One" }),
        MakeBook("Gamma Book", 2020, 10, "/gamma", new[] { "Author Two" }),
        MakeBook("Delta Book", 1995, 5,  "/delta", new[] { "Author Three" }),
        MakeBook("No Year",    null, 8,  "/noyear",new[] { "Author Four" }),
    };

    // ==========================================
    // 1. EXTENSIONS
    // ==========================================

    [Fact]
    public void BookExtensions_GetAge_CalculatesCorrectAgeAndHandlesEdgeCases()
    {
        var book2010 = MakeBook("Book 2010", 2010, 5, "/k1");
        var bookFuture = MakeBook("Book Future", 2030, 5, "/k2");
        var bookNoYear = MakeBook("Book No Year", null, 5, "/k3");

        Assert.Equal(16, book2010.GetAge(2026));
        Assert.Equal(0, bookFuture.GetAge(2026));
        Assert.Equal(0, bookNoYear.GetAge(2026));
        Assert.Throws<ArgumentNullException>(() => ((Book)null!).GetAge(2026));
    }

    [Fact]
    public void BookExtensions_WithMinimumEditions_FiltersCorrectly()
    {
        var books = SampleBooks();

        var highEditions = books.WithMinimumEditions(15).ToList();
        Assert.Single(highEditions);
        Assert.Equal("Alpha Book", highEditions[0].Title);

        var allEditions = books.WithMinimumEditions(0).ToList();
        Assert.Equal(books.Count, allEditions.Count);

        var negativeTreatedAsZero = books.WithMinimumEditions(-5).ToList();
        Assert.Equal(books.Count, negativeTreatedAsZero.Count);

        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<Book>)null!).WithMinimumEditions(5));
    }

    // ==========================================
    // 2. LINQ REPORTS
    // ==========================================

    [Fact]
    public void AuthorReport_ComputesCorrectAggregatesAndDeterministicOrdering()
    {
        var books = SampleBooks();

        // minYear: 2000. Filtered: Alpha (2010, 20), Beta (2015, 10), Gamma (2020, 10).
        // Author One: Alpha (20) + Beta (10) = 30 editions, 2 books, min 2010, max 2015.
        // Author Two: Alpha (20) + Gamma (10) = 30 editions, 2 books, min 2010, max 2020.
        // Tied editions (30 vs 30): tie broken by Author ascending ("Author One" < "Author Two").
        var report = LinqReports.GenerateAuthorReport(books, 2000);

        Assert.Equal(2, report.Count);

        Assert.Equal("Author One", report[0].Author);
        Assert.Equal(2, report[0].BookCount);
        Assert.Equal(30, report[0].TotalEditions);
        Assert.Equal(2010, report[0].EarliestYear);
        Assert.Equal(2015, report[0].LatestYear);

        Assert.Equal("Author Two", report[1].Author);
        Assert.Equal(2, report[1].BookCount);
        Assert.Equal(30, report[1].TotalEditions);
        Assert.Equal(2010, report[1].EarliestYear);
        Assert.Equal(2020, report[1].LatestYear);
    }

    [Fact]
    public void AuthorReport_HandlesEmptyAndNoMatchingRecords()
    {
        var empty = LinqReports.GenerateAuthorReport(new List<Book>(), 2000);
        Assert.Empty(empty);

        var books = SampleBooks();
        var noMatches = LinqReports.GenerateAuthorReport(books, 3000);
        Assert.Empty(noMatches);

        Assert.Throws<ArgumentNullException>(() => LinqReports.GenerateAuthorReport(null!, 2000));
    }

    [Fact]
    public void PriorityReport_IntegratesCurriedScorerAndNativePriority()
    {
        var books = SampleBooks();

        var (items, decades) = LinqReports.GeneratePriorityReport(
            books, 2000, CurriedScoring.PopularityBiasedScorer, referenceYear: 2026);

        // Alpha Book: 20 editions, year 2010 (age = 16).
        // Native: (20 * 10) + (100 / 2) - 16 = 200 + 50 - 16 = 234.
        var alpha = items.Single(i => i.Title == "Alpha Book");
        Assert.Equal(234UL, alpha.NativePriority);
        Assert.Equal(2010, alpha.PublishYear);
        Assert.Equal(2010, alpha.Decade);

        // Check decades grouping
        Assert.Contains(decades, d => d.Decade == 2010);
        Assert.Contains(decades, d => d.Decade == 2020);
    }

    [Fact]
    public void PriorityReport_HandlesEmptyDataset_AndTiedScoresDeterministically()
    {
        var (emptyItems, emptyDecades) = LinqReports.GeneratePriorityReport(
            new List<Book>(), 2000, CurriedScoring.PopularityBiasedScorer);
        Assert.Empty(emptyItems);
        Assert.Empty(emptyDecades);

        // Tied scores: "Beta Book" and "Gamma Book" with identical score
        var tiedBooks = new List<Book>
        {
            MakeBook("Zeta Tied", 2015, 10, "/zeta"),
            MakeBook("Alpha Tied", 2015, 10, "/alpha_tied")
        };

        var (tiedReport, _) = LinqReports.GeneratePriorityReport(
            tiedBooks, 2000, _ => 50.0);

        Assert.Equal(2, tiedReport.Count);
        Assert.Equal("Alpha Tied", tiedReport[0].Title);
        Assert.Equal("Zeta Tied", tiedReport[1].Title);
        Assert.Equal(1, tiedReport[0].Rank);
        Assert.Equal(2, tiedReport[1].Rank);
    }

    // ==========================================
    // 3. CURRYING & CONFIGURED FUNCTIONS
    // ==========================================

    [Fact]
    public void CurriedScoring_TwoConfigurations_ProduceDifferentResultsOnSameInput()
    {
        var oldPopular = MakeBook("Old Classic", 1950, 100, "/classic");
        var recentNiche = MakeBook("Recent Tech", 2025, 2, "/tech");
        var testSet = new[] { oldPopular, recentNiche };

        var popScorer = CurriedScoring.PopularityBiasedScorer;
        var recScorer = CurriedScoring.RecencyBiasedScorer;

        double oldPopScore = popScorer(oldPopular);
        double recentPopScore = popScorer(recentNiche);

        double oldRecScore = recScorer(oldPopular);
        double recentRecScore = recScorer(recentNiche);

        // Under popularity bias: old classic with 100 editions scores higher
        Assert.True(oldPopScore > recentPopScore);

        // Under recency bias: recent tech with 2025 publication scores higher
        Assert.True(recentRecScore > oldRecScore);

        // Rank order is inverted on the exact same dataset
        var popRank = BookPipeline.FindResources(testSet, 1900, popScorer);
        var recRank = BookPipeline.FindResources(testSet, 1900, recScorer);

        Assert.Equal("Old Classic", popRank[0].Book.Title);
        Assert.Equal("Recent Tech", recRank[0].Book.Title);
    }

    // ==========================================
    // 4. DEFERRED EXECUTION
    // ==========================================

    [Fact]
    public void DeferredExecution_DemonstratesLazyEvaluationAndSnapshotReuse()
    {
        var books = SampleBooks();
        int evaluationCount = 0;

        double TestScorer(Book b)
        {
            evaluationCount++;
            return b.EditionCount;
        }

        // 1. Creation does NOT execute body
        var lazyEnumerable = DeferredExecution.StreamScoreBooks(books, TestScorer);
        Assert.Equal(0, evaluationCount);

        // 2. Iterating 1 item triggers only 1 evaluation
        using (var enumerator = lazyEnumerable.GetEnumerator())
        {
            Assert.Equal(0, evaluationCount);
            Assert.True(enumerator.MoveNext());
            Assert.Equal(1, evaluationCount);
            Assert.Equal(books[0].Title, enumerator.Current.Book.Title);
        }

        // 3. Materialization point creates a stable snapshot
        evaluationCount = 0;
        var snapshot = lazyEnumerable.ToList();
        Assert.Equal(books.Count, evaluationCount);

        // 4. Repeated enumeration of the snapshot performs 0 additional evaluations
        for (int i = 0; i < 3; i++)
        {
            var count = snapshot.Count;
            var titles = snapshot.Select(s => s.Book.Title).ToArray();
            Assert.Equal(books.Count, titles.Length);
            Assert.Equal(books.Count, evaluationCount); // unchanged!
        }
    }

    // ==========================================
    // 5. RESOURCE DISPOSAL
    // ==========================================

    [Fact]
    public void ResourceDisposal_StreamLines_ReleasesFileLockOnEarlyStop()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"stream_test_{Guid.NewGuid():N}.txt");
        File.WriteAllLines(tempPath, new[] { "Line 1", "Line 2", "Line 3", "Line 4" });

        try
        {
            // Early stop with Take(1)
            var firstLine = ReportExporter.StreamLines(tempPath).Take(1).ToList();
            Assert.Single(firstLine);
            Assert.Equal("Line 1", firstLine[0]);

            // If the stream were not disposed upon early stop, opening in exclusive mode would throw IOException
            using var exclusiveStream = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.NotNull(exclusiveStream);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task ResourceDisposal_ExportReport_WritesSafelyAndHandlesBoundaryError()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.txt");
        string content = "Sample Report Content\nLine 2";

        try
        {
            var result = await ReportExporter.ExportReportAsync(tempPath, content);
            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(tempPath));

            string readBack = await File.ReadAllTextAsync(tempPath);
            Assert.Equal(content, readBack);

            // Error boundary: empty path
            var invalidResult = await ReportExporter.ExportReportAsync("", content);
            Assert.False(invalidResult.IsSuccess);
            var failure = Assert.IsType<Result<string, DomainError>.Failure>(invalidResult);
            Assert.Equal("InvalidFilePath", failure.Error.Code);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    // ==========================================
    // 6. NATIVE INTEGRATION & ADAPTER ERROR BOUNDARIES
    // ==========================================

    [Theory]
    [InlineData(-1, 10, 5, "Invalid edition count")]
    [InlineData(60000, 10, 5, "Invalid edition count")]
    [InlineData(10, -1, 5, "The ratings count cannot be negative")]
    [InlineData(10, 10, -1, "Invalid book age")]
    [InlineData(10, 10, 1500, "Invalid book age")]
    public void NativeAdapter_ControlledFailures_ReturnClearResultFailures(
        int editions, int ratings, int age, string expectedErrorSubstring)
    {
        var result = NativeAdapter.CalculatePrioritySafe(editions, ratings, age);

        Assert.False(result.IsSuccess);
        var failure = Assert.IsType<Result<ulong, string>.Failure>(result);
        Assert.Contains(expectedErrorSubstring, failure.Error);
    }

    // ==========================================
    // 7. COMPLETE WORKFLOW INTEGRATION
    // ==========================================

    [Fact]
    public void CompleteWorkflow_IntegratesApiDataWithReportsAndNativeCall()
    {
        var dtos = new List<BookDto>
        {
            new() { Title = "Functional Architecture", Authors = new() { "Mark S." }, FirstPublishYear = 2021, EditionCount = 12, Key = "/fa" },
            new() { Title = "Pure FP in C#", Authors = new() { "John D." }, FirstPublishYear = 2023, EditionCount = 8, Key = "/pfp" },
            new() { Title = "Legacy OOP", Authors = new() { "Old A." }, FirstPublishYear = 1995, EditionCount = 4, Key = "/oop" }
        };

        var (books, errors) = BookParser.ParseAll(dtos);
        Assert.Empty(errors);
        Assert.Equal(3, books.Length);

        // Run author report
        var authorReport = LinqReports.GenerateAuthorReport(books, 2000);
        Assert.Equal(2, authorReport.Count);

        // Run priority report
        var (priorityReport, decades) = LinqReports.GeneratePriorityReport(
            books, 2000, CurriedScoring.PopularityBiasedScorer, referenceYear: 2026);

        Assert.Equal(2, priorityReport.Count);
        Assert.All(priorityReport, item => Assert.True(item.NativePriority > 0));
        Assert.Single(decades);
        Assert.Equal(2020, decades[0].Decade);
    }
}
