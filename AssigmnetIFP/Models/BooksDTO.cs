using System.Text.Json.Serialization;
namespace StudyResourceFinder.Models;
public sealed class BookDto
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("author_name")] public List<string>? Authors { get; set; }
    [JsonPropertyName("first_publish_year")] public int? FirstPublishYear { get; set; }
    [JsonPropertyName("edition_count")] public int? EditionCount { get; set; }
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("language")] public List<string>? Languages { get; set; }
}