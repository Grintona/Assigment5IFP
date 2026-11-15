using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using StudyResourceFinder.Models;
using StudyResourceFinder.Services;

const string DefaultFile = "Data/books.json";

Console.WriteLine("==================================================");
Console.WriteLine("    ACADEMIC STUDY RESOURCE & LENDING SYSTEM      ");
Console.WriteLine("         Final Project (FP 2026-2027)             ");
Console.WriteLine("==================================================");
Console.WriteLine("1. Search books via Open Library API");
Console.WriteLine("2. Load books from a JSON file (Offline / Recommended)");
Console.WriteLine("3. Exit");
Console.WriteLine("==================================================");
Console.Write("Choose an option: ");
string? choice = Console.ReadLine();

if (choice != "1" && choice != "2")
{
    Console.WriteLine(choice == "3" ? "Exiting..." : "Invalid choice. Exiting...");
    return;
}

string sourceInput;
if (choice == "1")
{
    Console.Write("Enter search query: ");
    string? query = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(query))
    {
        Console.WriteLine("Search query cannot be empty.");
        return;
    }
    sourceInput = query;
}
else
{
    Console.Write($"Enter file path (Enter for default '{DefaultFile}'): ");
    string? path = Console.ReadLine();
    sourceInput = string.IsNullOrWhiteSpace(path)
        ? Path.Combine(AppContext.BaseDirectory, DefaultFile)
        : path.Trim();
}

Console.Write("Enter minimum publication year (e.g. 2000): ");
if (!BookParser.TryParseMinYear(Console.ReadLine(), out int minYear, out string yearError))
{
    Console.WriteLine(yearError);
    return;
}

IReadOnlyList<BookDto> dtos;

try
{
    if (choice == "1")
    {
        Console.WriteLine($"Searching for books with query: '{sourceInput}'...");
        using var httpClient = new HttpClient();
        dtos = await new OpenLibraryClient(httpClient).SearchBooksAsync(sourceInput);
    }
    else
    {
        Console.WriteLine($"Loading books from file: {sourceInput}");
        var fileResult = await OpenLibraryClient.LoadFromFileResult(sourceInput);
        if (fileResult is Result<IReadOnlyList<BookDto>, DomainError>.Failure fileFailure)
        {
            Console.WriteLine($"[ERROR] {fileFailure.Error}");
            return;
        }

        dtos = ((Result<IReadOnlyList<BookDto>, DomainError>.Success)fileResult).Value;
    }
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Error fetching data from Open Library API: {ex.Message}");
    return;
}
catch (IOException ex) 
{
    Console.WriteLine($"Error reading file: {ex.Message}");
    return;
}
catch (JsonException ex)
{
    Console.WriteLine($"Invalid JSON: {ex.Message}");
    return;
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Error processing data: {ex.Message}");
    return;
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid argument: {ex.Message}");
    return;
}

var parseResult = BookParser.ParseAllResult(dtos);
var (books, errors) = BookParser.ParseAll(dtos);

Console.WriteLine();
Console.WriteLine($"Loaded {dtos.Count} records: {books.Length} valid, {errors.Length} rejected.");
if (parseResult is Result<ImmutableArray<Book>, DomainError>.Success validBooksResult)
{
    Console.WriteLine($"Verified {validBooksResult.Value.Length} valid domain records via ParseAllResult.");
}
foreach (var error in errors)
{
    Console.WriteLine($"  [Rejected] {error}");
}

