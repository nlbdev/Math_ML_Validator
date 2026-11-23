// ============================================================
// RegressionCheck.cs
// Runs every rule in Tests.TESTS against the test EPUBs and compares
// the number of hits per rule with the counts in expected.txt.
//
// - Uses EpubValidator.ValidateEntry, the same function the real program uses.
// - Runs ALL rules: rules.txt and settings.txt are ignored.
// - Writes nothing: no JSON/HTML reports, C:\mathml_validator is not touched.
//
// Usage (from the project folder):
//   dotnet run --project tools\RegressionCheck
//   dotnet run --project tools\RegressionCheck -- [test folder] [options]
//
// Options:
//   --expected <file>  expected counts file (default: <test folder>\expected.txt)
//   --only <text>      only check EPUBs whose file name contains <text>
//   --details          also list every hit (line:column, rule, exact node)
//   --print            print the current counts in expected.txt format, no comparison
//
// Exit code: 0 = all passed, 1 = at least one FAIL, 2 = setup error.
// ============================================================

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Math_ML_Validator;

static class RegressionCheck
{
    /// <summary>One line of expected.txt: this EPUB should give exactly Count hits for Rule.</summary>
    record Expectation(string File, string Rule, int Count, int LineNo);

    /// <summary>Issue IDs produced by the validator itself rather than by a rule in Tests.TESTS.</summary>
    static readonly HashSet<string> BuiltInIds = new(StringComparer.Ordinal) { "entry-parse-error", "epub-open-error" };

