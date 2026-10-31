# Study Resource Finder — Assignments 5, 6, 7 and 8



## Team

| Name | Group | Implemented logic | Tests | Documentation |
|---|---|---|---|---|
| **Gulyayev Mikhail** | **IT-2512** | All of it: models, input validation, pipeline stages, policies, console interface | All automated tests | This README |

Individual work (team of 1).
https://github.com/Grintona/Assigment5IFP

## 1. Topic and workflow

**Topic:** Study Resource Finder, a console tool that recommends books for studying a topic.

## Assignment 7: explicit outcomes and exception boundaries

The Assignment 7 implementation keeps the existing pure pipeline and adds an explicit
`Result<T, E>` in `AssigmnetIFP/Models/Result.cs`. `Success` always contains its value and
`Failure` always contains a `DomainError`; there is no success case with a missing payload.
`Map` transforms a success, while `Bind` continues only after a success. Both propagate a
failure without invoking the supplied function.

The integrated chain is `BookPipeline.FindResourcesResult`: it validates the search, filters
books, calls the Result-returning `ScoreBooksResult` through `Bind`, and maps the scored list
to ranked recommendations. `ScoreBooksResult` rejects a policy that throws the expected
scoring exception or produces `NaN`/infinite output, so this `Bind` is a real continuation
that can fail rather than an `Ok(...)` wrapper around a total operation. Its expected domain
failures are:

| Code | Meaning |
|---|---|
| `MissingBooks` | no input collection was supplied |
| `InvalidMinimumYear` | the requested year is negative |
| `MissingPolicy` | no scoring policy was supplied |
| `ScoringRejected` | the selected policy cannot calculate a book score |
| `InvalidScore` | the selected policy returned `NaN` or infinity |
| `BookNotFound` | a lookup key has no matching book |
| `InvalidWorkflowState` | a completed or rejected workflow cannot run again |

`BookPipeline.FindByKey` is the explicit present/absent lookup. A missing book is a
`BookNotFound` failure; a real `EditionCount` of `0` remains a successful present value.
Validation errors describe invalid domain input, while workflow errors describe an operation
that is not allowed in the current state.

`OpenLibraryClient.LoadFromFileResult` is the operational boundary. It converts missing files,
malformed JSON, access errors, and invalid response shape into `DomainError` values. The file is
fully deserialized before any records are returned, so malformed content cannot partially update
the valid book collection. `Program.cs` handles the final Result case and displays the code and
message to the user.

**Intended users:** students who search for books on a subject and want only reasonably recent books, ordered either by popularity or by recency.

**Operation implemented completely:** *select books, score them with a chosen policy, produce an ordered list of recommendations.*

```
raw text/JSON ──► BookParser ──► Book (valid) ──► Stage 1 ──► Stage 2 ──► Stage 3 ──► Recommendation list ──► console
 (boundary: validation)                   (pure functional core)                          (output after computing)
```

### Input

Books come from one of two sources, chosen in the menu:

1. **Open Library API** (`https://openlibrary.org/search.json`, first 10 results for the query).
2. **A JSON file** in the same format (`AssigmnetIFP/Data/books.json`, used for the reproducible demonstration and for defense).

Plus one user value: **minimum publication year** (integer, `>= 0`).

### Input fields (one book record)

| JSON field | Required | Rule |
|---|---|---|
| `title` | yes | not empty or whitespace |
| `key` | yes | not empty or whitespace |
| `edition_count` | yes | integer `>= 0` (**missing is rejected**, it is not replaced by 0; an explicit `0` is valid) |
| `first_publish_year` | no | integer `>= 0`; missing means "year unknown" (stored as `null`) |
| `author_name` | no | list of strings; missing is stored as an **empty list** (documented rule) |
| `language` | no | list of strings; missing is stored as an **empty list** (documented rule) |

### Business rules

1. A book is **selected** only if its publication year is known and `year >= minimum year` (the threshold is **inclusive**).
2. Each selected book gets a **score** from the chosen policy:
   - **Popularity policy:** score = `edition_count`.
   - **Recency policy:** score = `first_publish_year`.
3. Books are **ordered by score descending**; equal scores are ordered by title (ordinal, ascending), so the output is deterministic.
4. The result is a list of `Recommendation(Rank, Book, Score)` with ranks `1..n`. An empty selection gives an empty result, printed as `(no results)`.

### Expected result

An ordered, ranked list of books. The output line format is:

```
<rank>. <title> | <authors, comma separated, or "unknown"> | year <year> | score <score>
```

## 2. Dataset and expected results (documented before running)

File: `AssigmnetIFP/Data/books.json` (7 records), minimum year **2000**.

| # | Title | Year | Editions | Expected |
|---|---|---|---|---|
| 1 | Old Book | 1999 | 5 | valid, **rejected by Stage 1** (1999 < 2000) |
| 2 | Boundary Book | **2000** | 10 | valid, **accepted: boundary value** (2000 = threshold, inclusive) |
| 3 | Popular Book | 2010 | 20 | accepted |
| 4 | Mid Book | 2015 | 8 | accepted |
| 5 | Recent Book | 2022 | 2 | accepted |
| 6 | Unknown Year | missing | 7 | valid, **rejected by Stage 1** (year unknown) |
| 7 | *(empty title)* | 2020 | 3 | **rejected by the parser** (`Title is missing.`) |

Loading summary: `Loaded 7 records: 6 valid, 1 rejected.`

**Popularity policy:**

| Rank | Title | Score |
|---|---|---|
| 1 | Popular Book | 20 |
| 2 | Boundary Book | 10 |
| 3 | Mid Book | 8 |
| 4 | Recent Book | 2 |

**Recency policy:**

| Rank | Title | Score |
|---|---|---|
| 1 | Recent Book | 2022 |
| 2 | Mid Book | 2015 |
| 3 | Popular Book | 2010 |
| 4 | Boundary Book | 2000 |

