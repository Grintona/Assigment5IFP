using System.Net.Http;
using System.Text.Json;
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

List<BookDto> dtos;

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
        dtos = await OpenLibraryClient.LoadFromFile(sourceInput);
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
        PrintBooks.Print("Popularity", BookPipeline.FindResources(books, minYear, Policies.Popularity));
        break;
    case "2":
        PrintBooks.Print("Recency", BookPipeline.FindResources(books, minYear, Policies.Recency));
        break;
    case "3":
        PrintBooks.Print("Popularity", BookPipeline.FindResources(books, minYear, Policies.Popularity));
        PrintBooks.Print("Recency", BookPipeline.FindResources(books, minYear, Policies.Recency));
        break;
    default:
        Console.WriteLine("Invalid choice. Exiting...");
        break;
}