bool exit = false;
while (!exit)
{
    Console.WriteLine();
    Console.WriteLine("==================================================");
    Console.WriteLine("               APPLICATION WORKFLOWS              ");
    Console.WriteLine("==================================================");
    Console.WriteLine("1. Workflow 1: Resource Discovery & Recommendations");
    Console.WriteLine("2. Workflow 2: Generate Personalized Study Plan");
    Console.WriteLine("3. Workflow 3: Request Academic Book Loan / Reservation");
    Console.WriteLine("4. LINQ Report 1: Author Publishing & Edition Reach");
    Console.WriteLine("5. LINQ Report 2: Priority Score & Decade Dynamics");
    Console.WriteLine("6. Compare Configured Scoring Policies (Curried)");
    Console.WriteLine("7. Demonstrate Lazy Processing & Deferred Execution (yield return)");
    Console.WriteLine("8. Catalog Lookup by Key (FindByKey & Option<Book>)");
    Console.WriteLine("9. Export Reports & Artifacts to Files (Safe Disposal)");
    Console.WriteLine("10. Run Full Capstone Demonstration (All Constructs)");
    Console.WriteLine("0. Exit");
    Console.Write("Enter your choice: ");
    string? menuChoice = Console.ReadLine();
    Console.WriteLine("==================================================");

    switch (menuChoice)
    {
        case "1":
            RunWorkflow1Menu(books, minYear);
            break;

        case "2":
            RunWorkflow2StudyPlan(books);
            break;

        case "3":
            RunWorkflow3LoanReservation(books);
            break;

        case "4":
            RunAuthorReport(books, minYear);
            break;

        case "5":
            RunPriorityReport(books, minYear);
            break;

        case "6":
            RunConfiguredCurriedComparison(books, minYear);
            break;

        case "7":
            RunLazyProcessingDemo(books);
            break;

        case "8":
            RunCatalogLookup(books);
            break;

        case "9":
            await RunExportAllArtifactsAsync(books, minYear);
            break;

        case "10":
            await RunFullCapstoneDemonstration(books, minYear);
            break;

        case "0":
            exit = true;
            Console.WriteLine("Exiting application.");
            break;

        default:
            Console.WriteLine("Invalid choice.");
            break;
    }
}

static void RunWorkflow1Menu(IEnumerable<Book> books, int minYear)
{
    Console.WriteLine("==============================");
    Console.WriteLine("Workflow 1: Choose Ranking Policy");
    Console.WriteLine("1. Popularity (Editions)");
    Console.WriteLine("2. Recency (Publish Year)");
    Console.WriteLine("3. Both (Side-by-side comparison)");
    Console.Write("Enter your choice: ");
    string? policyChoice = Console.ReadLine();
    Console.WriteLine("==============================");

    switch (policyChoice)
    {
        case "1":
            RunWorkflow1(books, minYear, Policies.Popularity, "Popularity Policy");
            break;
        case "2":
            RunWorkflow1(books, minYear, Policies.Recency, "Recency Policy");
            break;
        case "3":
            RunWorkflow1(books, minYear, Policies.Popularity, "Popularity Policy");
            RunWorkflow1(books, minYear, Policies.Recency, "Recency Policy");
            break;
        default:
            Console.WriteLine("Invalid choice.");
            break;
    }
}

static void RunWorkflow1(
    IEnumerable<Book> books,
    int minimumYear,
    Func<Book, double> scoringPolicy,
    string policyName)
{
    var validationResult = ValidatedSearch.Create(books, minimumYear, scoringPolicy);
    if (validationResult is ValidationResult<ValidatedSearch>.Invalid invalid)
    {
        Console.WriteLine($"Workflow 1 validation failed: {string.Join("; ", invalid.Reasons)}");
        return;
    }

    var validSearch = ((ValidationResult<ValidatedSearch>.Valid)validationResult).Value;
    var state = new BookWorkflowState.Pending(validSearch);
    var transitionResult = BookWorkflow.TransitionResult(state)
        .Map(st => ((BookWorkflowState.Completed)st).Recommendations);

    if (transitionResult is Result<IReadOnlyList<Recommendation>, DomainError>.Failure failure)
    {
        Console.WriteLine($"Workflow 1 failed: [{failure.Error.Code}] {failure.Error.Message}");
        return;
    }

    var recommendations = ((Result<IReadOnlyList<Recommendation>, DomainError>.Success)transitionResult).Value;
    PrintBooks.Print(policyName, recommendations);
}

static void RunWorkflow2StudyPlan(IEnumerable<Book> books)
{
    Console.WriteLine("=== Workflow 2: Generate Personalized Study Plan ===");
    Console.Write("Enter study subject/topic (Enter for all eligible): ");
    string? topic = Console.ReadLine()?.Trim() ?? "";

    Console.WriteLine("Choose difficulty level:");
    Console.WriteLine("1. Beginner (min year 1990, 4 hrs/week)");
    Console.WriteLine("2. Intermediate (min year 2005, 8 hrs/week)");
    Console.WriteLine("3. Advanced (min year 2015, 12 hrs/week)");
    Console.Write("Choice: ");
    string? diffChoice = Console.ReadLine();

    var difficulty = diffChoice switch
    {
        "1" => StudyPlanDifficulty.Beginner,
        "3" => StudyPlanDifficulty.Advanced,
        _ => StudyPlanDifficulty.Intermediate
    };

    Console.Write("Enter target duration in weeks (e.g. 4): ");
    if (!int.TryParse(Console.ReadLine(), out int weeks) || weeks <= 0)
    {
        weeks = 4;
    }

    var result = StudyPlanWorkflow.GenerateStudyPlanResult(books, topic, difficulty, weeks, maxBooksPerAuthor: 2);
    if (result is Result<StudyPlan, DomainError>.Failure failure)
    {
        Console.WriteLine($"[WORKFLOW 2 REJECTED]: {failure.Error}");
        return;
    }

    var plan = ((Result<StudyPlan, DomainError>.Success)result).Value;
    Console.WriteLine(ReportExporter.FormatStudyPlan(plan));
}

