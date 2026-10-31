using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using StudyResourceFinder.Models;
using StudyResourceFinder.Services;

const string DefaultFile = "Data/books.json";

Console.WriteLine("==============================");
Console.WriteLine("    STUDY RESOURCE FINDER");
Console.WriteLine("==============================");
Console.WriteLine("1. Search books via Open Library API");
Console.WriteLine("2. Load books from a JSON file");
Console.WriteLine("3. Exit");
Console.WriteLine("==============================");
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
    Console.Write($"Enter file path (Enter for DefaultFile): ");
    string? path = Console.ReadLine();
    sourceInput = string.IsNullOrWhiteSpace(path)
        ? Path.Combine(AppContext.BaseDirectory, DefaultFile)
        : path.Trim();
}

Console.Write("Enter minimum publication year: ");
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
        Console.WriteLine($"Searching for books with query: {sourceInput}");
        using var httpClient = new HttpClient();
        dtos = await new OpenLibraryClient(httpClient).SearchBooksAsync(sourceInput);
    }
    else
    {
        Console.WriteLine($"Loading books from file: {sourceInput}");
        var fileResult = await OpenLibraryClient.LoadFromFileResult(sourceInput);
        if (fileResult is Result<IReadOnlyList<BookDto>, DomainError>.Failure fileFailure)
        {
            Console.WriteLine(fileFailure.Error);
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

var (books, errors) = BookParser.ParseAll(dtos);
Console.WriteLine();
Console.WriteLine($"Loaded {dtos.Count} records: {books.Length} valid, {errors.Length} rejected.");
foreach (var error in errors)
{
    Console.WriteLine($"  Rejected: {error}");
}

bool exit = false;
while (!exit)
{
    Console.WriteLine();
    Console.WriteLine("==================================================");
    Console.WriteLine("Choose action:");
    Console.WriteLine("1. Original Recommendations (Popularity / Recency)");
    Console.WriteLine("2. LINQ Report 1: Author Publishing & Editions");
    Console.WriteLine("3. LINQ Report 2: Curried Scoring & Native Priority");
    Console.WriteLine("4. Compare Configured Curried Functions");
    Console.WriteLine("5. Export Reports to Files (Safe Resource Disposal)");
    Console.WriteLine("6. Run Full Assignment 8 Demonstration");
    Console.WriteLine("0. Exit");
    Console.Write("Enter your choice: ");
    string? menuChoice = Console.ReadLine();
    Console.WriteLine("==================================================");

    switch (menuChoice)
    {
        case "1":
            RunOriginalWorkflowMenu(books, minYear);
            break;

        case "2":
            RunAuthorReport(books, minYear);
            break;

        case "3":
            RunPriorityReport(books, minYear);
            break;

        case "4":
            RunConfiguredCurriedComparison(books, minYear);
            break;

        case "5":
            await RunExportReportsAsync(books, minYear);
            break;

        case "6":
            Console.WriteLine("\n>>> RUNNING FULL ASSIGNMENT 8 DEMONSTRATION <<<\n");
            RunAuthorReport(books, minYear);
            RunPriorityReport(books, minYear);
            RunConfiguredCurriedComparison(books, minYear);
            await RunExportReportsAsync(books, minYear);
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

static async Task RunExportReportsAsync(IEnumerable<Book> books, int minYear)
{
    string outDir = Path.Combine(AppContext.BaseDirectory, "reports");
    string authorPath = Path.Combine(outDir, "author_statistics_report.txt");
    string priorityPath = Path.Combine(outDir, "priority_native_report.txt");

    var authorData = LinqReports.GenerateAuthorReport(books, minYear);
    var authorText = ReportExporter.FormatAuthorReport(authorData);
    var authorResult = await ReportExporter.ExportReportAsync(authorPath, authorText);

    if (authorResult is Result<string, DomainError>.Success s1)
    {
        Console.WriteLine($"[OK] Exported Author Report to: {s1.Value}");
    }
    else if (authorResult is Result<string, DomainError>.Failure f1)
    {
        Console.WriteLine($"[FAILED] Exporting Author Report: {f1.Error}");
    }

    var (priorityItems, decades) = LinqReports.GeneratePriorityReport(
        books, minYear, CurriedScoring.PopularityBiasedScorer);
    var priorityText = ReportExporter.FormatPriorityReport(priorityItems, decades);
    var priorityResult = await ReportExporter.ExportReportAsync(priorityPath, priorityText);

    if (priorityResult is Result<string, DomainError>.Success s2)
    {
        Console.WriteLine($"[OK] Exported Priority Report to: {s2.Value}");
    }
    else if (priorityResult is Result<string, DomainError>.Failure f2)
    {
        Console.WriteLine($"[FAILED] Exporting Priority Report: {f2.Error}");
    }
}

static void RunOriginalWorkflowMenu(IEnumerable<Book> books, int minYear)
{
    Console.WriteLine("==============================");
    Console.WriteLine("Choose ranking policy:");
    Console.WriteLine("1. Popularity");
    Console.WriteLine("2. Recency");
    Console.WriteLine("3. Both (compare)");
    Console.Write("Enter your choice: ");
    string? policyChoice = Console.ReadLine();
    Console.WriteLine("==============================");

    switch (policyChoice)
    {
        case "1":
            RunWorkflow(books, minYear, Policies.Popularity, "Popularity");
            break;
        case "2":
            RunWorkflow(books, minYear, Policies.Recency, "Recency");
            break;
        case "3":
            RunWorkflow(books, minYear, Policies.Popularity, "Popularity");
            RunWorkflow(books, minYear, Policies.Recency, "Recency");
            break;
        default:
            Console.WriteLine("Invalid choice.");
            break;
    }
}

static void RunWorkflow(
    IEnumerable<Book> books,
    int minimumYear,
    Func<Book, double> scoringPolicy,
    string policyName)
{
    var result = BookPipeline.FindResourcesResult(books, minimumYear, scoringPolicy);
    if (result is Result<IReadOnlyList<Recommendation>, DomainError>.Failure failure)
    {
        Console.WriteLine($"Workflow failed: {failure.Error}");
        return;
    }

    var recommendations = ((Result<IReadOnlyList<Recommendation>, DomainError>.Success)result).Value;
    PrintBooks.Print(policyName, recommendations);
}
