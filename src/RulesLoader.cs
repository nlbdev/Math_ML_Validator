// ============================================================
// RulesLoader.cs
// Responsible for loading and filtering active test rules.
//
// Reads the optional rules.txt file from the input folder and
// returns a filtered list of TestRule objects to run.
// Falls back to all available tests if the file is missing,
// empty, or contains no matching rule IDs.
//
// The resulting list can additionally be narrowed down to the
// rules tagged with a given Library, Language and/or MathML
// version, which can optionally be configured in settings.txt.
// ============================================================

using Math_ML_Validator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Math_ML_Validator.Tests;

/// <summary>
/// Loads active test rules from rules.txt, or falls back to all tests.
/// Also writes a reference rules.txt when none exists.
/// </summary>
static class RulesLoader
{
    /// <summary>
    /// How to treat rules that have no Library/Language set.
    ///   true  = untagged rules are generic and always run (default).
    ///   false = when a filter is given, only rules explicitly tagged with it run.
    /// </summary>
    const bool IncludeUntaggedRules = true;

    /// <summary>
    /// Determines which tests to run: first by rule ID (rules.txt), then by library/language/MathML version.
    /// A null/empty filter value means "do not filter on that field".
    /// </summary>
    /// <param name="rulesFile">Full path to the rules.txt file</param>
    /// <param name="htmlFolder">Folder path used when writing the default test list</param>
    /// <param name="library">Optional library to run, e.g. "tibi". Null/empty = every library.</param>
    /// <param name="language">Optional language to run, e.g. "no". Null/empty = every language.</param>
    /// <param name="mathmlVersion">Optional MathML version to run, e.g. "MathML3". Null/empty = every version.</param>
    /// <returns>The list of TestRule objects to run</returns>
    public static List<TestRule> Load(string rulesFile, string htmlFolder, string? library = null, string? language = null, string? mathmlVersion = null)
    {
        // Step 1 — existing behaviour: select rules by ID from rules.txt (or take all tests)
        var byId = LoadByIds(rulesFile, htmlFolder);

        // Step 2 — new behaviour: keep only the rules for the requested library/language/MathML version
        return Filter(byId, library, language, mathmlVersion);
    }

    /// <summary>
    /// Keeps only the rules matching the requested library, language and/or MathML version.
    /// A rule whose Library/Language/MathML_version is null or empty is treated as generic
    /// (see <see cref="IncludeUntaggedRules"/>).
    /// </summary>
    /// <param name="tests">The rules to filter</param>
    /// <param name="library">Requested library, or null/empty for no library filtering</param>
    /// <param name="language">Requested language, or null/empty for no language filtering</param>
    /// <param name="mathmlVersion">Requested MathML version, or null/empty for no version filtering</param>
    /// <returns>The rules that match all of the given filters</returns>
    public static List<TestRule> Filter(List<TestRule> tests, string? library, string? language, string? mathmlVersion)
    {
        // No filter requested — behave exactly as before and run everything selected so far
        if (string.IsNullOrWhiteSpace(library) && string.IsNullOrWhiteSpace(language) && string.IsNullOrWhiteSpace(mathmlVersion))
            return tests;

        var selected = tests
            .Where(t => Matches(t.Library, library)
                     && Matches(t.Language, language)
                     && Matches(t.MathML_version, mathmlVersion))
            .ToList();

        Console.WriteLine($"Filtering rules by library='{(string.IsNullOrWhiteSpace(library) ? "any" : library)}', " +
                          $"language='{(string.IsNullOrWhiteSpace(language) ? "any" : language)}', " +
                          $"mathml_version='{(string.IsNullOrWhiteSpace(mathmlVersion) ? "any" : mathmlVersion)}' — " +
                          $"running {selected.Count} of {tests.Count} rule(s).");

        if (!selected.Any())
            Console.WriteLine("Warning: no rule matched the requested library/language/MathML version — nothing will be checked.");

        return selected;
    }

