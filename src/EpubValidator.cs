// ============================================================
// EpubValidator.cs
// Validates a single EPUB ZIP archive entry for MathML issues.
//
// Opens each XHTML/HTML/XML entry in the EPUB, parses it as XML,
// and runs all active test rules against it, collecting any
// issues into a list of IssueEntry objects.
// ============================================================

using Math_ML_Validator;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Validates MathML content within individual EPUB ZIP archive entries.
/// </summary>
static class EpubValidator
{
    // MathML XML namespace constant used when querying XML elements
    static readonly XNamespace m = "http://www.w3.org/1998/Math/MathML";

    /// <summary>
    /// Validates all MathML elements within a single ZIP archive entry (XHTML/HTML/XML file).
    /// Runs each active test rule against the parsed document and collects issues.
    /// </summary>
    /// <param name="entry">The ZIP archive entry to validate</param>
    /// <param name="activeTests">The list of test rules to run against this entry</param>
    /// <returns>A list of issues found in this entry</returns>
    public static List<IssueEntry> ValidateEntry(ZipArchiveEntry entry, List<TestRule> activeTests)
    {
        var issues = new List<IssueEntry>();

        // Read the full text content of the entry
        using var s = entry.Open();
        using var sr = new StreamReader(s);
        var content = sr.ReadToEnd();

        // Quick pre-check: skip entries that contain no MathML at all
        if (!content.Contains("<math", StringComparison.OrdinalIgnoreCase)
            && !content.Contains(m.NamespaceName, StringComparison.OrdinalIgnoreCase))
            return issues;

        // Parse the content as XML, ignoring DTD declarations, and preserving line info for error reporting
        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using var xr = XmlReader.Create(new StringReader(content), settings);
            doc = XDocument.Load(xr, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            // If parsing fails entirely, record the parse error and return early
            issues.Add(new IssueEntry
            {
                EntryPath = entry.FullName,
                TestId = "entry-parse-error",
                TestDesc = ex.Message,
                Snippet = "",
                ExactNode = "",
                Line = 0,
                Column = 0
            });
            return issues;
        }

        // NOTE: use activeTests (possibly filtered from rules.txt) instead of all TESTS
        foreach (var rule in activeTests)
        {
            // Run the rule's checker function; fall back to empty if it throws
            IEnumerable<XElement> matches = Enumerable.Empty<XElement>();
            try
            {
                matches = rule.Checker(doc) ?? Enumerable.Empty<XElement>();
            }
            catch
            {
                matches = Enumerable.Empty<XElement>();
            }

            // For each matched element, extract location info and build an IssueEntry
            foreach (var el in matches)
            {
                // default values
                string outer = "";
                int line = 0, col = 0;
                string snippet = "";

                // If the rule returned a Context wrapper, use its Snippet and ExactNode children and attributes
                if (string.Equals(el.Name.LocalName, "Context", StringComparison.OrdinalIgnoreCase))
                {
                    // Prefer attributes on Context for line/column
                    var la = el.Attribute("line") ?? el.Attribute("Line");
                    var ca = el.Attribute("column") ?? el.Attribute("Column");
                    if (la != null && int.TryParse(la.Value, out var laVal)) line = laVal;
                    if (ca != null && int.TryParse(ca.Value, out var caVal)) col = caVal;

                    // Try to extract Snippet and ExactNode children
                    var snippetElem = el.Element("Snippet")?.Elements().FirstOrDefault();
                    var exactElem = el.Element("ExactNode")?.Elements().FirstOrDefault();

                    // If ExactNode present, use its outer xml as ExactNode
                    if (exactElem != null)
                    {
                        outer = XmlHelpers.SafeOuterXml(exactElem);
                    }

                    // If Snippet present, use it for snippet (serialize and window around exact if possible)
                    if (snippetElem != null)
                    {
                        var ctxOuter = XmlHelpers.SafeOuterXml(snippetElem, 2000);
                        if (!string.IsNullOrEmpty(outer))
                        {
                            // Try to find the exact node within the snippet and extract a windowed substring around it
                            var idx = ctxOuter.IndexOf(outer, StringComparison.Ordinal);
                            if (idx >= 0)
                            {
                                int start = Math.Max(0, idx - 50);
                                int len = Math.Min(800, outer.Length + 100);
                                if (start + len > ctxOuter.Length) len = ctxOuter.Length - start;
                                snippet = ctxOuter.Substring(start, len);
                            }
                            else
                            {
                                // Exact node not found in snippet context — use full context (capped at 800 chars)
                                snippet = ctxOuter.Length <= 800 ? ctxOuter : ctxOuter.Substring(0, 800);
                            }
                        }
                        else
                        {
                            // No exact node — use the full snippet context (capped at 800 chars)
                            snippet = ctxOuter.Length <= 800 ? ctxOuter : ctxOuter.Substring(0, 800);
                        }
                    }

                    // If either ExactNode or Snippet missing, fall back to old behavior below
                    if (string.IsNullOrEmpty(outer) || string.IsNullOrEmpty(snippet))
                    {
                        // fallback: treat entire Context as outer for offset lookup/snippet
                        outer = XmlHelpers.SafeOuterXml(el);
                        if (line == 0 && col == 0)
                        {
                            // No line info from attributes — compute from raw text offset
                            int offset = XmlHelpers.IndexOfNormalized(content, outer);
                            if (offset >= 0) XmlHelpers.GetLineColumnFromOffset(content, offset, out line, out col);
                        }
                        snippet = XmlHelpers.TruncateCollapseWhitespace(XmlHelpers.GetContextFromParent(el.Parent, outer), 800);
                    }
                    else
                    {
                        // Truncate and normalize whitespace in the snippet
                        snippet = XmlHelpers.TruncateCollapseWhitespace(snippet, 800);
                    }
                }
                else
                {
                    // Old behavior (no Context wrapper) -- determine outer, line/col, snippet from parent
                    outer = XmlHelpers.SafeOuterXml(el);
                    if (el is IXmlLineInfo li && li.HasLineInfo())
                    {
                        // Use line info embedded in the parsed XML node
                        line = li.LineNumber;
                        col = li.LinePosition;
                    }
                    else
                    {
                        // No embedded line info — compute from raw text offset
                        int offset = XmlHelpers.IndexOfNormalized(content, outer);
                        if (offset >= 0) XmlHelpers.GetLineColumnFromOffset(content, offset, out line, out col);
                    }

                    // Extract surrounding parent context for the snippet display
                    snippet = XmlHelpers.GetContextFromParent(el.Parent, outer);
                    snippet = XmlHelpers.TruncateCollapseWhitespace(snippet, 800);
                }

                // Add IssueEntry
                issues.Add(new IssueEntry
                {
                    EntryPath = entry.FullName,
                    TestId = rule.Id,
                    TestDesc = rule.Description,
                    Snippet = snippet,
                    ExactNode = XmlHelpers.TruncateCollapseWhitespace(outer, 600),
                    Line = line,
                    Column = col
                });
            }
        }

        return issues;
    }
}