**The policy changes the result:** the first place is *Popular Book* under Popularity and *Recent Book* under Recency, on the same data and with the same pipeline code.

**Boundary:** with minimum year 2000 *Boundary Book* is included; with 2001 it is excluded.

**Empty result:** with minimum year 3000 nothing is selected and the output is `(no results)`.

## 3. Pipeline stages

All stages are in `AssigmnetIFP/Services/BookPipeline.cs`. They are pure functions: they do not read or write `Console`, files, the clock or random values, and they do not change their arguments. They return new immutable collections.

| Stage | Method | Input → Output | Rule | Example |
|---|---|---|---|---|
| 1. Select | `FilterBooks` | `IEnumerable<Book>`, `int minimumYear` → `IReadOnlyList<Book>` | keep books with a known year `>= minimumYear` | `Boundary Book` (2000), threshold 2000 → kept; `Old Book` (1999) → dropped |
| 2. Score | `ScoreBooks` | `IEnumerable<Book>`, `Func<Book,double>` → `IReadOnlyList<ScoredBook>` | call the policy delegate for each book | Popularity: `Boundary Book` → `ScoredBook(Boundary Book, 10.0)` |
| 3. Rank | `RankBooks` | `IEnumerable<ScoredBook>` → `IReadOnlyList<Recommendation>` | sort by score descending, then title; assign ranks | `Boundary Book` with score 10.0 in second place → `Recommendation(2, Boundary Book, 10.0)` |

`BookPipeline.FindResources(books, minimumYear, scoringPolicy)` composes them visibly:

```csharp
RankBooks(ScoreBooks(FilterBooks(books, minimumYear), scoringPolicy));
```

### Trace of one record (`Boundary Book`, Popularity, minimum year 2000)

| Step | Type | Value |
|---|---|---|
| JSON record | `BookDto` | `title="Boundary Book"`, `first_publish_year=2000`, `edition_count=10`, ... |
| After `BookParser.TryParseBook` | `Book` | `Book("Boundary Book", ["Author D"], 2000, 10, "/boundary", ["eng"])` |
| After Stage 1 | `Book` inside `IReadOnlyList<Book>` | kept, because `2000 >= 2000` |
| After Stage 2 | `ScoredBook` | `ScoredBook(Book, 10.0)` from `Policies.Popularity` |
| After Stage 3 | `Recommendation` | `Recommendation(Rank: 2, Book, Score: 10.0)` |

## 4. Configurable rule (higher-order function)

- **Higher-order function:** `BookPipeline.ScoreBooks(books, Func<Book,double> scoringPolicy)`. It **invokes the delegate** for every book: `scoringPolicy(b)`.
- **Two policies** for the same rule ("how a book is scored"): `Policies.Popularity` and `Policies.Recency` (`AssigmnetIFP/Services/Policies.cs`).
- **Selection happens outside the pure pipeline**, in the `switch` in `Program.cs`, and the chosen method is passed as an argument. There is no mutable global setting, and nothing is captured and modified during processing.
- Menu option **3. Both (compare)** runs both policies on the same dataset and shows different results.

## 5. Functional core and separation of input/output

- **Parse and validate at the boundary:** `BookParser` (`TryParseBook`, `ParseAll`, `TryParseMinYear`) converts raw `BookDto` and text to valid `Book` values. Invalid records are rejected with a clear message. Nothing is silently replaced with a default. Missing `edition_count` is rejected, `0` and an unknown year are valid values. A valid empty dataset is not an error.
- **Immutable models:** `Book`, `ScoredBook`, `Recommendation` are records. Collections inside `Book` are `ImmutableArray<string>`, copied when a book is created, so a caller cannot change them later.
- **Pure stages:** no mutation of arguments, null arguments are rejected with `ArgumentNullException`.
- **Input and output are outside the core:** `Program.cs`, `OpenLibraryClient` (HTTP and file reading) and `PrintBooks` (console output) run before or after the pipeline.

## 6. Requirement to implementation map

| Requirement | Files / methods |
|---|---|
| Typed immutable models | `Models/Book.cs`, `Models/ScoredBook.cs`, `Models/Recommendation.cs` |
| Raw data kept separate from valid data | `Models/BooksDTO.cs` (`BookDto`), `Models/Book.cs` |
| Validation at the boundary | `Models/BookParser.cs`: `TryParseBook`, `ParseAll`, `TryParseMinYear` |
| At least three pure stages, composed | `Services/BookPipeline.cs`: `FilterBooks`, `ScoreBooks`, `RankBooks`, `FindResources` |
| Higher-order function calling a delegate | `BookPipeline.ScoreBooks` (`scoringPolicy(b)`) |
| Two policies | `Services/Policies.cs`: `Popularity`, `Recency` |
| Policy chosen outside the pipeline | `Program.cs` (`switch (policyChoice)`) |
| Purity: I/O after computing | `Program.cs`, `Services/OpenLibraryClient.cs`, `Services/PrintBooks.cs` |
| Data source choice (API or file) | `Program.cs` menu, `OpenLibraryClient.SearchBooksAsync`, `OpenLibraryClient.LoadFromFileResult` |
| Sample dataset | `Data/books.json` |
| Automated tests | `Assignment1FP.Tests/UnitTest1.cs` (class `PipelineTests`) |

## 7. Tests

Framework: xUnit 2.9.3. File: `Assignment1FP.Tests/UnitTest1.cs`.

