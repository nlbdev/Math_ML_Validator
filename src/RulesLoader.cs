// ============================================================
// RulesLoader.cs
// Responsible for loading and filtering active test rules.
//
// Reads the optional rules.txt file from the input folder and
// returns a filtered list of TestRule objects to run.
// Falls back to all available tests if the file is missing,
// empty, or contains no matching rule IDs.
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
    /// Determines which tests to run by reading the rules.txt file.
    /// If the file does not exist, all tests are run and a template rules.txt is written.
    /// If the file exists but is empty or has no matching IDs, all tests are run.
    /// </summary>
    /// <param name="rulesFile">Full path to the rules.txt file</param>
    /// <param name="htmlFolder">Folder path used when writing the default test list</param>
    /// <returns>The filtered (or full) list of TestRule objects to run</returns>
    public static List<TestRule> Load(string rulesFile, string htmlFolder)
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
                // Write each test as a commented description block followed by its rule ID
                tw.WriteLine( "#*******" + (t.Description+ "*******" ?? "")+"\n"+ t.Id +"\n");
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