static void RunWorkflow3LoanReservation(IEnumerable<Book> books)
{
    Console.WriteLine("=== Workflow 3: Request Academic Book Loan / Reservation ===");
    Console.Write("Enter student ID (e.g. ST-2026-001): ");
    string? studentId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(studentId)) studentId = "ST-2026-DEFAULT";

    Console.WriteLine("Choose student tier:");
    Console.WriteLine("1. Standard (Max 2 books, max 4 weeks)");
    Console.WriteLine("2. Honors (Max 4 books, max 8 weeks)");
    Console.WriteLine("3. Researcher (Max 8 books, max 16 weeks)");
    Console.Write("Choice: ");
    string? tierChoice = Console.ReadLine();

    var tier = tierChoice switch
    {
        "2" => BorrowerTier.Honors,
        "3" => BorrowerTier.Researcher,
        _ => BorrowerTier.Standard
    };

    var catalogList = books.ToList();
    Console.WriteLine("\nAvailable catalog books to request:");
    for (int i = 0; i < Math.Min(catalogList.Count, 6); i++)
    {
        Console.WriteLine($"  - Key: '{catalogList[i].Key}' -> Title: '{catalogList[i].Title}'");
    }

    Console.Write("\nEnter book keys separated by comma: ");
    string? rawKeys = Console.ReadLine();
    var keys = (rawKeys ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (keys.Length == 0)
    {
        keys = catalogList.Take(2).Select(b => b.Key).ToArray();
        Console.WriteLine($"Defaulting to keys: {string.Join(", ", keys)}");
    }

    Console.Write("Enter loan duration in weeks: ");
    if (!int.TryParse(Console.ReadLine(), out int weeks) || weeks <= 0)
    {
        weeks = 2;
    }

    Console.WriteLine("\nChoose fee policy:");
    Console.WriteLine("1. Standard Policy ($1.50/day overdue)");
    Console.WriteLine("2. Academic Discount Policy ($0.50/day overdue)");
    Console.Write("Choice (Enter to use tier default): ");
    string? feeChoice = Console.ReadLine();

    Func<int, double> feePolicy = feeChoice switch
    {
        "1" => LoanWorkflow.StandardFeePolicy,
        "2" => LoanWorkflow.AcademicDiscountFeePolicy,
        _ => tier == BorrowerTier.Standard ? LoanWorkflow.StandardFeePolicy : LoanWorkflow.AcademicDiscountFeePolicy
    };

    var draftResult = LoanWorkflow.CreateDraft(studentId, tier, keys, weeks);
    if (draftResult is Result<LoanState.Draft, DomainError>.Failure draftFail)
    {
        Console.WriteLine($"[DRAFT REJECTED]: {draftFail.Error}");
        return;
    }
    var draft = ((Result<LoanState.Draft, DomainError>.Success)draftResult).Value;
    Console.WriteLine("[Step 1] Draft Loan Created Successfully.");

    var validateResult = LoanWorkflow.ValidateDraft(draft, catalogList, feePolicy);
    if (validateResult is Result<LoanState.Validated, DomainError>.Failure valFail)
    {
        Console.WriteLine($"[VALIDATION REJECTED]: {valFail.Error}");
        return;
    }
    var validated = ((Result<LoanState.Validated, DomainError>.Success)validateResult).Value;
    Console.WriteLine($"[Step 2] Loan Validated: {validated.Books.Length} book(s) verified against tier quotas.");

    var approveResult = LoanWorkflow.ApproveLoan(validated);
    if (approveResult is Result<LoanState.Approved, DomainError>.Failure appFail)
    {
        Console.WriteLine($"[APPROVAL REJECTED]: {appFail.Error}");
        return;
    }
    var approved = ((Result<LoanState.Approved, DomainError>.Success)approveResult).Value;
    Console.WriteLine("\n[Step 3] Loan Approved:");
    Console.WriteLine(ReportExporter.FormatLoanAgreement(approved));
}