| Required case | Test | Expected |
|---|---|---|
| Normal workflow | `NormalWorkflow_ProducesDocumentedResult` | Popularity result is Popular, Boundary, Mid, Recent with scores 20, 10, 8, 2 and ranks 1 to 4 |
| Empty dataset | `EmptyDataset_ReturnsEmptyResult` | no errors, empty result, no crash |
| Boundary value | `BoundaryYear_IsIncluded_AndOneYearEarlierIsExcluded` | year 2000 included, 1999 excluded, unknown year excluded |
| Invalid input | `InvalidYear_IsRejected` (4 cases: `null`, `""`, `"abc"`, `"-5"`) | rejected with a message |
| Missing input | `MissingRequiredFields_AreRejected_NoFabricatedResult` | `null` record, missing title, key or `edition_count` are rejected, no book is created; `null` pipeline arguments throw `ArgumentNullException` |
| Two policies | `TwoPolicies_DifferOnSameInput` | Popularity: Popular, Boundary, Mid, Recent; Recency: Recent, Mid, Popular, Boundary |
| Purity and stages | `Stages_DoNotModifyCallerData` | each stage separately; input list, stage results and authors arrays remain unchanged |

27 automated tests, including Result composition, three domain failures, present and absent
lookup, skipped Map/Bind functions, missing file, malformed JSON, and workflow state failures.

**Latest Assignment 7 test outcome:** `Passed: 27, Failed: 0, Skipped: 0, Total: 27`

## 8. Build, run and test

- **.NET SDK:** 10.0 (`dotnet --version`), target framework `net10.0`.
- **Prerequisites:** the .NET SDK; internet access is needed only for the API option (the file option works offline).
- **Working directory:** the repository root, the folder that contains `Assignment5.slnx`, `AssigmnetIFP` and `Assignment1FP.Tests`.

```bash
dotnet build
dotnet test
dotnet run --project AssigmnetIFP
```

### Normal demonstration (file, both policies)

Run `dotnet run --project AssigmnetIFP` and enter:

| Prompt | Input |
|---|---|
| Choose an option | `2` |
| Enter file path | *(just Enter: default `Data/books.json`)* |
| Enter minimum publication year | `2000` |
| Enter your choice (policy) | `3` |

Expected output:

```
Loaded 7 records: 6 valid, 1 rejected.
  Rejected: Title is missing.
--- Popularity ---
1. Popular Book | Author B | year 2010 | score 20
2. Boundary Book | Author D | year 2000 | score 10
3. Mid Book | Author F | year 2015 | score 8
4. Recent Book | Author C | year 2022 | score 2

--- Recency ---
1. Recent Book | Author C | year 2022 | score 2022
2. Mid Book | Author F | year 2015 | score 2015
3. Popular Book | Author B | year 2010 | score 2010
4. Boundary Book | Author D | year 2000 | score 2000
```

### Failure and edge demonstrations

| Scenario | Input | Expected |
|---|---|---|
| Invalid year | option `2`, Enter, year `abc` | `Year must be an integer.` |
| Missing year | option `2`, Enter, empty year | `Year is missing.` |
| Negative year | option `2`, Enter, year `-5` | `Year cannot be negative.` |
| Empty search query | option `1`, empty query | `Search query cannot be empty.` |
| Missing file | option `2`, path `nope.json` | `Error reading file: ...` |
| Empty result | option `2`, Enter, year `3000`, policy `1` | `(no results)` |
| Boundary | option `2`, Enter, year `2001`, policy `1` | `Boundary Book` is not in the result |
| Open Library API | option `1`, query `programming c#`, year `2015` | results depend on the live API |

## 9. Limitations

- The API option requests only the first 10 results (`limit=10`), so after the year filter only a few books may remain. This is expected behaviour, not an error.
- Missing `author_name` / `language` are treated as empty lists, by design (see the field table).

## 10. Contribution

**Gulyayev Mikhail, It-2512:** designed the workflow and dataset, implemented the models, parser/validation, the three pipeline stages, both policies, the console interface and the API/file loading, wrote all automated tests and this documentation.

## 11. Assignment 6 — Handling Temporal Coupling

### Hidden dependency: unsafe before example

The old pipeline API made the prerequisite easy to miss:

```csharp
// Unsafe: raw values are accepted and the caller must remember the valid order.
var result = BookPipeline.FindResources(books, minimumYear, Policies.Recency);
```

If `minimumYear` is negative, or if a caller sends a null scoring policy, the operation is invalid only after the call begins. A caller could also call the stages in the wrong order, or run a completed workflow again, because no state represented those facts. The safe console path now creates a validated request first and transitions an explicit `Pending` state exactly once.

### Validated factory

`Models/ValidatedSearch.cs` contains `ValidatedSearch.Create`. Its constructor is private, so external callers cannot create a validated request without the factory. The factory checks that:

1. The book collection exists and contains no null records.
2. Every materialized book has a non-empty title and key, a non-negative edition count, and either an unknown or non-negative publication year.
3. The minimum publication year is non-negative.
4. A scoring policy exists.

It returns `ValidationResult<ValidatedSearch>.Valid` with an immutable book collection, or `ValidationResult<ValidatedSearch>.Invalid` with every reason. No partially constructed valid request escapes.

### Explicit cases and safe transitions

`Models/ValidationResult.cs` is a closed record hierarchy with `Valid(Value)` and `Invalid(Reasons)`. `Services/BookWorkflow.cs` defines the mutually exclusive workflow states:

- `Pending(ValidatedSearch)` means prerequisites were satisfied and the search may run.
- `Completed(Recommendations)` carries the result and cannot be completed again.
- `Rejected(Reason)` records a failed prerequisite or invalid sequence.

`BookWorkflow.Transition` uses an exhaustive switch expression. `Pending` produces a new `Completed` state, `Completed` rejects repeated operations, and `Rejected` remains unsuccessful. The console calls `ValidatedSearch.Create`, pattern-matches its result, then passes only the valid value into `Pending`.

### Demonstrations

