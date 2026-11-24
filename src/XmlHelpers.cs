// ============================================================
// XmlHelpers.cs
// Utility methods for XML serialization, text searching,
// and line/column offset resolution.
//
// Contains:
//   - SafeOuterXml              : serialize XElement to a compact string
//   - GetContextFromParent      : extract a windowed context snippet
//   - TruncateCollapseWhitespace: collapse and truncate strings
//   - IndexOfNormalized         : whitespace-normalized string search
//   - GetLineColumnFromOffset   : convert char offset to line/column
//   - GetContextAncestor        : pick the snippet element for a rule hit
//   - MakeContext               : build the <Context> wrapper a rule can return
// ============================================================

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Utility methods for XML text extraction, searching, and position tracking.
/// </summary>
static class XmlHelpers
{
    /// <summary>
    /// Serializes an XElement to a single-line string, capped at maxLen characters.
    /// Removes newlines and carriage returns for compact display.
    /// Returns a safe fallback value if the element is null or serialization fails.
    /// </summary>
    /// <param name="el">The element to serialize</param>
    /// <param name="maxLen">Maximum character length before truncation (default 600)</param>
    public static string SafeOuterXml(XElement el, int maxLen = 600)
    {
        if (el == null) return "";
        try
        {
            var xml = el.ToString(SaveOptions.DisableFormatting)
                        .Replace("\r", "")
                        .Replace("\n", " ")
                        .Trim();
            if (xml.Length <= maxLen) return xml;
            return xml.Substring(0, maxLen) + "...";
        }
        catch
        {
            return el?.Name.LocalName ?? "";
        }
    }

    /// <summary>
    /// Extracts a context snippet from a parent element, windowed around the known child XML.
    /// If the parent XML is short enough, it returns it in full.
    /// Otherwise it finds the child's position and returns a substring around it.
    /// </summary>
    /// <param name="parent">The parent XElement (context container)</param>
    /// <param name="childOuter">The serialized string of the child element to locate</param>
    public static string GetContextFromParent(XElement parent, string childOuter)
    {
        try
        {
            if (parent == null) return childOuter;

            // Collapse the parent to a single line for searching
            var parentXml = parent.ToString(SaveOptions.DisableFormatting).Replace("\r", "").Replace("\n", " ").Trim();

            // If the parent XML is short, return it in full
            if (parentXml.Length <= 1000) return parentXml;

            // Try to locate the child within the parent and return a window around it
            var idx = parentXml.IndexOf(childOuter, StringComparison.Ordinal);
            if (idx >= 0)
            {
                int start = Math.Max(0, idx - 2);
                int len = Math.Min(600, childOuter.Length + 300);
                if (start + len > parentXml.Length) len = parentXml.Length - start;
                return parentXml.Substring(start, len);
            }

            // Child not found in parent — return the beginning of the parent XML
            return parentXml.Substring(0, 600);
        }
        catch { return childOuter; }
    }

    /// <summary>
    /// Collapses all internal whitespace runs to single spaces and truncates to max characters.
    /// Appends "..." if truncation occurs.
    /// </summary>
    /// <param name="s">The input string</param>
    /// <param name="max">Maximum output length (default 600)</param>
    public static string TruncateCollapseWhitespace(string s, int max = 600)
    {
        if (s == null) return "";
        var collapsed = Regex.Replace(s, @"\s+", " ").Trim();
        if (collapsed.Length <= max) return collapsed;
        return collapsed.Substring(0, max) + "...";
    }

    /// <summary>
    /// Searches for a needle string inside a haystack, after normalizing all whitespace in both.
    /// Returns the starting index in the normalized haystack, or -1 if not found.
    /// </summary>
    /// <param name="haystack">The source text to search in</param>
    /// <param name="needle">The text pattern to search for</param>
    public static int IndexOfNormalized(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return -1;
        var n = Regex.Replace(needle, @"\s+", " ").Trim();
        var h = Regex.Replace(haystack, @"\s+", " ").Trim();
        return h.IndexOf(n, StringComparison.Ordinal);
    }

    /// <summary>
    /// Converts a character offset in a multi-line string to a 1-based line and column number.
    /// Reads through the text line by line to find the offset position.
    /// </summary>
    /// <param name="text">The full source text</param>
    /// <param name="offset">The character offset to resolve</param>
    /// <param name="line">Output: 1-based line number</param>
    /// <param name="col">Output: 1-based column number</param>
    public static void GetLineColumnFromOffset(string text, int offset, out int line, out int col)
    {
        line = 1; col = 1;
        if (offset <= 0) return;

        int current = 0;
        using var reader = new StringReader(text);
        string l;
        int lineno = 1;
        while ((l = reader.ReadLine()) != null)
        {
            if (current + l.Length >= offset)
            {
                // Found the line containing the offset — compute column within it
                line = lineno;
                col = offset - current + 1;
                return;
            }
            current += l.Length + 1; // +1 for the newline character consumed by ReadLine
            lineno++;
        }

        // Offset is past the end of the content — report last line
        line = lineno;
        col = 1;
    }

    /// <summary>
    /// Chooses the element to show as the snippet for a rule hit:
    /// the node itself if it is &lt;math&gt;, otherwise the nearest &lt;math&gt; ancestor,
    /// otherwise the nearest &lt;p&gt; ancestor, otherwise the immediate parent.
    /// </summary>
    /// <param name="node">The node the rule flagged</param>
    public static XElement? GetContextAncestor(XElement? node)
    {
        if (node == null) return null;
        if (string.Equals(node.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase)) return node;
        var mathAnc = node.Ancestors().FirstOrDefault(a => string.Equals(a.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase));
        if (mathAnc != null) return mathAnc;
        var pAnc = node.Ancestors().FirstOrDefault(a => string.Equals(a.Name.LocalName, "p", StringComparison.OrdinalIgnoreCase));
        if (pAnc != null) return pAnc;
        return node.Parent;
    }

    /// <summary>
    /// Builds the &lt;Context&gt; wrapper that a rule returns when the problem is not one simple element
    /// (for example several sibling elements, or a text node). EpubValidator reads it like this:
    /// &lt;Context line=".." column=".."&gt;&lt;Snippet&gt;(copy of contextAncestor)&lt;/Snippet&gt;&lt;ExactNode&gt;(exactNode)&lt;/ExactNode&gt;&lt;/Context&gt;
    /// </summary>
    /// <param name="location">The node whose line/column is reported (element or text node)</param>
    /// <param name="contextAncestor">The element shown as snippet (copied); null = no snippet</param>
    /// <param name="exactNode">The node(s) shown as exact node; wrap several siblings in one element</param>
    public static XElement MakeContext(XObject location, XElement? contextAncestor, XElement exactNode)
    {
        var wrapper = new XElement("Context",
            contextAncestor != null ? new XElement("Snippet", new XElement(contextAncestor)) : null,
            new XElement("ExactNode", exactNode)
        );

        if (location is IXmlLineInfo li && li.HasLineInfo())
        {
            if (li.LineNumber > 0) wrapper.SetAttributeValue("line", li.LineNumber);
            if (li.LinePosition > 0) wrapper.SetAttributeValue("column", li.LinePosition);
        }

        return wrapper;
    }
}
