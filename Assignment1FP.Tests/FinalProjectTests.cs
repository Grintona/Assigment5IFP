using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StudyResourceFinder.Models;
using StudyResourceFinder.Services;
using Xunit;

namespace StudyResourceFinder.Tests;

public class FinalProjectTests
{
    private static readonly ImmutableArray<Book> SampleCatalog = ImmutableArray.Create(
        new Book(
            Title: "Functional Architecture",
            Authors: ImmutableArray.Create("Mark S.", "Jane D."),
            FirstPublishYear: 2021,
            EditionCount: 15,
            Key: "/books/func-arch",
            Languages: ImmutableArray.Create("eng")),
        new Book(
            Title: "Pure FP in C#",
            Authors: ImmutableArray.Create("John D."),
            FirstPublishYear: 2023,
            EditionCount: 12,
            Key: "/books/pure-fp",
            Languages: ImmutableArray.Create("eng")),
        new Book(
            Title: "Category Theory for Programmers",
            Authors: ImmutableArray.Create("Milewski B."),
            FirstPublishYear: 2018,
            EditionCount: 25,
            Key: "/books/cat-theory",
            Languages: ImmutableArray.Create("eng")),
        new Book(
            Title: "Classic Algorithms",
            Authors: ImmutableArray.Create("John D."),
            FirstPublishYear: 1995,
            EditionCount: 8,
            Key: "/books/classic-algo",
            Languages: ImmutableArray.Create("eng"))
    );

    #region Option<T> Tests

    [Fact]
    public void Option_SomeAndNone_DemonstrateExplicitPresenceAndAbsence()
    {
        var some = Option<string>.SomeValue("functional");
        var none = Option<string>.NoneValue();

        Assert.True(some.IsSome);
        Assert.False(some.IsNone);
        Assert.False(none.IsSome);
        Assert.True(none.IsNone);
    }

    [Fact]
    public void Option_MapAndBind_TransformValueOrShortCircuit()
    {
        var some = Option<int>.SomeValue(10);
        var none = Option<int>.NoneValue();

        var mappedSome = some.Map(x => x * 2);
        var mappedNone = none.Map(x => x * 2);

        Assert.Equal(20, ((Option<int>.Some)mappedSome).Value);
        Assert.True(mappedNone.IsNone);

        var boundSome = some.Bind(x => Option<string>.SomeValue($"Val:{x}"));
        var boundNone = none.Bind(x => Option<string>.SomeValue($"Val:{x}"));

        Assert.Equal("Val:10", ((Option<string>.Some)boundSome).Value);
        Assert.True(boundNone.IsNone);
    }

    [Fact]
    public void Option_Match_BranchesCorrectly()
    {
        var some = Option<int>.SomeValue(42);
        var none = Option<int>.NoneValue();

        string resSome = some.Match(v => $"Number: {v}", () => "Nothing");
        string resNone = none.Match(v => $"Number: {v}", () => "Nothing");

        Assert.Equal("Number: 42", resSome);
        Assert.Equal("Nothing", resNone);
    }

    [Fact]
    public void Option_ToResult_ConvertsToResultWithoutNulls()
    {
        var some = Option<string>.SomeValue("Found");
        var none = Option<string>.NoneValue();

        var okResult = some.ToResult(new DomainError("NotFound", "Missing"));
        var failResult = none.ToResult(new DomainError("NotFound", "Missing"));

        Assert.True(okResult.IsSuccess);
        Assert.Equal("Found", ((Result<string, DomainError>.Success)okResult).Value);

        Assert.False(failResult.IsSuccess);
        Assert.Equal("NotFound", ((Result<string, DomainError>.Failure)failResult).Error.Code);
    }

    [Fact]
    public void FindBookOption_ReturnsSomeForExistingBook_AndNoneForMissing()
    {
        var someBook = BookPipeline.FindBookOption(SampleCatalog, "/books/func-arch");
        var noneBook = BookPipeline.FindBookOption(SampleCatalog, "/books/non-existent");
        var nullQuery = BookPipeline.FindBookOption(SampleCatalog, null);

        Assert.True(someBook.IsSome);
        Assert.Equal("Functional Architecture", ((Option<Book>.Some)someBook).Value.Title);

        Assert.True(noneBook.IsNone);
        Assert.True(nullQuery.IsNone);
    }

    #endregion

    #region Workflow 2: Study Plan Tests

