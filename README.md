
# MathML EPUB Validator

This application scans EPUB files and validates MathML content based on customizable rule sets. It generates structured JSON reports and human‑readable HTML summaries.

## Features
- Reads EPUB files from a predefined folder
- Extracts and validates MathML from HTML/XHTML/XML entries
- Supports optional rule filtering using `rules.txt`
- Supports running only the rules tagged with a given library, language and/or MathML version
- Outputs JSON and HTML validation reports

## Processing Flow
1. Optionally load rule definitions from `rules.txt`.
2. Enumerate all EPUB files recursively in the input directory.
3. Open each EPUB as a ZIP archive.
4. Parse HTML/XHTML/XML files.
5. Run MathML validation rules.
6. Produce JSON and HTML summary reports.

## Setup
Instructions on how to download and run the validator can be found here: [How to download and run the validator](https://github.com/nlbdev/Math_ML_Validator/blob/master/doc/build-csharp-from-github.md).

## Usage
Place EPUB files inside the folder:
```
C:/mathml_validator
```
Run the program. Reports will be saved to:
```
C:/mathml_validator/report/json
C:/mathml_validator/report/html
```

### Running only some rules
Each rule may optionally be tagged with a `Library`, `Language` and/or `MathML_version` in `Tests.cs`:
```csharp
new TestRule {
    Id = "math-wrong-number-of-children-3",
    Library = "tibi",
    Language = "no",
    MathML_version = "MathML3",
    ...
}
```
To run only the matching rules, set the filter in `settings.txt` next to the EPUB files
(`C:/mathml_validator/settings.txt`). A commented-out template is written automatically on the first run:
```
library        = tibi
language       = no
mathml_version = MathML3
```
Notes:
- Without a settings file, or with all values empty, every active rule runs, exactly as before.
- Any combination of the three keys can be used; each one is optional.
- The values can also be set directly in code (`filterLibrary` / `filterLanguage` / `filterMathMLVersion` in `Program.cs`);
  `settings.txt` overrides them when it supplies a non-empty value.
- Matching is case-insensitive, and a rule may list several values: `MathML_version = "MathML3,MathML4"`.
- A rule with no `Library`/`Language`/`MathML_version` is treated as generic and runs for every filter.
  To make untagged rules be skipped when a filter is given, set `IncludeUntaggedRules = false` in `RulesLoader.cs`.
- The library/language/MathML version filter is applied *after* the `rules.txt` ID filter, so the two can be combined.

## Regression check
`tools/RegressionCheck` is a small separate program for developers. It runs **every** rule in `Tests.cs` on the
test EPUBs in `test_files` and compares the number of hits per rule with `test_files/expected.txt`.
It compiles the validator's own `src` files (all except `Program.cs`) and uses the same `EpubValidator.ValidateEntry`
as the real program. It ignores `rules.txt` and `settings.txt`, writes no reports and does not touch `C:/mathml_validator`.

Run it from the project folder:
```
dotnet run --project tools/RegressionCheck
```
Each expected line prints `PASS` or `FAIL`. The exit code is 0 when everything passes and 1 when something fails.
A rule that gives hits on an EPUB without being listed for it in `expected.txt` is also a `FAIL`, so a new rule
that breaks an old one shows up.

`expected.txt` has one line per EPUB and rule (file names may contain spaces; `#` starts a comment):
```
test_issue_9.epub    math-units-mathvariant   7
```
Lines starting with `# PLANNED` hold the counts for rules that are not implemented yet.

Options (after `--`):
- `--only <text>`: check only the EPUBs whose file name contains the text, e.g. `--only test_issue_23`
- `--details`: list every hit with entry, line:column and the exact node
- `--print`: print the current counts in `expected.txt` format without comparing (to create or update the baseline)
- `--expected <file>`: use another expected counts file
- a folder path: use another folder of test EPUBs

Workflow for a new rule: add the rule to `Tests.cs`, remove `# PLANNED` from its line in `expected.txt`
(or add a line), run the check, then add the rule ID to `C:/mathml_validator/rules.txt` so the real program runs it.
