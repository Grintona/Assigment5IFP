using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public static class StudyPlanWorkflow
{

    public static IReadOnlyList<Book> EnforceAuthorDiversity(
        IEnumerable<Book> books,
        int maxCountPerAuthor,
        Func<Book, string>? primaryAuthorSelector = null)
    {
        ArgumentNullException.ThrowIfNull(books);
        if (maxCountPerAuthor <= 0) maxCountPerAuthor = 1;

        var selector = primaryAuthorSelector ?? (b => b.Authors.IsDefaultOrEmpty ? "Unknown" : b.Authors[0]);
        var authorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = ImmutableArray.CreateBuilder<Book>();

        foreach (var book in books)
        {
            string author = selector(book);
            int current = authorCounts.GetValueOrDefault(author, 0);
            if (current < maxCountPerAuthor)
            {
                authorCounts[author] = current + 1;
                result.Add(book);
            }
        }

        return result.ToImmutable();
    }

    public static IReadOnlyList<Book> FilterCurriculumResources(
        IEnumerable<Book> books,
        string topic,
        StudyPlanDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(books);

        int minYear = difficulty switch
        {
            StudyPlanDifficulty.Beginner => 1990,
            StudyPlanDifficulty.Intermediate => 2005,
            StudyPlanDifficulty.Advanced => 2015,
            _ => 2000
        };

        return books
            .Where(b => b.FirstPublishYear.HasValue && b.FirstPublishYear.Value >= minYear)
            .Where(b => string.IsNullOrWhiteSpace(topic) ||
                        b.Title.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                        b.Authors.Any(a => a.Contains(topic, StringComparison.OrdinalIgnoreCase)))
            .ToImmutableArray();
    }

    public static IReadOnlyList<Book> ConstrainCurriculum(
        IEnumerable<Book> books,
        Func<IEnumerable<Book>, IReadOnlyList<Book>> constraintStrategy)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(constraintStrategy);
        return constraintStrategy(books);
    }

    public static IReadOnlyList<StudyModule> ScheduleModules(
        IReadOnlyList<Book> selectedBooks,
        StudyPlanDifficulty difficulty,
        int targetWeeks)
    {
        ArgumentNullException.ThrowIfNull(selectedBooks);
        if (selectedBooks.Count == 0 || targetWeeks <= 0)
            return ImmutableArray<StudyModule>.Empty;

        var modules = ImmutableArray.CreateBuilder<StudyModule>();
        int booksToUse = Math.Min(selectedBooks.Count, targetWeeks);

        for (int i = 0; i < booksToUse; i++)
        {
            var book = selectedBooks[i];
            int week = i + 1;

            var (hours, objective) = difficulty switch
            {
                StudyPlanDifficulty.Beginner =>
                    (4, $"Week {week}: Foundational concepts & introductory reading for '{book.Title}'"),

                StudyPlanDifficulty.Intermediate =>
                    (8, $"Week {week}: Practical implementation & case study analysis of '{book.Title}'"),

                StudyPlanDifficulty.Advanced =>
                    (12, $"Week {week}: In-depth critical synthesis & architecture evaluation of '{book.Title}'"),

                _ => (6, $"Week {week}: General study of '{book.Title}'")
            };

            modules.Add(new StudyModule(week, book, hours, objective));
        }

        return modules.ToImmutable();
    }

    public static Result<StudyPlan, DomainError> GenerateStudyPlanResult(
        IEnumerable<Book>? books,
        string topic,
        StudyPlanDifficulty difficulty,
        int targetWeeks,
        int maxBooksPerAuthor = 2)
    {
        if (books is null)
            return Result<StudyPlan, DomainError>.Fail(
                new DomainError("MissingBooks", "Books collection is required to build a study plan."));

        if (targetWeeks <= 0 || targetWeeks > 52)
            return Result<StudyPlan, DomainError>.Fail(
                new DomainError("InvalidTargetWeeks", $"Target weeks must be between 1 and 52. Received: {targetWeeks}."));

        var eligible = FilterCurriculumResources(books, topic, difficulty);
        if (eligible.Count == 0)
        {
            return Result<StudyPlan, DomainError>.Fail(
                new DomainError("EmptyStudyPlan", $"No eligible books found for topic '{topic}' at difficulty level '{difficulty}'."));
        }

        var constrained = ConstrainCurriculum(eligible, b => EnforceAuthorDiversity(b, maxBooksPerAuthor));

        var modules = ScheduleModules(constrained, difficulty, targetWeeks);
        if (modules.Count == 0)
        {
            return Result<StudyPlan, DomainError>.Fail(
                new DomainError("SchedulingFailed", "Could not schedule any modules for the requested plan."));
        }

        string pacingSummary = difficulty switch
        {
            StudyPlanDifficulty.Beginner => "Light pace (4 hrs/week) — focused on clarity and core mastery.",
            StudyPlanDifficulty.Intermediate => "Moderate pace (8 hrs/week) — balanced theory and practical assignments.",
            StudyPlanDifficulty.Advanced => "Rigorous pace (12 hrs/week) — deep technical exploration and research.",
            _ => "Standard pace"
        };

        int totalHours = modules.Sum(m => m.WeeklyReadingHours);
        var plan = new StudyPlan(topic, difficulty, modules.ToImmutableArray(), totalHours, pacingSummary);

        return Result<StudyPlan, DomainError>.Ok(plan);
    }
}