    [Fact]
    public void StudyPlanWorkflow_GeneratesValidPlanWithCorrectDifficultyPacing()
    {
        var result = StudyPlanWorkflow.GenerateStudyPlanResult(
            SampleCatalog,
            topic: "FP",
            difficulty: StudyPlanDifficulty.Intermediate,
            targetWeeks: 3,
            maxBooksPerAuthor: 2);

        Assert.True(result.IsSuccess);
        var plan = ((Result<StudyPlan, DomainError>.Success)result).Value;

        Assert.Equal(StudyPlanDifficulty.Intermediate, plan.Difficulty);
        Assert.NotEmpty(plan.Modules);
        Assert.All(plan.Modules, m => Assert.Equal(8, m.WeeklyReadingHours));
        Assert.True(plan.TotalReadingHours > 0);
    }

    [Fact]
    public void StudyPlanWorkflow_EnforceAuthorDiversity_CapsBooksPerAuthor()
    {

        var constrained = StudyPlanWorkflow.EnforceAuthorDiversity(SampleCatalog, maxCountPerAuthor: 1);

        int johnDCount = constrained.Count(b => b.Authors.Contains("John D."));
        Assert.Equal(1, johnDCount);
    }

    [Theory]
    [InlineData(StudyPlanDifficulty.Beginner, 4)]
    [InlineData(StudyPlanDifficulty.Intermediate, 8)]
    [InlineData(StudyPlanDifficulty.Advanced, 12)]
    public void StudyPlanWorkflow_PatternMatchingAssignsSpecificHoursPerDifficulty(
        StudyPlanDifficulty difficulty, int expectedHours)
    {
        var modules = StudyPlanWorkflow.ScheduleModules(SampleCatalog, difficulty, targetWeeks: 2);

        Assert.NotEmpty(modules);
        Assert.All(modules, m => Assert.Equal(expectedHours, m.WeeklyReadingHours));
    }

    [Fact]
    public void StudyPlanWorkflow_RejectsInvalidWeeksOrMissingTopic()
    {
        var invalidWeeks = StudyPlanWorkflow.GenerateStudyPlanResult(
            SampleCatalog, topic: "FP", difficulty: StudyPlanDifficulty.Beginner, targetWeeks: -1);

        var missingTopic = StudyPlanWorkflow.GenerateStudyPlanResult(
            SampleCatalog, topic: "CompletelyUnknownSubjectXYZ", difficulty: StudyPlanDifficulty.Beginner, targetWeeks: 4);

        Assert.False(invalidWeeks.IsSuccess);
        Assert.Equal("InvalidTargetWeeks", ((Result<StudyPlan, DomainError>.Failure)invalidWeeks).Error.Code);

        Assert.False(missingTopic.IsSuccess);
        Assert.Equal("EmptyStudyPlan", ((Result<StudyPlan, DomainError>.Failure)missingTopic).Error.Code);
    }

    #endregion

    #region Workflow 3: Safe Multi-Stage Book Loan Tests

    [Fact]
    public void LoanWorkflow_SuccessfulThreeStageTransition()
    {

        var draftRes = LoanWorkflow.CreateDraft(
            "ST-2026-001",
            BorrowerTier.Honors,
            new[] { "/books/func-arch", "/books/pure-fp" },
            requestedWeeks: 4);

        Assert.True(draftRes.IsSuccess);
        var draft = ((Result<LoanState.Draft, DomainError>.Success)draftRes).Value;

        var valRes = LoanWorkflow.ValidateDraft(draft, SampleCatalog, LoanWorkflow.AcademicDiscountFeePolicy);
        Assert.True(valRes.IsSuccess);
        var validated = ((Result<LoanState.Validated, DomainError>.Success)valRes).Value;
        Assert.Equal(2, validated.Books.Length);

        var appRes = LoanWorkflow.ApproveLoan(validated);
        Assert.True(appRes.IsSuccess);
        var approved = ((Result<LoanState.Approved, DomainError>.Success)appRes).Value;

        Assert.StartsWith("LOAN-", approved.LoanId);
        Assert.Equal("ST-2026-001", approved.StudentId);
        Assert.True(approved.DueDate > DateTime.UtcNow);
        Assert.Equal(0.50, approved.DailyLateFee);
    }

