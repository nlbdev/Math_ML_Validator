
# MathML EPUB Validator

This application scans EPUB files and validates MathML content based on customizable rule sets. It generates structured JSON reports and human‑readable HTML summaries.

## Features
- Reads EPUB files from a predefined folder
- Extracts and validates MathML from HTML/XHTML/XML entries
- Supports optional rule filtering using `rules.txt`
- Outputs JSON and HTML validation reports

## Processing Flow
1. Optionally load rule definitions from `rules.txt`.
2. Enumerate all EPUB files recursively in the input directory.
3. Open each EPUB as a ZIP archive.
4. Parse HTML/XHTML/XML files.
5. Run MathML validation rules.
6. Produce JSON and HTML summary reports.

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