static void RunAuthorReport(IEnumerable<Book> books, int minYear)
{
    var report = LinqReports.GenerateAuthorReport(books, minYear);
    Console.WriteLine(ReportExporter.FormatAuthorReport(report));
}

static void RunPriorityReport(IEnumerable<Book> books, int minYear)
{
    var (items, decades) = LinqReports.GeneratePriorityReport(
        books, minYear, CurriedScoring.PopularityBiasedScorer);
    Console.WriteLine(ReportExporter.FormatPriorityReport(items, decades));
}

static void RunConfiguredCurriedComparison(IEnumerable<Book> books, int minYear)
{
    Console.WriteLine("--- Comparing Two Partially Applied Curried Functions ---");
    var popRecommendations = BookPipeline.FindResources(books, minYear, CurriedScoring.PopularityBiasedScorer);
    PrintBooks.Print("Configuration 1: Popularity-Biased (EditionWeight: 1.5, RecencyWeight: 0.2)", popRecommendations);

    var recRecommendations = BookPipeline.FindResources(books, minYear, CurriedScoring.RecencyBiasedScorer);
    PrintBooks.Print("Configuration 2: Recency-Biased (EditionWeight: 0.1, RecencyWeight: 2.5)", recRecommendations);
}

static void RunLazyProcessingDemo(IEnumerable<Book> books)
{
    Console.WriteLine("=== Demonstration of Lazy Processing & Deferred Execution (yield return) ===");
    Console.WriteLine("1. Stream scoring with yield return (DeferredExecution.StreamScoreBooks):");
    int count = 0;
    foreach (var item in DeferredExecution.StreamScoreBooks(books, CurriedScoring.PopularityBiasedScorer))
    {
        count++;
        Console.WriteLine($"   [Lazy Item {count}] Title: '{item.Book.Title}' -> Score: {item.Score:F1}");
        if (count >= 3)
        {
            Console.WriteLine("   ... (early break to demonstrate deferred processing without iterating entire sequence)");
            break;
        }
    }

    string sampleReport = Path.Combine(AppContext.BaseDirectory, "reports", "author_statistics_report.txt");
    if (File.Exists(sampleReport))
    {
        Console.WriteLine("\n2. File streaming with yield return (ReportExporter.StreamLines):");
        int lineNum = 0;
        foreach (var line in ReportExporter.StreamLines(sampleReport).Take(4))
        {
            lineNum++;
            Console.WriteLine($"   [StreamLine {lineNum}] {line}");
        }
    }
}

static void RunCatalogLookup(IEnumerable<Book> books)
{
    Console.WriteLine("=== Catalog Lookup by Key (FindByKey & Option<Book>) ===");
    Console.Write("Enter book key to search (e.g. '/boundary'): ");
    string? key = Console.ReadLine();

    var option = BookPipeline.FindBookOption(books, key);
    var matchString = option.Match(
        some => $"[Option.Some] Found: '{some.Title}' (Publish year: {some.FirstPublishYear}, Editions: {some.EditionCount})",
        () => "[Option.None] Book was not found in catalog."
    );
    Console.WriteLine(matchString);

    var result = BookPipeline.FindByKey(books, key);
    if (result is Result<Book, DomainError>.Success ok)
    {
        Console.WriteLine($"[Result.Success] '{ok.Value.Title}' found via FindByKey.");
    }
    else if (result is Result<Book, DomainError>.Failure fail)
    {
        Console.WriteLine($"[Result.Failure] Code: {fail.Error.Code}, Message: {fail.Error.Message}");
    }
}