    /// <summary>"&lt;file&gt;.epub   &lt;rule-id&gt;   &lt;count&gt;" — file names may contain spaces.</summary>
    static readonly Regex ExpectationLine = new(@"^(?<file>.+?\.epub)\s+(?<rule>\S+)\s+(?<count>\d+)$",
                                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static int passed, failed;

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // ---- Arguments ----------------------------------------------------
        string? testFolder = null, expectedFile = null, only = null;
        bool details = false, print = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--expected":
                case "--only":
                    if (i + 1 >= args.Length) { Console.WriteLine($"Missing value after {args[i]}"); return 2; }
                    if (args[i] == "--expected") expectedFile = args[++i]; else only = args[++i];
                    break;
                case "--details": details = true; break;
                case "--print": print = true; break;
                case "-h":
                case "--help": PrintUsage(); return 0;
                default:
                    if (args[i].StartsWith("-")) { Console.WriteLine($"Unknown option: {args[i]}"); PrintUsage(); return 2; }
                    testFolder = args[i];
                    break;
            }
        }

        testFolder ??= FindTestFolder();
        if (testFolder == null || !Directory.Exists(testFolder))
        {
            Console.WriteLine($"Test folder not found: {testFolder ?? "(could not find a 'test_files' folder)"}");
            Console.WriteLine("Pass it as the first argument, e.g. C:\\claude\\Math_ML_Validator\\test_files");
            return 2;
        }
        testFolder = Path.GetFullPath(testFolder);
        expectedFile = Path.GetFullPath(expectedFile ?? Path.Combine(testFolder, "expected.txt"));

        var epubs = Directory.GetFiles(testFolder, "*.epub")
                             .Where(p => only == null || Path.GetFileName(p).Contains(only, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                             .ToList();
        if (!epubs.Any())
        {
            Console.WriteLine($"No .epub files found in {testFolder}" + (only != null ? $" matching '{only}'" : ""));
            return 2;
        }

        // ---- Run the validator on every EPUB --------------------------------
        var actual = epubs.ToDictionary(p => Path.GetFileName(p), Validate, StringComparer.OrdinalIgnoreCase);

        var ruleOrder = Tests.TESTS.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        int OrderOf(string id) => ruleOrder.TryGetValue(id, out var i) ? i : int.MaxValue;

        if (print)
        {
            PrintCounts(actual, OrderOf);
            return 0;
        }

        // ---- Read expected.txt ----------------------------------------------
        if (!File.Exists(expectedFile))
        {
            Console.WriteLine($"Expected counts file not found: {expectedFile}");
            Console.WriteLine("Create it from the current results with:  dotnet run --project tools\\RegressionCheck -- --print");
            return 2;
        }
        var expectations = ReadExpectations(expectedFile, out var formatErrors);
        if (formatErrors.Any())
        {
            foreach (var e in formatErrors) Console.WriteLine(e);
            return 2;
        }

        var knownIds = new HashSet<string>(Tests.TESTS.Select(t => t.Id), StringComparer.Ordinal);
        knownIds.UnionWith(BuiltInIds);

        Console.WriteLine($"Regression check: {epubs.Count} EPUB(s), {Tests.TESTS.Count} rule(s)");
        Console.WriteLine($"  test folder : {testFolder}");
        Console.WriteLine($"  expected    : {expectedFile}");

        int ruleWidth = knownIds.Concat(expectations.Select(e => e.Rule)).Max(id => id.Length) + 2;

        // ---- Compare, file by file ------------------------------------------
        foreach (var (file, issues) in actual)
        {
            Console.WriteLine();
            Console.WriteLine(file);

            var counts = issues.GroupBy(i => i.TestId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var expectedHere = expectations.Where(e => string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase))
                                           .OrderBy(e => OrderOf(e.Rule)).ToList();

            foreach (var exp in expectedHere)
            {
                var got = counts.TryGetValue(exp.Rule, out var c) ? c : 0;
                if (!knownIds.Contains(exp.Rule))
                    Fail($"{exp.Rule.PadRight(ruleWidth)} unknown rule ID (typo, or rule removed?) - expected.txt line {exp.LineNo}");
                else if (got == exp.Count)
                    Pass($"{exp.Rule.PadRight(ruleWidth)} {got}");
                else
                    Fail($"{exp.Rule.PadRight(ruleWidth)} expected {exp.Count}, got {got}");
            }

            // Hits from rules that expected.txt does not mention for this file
            foreach (var (rule, got) in counts.OrderBy(kv => OrderOf(kv.Key)))
            {
                if (expectedHere.Any(e => e.Rule == rule)) continue;
                Fail($"{rule.PadRight(ruleWidth)} unexpected: {got} hit(s), not listed in expected.txt");
            }

            if (!expectedHere.Any() && !counts.Any())
                Pass("(no hits, as expected)");

            if (details) PrintDetails(issues, OrderOf);
        }

        // EPUBs listed in expected.txt that are not in the test folder
        foreach (var missing in expectations.Select(e => e.File).Distinct(StringComparer.OrdinalIgnoreCase)
                                            .Where(f => !actual.ContainsKey(f))
                                            .Where(f => only == null || f.Contains(only, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine();
            Console.WriteLine(missing);
            Fail("EPUB listed in expected.txt but not found in the test folder");
        }

        Console.WriteLine();
        Console.ForegroundColor = failed == 0 ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"Result: {passed} passed, {failed} failed");
        Console.ResetColor();
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// Validates every document entry of one EPUB, the same way Program.cs does,
    /// but with all rules and without writing any report.
    /// </summary>
    static List<IssueEntry> Validate(string epubPath)
    {
        var issues = new List<IssueEntry>();
        try
        {
            using var zip = ZipFile.OpenRead(epubPath);
            var entries = zip.Entries.Where(e => !e.FullName.EndsWith("/") &&
                                                 (e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)
                                                  || e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                                                  || e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)));
            foreach (var entry in entries)
            {
                try
                {
                    issues.AddRange(EpubValidator.ValidateEntry(entry, Tests.TESTS));
                }
                catch (Exception ex)
                {
                    issues.Add(new IssueEntry { EntryPath = entry.FullName, TestId = "entry-parse-error", TestDesc = ex.Message });
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(new IssueEntry { EntryPath = "", TestId = "epub-open-error", TestDesc = ex.Message });
        }
        return issues;
    }

    /// <summary>Reads expected.txt. Blank lines and text after '#' are ignored.</summary>
    static List<Expectation> ReadExpectations(string path, out List<string> errors)
    {
        var list = new List<Expectation>();
        errors = new List<string>();
        var lines = File.ReadAllLines(path, Encoding.UTF8);

        for (int n = 0; n < lines.Length; n++)
        {
            var line = lines[n].TrimStart('\uFEFF');
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash);
            line = line.Trim();
            if (line.Length == 0) continue;

            var m = ExpectationLine.Match(line);
            if (!m.Success)
            {
                errors.Add($"expected.txt line {n + 1}: cannot read \"{lines[n].Trim()}\" - use: <file>.epub  <rule-id>  <count>");
                continue;
            }

            var exp = new Expectation(m.Groups["file"].Value.Trim(), m.Groups["rule"].Value, int.Parse(m.Groups["count"].Value), n + 1);
            var dup = list.FirstOrDefault(e => e.Rule == exp.Rule && string.Equals(e.File, exp.File, StringComparison.OrdinalIgnoreCase));
            if (dup != null)
                errors.Add($"expected.txt line {n + 1}: {exp.File} / {exp.Rule} is already listed on line {dup.LineNo}");
            else
                list.Add(exp);
        }
        return list;
    }

    /// <summary>Prints the current hit counts in expected.txt format (for creating or updating a baseline).</summary>
    static void PrintCounts(Dictionary<string, List<IssueEntry>> actual, Func<string, int> orderOf)
    {
        int fileWidth = actual.Keys.Max(f => f.Length) + 2;
        int ruleWidth = Math.Max(30, actual.Values.SelectMany(v => v).Select(i => i.TestId.Length).DefaultIfEmpty(0).Max()) + 2;

        foreach (var (file, issues) in actual)
        {
            var groups = issues.GroupBy(i => i.TestId).OrderBy(g => orderOf(g.Key)).ToList();
            if (!groups.Any())
            {
                Console.WriteLine($"# {file}: no hits");
                continue;
            }
            foreach (var g in groups)
                Console.WriteLine($"{file.PadRight(fileWidth)}{g.Key.PadRight(ruleWidth)}{g.Count()}");
        }
    }

    /// <summary>Lists every hit of one EPUB: entry, line:column, rule and exact node (or error message).</summary>
    static void PrintDetails(List<IssueEntry> issues, Func<string, int> orderOf)
    {
        foreach (var i in issues.OrderBy(i => orderOf(i.TestId)).ThenBy(i => i.EntryPath).ThenBy(i => i.Line).ThenBy(i => i.Column))
        {
            var what = string.IsNullOrEmpty(i.ExactNode) ? i.TestDesc : i.ExactNode;
            what = Regex.Replace(what ?? "", @"\s+xmlns(:\w+)?=""[^""]+""", "");   // hide namespace noise
            if (what.Length > 140) what = what.Substring(0, 140) + "...";
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"        {Path.GetFileName(i.EntryPath)} {i.Line}:{i.Column}  {i.TestId}  {what}");
            Console.ResetColor();
        }
    }

    /// <summary>Looks for a 'test_files' folder in the current folder, the program folder, or any parent of them.</summary>
    static string? FindTestFolder()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "test_files");
                if (Directory.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    static void Pass(string message)
    {
        passed++;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  PASS  ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    static void Fail(string message)
    {
        failed++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  FAIL  ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project tools\\RegressionCheck -- [test folder] [--expected <file>] [--only <text>] [--details] [--print]");
        Console.WriteLine("  test folder        folder with the test EPUBs (default: the nearest 'test_files' folder)");
        Console.WriteLine("  --expected <file>  expected counts file (default: <test folder>\\expected.txt)");
        Console.WriteLine("  --only <text>      only check EPUBs whose file name contains <text>");
        Console.WriteLine("  --details          also list every hit with line:column and the exact node");
        Console.WriteLine("  --print            print the current counts in expected.txt format, without comparing");
    }
}