    [Fact]
    public void LoanWorkflow_RejectsBorrowQuotaExceededForTier()
    {

        var draft = new LoanState.Draft(
            "ST-STANDARD",
            BorrowerTier.Standard,
            ImmutableArray.Create("/books/func-arch", "/books/pure-fp", "/books/cat-theory"),
            RequestedWeeks: 2);

        var result = LoanWorkflow.ValidateDraft(draft, SampleCatalog);

        Assert.False(result.IsSuccess);
        var failure = (Result<LoanState.Validated, DomainError>.Failure)result;
        Assert.Equal("BorrowQuotaExceeded", failure.Error.Code);
    }

    [Fact]
    public void LoanWorkflow_RejectsMissingBookInCatalog()
    {
        var draft = new LoanState.Draft(
            "ST-HONORS",
            BorrowerTier.Honors,
            ImmutableArray.Create("/books/func-arch", "/books/ghost-book"),
            RequestedWeeks: 2);

        var result = LoanWorkflow.ValidateDraft(draft, SampleCatalog);

        Assert.False(result.IsSuccess);
        var failure = (Result<LoanState.Validated, DomainError>.Failure)result;
        Assert.Equal("BookNotFound", failure.Error.Code);
    }

    [Fact]
    public void LoanWorkflow_SafeOperationSequence_PreventsDirectDraftApproval()
    {
        var draft = new LoanState.Draft(
            "ST-DIRECT",
            BorrowerTier.Standard,
            ImmutableArray.Create("/books/func-arch"),
            RequestedWeeks: 2);

        var result = LoanWorkflow.ApproveLoan(draft);

        Assert.False(result.IsSuccess);
        var failure = (Result<LoanState.Approved, DomainError>.Failure)result;
        Assert.Equal("InvalidStateTransition", failure.Error.Code);
    }

    [Fact]
    public void LoanWorkflow_SafeOperationSequence_PreventsReapprovingApprovedLoan()
    {
        var approved = new LoanState.Approved(
            "LOAN-EXISTING",
            "ST-STUDENT",
            BorrowerTier.Standard,
            SampleCatalog.Take(1).ToImmutableArray(),
            DateTime.UtcNow.AddDays(14),
            DailyLateFee: 1.50);

        var result = LoanWorkflow.ApproveLoan(approved);

        Assert.False(result.IsSuccess);
        var failure = (Result<LoanState.Approved, DomainError>.Failure)result;
        Assert.Equal("InvalidStateTransition", failure.Error.Code);
    }

    #endregion

    #region Export Persistence Tests

    [Fact]
    public async Task ReportExporter_ExportsStudyPlanAndLoanAgreementSafely()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "study_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string planFile = Path.Combine(tempDir, "test_study_plan.txt");
        string loanFile = Path.Combine(tempDir, "test_loan.txt");