static async Task RunExportAllArtifactsAsync(IEnumerable<Book> books, int minYear)
{
    string outDir = Path.Combine(AppContext.BaseDirectory, "reports");
    string authorPath = Path.Combine(outDir, "author_statistics_report.txt");
    string priorityPath = Path.Combine(outDir, "priority_native_report.txt");
    string studyPlanPath = Path.Combine(outDir, "sample_study_plan.txt");
    string loanPath = Path.Combine(outDir, "sample_loan_agreement.txt");

    var authorData = LinqReports.GenerateAuthorReport(books, minYear);
    var authorText = ReportExporter.FormatAuthorReport(authorData);
    var authorResult = await ReportExporter.ExportReportAsync(authorPath, authorText);
    Console.WriteLine(authorResult.IsSuccess ? $"[OK] Exported: {authorPath}" : "[FAILED] Author Report");

    var (priorityItems, decades) = LinqReports.GeneratePriorityReport(
        books, minYear, CurriedScoring.PopularityBiasedScorer);
    var priorityText = ReportExporter.FormatPriorityReport(priorityItems, decades);
    var priorityResult = await ReportExporter.ExportReportAsync(priorityPath, priorityText);
    Console.WriteLine(priorityResult.IsSuccess ? $"[OK] Exported: {priorityPath}" : "[FAILED] Priority Report");

    var planResult = StudyPlanWorkflow.GenerateStudyPlanResult(books, "", StudyPlanDifficulty.Intermediate, 4);
    if (planResult is Result<StudyPlan, DomainError>.Success sPlan)
    {
        var planExport = await ReportExporter.ExportStudyPlanAsync(studyPlanPath, sPlan.Value);
        Console.WriteLine(planExport.IsSuccess ? $"[OK] Exported: {studyPlanPath}" : "[FAILED] Study Plan");
    }

    var catalogList = books.ToList();
    var loanRes = LoanWorkflow.CreateDraft("ST-2026-EXPORT", BorrowerTier.Honors, catalogList.Take(2).Select(b => b.Key), 4)
        .Bind(draft => LoanWorkflow.ValidateDraft(draft, catalogList, LoanWorkflow.AcademicDiscountFeePolicy))
        .Bind(validated => LoanWorkflow.ApproveLoan(validated));

    if (loanRes is Result<LoanState.Approved, DomainError>.Success sApp)
    {
        var loanExport = await ReportExporter.ExportLoanAgreementAsync(loanPath, sApp.Value);
        Console.WriteLine(loanExport.IsSuccess ? $"[OK] Exported: {loanPath}" : "[FAILED] Loan Agreement");
    }
}

static async Task RunFullCapstoneDemonstration(IEnumerable<Book> books, int minYear)
{
    Console.WriteLine("\n========================================================");
    Console.WriteLine(">>> RUNNING FULL FINAL PROJECT DEMONSTRATION <<<");
    Console.WriteLine("========================================================\n");

    Console.WriteLine("--> WORKFLOW 1: Resource Discovery & Ranking (with ValidatedSearch & BookWorkflow)");
    RunWorkflow1(books, minYear, CurriedScoring.PopularityBiasedScorer, "Curried Popularity");

    Console.WriteLine("\n--> WORKFLOW 2: Personalized Study Plan Generation");
    var planRes = StudyPlanWorkflow.GenerateStudyPlanResult(books, "", StudyPlanDifficulty.Intermediate, 3);
    if (planRes is Result<StudyPlan, DomainError>.Success sPlan)
        Console.WriteLine(ReportExporter.FormatStudyPlan(sPlan.Value));

    Console.WriteLine("\n--> WORKFLOW 3: Safe Multi-Stage Book Loan (with AcademicDiscountFeePolicy)");
    var catalog = books.ToList();
    var loanRes = LoanWorkflow.CreateDraft("ST-DEFENSE-01", BorrowerTier.Honors, catalog.Take(2).Select(b => b.Key), 3)
        .Bind(draft => LoanWorkflow.ValidateDraft(draft, catalog, LoanWorkflow.AcademicDiscountFeePolicy))
        .Bind(validated => LoanWorkflow.ApproveLoan(validated));

    if (loanRes is Result<LoanState.Approved, DomainError>.Success sApp)
        Console.WriteLine(ReportExporter.FormatLoanAgreement(sApp.Value));

    Console.WriteLine("\n--> LAZY PROCESSING & DEFERRED EXECUTION (yield return):");
    RunLazyProcessingDemo(books);

    Console.WriteLine("\n--> CATALOG LOOKUP (FindByKey & Option<Book>):");
    var lookupKey = catalog.FirstOrDefault()?.Key ?? "/boundary";
    var opt = BookPipeline.FindBookOption(books, lookupKey);
    Console.WriteLine(opt.Match(
        b => $"[Option.Some] Found '{b.Title}'",
        () => "[Option.None] Book not found"));

    Console.WriteLine("\n--> LINQ REPORT 1: Author Publishing & Edition Reach");
    RunAuthorReport(books, minYear);

    Console.WriteLine("\n--> LINQ REPORT 2: Curried & Native Priority Dynamics");
    RunPriorityReport(books, minYear);

    Console.WriteLine("\n--> SAFE FILE EXPORT:");
    await RunExportAllArtifactsAsync(books, minYear);
    Console.WriteLine("\n>>> DEMONSTRATION COMPLETED SUCCESSFULLY <<<");
}
