// ============================================================
// Models.cs
// Data model classes used across the MathML Validator.
//
// Contains:
//   - EpubReport  : top-level report for a single EPUB file
//   - IssueEntry  : a single MathML validation issue
// ============================================================

using System.Collections.Generic;

/// <summary>
/// Represents the validation report for a single EPUB file.
/// </summary>
class EpubReport
{
    /// <summary>The original file name of the EPUB (e.g. "book.epub")</summary>
    public string FileName { get; set; }

    /// <summary>The full file system path to the EPUB file</summary>
    public string FilePath { get; set; }

    /// <summary>All validation issues found across the EPUB's entries</summary>
    public List<IssueEntry> Issues { get; set; }
}

/// <summary>
/// Represents a single MathML validation issue found within an EPUB entry.
/// </summary>
class IssueEntry
{
    /// <summary>The path of the entry within the EPUB ZIP where the issue was found</summary>
    public string EntryPath { get; set; }

    /// <summary>The rule ID that triggered this issue (e.g. "math-001")</summary>
    public string TestId { get; set; }

    /// <summary>Human-readable description of the violated rule</summary>
    public string TestDesc { get; set; }

    /// <summary>A surrounding context snippet of XML around the problematic node</summary>
    public string Snippet { get; set; }

    /// <summary>The exact serialized XML of the node that failed the rule</summary>
    public string ExactNode { get; set; }

    /// <summary>1-based line number of the issue within the entry file</summary>
    public int Line { get; set; }

    /// <summary>1-based column number of the issue within the entry file</summary>
    public int Column { get; set; }
}