    /// <summary>
    /// Case-insensitive match of a rule's tag against the requested value.
    /// The rule's value may be a comma separated list ("tibi,statped").
    /// </summary>
    /// <param name="ruleValue">The rule's Library, Language or MathML_version value (may be null/empty)</param>
    /// <param name="wanted">The requested value (null/empty means "match anything")</param>
    /// <returns>True when the rule should be kept</returns>
    static bool Matches(string? ruleValue, string? wanted)
    {
        if (string.IsNullOrWhiteSpace(wanted)) return true;             // no filter on this field
        if (string.IsNullOrWhiteSpace(ruleValue)) return IncludeUntaggedRules;  // untagged / generic rule

        return ruleValue.Split(',')
                        .Select(v => v.Trim())
                        .Any(v => string.Equals(v, wanted.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reads the optional settings file and, when present, overrides the library/language/MathML version filter.
    /// Format is one "key = value" per line, '#' starts a comment:
    ///     library        = tibi
    ///     language       = no
    ///     mathml_version = MathML3
    /// A missing file, a missing key or an empty value leaves the current value untouched.
    /// When the file does not exist, a commented-out template is written for reference.
    /// </summary>
    /// <param name="settingsFile">Full path to the settings.txt file</param>
    /// <param name="library">Library filter to set (unchanged when not present in the file)</param>
    /// <param name="language">Language filter to set (unchanged when not present in the file)</param>
    /// <param name="mathmlVersion">MathML version filter to set (unchanged when not present in the file)</param>
    public static void LoadFilterSettings(string settingsFile, ref string? library, ref string? language, ref string? mathmlVersion)
    {
        try
        {
            Console.WriteLine($"Checking for settings file: {settingsFile}");
            if (!File.Exists(settingsFile))
            {
                // No settings file — keep whatever was set in code and write a template for reference
                WriteFilterSettingsTemplate(settingsFile);
                return;
            }

            foreach (var rawLine in File.ReadAllLines(settingsFile, Encoding.UTF8))
            {
                var line = rawLine.Split('#')[0].Trim();   // allow '#' comments
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;                     // not a "key = value" line

                // Ignore '_' and '-' in keys so mathml_version / mathml-version / mathmlversion all work
                var key = line.Substring(0, eq).Trim().Replace("_", "").Replace("-", "");
                var value = line.Substring(eq + 1).Trim();
                if (string.IsNullOrEmpty(value)) continue; // empty value = no filter on this field

                if (string.Equals(key, "library", StringComparison.OrdinalIgnoreCase)) library = value;
                else if (string.Equals(key, "language", StringComparison.OrdinalIgnoreCase)) language = value;
                else if (string.Equals(key, "mathmlversion", StringComparison.OrdinalIgnoreCase)) mathmlVersion = value;
            }

            Console.WriteLine($"Loaded settings.txt — library='{library}', language='{language}', mathml_version='{mathmlVersion}'.");
        }
        catch (Exception exSettings)
        {
            // If reading the settings file fails, fall back gracefully to no library/language/MathML version filter
            Console.WriteLine($"Failed to read settings.txt — continuing without a library/language/MathML version filter. Error: {exSettings.Message}");
        }
    }

    /// <summary>
    /// Writes a commented-out settings.txt template so the available keys are discoverable.
    /// </summary>
    /// <param name="settingsFile">Full path to write the settings.txt file</param>
    static void WriteFilterSettingsTemplate(string settingsFile)
    {
        try
        {
            using var tw = new StreamWriter(settingsFile, false, Encoding.UTF8);
            tw.WriteLine("# Optional filter: run only the rules tagged with this library / language / MathML version.");
            tw.WriteLine("# Remove the leading '#' and set a value to activate it.");
            tw.WriteLine("# Missing or empty = no filtering on that field (all rules run).");
            tw.WriteLine("#library        = tibi");
            tw.WriteLine("#language       = no");
            tw.WriteLine("#mathml_version = MathML3");
            tw.Flush();
            Console.WriteLine($"Settings template written: {settingsFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write settings template: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines which tests to run by reading the rules.txt file.
    /// If the file does not exist, all tests are run and a template rules.txt is written.
    /// If the file exists but is empty or has no matching IDs, all tests are run.
    /// </summary>
    /// <param name="rulesFile">Full path to the rules.txt file</param>
    /// <param name="htmlFolder">Folder path used when writing the default test list</param>
    /// <returns>The filtered (or full) list of TestRule objects to run</returns>
    static List<TestRule> LoadByIds(string rulesFile, string htmlFolder)
    {
        // Attempt to read optional rules.txt in inputFolder root
        try
        {
            //var rulesFile = Path.Combine(inputFolder, "rules.txt");
            Console.WriteLine($"Checking for rules file: {rulesFile}");
            if (!File.Exists(rulesFile))
            {
                // No rules file found — run all available tests and write the test list for reference
                Console.WriteLine("No rules.txt found; running all tests.");
                WriteTestsList(rulesFile, TESTS);
                return TESTS;
            }

            if (File.Exists(rulesFile))
            {
                // Read the rules file, strip inline '#' comments, trim whitespace, and skip blank lines
                var lines = File.ReadAllLines(rulesFile, Encoding.UTF8)
                                .Select(l => l.Split('#')[0]) // allow '#' comments
                                .Select(l => l.Trim())
                                .Where(l => !string.IsNullOrEmpty(l))
                                .ToList();

                if (lines.Any())
                {
                    // Build a case-insensitive set of allowed rule IDs and filter the full test list
                    var allowed = new HashSet<string>(lines, StringComparer.OrdinalIgnoreCase);
                    var selected = TESTS.Where(t => allowed.Contains(t.Id)).ToList();

                    if (selected.Any())
                    {
                        // At least one matching rule found — use only those tests
                        Console.WriteLine($"Loaded rules.txt — running {selected.Count} rule(s).");
                        return selected;
                    }
                    else
                    {
                        // Rules file had entries but none matched known test IDs — fall back to all tests
                        Console.WriteLine("Warning: rules.txt found but none of the listed rule IDs matched available tests. Running all tests.");
                        return TESTS;
                    }
                }
                else
                {
                    // Rules file exists but is empty — run all tests
                    Console.WriteLine("rules.txt is empty; running all tests.");
                    return TESTS;
                }
            }
        }
        catch (Exception exRules)
        {
            // If reading the rules file fails for any reason, fall back gracefully to all tests
            Console.WriteLine($"Failed to read rules.txt — continuing with all tests. Error: {exRules.Message}");
        }

        return TESTS;
    }

    /// <summary>
    /// Writes all available test rule IDs and descriptions to the rules.txt file.
    /// This creates a reference/template file when no rules.txt exists yet.
    /// Each entry is preceded by a comment line with the rule description.
    /// </summary>
    /// <param name="rulesFile">Full path to write the rules.txt file</param>
    /// <param name="tests">The collection of test rules to write out</param>
    public static void WriteTestsList(string rulesFile, IEnumerable<TestRule> tests)
    {
        try
        {
            using var tw = new StreamWriter(rulesFile, false, Encoding.UTF8);
            foreach (var t in tests)
            {
                // Show the rule's library/language tags as comments (informational only)
                var tags = (string.IsNullOrWhiteSpace(t.Library) ? "" : "# library: " + t.Library + "\n")
                         + (string.IsNullOrWhiteSpace(t.Language) ? "" : "# language: " + t.Language + "\n")
                         + (string.IsNullOrWhiteSpace(t.MathML_version) ? "" : "# mathml_version: " + t.MathML_version + "\n");

                // Write each test as a commented description block followed by its rule ID
                tw.WriteLine( "#*******" + (t.Description+ "*******" ?? "")+"\n"+ tags + t.Id +"\n");
            }
            tw.Flush();
            Console.WriteLine($"Tests list written: {rulesFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write tests list: {ex.Message}");
        }
    }
}