        try
        {
            var plan = new StudyPlan(
                "FP in Practice",
                StudyPlanDifficulty.Beginner,
                ImmutableArray.Create(new StudyModule(1, SampleCatalog[0], 4, "Read Chapters 1-3")),
                TotalReadingHours: 4,
                RecommendedPacing: "Steady pace");

            var loan = new LoanState.Approved(
                "LOAN-TEST",
                "ST-001",
                BorrowerTier.Standard,
                SampleCatalog.Take(1).ToImmutableArray(),
                DateTime.UtcNow.AddDays(7),
                1.50);

            var planExport = await ReportExporter.ExportStudyPlanAsync(planFile, plan);
            var loanExport = await ReportExporter.ExportLoanAgreementAsync(loanFile, loan);

            Assert.True(planExport.IsSuccess);
            Assert.True(File.Exists(planFile));
            string planContent = await File.ReadAllTextAsync(planFile);
            Assert.Contains("FP in Practice", planContent);

            Assert.True(loanExport.IsSuccess);
            Assert.True(File.Exists(loanFile));
            string loanContent = await File.ReadAllTextAsync(loanFile);
            Assert.Contains("LOAN-TEST", loanContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Result_SuccessAndFailure_EnforceNonNullInvariants()
    {
        Assert.Throws<ArgumentNullException>(() => new Result<string, string>.Success(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string, string>.Ok(null!));
        Assert.Throws<ArgumentNullException>(() => new Result<string, string>.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string, string>.Fail(null!));
    }

    [Fact]
    public void ValidatedSearch_MissingKey_IndividuallyRejected()
    {
        var book = new Book("Valid Title", ImmutableArray<string>.Empty, 2020, 1, "", ImmutableArray<string>.Empty);
        var result = ValidatedSearch.Create(new[] { book }, 2000, Policies.Popularity);
        Assert.IsType<ValidationResult<ValidatedSearch>.Invalid>(result);
        var invalid = (ValidationResult<ValidatedSearch>.Invalid)result;
        Assert.Contains(invalid.Reasons, r => r.Contains("Key is missing for book at index 0."));
    }

    [Fact]
    public void ValidatedSearch_MissingTitle_IndividuallyRejected()
    {
        var book = new Book("   ", ImmutableArray<string>.Empty, 2020, 1, "/key", ImmutableArray<string>.Empty);
        var result = ValidatedSearch.Create(new[] { book }, 2000, Policies.Popularity);
        Assert.IsType<ValidationResult<ValidatedSearch>.Invalid>(result);
        var invalid = (ValidationResult<ValidatedSearch>.Invalid)result;
        Assert.Contains(invalid.Reasons, r => r.Contains("Title is missing for book at index 0."));
    }

    [Fact]
    public void ValidatedSearch_NegativeEditionCount_IndividuallyRejected()
    {
        var book = new Book("Valid Title", ImmutableArray<string>.Empty, 2020, -3, "/key", ImmutableArray<string>.Empty);
        var result = ValidatedSearch.Create(new[] { book }, 2000, Policies.Popularity);
        Assert.IsType<ValidationResult<ValidatedSearch>.Invalid>(result);
        var invalid = (ValidationResult<ValidatedSearch>.Invalid)result;
        Assert.Contains(invalid.Reasons, r => r.Contains("Edition count cannot be negative for book at index 0."));
    }

    [Fact]
    public void ValidatedSearch_NegativePublishYear_IndividuallyRejected()
    {
        var book = new Book("Valid Title", ImmutableArray<string>.Empty, -10, 1, "/key", ImmutableArray<string>.Empty);
        var result = ValidatedSearch.Create(new[] { book }, 2000, Policies.Popularity);
        Assert.IsType<ValidationResult<ValidatedSearch>.Invalid>(result);
        var invalid = (ValidationResult<ValidatedSearch>.Invalid)result;
        Assert.Contains(invalid.Reasons, r => r.Contains("Publication year cannot be negative for book at index 0."));
    }

    [Fact]
    public void ValidatedSearch_NullBookInArray_IndividuallyRejected()
    {
        var result = ValidatedSearch.Create(new Book[] { null! }, 2000, Policies.Popularity);
        Assert.IsType<ValidationResult<ValidatedSearch>.Invalid>(result);
        var invalid = (ValidationResult<ValidatedSearch>.Invalid)result;
        Assert.Contains(invalid.Reasons, r => r.Contains("Book at index 0 cannot be null."));
    }

    [Fact]
    public void ValidatedSearch_CreateResult_IndividualFieldRejections()
    {
        var missingKeyBook = new Book("Title", ImmutableArray<string>.Empty, 2020, 1, "", ImmutableArray<string>.Empty);
        var res1 = ValidatedSearch.CreateResult(new[] { missingKeyBook }, 2000, Policies.Popularity);
        Assert.False(res1.IsSuccess);
        Assert.Equal("MissingKey", ((Result<ValidatedSearch, DomainError>.Failure)res1).Error.Code);

        var missingTitleBook = new Book("", ImmutableArray<string>.Empty, 2020, 1, "/key", ImmutableArray<string>.Empty);
        var res2 = ValidatedSearch.CreateResult(new[] { missingTitleBook }, 2000, Policies.Popularity);
        Assert.False(res2.IsSuccess);
        Assert.Equal("MissingTitle", ((Result<ValidatedSearch, DomainError>.Failure)res2).Error.Code);

        var negEditionBook = new Book("Title", ImmutableArray<string>.Empty, 2020, -5, "/key", ImmutableArray<string>.Empty);
        var res3 = ValidatedSearch.CreateResult(new[] { negEditionBook }, 2000, Policies.Popularity);
        Assert.False(res3.IsSuccess);
        Assert.Equal("NegativeEditionCount", ((Result<ValidatedSearch, DomainError>.Failure)res3).Error.Code);

        var nullBook = ValidatedSearch.CreateResult(new Book[] { null! }, 2000, Policies.Popularity);
        Assert.False(nullBook.IsSuccess);
        Assert.Equal("MissingBook", ((Result<ValidatedSearch, DomainError>.Failure)nullBook).Error.Code);
    }

    #endregion
}
