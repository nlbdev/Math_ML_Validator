// ============================================================
// Program.cs
// MathML Validator for EPUB files — Entry Point
//
// This program scans a fixed input folder for .epub files,
// validates their MathML content against a set of rules,
// and produces JSON and HTML reports for each file.
//
// Key behaviors:
//   - Reads optional rules.txt to filter which tests to run
//   - Skips EPUB files that already have a generated HTML summary
//   - Writes per-file JSON and HTML reports to output folders
//
// Split across:
//   - Program.cs          : entry point and orchestration
//   - Models.cs           : EpubReport and IssueEntry data classes
//   - RulesLoader.cs      : loads and filters test rules from rules.txt
//   - EpubValidator.cs    : validates a single ZIP entry for MathML issues
//   - XmlHelpers.cs       : XML serialization and text search utilities
//   - HtmlReportWriter.cs : HTML summary page generation
// ============================================================

using Math_ML_Validator;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Math_ML_Validator.Tests;

class Program
{
    // Fixed folders — paths used throughout the program for input, JSON output, and HTML output
    static string inputFolder = @"C:\mathml_validator";
    static string jsonFolder = @"C:\mathml_validator\report\json";
    static string htmlFolder = @"C:\mathml_validator\report\html";

    // Path to the optional rules file that controls which tests are active
    static string rulesFile = Path.Combine(inputFolder, "rules.txt");

    // Path to the optional settings file that can set the library/language/MathML version filter
    static string settingsFile = Path.Combine(inputFolder, "settings.txt");

    // Optional filters — run only the rules tagged with this Library / Language / MathML version.
    // null or empty = no filtering (previous behaviour: run every active rule).
    // Set here in code, or in settings.txt:  library = tibi / language = no / mathml_version = MathML3
    static string? filterLibrary = null;
    static string? filterLanguage = null;
    static string? filterMathMLVersion = null;

    /// <summary>
    /// Entry point. Loads optional rules, discovers EPUB files, validates each one,
    /// and writes JSON + HTML reports.
    /// </summary>
    /// <returns>Exit code: 0 = success, 2 = input folder missing</returns>
    static int Main()
    {
        // Optional settings.txt overrides for the library/language/MathML version filter
        RulesLoader.LoadFilterSettings(settingsFile, ref filterLibrary, ref filterLanguage, ref filterMathMLVersion);

        // Load active test rules from rules.txt, then keep only those matching the requested filters
        List<TestRule> ActiveTests = RulesLoader.Load(rulesFile, htmlFolder, filterLibrary, filterLanguage, filterMathMLVersion);

        // Verify the input folder exists before proceeding
        if (!Directory.Exists(inputFolder))
        {
            Console.WriteLine($"Input folder not found: {inputFolder}");
            return 2;
        }

        // Ensure output directories exist (creates them if they don't)
        Directory.CreateDirectory(jsonFolder);
        Directory.CreateDirectory(htmlFolder);

        // Discover all .epub files recursively under the input folder
        var epubFiles = Directory.EnumerateFiles(inputFolder, "*.epub", SearchOption.AllDirectories).ToList();
        if (!epubFiles.Any())
        {
            Console.WriteLine("No .epub files found.");
            return 0;
        }

        // Process each EPUB file individually
        foreach (var epubPath in epubFiles)
        {
            var fileName = Path.GetFileName(epubPath);
            var safeName = HtmlReportWriter.MakeSafeFileName(Path.GetFileNameWithoutExtension(fileName));
            var jsonPath = Path.Combine(jsonFolder, safeName + ".report.json");
            var htmlPath = Path.Combine(htmlFolder, safeName + "_summary.html");

            // If corresponding summary HTML already exists, skip validation
            if (File.Exists(htmlPath))
            {
                Console.WriteLine($"Skipping {fileName} because summary already exists: {htmlPath}");
                continue;
            }

            Console.WriteLine($"Processing: {fileName}");

            // Initialize a new report object for this EPUB
            var report = new EpubReport { FileName = fileName, FilePath = epubPath, Issues = new List<IssueEntry>() };

            try
            {
                // Open the EPUB (which is a ZIP archive) and iterate relevant document entries
                using var zip = ZipFile.OpenRead(epubPath);
                var entries = zip.Entries
                    .Where(e => !e.FullName.EndsWith("/") &&
                                (e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)
                                 || e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                                 || e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                // Validate each qualifying entry and collect any issues
                foreach (var entry in entries)
                {
                    try
                    {
                        var entryIssues = EpubValidator.ValidateEntry(entry, ActiveTests);
                        report.Issues.AddRange(entryIssues);
                    }
                    catch (Exception exEntry)
                    {
                        // If a single entry fails to validate, record the error and continue with others
                        report.Issues.Add(new IssueEntry
                        {
                            EntryPath = entry.FullName,
                            TestId = "entry-parse-error",
                            TestDesc = exEntry.Message,
                            Snippet = "",
                            ExactNode = "",
                            Line = 0,
                            Column = 0
                        });
                    }
                }
            }
            catch (Exception exZip)
            {
                // If the entire EPUB ZIP cannot be opened, record a top-level error for this file
                report.Issues.Add(new IssueEntry
                {
                    EntryPath = "",
                    TestId = "epub-open-error",
                    TestDesc = exZip.Message,
                    Snippet = "",
                    ExactNode = "",
                    Line = 0,
                    Column = 0
                });
            }

            try
            {
                // Serialize the report to indented JSON and write the HTML summary page
                var j = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonPath, j);
                HtmlReportWriter.WriteHtmlSummary(report, htmlPath);
                Console.WriteLine($"  JSON saved: {jsonPath}");
                Console.WriteLine($"  HTML saved: {htmlPath}");
            }
            catch (Exception exWrite)
            {
                Console.WriteLine($"Failed to write report for {fileName}: {exWrite.Message}");
            }
        }

        Console.WriteLine("All done.");
        return 0;
    }
}
