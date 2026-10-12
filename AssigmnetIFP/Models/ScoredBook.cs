namespace StudyResourceFinder.Models;

public sealed record ScoredBook(
    Book Book,
    double Score
);