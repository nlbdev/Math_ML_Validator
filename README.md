
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
