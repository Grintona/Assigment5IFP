using System.Text.Json.Serialization;
using System.Text.Json;
namespace StudyResourceFinder.Models;
public record OpenLibraryResponse(
    
    [property: JsonPropertyName("numFound")]
    int NumFound,

    [property: JsonPropertyName("start")]
    int Start,

    [property: JsonPropertyName("docs")]
    List<BookDto>? Docs
    
);
