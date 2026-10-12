using System.Collections.Immutable;
namespace StudyResourceFinder.Models;

public sealed record Book(
    string Title,
    ImmutableArray<string> Authors,
    int? FirstPublishYear, 
    int EditionCount,
    string Key,
    ImmutableArray<string> Languages);