- Success: load `Data/books.json`, enter minimum year `2000`, choose policy `1` or `2`.
- Invalid factory input: `ValidatedSearch.Create(books, -1, Policies.Popularity)` returns `Invalid` with `Minimum year cannot be negative.`
- Missing prerequisite: transitioning `Rejected("missing validation")` returns `Invalid` and cannot produce recommendations.
- Repeated operation: transitioning a `Completed` state returns `Invalid` with `The search has already been completed.`
- Boundary: year `2000` remains included; year `1999` is excluded.

The legacy three-stage functions remain pure and are covered by the Assignment 5 tests, but the active console workflow is `ValidatedSearch.Create` -> `Pending` -> `BookWorkflow.Transition` -> `Completed`.

### Assignment 6 requirement map

| Requirement | Implementation and tests |
|---|---|
| Unsafe before example | This section; active console path no longer uses raw workflow calls |
| Validated factory | `Models/ValidatedSearch.cs`, `ValidatedSearchFactory_AcceptsValidInput`, rejection tests |
| Restricted construction | Private `ValidatedSearch` constructor and immutable properties |
| Distinct cases | `Models/ValidationResult.cs`, `Services/BookWorkflow.cs`, `WorkflowCases_AreReachableAndPatternMatched` |
| Pattern matching in application path | `Program.cs` and `BookWorkflow.Transition` |
| Missing prerequisite | `FailedTransition_PreservesOriginalState` and rejected-state branch |
| Repeated operation | `RepeatedWorkflowOperation_IsRejected` |
| Boundary and normal operation | `BoundaryYear_IsIncluded_AndOneYearEarlierIsExcluded`, `NormalWorkflow_ProducesDocumentedResult` |
| State preservation | `FailedTransition_PreservesOriginalState`, `Stages_DoNotModifyCallerData` |

### Assignment 6 tests

The project now has 17 passing automated test cases. In addition to the earlier pipeline tests, the new tests cover factory success, invalid book values, negative-year rejection, missing books and policy, every declared state case, missing prerequisite, repeated operation, and preservation of the original pending state.

```bash
dotnet test Assignment1FP.Tests/Assignment1FP.Tests.csproj
```

Latest result: `Passed: 17, Failed: 0, Skipped: 0, Total: 17`.

=============================================================================================================================

## Assignment 6 — Task 1: Identify the Hidden Dependency

### Operation

The operation is processing books through `BookPipeline.FindResources()`, which filters, scores, and ranks books.

### Unsafe example


var book = new Book(
    "",
    ImmutableArray<string>.Empty,
    2020,
    -5,
    "",
    ImmutableArray<string>.Empty);

var result = BookPipeline.FindResources(
    new[] { book },
    2000,
    Policies.Popularity);


### Hidden prerequisite

The application should validate a book before using it in the workflow. The existing validation logic is implemented in `BookParser.TryParseBook()`.

However, the `Book` record has a publicly accessible constructor. A caller can create a `Book` directly and bypass the parser.

### Required order

The intended order is:

1. Receive raw book data.
2. Validate the data.
3. Create a validated book value.
4. Pass the validated value to the processing pipeline.
5. Produce the recommendations.

### What goes wrong

If a caller skips validation, a book with an empty title, an empty key, or a negative edition count can enter the pipeline. The pipeline does not enforce all the validation rules defined by the parser.

This can lead to invalid domain data being processed and included in the recommendation workflow.

=============================================================================================================================

# Assignment 7 — Exception Handling in Functional Programming

## Purpose

Assignment 7 adds explicit domain outcomes, missing-value handling, `Map` and `Bind`
composition, and exception handling at the file/API boundary. Expected business failures are
represented as values instead of being implemented with throw/catch logic.

## Result type

`AssigmnetIFP/Models/Result.cs` defines the following closed result type:

- `Result<T, E>.Success(T Value)` contains a successful value.
- `Result<T, E>.Failure(E Error)` contains an error and no success value.
- `Map` transforms the value only for `Success`.
- `Bind` continues with a function returning another `Result` only for `Success`.

`Map` and `Bind` propagate an existing failure and do not call their supplied functions after
an error. This prevents invalid or missing values from being passed to later workflow stages.

## Domain errors

`AssigmnetIFP/Models/DomainError.cs` stores an error code and a useful message. The application
uses these expected domain failures:

| Error code | Meaning |
|---|---|
| `MissingBooks` | The input collection is missing. |
| `InvalidMinimumYear` | The requested minimum publication year is negative. |
| `MissingPolicy` | No scoring policy was supplied. |
| `BookNotFound` | A lookup key does not match any book. |
| `ScoringRejected` | The selected scoring policy cannot calculate a score. |
| `InvalidScore` | A scoring policy returned `NaN` or infinity. |
| `InvalidWorkflowState` | The requested workflow operation is not allowed in the current state. |

The parser also returns structured errors for missing title, key, edition count, and invalid
publication data. A valid edition count of `0` is preserved as data and is not treated as a
missing value.

## Result pipeline and Bind

`Services/BookPipeline.cs` contains the integrated workflow:

```csharp
ValidatedSearch.CreateResult(books, minimumYear, scoringPolicy)
  .Bind(search => FilterBooksResult(search))
  .Bind(filtered => ScoreBooksResult(filtered, scoringPolicy!))
  .Map(RankBooks);
```

The first `Bind` continues only after search validation succeeds. The second `Bind` uses the
real fallible operation `ScoreBooksResult`. It returns `Failure` when the scoring policy throws
the expected `InvalidOperationException`, returns `NaN`, or returns infinity. Therefore this
`Bind` is not an artificial `Ok(...)` wrapper: it can reject the request and preserve the error
through the rest of the chain. When scoring fails, `RankBooks` is not called.

`Map(RankBooks)` is used for the final pure transformation from scored books to ranked
recommendations. The final `Result` is handled at the application boundary in `Program.cs`.

## Missing-value lookup

`BookPipeline.FindByKey` is an explicit lookup operation:

