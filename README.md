# Study Resource Finder — Assignment 5 (Composing Functions Using Pipelining)



## Team

| Name | Group | Implemented logic | Tests | Documentation |
|---|---|---|---|---|
| **<Gulyayev Mikhail** | **IT-2512** | All of it: models, input validation, pipeline stages, policies, console interface | All automated tests | This README |

Individual work (team of 1).

## 1. Topic and workflow

**Topic:** Study Resource Finder, a console tool that recommends books for studying a topic.

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
| Data source choice (API or file) | `Program.cs` menu, `OpenLibraryClient.SearchBooksAsync`, `OpenLibraryClient.LoadFromFile` |
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

7 test methods, 9 test runs (one theory with 4 cases).

**Latest test outcome:** `<paste the last line of "dotnet test" here, e.g. "Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9">`

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
