// ============================================================
// HtmlReportWriter.cs
// Generates the HTML summary report for a validated EPUB file.
//
// Produces a self-contained HTML page that groups validation
// issues by rule, shows occurrence counts, and renders
// collapsible MathML snippets with highlighted exact nodes.
//
// Also contains file-name sanitization and HTML escaping helpers.
// ============================================================

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Writes the HTML validation summary report for a single EPUB.
/// </summary>
static class HtmlReportWriter
{
    /// <summary>
    /// Writes a self-contained HTML summary page for a single EPUB validation report.
    /// Groups issues by TestId + TestDesc, and shows collapsible occurrence lists with
    /// highlighted MathML snippets. Strips xmlns declarations from snippets for readability.
    /// </summary>
    /// <param name="report">The validation report data to render</param>
    /// <param name="outPath">File path to write the HTML output to</param>
    public static void WriteHtmlSummary(EpubReport report, string outPath)
    {
        // Group issues by their rule ID and description for display
        var grouped = report.Issues
            .GroupBy(i => new { i.TestId, i.TestDesc })
            .ToList();

        // minimal helper to strip xmlns declarations
        string StripXmlns(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return Regex.Replace(s, @"\s+xmlns(:\w+)?=""[^""]+""", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        using var tw = new StreamWriter(outPath, false);

        // Write the HTML document header with embedded CSS styling
        tw.WriteLine("<!doctype html>");
        tw.WriteLine("<html lang=\"en\">");
        tw.WriteLine("<head>");
        tw.WriteLine("  <meta charset=\"utf-8\"/>");
        tw.WriteLine($"  <title>Validation summary for {HtmlEscape(report.FileName)}</title>");
        tw.WriteLine("  <style>");
        tw.WriteLine("    body { font-family: Arial, Helvetica, sans-serif; margin: 20px; }");
        tw.WriteLine("    h1 { font-size: 1.2rem; }");
        tw.WriteLine("    .group { margin-bottom: 18px; }");
        tw.WriteLine("    .group h2 { font-size: 1rem; margin: 6px 0; background:#31473A; color:#EDF4F2;    }");
        tw.WriteLine("    .count { color:#EDF4F2; font-size: 0.9rem; }");
        tw.WriteLine("    .issue { margin: 10px 0; padding: 8px; background: #cad6f2; border: 1px solid #eee; }");
        tw.WriteLine("    .entry { font-family: monospace; font-size: 0.9rem; color: #222; }");
        tw.WriteLine("    details { margin-top: 6px; }");
        tw.WriteLine("    summary { cursor: pointer; }");
        tw.WriteLine("    .snippet { margin-top:8px; padding:8px; background:#fff; border:1px solid #ddd; overflow:auto; }");
        tw.WriteLine("    .node { display:inline-block; padding:4px; border:2px solid #c33; background:#fff0f0; color:#900; border-radius:4px; }");
        tw.WriteLine("    code { white-space: pre-wrap; font-family: Consolas, 'Courier New', monospace; font-size:0.9rem; }");
        tw.WriteLine("  </style>");
        tw.WriteLine("</head>");
        tw.WriteLine("<body>");

        // Page header with file path and total issue count
        tw.WriteLine($"  <h1>Validation summary for {HtmlEscape(report.FileName)}</h1>");
        tw.WriteLine($"  <p><strong>Path:</strong> {HtmlEscape(report.FilePath)}</p>");
        tw.WriteLine($"  <p><strong>Total issues:</strong> {report.Issues.Count}</p>");
        tw.WriteLine("  <hr/>");

        if (!report.Issues.Any())
        {
            // No issues found — display a simple success message
            tw.WriteLine("  <p>No MathML validation issues found.</p>");
        }
        else
        {
            // Render each group of issues as a collapsible section
            foreach (var g in grouped)
            {
                var testId = HtmlEscape(g.Key.TestId);
                var testDesc = HtmlEscape(g.Key.TestDesc);
                var count = g.Count();

                tw.WriteLine("  <div class=\"group\">");
                tw.WriteLine($"    <h2>{testId} <span class=\"count\">- {count} occurrence(s)</span></h2>");
                tw.WriteLine($"    <p class=\"rule\">Rule:- <strong>{testDesc}</strong></p>");
                tw.WriteLine("    <details>");
                tw.WriteLine("      <summary>Show occurrences</summary>");
                int idx = 0;
                foreach (var issue in g)
                {
                    idx++;
                    tw.WriteLine("      <div class=\"issue\">");
                    tw.WriteLine($"        <div><strong>#{idx}</strong> Entry: <span class=\"entry\">{HtmlEscape(issue.EntryPath)}</span></div>");
                    tw.WriteLine($"        <div>Location: line {issue.Line}, column {issue.Column}</div>");

                    // Minimal change: strip xmlns only from Snippet and ExactNode before escaping/display
                    var rawContext = StripXmlns(issue.Snippet);
                    var rawExact = StripXmlns(issue.ExactNode);

                     var context = HtmlEscape(rawContext);
                    //var context = HtmlEscape(issue.Snippet);// no xmlns stripping here

                    // Remove wrapper and math root tags from the exact node before display
					rawExact = rawExact.Replace("<wrapper>", "").Replace("</wrapper>", "").Trim();
                    rawExact = rawExact.Replace("<math>", "").Replace("</math>", "").Trim();
                    var exact = HtmlEscape(rawExact); // this will display without xmlns
                    //var exact = HtmlEscape(issue.ExactNode); // this will display xmlns if present, 

                    // Build the rendered HTML for the snippet block:
                    // Append the exact node below the context, highlighted in a red-bordered span
                    string rendered;
                    /*
                    if (!string.IsNullOrEmpty(exact) && context.Contains(exact))
                    {
                        rendered = context.Replace(exact, $"<span class=\"node\">{exact}</span>");
                    }
                    else if
                    */
                    if
                    (!string.IsNullOrEmpty(context))
                    {
                        rendered = context + "<br/><span class=\"node\">" + exact + "</span>";
                    }
                    else
                    {
                        rendered = "<span class=\"node\">" + exact + "</span>";
                    }

                    tw.WriteLine("        <div class=\"snippet\"><code>");
                    tw.WriteLine(rendered);
                    tw.WriteLine("        </code></div>");

                    tw.WriteLine("      </div>");
                }
                tw.WriteLine("    </details>");
                tw.WriteLine("  </div>");
            }
        }

        tw.WriteLine("</body>");
        tw.WriteLine("</html>");
        tw.Flush();
    }

    /// <summary>
    /// Replaces any characters that are invalid in file names with underscores.
    /// Used to create safe output file names from EPUB file names.
    /// </summary>
    /// <param name="name">The raw file name (without extension)</param>
    /// <returns>A file-system-safe version of the name</returns>
    public static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    /// <summary>
    /// Escapes special HTML characters in a string to prevent XSS and rendering issues.
    /// Handles: &amp; &lt; &gt; &quot; &#39;
    /// </summary>
    /// <param name="s">The raw string to escape</param>
    /// <returns>HTML-safe version of the string, or empty string if null</returns>
    public static string HtmlEscape(string s)
    {
        if (s == null) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    }
}