- a matching book returns `Result.Success<Book, DomainError>`;
- an absent key returns `Result.Failure<Book, DomainError>` with `BookNotFound`;
- an empty lookup key returns `MissingLookupKey`;
- a book with `EditionCount = 0` is still a present and valid book.

No fabricated book or `default(Book)` value is used to represent absence.

## Operational exception boundary

`OpenLibraryClient.LoadFromFileResult` is the boundary for JSON file operations. It catches and
converts the following operational problems into clear failures:

- missing file or directory;
- access denied;
- malformed JSON;
- invalid response structure;
- other file read errors.

The file is deserialized completely before the result is returned. Consequently, malformed
input cannot partially update the valid book collection. Operational failures are distinct from
domain failures: a malformed file is an input/environment problem, while an invalid year or an
unavailable score is a business-rule problem.

## Demonstrations

Run the application from the repository root:

```bash
dotnet run --project AssigmnetIFP
```

The following cases are required and implemented:

1. Complete success: load `Data/books.json`, enter year `2000`, and choose policy `1`.
2. Domain failure: enter a negative year or use an invalid scoring policy.
3. Present lookup: look up `/boundary`.
4. Missing lookup: look up `/does-not-exist` and receive `BookNotFound`.
5. Missing file: provide a path that does not exist and receive `FileNotFound`.
6. Malformed file: provide a JSON file with invalid JSON and receive `MalformedJson`.
7. Empty result: enter year `3000`; the workflow completes with no recommendations.

## Assignment 7 requirement map

| Requirement | Implementation |
|---|---|
| Explicit success and failure outcomes | `Models/Result.cs` |
| Useful structured errors | `Models/DomainError.cs` |
| Three or more domain failures | `ValidatedSearch.CreateResult`, `FindByKey`, `ScoreBooksResult`, `BookWorkflow.TransitionResult` |
| Explicit missing-value lookup | `BookPipeline.FindByKey` |
| `Map` transformation | `BookPipeline.FindResourcesResult` maps with `RankBooks` |
| Fallible `Bind` continuation | `BookPipeline.ScoreBooksResult` |
| Failure propagation and skipped functions | `Result_FailureSkipsMapAndBind`, `ResultChain_RejectsUnusableScoreAndSkipsRanking` |
| Missing and malformed file handling | `OpenLibraryClient.LoadFromFileResult` |
| Final boundary handling | `Program.cs` |
| Automated tests | `Assignment1FP.Tests/UnitTest1.cs` |

## Assignment 7 tests

The test project contains 28 passing tests. The Assignment 7 tests cover complete success,
three domain failures, present and absent lookup, legitimate zero values, successful `Map`,
successful `Bind`, failure propagation, skipped dependent functions, workflow-state rejection,
missing files, malformed JSON, and preservation of consistent state.

Run the tests with:

```bash
dotnet test Assignment1FP.Tests/Assignment1FP.Tests.csproj
```

Latest result: `Passed: 28, Failed: 0, Skipped: 0, Total: 28`.

## Assignment 7 contribution

**Gulyayev Mikhail, IT-2512:** implemented the `Result` and `DomainError` models, integrated
`Map` and fallible `Bind` into the book workflow, added explicit lookup outcomes, converted
file and JSON exceptions into boundary failures, wrote the Assignment 7 tests, and documented
the implementation and demonstrations.



# Assignment 8 — Extension Methods, LINQ, and Native Integration

## Overview and Contribution

| Name | Group | Implemented Logic | Tests | Documentation |
|---|---|---|---|---|
| **Gulyayev Mikhail** | **IT-2512** | Custom extension methods, two LINQ reports, curried scoring and partial application, deferred execution with `yield return`, safe stream disposal with `await using`, native assembly library integration into live API workflow | 16 new automated tests in `Assignment8Tests.cs` (45 tests total) | This Assignment 8 documentation |

Individual work (team of 1).

---

## Task 1: Custom Extension Methods

Implemented in `AssigmnetIFP/Services/BookExtensions.cs`:

### 1. `GetAge(this Book book, int referenceYear = 2026)`
- **Receiver Type:** `Book`
- **Return Type:** `int`
- **Expected Behaviour:** Computes `Math.Max(0, referenceYear - book.FirstPublishYear.Value)` when `FirstPublishYear` is present; returns `0` if unknown or null. Throws `ArgumentNullException` if the book receiver is null.
- **Why it belongs on `Book`:** The age of a book is an intrinsic domain property derived directly from its publication year and a reference point in time. Placing this on `Book` enables clean dot-syntax (`book.GetAge(2026)`) when preparing inputs for the native priority calculation.

### 2. `WithMinimumEditions(this IEnumerable<Book> books, int minEditions)`
- **Receiver Type:** `IEnumerable<Book>`
- **Return Type:** `IEnumerable<Book>`
- **Expected Behaviour:** Filters a sequence of books, yielding only items where `EditionCount >= minEditions`. Clamps negative thresholds to `0`. Throws `ArgumentNullException` if the sequence is null.
- **Why it belongs on `IEnumerable<Book>`:** Filtering collections of domain items is an idiomatic query pipeline operation. Placing this on `IEnumerable<Book>` allows seamless LINQ chaining (`books.WithMinimumEditions(5).Where(...)`) without wrapping the collection in artificial container types.

Both extension methods are called in the application (`Program.cs` and `LinqReports.cs`) and covered by automated tests in `Assignment8Tests.cs`.

---

## Task 2: Two LINQ Reports

Implemented in `AssigmnetIFP/Services/LinqReports.cs`, separated from Console UI and file output. Both operate on live API data (or loaded JSON data).

