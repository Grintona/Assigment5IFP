using System.Collections.Immutable;

namespace StudyResourceFinder.Models;

public enum StudyPlanDifficulty
{
    Beginner,
    Intermediate,
    Advanced
}

public sealed record StudyModule(
    int WeekNumber,
    Book Resource,
    int WeeklyReadingHours,
    string LearningObjective);

public sealed record StudyPlan(
    string Topic,
    StudyPlanDifficulty Difficulty,
    ImmutableArray<StudyModule> Modules,
    int TotalReadingHours,
    string RecommendedPacing);
