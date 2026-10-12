using System.Net.Http;
using System.Text.Json;
using StudyResourceFinder.Models;
namespace StudyResourceFinder.Services;
public class OpenLibraryClient
{
    private readonly HttpClient _httpClient;
   public OpenLibraryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private async Task<string> SearchAsync(string query){
        if (string.IsNullOrWhiteSpace(query))
            {
            throw new ArgumentException(
            "Search query cannot be empty.",
            nameof(query)
            );
            }
        string url =
        $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query)}&limit=10";
        return await _httpClient.GetStringAsync(url);  
    }
    
public static async Task<List<BookDto>> LoadFromFile(string path)
{
    await using var stream = File.OpenRead(path);

    var response = await JsonSerializer.DeserializeAsync<OpenLibraryResponse>(stream)
        ?? throw new InvalidOperationException("File is empty or invalid.");

    return response.Docs
        ?? throw new InvalidOperationException("File has no 'docs' field.");
}

    public async Task<List<BookDto>> SearchBooksAsync(string query){
        string json = await SearchAsync(query);
        var response = JsonSerializer.Deserialize<OpenLibraryResponse>(json);
        if (response == null)
        {
             throw new InvalidOperationException("API returned an empty response.");
        }
        if (response.Docs == null)
        {
        throw new InvalidOperationException(
        "API response does not contain the 'docs' field."
        );
        }

        return response.Docs;
    }
}