### Report 1: Author Publishing & Edition Aggregation Report
- **Purpose:** Analyze author output, collective edition reach, and career span across study resources.
- **Intended User:** Curriculum planners, researchers, and students identifying the most prolific and widely published authors on a subject.
- **Input:** `IEnumerable<Book>`, `int minimumYear`, `int minEditions = 0`.
- **Filters:**
  1. `WithMinimumEditions(minEditions)`: filters books by edition threshold.
  2. `Where`: selects books with `FirstPublishYear >= minimumYear` and non-empty author list.
- **Query Stages & LINQ Operators:**
  1. `WithMinimumEditions`: custom extension filter stage.
  2. `Where`: year and author presence filter.
  3. `SelectMany`: flattens `book.Authors` into `(Author, Book)` pairs so multi-author books contribute to each author.
  4. `GroupBy(x => x.Author)`: groups records by author name.
  5. `Select`: computes aggregations:
     - `BookCount`: `Count()`
     - `TotalEditions`: `Sum(x => x.Book.EditionCount)`
     - `EarliestYear`: `Min(x => x.Book.FirstPublishYear!.Value)`
     - `LatestYear`: `Max(x => x.Book.FirstPublishYear!.Value)`
  6. `OrderByDescending(r => r.TotalEditions)`: primary ordering by total editions descending.
  7. `ThenBy(r => r.Author, StringComparer.Ordinal)`: **deterministic tie-breaker** by author name ascending when total editions tie.
- **Output Fields:** Author Name, Book Count, Total Editions, Active Publication Span (earliest to latest year).
- **Expected Result:** A deterministically sorted list of `AuthorReportItem` records.

### Report 2: Curried & Native Priority Ranking Report
- **Purpose:** Provide a multi-faceted book recommendation report combining high-level configurable functional scoring with low-level native assembly priority calculation and decade grouping.
- **Intended User:** Students wanting custom-scored book recommendations alongside hardware-accelerated system priority scores and chronological distribution.
- **Input:** `IEnumerable<Book>`, `int minimumYear`, `Func<Book, double> curriedScorer`, `int referenceYear = 2026`.
- **Filters:** `Where(b => b.FirstPublishYear >= minimumYear)`.
- **Query Stages & LINQ Operators:**
  1. `Where`: filters books by publication year.
  2. `Select`: computes:
     - `book.GetAge(referenceYear)` via custom extension method.
     - Real native C/assembly calculation via `NativeAdapter.CalculatePrioritySafe(editions, ratings, age)`.
     - Configured functional score via `curriedScorer(book)`.
     - Decade grouping key `(year / 10) * 10`.
  3. `OrderByDescending(x => x.CurriedScore)`: primary ordering by curried functional score descending.
  4. `ThenBy(x => x.Book.Title, StringComparer.Ordinal)`: **deterministic tie-breaker** by title ascending when scores tie.
  5. `Select((x, index) => ...)`: projects into `BookPriorityReportItem` with rank `1..N`.
  6. Sub-aggregation: `GroupBy(item => item.Decade)` with `Count()` and `Average(item => (double)item.NativePriority)` to produce decade dynamics.
- **Output Fields:** Rank, Title, Primary Author, Publication Year, Decade, Curried Functional Score, Native Assembly Priority Score, and Decade Aggregates.

---

## Task 3: Configured Functions Through Currying

Implemented in `AssigmnetIFP/Services/CurriedScoring.cs`:

### Curried Function Signature
```
CreateScore : ScoringWeights -> (Book -> double)
```

In C#:
```csharp
public static Func<ScoringWeights, Func<Book, double>> CreateScore =>
    weights => book =>
    {
        int age = book.FirstPublishYear.HasValue
            ? Math.Max(0, weights.ReferenceYear - book.FirstPublishYear.Value)
            : 50;
        double recencyFactor = 100.0 / (age + 1.0);
        return (book.EditionCount * weights.EditionWeight) + (recencyFactor * weights.RecencyWeight);
    };
```

### Partial Application
Two configured functions are created by supplying the `ScoringWeights` configuration up-front:

```csharp
// Configuration 1: Popularity-Biased (heavily weights edition count)
public static readonly Func<Book, double> PopularityBiasedScorer =
    CreateScore(new ScoringWeights(EditionWeight: 1.5, RecencyWeight: 0.2));

// Configuration 2: Recency-Biased (heavily weights recent publication)
public static readonly Func<Book, double> RecencyBiasedScorer =
    CreateScore(new ScoringWeights(EditionWeight: 0.1, RecencyWeight: 2.5));
```

### Contrast on the Same Dataset
When evaluated on a dataset containing an older classic (e.g. 1950, 100 editions) and a recent modern book (e.g. 2025, 2 editions):
- `PopularityBiasedScorer` ranks the classic higher (due to 100 editions × 1.5).
- `RecencyBiasedScorer` ranks the recent book higher (due to high recency factor × 2.5).

Both functions are passed as arguments directly into workflows (`BookPipeline.FindResources` or `LinqReports.GeneratePriorityReport`) without changing any mutable global state.

---

## Task 4: Deferred Execution & Materialization

Implemented in `AssigmnetIFP/Services/DeferredExecution.cs`:

```csharp
public static IEnumerable<ScoredBook> StreamScoreBooks(
    IEnumerable<Book> books,
    Func<Book, double> scoringPolicy)
{
    ArgumentNullException.ThrowIfNull(books);
    ArgumentNullException.ThrowIfNull(scoringPolicy);

    foreach (var book in books)
    {
        double score = scoringPolicy(book);
        yield return new ScoredBook(book, score);
    }
}
```

### Verification with Test Probe Counter
In `Assignment8Tests.cs` (`DeferredExecution_DemonstratesLazyEvaluationAndSnapshotReuse`):
1. **Creation:** Calling `StreamScoreBooks(books, probe)` returns an enumerable; the probe counter is strictly `0`. No elements are processed.
2. **Deferred Iteration:** Calling `MoveNext()` on the enumerator advances evaluation by exactly 1 item; probe counter increments to `1`.
3. **Materialization Point:** Calling `.ToList()` creates an explicit in-memory snapshot, running the loop for all items in the sequence.
4. **Predictable Reuse:** Iterating the resulting `List<ScoredBook>` multiple times performs `0` additional evaluations. This prevents unexpected repeated calculations, file reads, or state changes.

---

## Task 5: Safe Resource Disposal

Implemented in `AssigmnetIFP/Services/ReportExporter.cs`:

### Asynchronous File Export with `await using`
```csharp
public static async Task<Result<string, DomainError>> ExportReportAsync(string filePath, string content)
{
    ...
    await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
    await using var writer = new StreamWriter(fileStream, Encoding.UTF8);
    await writer.WriteAsync(content);
    return Result<string, DomainError>.Ok(filePath);
}
```
- **Owner:** `ExportReportAsync`.
- **Release Point:** Exiting the `await using` scope immediately flushes and closes the stream and underlying OS file descriptor on both success and exception paths.
- **Error Handling:** Catches `UnauthorizedAccessException` and `IOException`, returning explicit `DomainError` failures (`FileAccessDenied`, `FileWriteFailed`).

### Safe Streaming Reader with `using` & `yield`
```csharp
public static IEnumerable<string> StreamLines(string filePath)
{
    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new StreamReader(fileStream, Encoding.UTF8);

    string? line;
    while ((line = reader.ReadLine()) != null)
    {
        yield return line;
    }
}
```
- In C#, `using` within an iterator method (`yield return`) generates an `IEnumerator` where `Dispose()` disposes the open stream.
- **Tested in `Assignment8Tests.cs`:** When an enumeration stops early (e.g. `StreamLines(path).Take(1).ToList()`), the file lock is immediately released, allowing subsequent exclusive-mode file access and deletion without `IOException`.

---

## Task 6: Review of Retention and Allocation (Resource Note)

### Materialization Points
- `BookParser.ParseAll`: materializes parsed DTOs into an `ImmutableArray<Book>`. This provides a stable, immutable dataset for multiple queries and workflow runs.
- `LinqReports.GenerateAuthorReport` and `GeneratePriorityReport`: call `.ToList()` at the end of their query pipelines to produce stable snapshots for display, ranking, and export.
- `DeferredExecution.StreamScoreBooks`: produces an `IEnumerable<ScoredBook>` that can be streamed for low memory consumption, or materialized via `.ToList()` when repeated indexing or ranking is required.

### Retained Data & Closures
- Deferred LINQ queries retain references to their source collections only until materialization occurs.
- Configured curried scorers capture their `ScoringWeights` in a lightweight closure (2 doubles and 1 integer). They do NOT capture or retain candidate `Book` collections.
- No static or long-lived caches, mutable collections, or event subscriptions are used. All transient pipeline objects are garbage-collected once the report or workflow finishes.

### Streaming vs Snapshot Trade-off
- **Streaming (`IEnumerable` with `yield return`):** Minimal memory footprint (`O(1)` space overhead per element) because items are processed one by one. Ideal for large datasets or single-pass processing. Trade-off: re-evaluating the enumerable re-executes calculations and file reads.
- **Snapshot (`List` / `ImmutableArray`):** Memory footprint is `O(N)` for the collection. Enables repeated iteration, random access, and deterministic sorting (`OrderBy`, `ThenBy`) without re-computation.
- **Design Decision:** The application streams parsing and scoring, but materializes at deterministic ranking boundaries (`OrderBy`) where all elements must be known to establish correct ranks.

---

## Tasks 7, 8 & 9: Native Assembly / C Library Integration

### 1. Function Contract
```c
uint64_t calculate_book_priority(uint64_t edition_count, uint64_t ratings_count, uint64_t book_age);
```
- **Inputs:**
  - `edition_count` (unsigned 64-bit integer, range 0..50,000)
  - `ratings_count` (unsigned 64-bit integer, >= 0)
  - `book_age` (unsigned 64-bit integer, range 0..1,000)
- **Output:** Unsigned 64-bit priority score (`uint64_t`).
- **Calculation Algorithm:**
  $$\text{score} = (\text{edition\_count} \times 10) + \lfloor\text{ratings\_count} / 2\rfloor - \text{book\_age}$$
  If the result is negative, it clamps to `0`.
- **Manually Checked Example:**
  - `edition_count = 12`, `ratings_count = 55`, `book_age = 15`:
  - $(12 \times 10) + \lfloor 55 / 2 \rfloor - 15 = 120 + 27 - 15 = 132$.

### 2. Implementation & Build Procedure
- **Source Files:**
  - `native/linux/library_linux.asm` (AMD64 assembly using System V ABI: `rdi`, `rsi`, `rdx`, return in `rax`).
  - `native/windows/library_windows.asm` (x64 assembly using Microsoft x64 calling convention: `rcx`, `rdx`, `r8`, return in `rax`).
- **Build Command:**
  ```bash
  # On Linux (builds libcalc.so):
  make -C native/linux

  # On Windows (builds libcalc.dll):
  make -C native/windows
  ```
- **Compiler Requirements:** `nasm` (Netwide Assembler) and `gcc` (GNU Compiler Collection).

### 3. C# P/Invoke Binding and Adapter
- **`NativeBridge.cs`:** Declares `[DllImport("calc", EntryPoint = "calculate_book_priority")]` and configures a dynamic `NativeLibrary.SetDllImportResolver` to resolve `libcalc.so` (Linux) or `libcalc.dll` (Windows) from the executing binary directory (`AppContext.BaseDirectory`).
- **`NativeAdapter.cs`:** Safe wrapper validating domain inputs before making the native call. Translates domain validation errors, `DllNotFoundException`, and `BadImageFormatException` into explicit `Result<ulong, string>` failures.

### 4. Integration into Real Workflow
The native function is invoked inside LINQ Report 2 (`LinqReports.GeneratePriorityReport`) for every candidate book retrieved from the API. The calculated native score is displayed in the report, used in decade aggregation averages, and exported to disk.

### 5. Integration Difficulties and Resolution
- **Dynamic Library Path Resolution:** Under `dotnet test`, the working directory differs from the test assembly's output directory. This was resolved using `NativeLibrary.SetDllImportResolver` combined with `AppContext.BaseDirectory` so that `libcalc.so` is located reliably regardless of current working directory.
- **ABI Differences:** Linux AMD64 passes the first three integer arguments in `rdi, rsi, rdx`, while Windows x64 uses `rcx, rdx, r8`. Separate platform implementations (`library_linux.asm` and `library_windows.asm`) ensure correct register usage.

---

## Complete Test Suite & Coverage

The test project now contains **45 automated tests** (29 from previous assignments + 16 new for Assignment 8).

### Required Coverage Verification

| Area | Requirement | Test Methods in `Assignment8Tests.cs` | Status |
|---|---|---|---|
| **REPORTS** | Normal results, empty dataset, no matches, tied sort values, grouping & totals | `AuthorReport_ComputesCorrectAggregatesAndDeterministicOrdering`<br>`AuthorReport_HandlesEmptyAndNoMatchingRecords`<br>`PriorityReport_IntegratesCurriedScorerAndNativePriority`<br>`PriorityReport_HandlesEmptyDataset_AndTiedScoresDeterministically` | **PASSED** |
| **EXTENSIONS** | Both custom extensions tested with meaningful assertions and used in code | `BookExtensions_GetAge_CalculatesCorrectAgeAndHandlesEdgeCases`<br>`BookExtensions_WithMinimumEditions_FiltersCorrectly` | **PASSED** |
| **CONFIGURED FUNCTIONS** | Two partially applied functions tested on identical input with different results | `CurriedScoring_TwoConfigurations_ProduceDifferentResultsOnSameInput` | **PASSED** |
| **DEFERRED EXECUTION** | No work on creation, work on iteration, materialization snapshot reuse | `DeferredExecution_DemonstratesLazyEvaluationAndSnapshotReuse` | **PASSED** |
| **RESOURCE LIFETIME** | Disposal on success and early stop (`Take(1)`); lock release | `ResourceDisposal_StreamLines_ReleasesFileLockOnEarlyStop`<br>`ResourceDisposal_ExportReport_WritesSafelyAndHandlesBoundaryError` | **PASSED** |
| **NATIVE INTEGRATION** | Real call matching manually expected value; controlled adapter failures | `CalculatePrioritySafe_ReturnsExpectedValue` (UnitTest1)<br>`NativeAdapter_ControlledFailures_ReturnClearResultFailures` | **PASSED** |
| **COMPLETE WORKFLOWS** | Normal workflow on API data; error path preserving state | `CompleteWorkflow_IntegratesApiDataWithReportsAndNativeCall`<br>`FailedTransition_PreservesOriginalState` | **PASSED** |
| **FILE BOUNDARIES** | Missing files, malformed JSON, and invalid export paths handled safely | `MissingFile_ReturnsOperationalFailureWithoutStateChange`<br>`MalformedFile_ReturnsFailureWithoutPartialUpdate`<br>`ResourceDisposal_ExportReport_WritesSafelyAndHandlesBoundaryError` | **PASSED** |

### Running Tests
```bash
# 1. Build the native shared library:
make -C native/linux

# 2. Run the full test suite:
dotnet test Assignment1FP.Tests/Assignment1FP.Tests.csproj
```
**Latest test outcome:** `Passed: 45, Failed: 0, Skipped: 0, Total: 45`.

---

## Defense Guide: What to Show During Individual Defense

1. **Run the Application on API Data:**
   ```bash
   dotnet run --project AssigmnetIFP
   ```
   - Choose Option `1`, enter search query (e.g. `functional programming` or `algorithms`), and enter minimum year (e.g. `2015`).
   - Run **Option 2** (LINQ Report 1): show author grouping, count, sum of editions, and deterministic tie-breaker sorting.
   - Run **Option 3** (LINQ Report 2): show curried functional score, real native assembly priority score, and decade summaries.
   - Run **Option 4** (Compare Configured Functions): show how `PopularityBiasedScorer` and `RecencyBiasedScorer` invert the ranking order on the exact same books.
   - Run **Option 5** (Export Reports): show safe export using `await using` to `reports/author_statistics_report.txt` and `reports/priority_native_report.txt`.
2. **Show Calls to Custom Extensions:**
   - In `LinqReports.cs`: show `books.WithMinimumEditions(minEditions)` and `b.GetAge(referenceYear)`.
   - Explain why `GetAge` belongs on `Book` and `WithMinimumEditions` belongs on `IEnumerable<Book>`.
3. **Show Curried Function & Partial Application:**
   - Show `CurriedScoring.CreateScore` in `Services/CurriedScoring.cs`.
   - Explain how `PopularityBiasedScorer` and `RecencyBiasedScorer` are created by fixing the `ScoringWeights` parameter.
4. **Demonstrate Deferred Execution & Safe Disposal:**
   - In `Assignment8Tests.cs`, walk through `DeferredExecution_DemonstratesLazyEvaluationAndSnapshotReuse` showing probe counter timing.
   - Walk through `ResourceDisposal_StreamLines_ReleasesFileLockOnEarlyStop` showing file lock release on early exit.
5. **Demonstrate Native Assembly Integration:**
   - Show `native/linux/library_linux.asm`, show `calculate_book_priority` export.
   - Show `NativeBridge.cs` P/Invoke declaration and `NativeAdapter.cs` safe validation wrapper.
   - Run `dotnet test --filter Native` to show real call verification ($12 \times 10 + 27 - 15 = 132$) and boundary rejection tests.
