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

namespace Math_ML_Validator
{
    public class TestRule
    {
        public string Id { get; init; }
        public string Description { get; init; }
        public Func<XDocument, IEnumerable<XElement>> Checker { get; init; }

        // Optional tags read by RulesLoader when filtering which rules to run.
        public string? Library { get; init; }
        public string? Language { get; init; }
        public string? MathML_version { get; init; }
    }

    public static class Tests
    {
        

        public static readonly List<TestRule> TESTS = new[]
        {

            new TestRule {
            Id = "math-wrong-number-of-children-2",
            Description = "Wrong number of children. There should be two children.",
            Checker = doc => {
                XNamespace m = "http://www.w3.org/1998/Math/MathML";
                var operatorLocalNames = new HashSet<string> {
                    "mfrac", "msup", "msub", "mroot", "mover", "munder"
                };

                // Find any <math> (namespaced or not) that contains one of the operator elements
                // (namespaced or not) with exactly one child element.
                return doc
                    .Descendants()                                   // search all elements
                    .Where(e => e.Name.LocalName == "math")         // find <math> regardless of namespace
                    .Where(math => math.Descendants()
                                        .Any(elem =>
                                            operatorLocalNames.Contains(elem.Name.LocalName)
                                            && elem.Elements().Count() == 1
                                        )
                    );
            }
        },


            new TestRule {
                Id = "math-wrong-number-of-children-3",
                Description = "Wrong number of children. There should be three children.",
                Checker = doc => {
                    XNamespace m = "http://www.w3.org/1998/Math/MathML";
                    var operatorLocalNames = new HashSet<string> { "munderover", "msubsup" };

                    return doc
                        .Descendants()                                 // consider all elements so namespace doesn't matter
                        .Where(e => e.Name.LocalName == "math")       // match <math> with or without namespace
                        .Where(math => math.Descendants()
                                            .Any(elem =>
                                                operatorLocalNames.Contains(elem.Name.LocalName)
                                                && elem.Elements().Count() < 3
                                            )
                              );
                }
            },

            new TestRule
            {
                Id = "math-number-in-mtext",
                Description = "Detect <mtext> nodes that contain numeric tokens which should be <mn> or split so numbers become <mn>. Exclude <mtext> inside <mtd intent='equation-label'>, and exclude pure equation labels in parentheses like (3) or (3.1). For Norwegian processing, optionally ignore dot-decimals.",
                Checker = doc =>
                {
                    // contains any digit
                    var containsDigit = new Regex(@"\d", RegexOptions.CultureInvariant);

                    // treat only parenthesized labels as equation labels: "(3)" or "(3.1)"
                    var parenthesizedLabel = new Regex(@"^\s*\(\s*\d+(?:[.,]\d+)?\s*\)\s*$", RegexOptions.CultureInvariant);

                    // number token (integer or decimal with comma or dot)
                    var numberToken = new Regex(@"\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant);

                    // Optional: for Norwegian content, set to true to ignore tokens with a dot as decimal separator
                    var ignorePeriodAsDecimalForNorwegian = false;

                    return doc.Descendants()
                              .Where(el => el.Name.LocalName == "mtext")
                              .Where(mtext =>
                              {
                                  var txt = (mtext.Value ?? string.Empty);
                                  if (string.IsNullOrWhiteSpace(txt)) return false;

                                  // skip if inside <mtd intent='equation-label'>
                                  if (mtext.Ancestors()
                                           .Any(a => a.Name.LocalName == "mtd" &&
                                                     string.Equals((string)a.Attribute("intent"), "equation-label", StringComparison.OrdinalIgnoreCase)))
                                      return false;

                                  // skip parenthesized equation labels only
                                  if (parenthesizedLabel.IsMatch(txt)) return false;

                                  // must contain at least one digit
                                  if (!containsDigit.IsMatch(txt)) return false;

                                  // Norwegian rule: if configured, ignore numbers that look like dot-decimals
                                  if (ignorePeriodAsDecimalForNorwegian)
                                  {
                                      var dotDecimal = new Regex(@"\d+\.\d+", RegexOptions.CultureInvariant);
                                      if (dotDecimal.IsMatch(txt)) return false;
                                  }

                                  // If there's a numeric token anywhere, flag the element.
                                  return numberToken.IsMatch(txt);
                              });
                }
            },

            new TestRule {
                Id = "math-single-mn",
                Description = "math that have exactly one child element and that child element is <mn>.",
                Checker = doc => doc
                    .Descendants()                                  // find all elements
                    .Where(e => e.Name.LocalName == "math")        // match <math> regardless of namespace
                    .Where(math => math.Elements().Count() == 1
                                   && math.Elements().First().Name.LocalName == "mn")
            },

            
            new TestRule
            {
                Id = "math-decimal-comma-split",
                Description = "Detect markup where a decimal written with a comma is split into <mn>,<mo>,<mn> (e.g. <mn>1</mn><mo>,</mo><mn>2</mn>). Flag only exact two-number comma pairs and exclude cases inside parentheses/brackets/braces or longer comma-separated lists (sets, vectors, coordinates).",

                Checker = doc =>
                {
                    bool IsMnNumber(XElement el)
                    {
                        if (el == null) return false;
                        if (!string.Equals(el.Name.LocalName, "mn", StringComparison.OrdinalIgnoreCase)) return false;
                        var txt = (el.Value ?? string.Empty).Trim();
                        return Regex.IsMatch(txt, @"^[0-9]+$");
                    }

                    bool IsCommaMo(XElement el)
                    {
                        if (el == null) return false;
                        if (!string.Equals(el.Name.LocalName, "mo", StringComparison.OrdinalIgnoreCase)) return false;
                        var txt = (el.Value ?? string.Empty).Trim();
                        return txt == ",";
                        //return txt == "," || txt == "\u002C"; //used in orignal test
                    }

                    bool HasMfencedAncestor(XElement el)
                    {
                        var p = el.Parent;
                        while (p != null)
                        {
                            if (string.Equals(p.Name.LocalName, "mfenced", StringComparison.OrdinalIgnoreCase)) return true;
                            p = p.Parent;
                        }
                        return false;
                    }

                    bool IsDirectlyBracketedByMo(XElement mn, XElement next2)
                    {
                        var prevEl = mn.ElementsBeforeSelf().LastOrDefault();
                        if (prevEl != null && string.Equals(prevEl.Name.LocalName, "mo", StringComparison.OrdinalIgnoreCase))
                        {
                            var v = (prevEl.Value ?? string.Empty).Trim();
                            if (v == "(" || v == "[" || v == "{") return true;
                        }

                        var nextEl = next2.ElementsAfterSelf().FirstOrDefault();
                        if (nextEl != null && string.Equals(nextEl.Name.LocalName, "mo", StringComparison.OrdinalIgnoreCase))
                        {
                            var v = (nextEl.Value ?? string.Empty).Trim();
                            if (v == ")" || v == "]" || v == "}") return true;
                        }

                        return false;
                    }

                    // Updated GetContextAncestor per request:
                    // - If the selected node itself is a <math>, return it as the context ancestor.
                    // - Otherwise return the nearest ancestor that is <math>; if none found, fall back to nearest <p>, then immediate parent.
                    XElement GetContextAncestor(XElement node)
                    {
                        if (node == null) return null;

                        // If the node itself is <math>, use it directly
                        if (string.Equals(node.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase))
                            return node;

                        // Prefer nearest ancestor <math>
                        var ancestorMath = node.Ancestors().FirstOrDefault(a => string.Equals(a.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase));
                        if (ancestorMath != null) return ancestorMath;

                        // If no math ancestor, prefer <p>
                        var ancestorP = node.Ancestors().FirstOrDefault(a => string.Equals(a.Name.LocalName, "p", StringComparison.OrdinalIgnoreCase));
                        if (ancestorP != null) return ancestorP;

                        // Fallback to immediate parent
                        return node.Parent;
                    }

                    var offenders = new List<XElement>();

                    foreach (var mn in doc.Descendants().Where(e => string.Equals(e.Name.LocalName, "mn", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!IsMnNumber(mn)) continue;

                        var next1 = mn.ElementsAfterSelf().FirstOrDefault();
                        if (next1 == null || !IsCommaMo(next1)) continue;

                        var next2 = next1.ElementsAfterSelf().FirstOrDefault();
                        if (next2 == null || !IsMnNumber(next2)) continue;

                        // exclude longer lists: immediate comma before or after
                        var prevEl = mn.ElementsBeforeSelf().LastOrDefault();
                        if (prevEl != null && string.Equals(prevEl.Name.LocalName, "mo", StringComparison.OrdinalIgnoreCase) && ((prevEl.Value ?? "").Trim() == ",")) continue;

                        var afterEl = next2.ElementsAfterSelf().FirstOrDefault();
                        if (afterEl != null && string.Equals(afterEl.Name.LocalName, "mo", StringComparison.OrdinalIgnoreCase) && ((afterEl.Value ?? "").Trim() == ",")) continue;

                        // exclude bracketed forms
                        if (HasMfencedAncestor(mn) || HasMfencedAncestor(next1) || HasMfencedAncestor(next2)) continue;
                        if (IsDirectlyBracketedByMo(mn, next2)) continue;

                        // get line/column from original if available
                        int line = -1, column = -1;
                        if (mn is IXmlLineInfo li && li.HasLineInfo())
                        {
                            line = li.LineNumber;
                            column = li.LinePosition;
                        }

                        // build the trio (ExactNode)
                        var trioClone = new XElement("wrapper",
                            new XElement(mn),
                            new XElement(next1),
                            new XElement(next2)
                        );
            
                        //if (line > 0) trioClone.SetAttributeValue("line", line);
                        //if (column > 0) trioClone.SetAttributeValue("column", column);

                        // find and clone the parent context using updated GetContextAncestor (Snippet)
                        var ctxAnc = GetContextAncestor(mn);
                        XElement contextClone = ctxAnc != null ? new XElement(ctxAnc) : null;

                        // build wrapper with explicit Snippet and ExactNode children
                        var wrapper = new XElement("Context",

                            contextClone != null ? new XElement("Snippet", contextClone) : null,
                            new XElement("ExactNode", trioClone)
                        );

                        // convenience attributes on wrapper
                        if (line > 0) wrapper.SetAttributeValue("line", line);
                        if (column > 0) wrapper.SetAttributeValue("column", column);

                        offenders.Add(wrapper);
                    }

                    return offenders.AsEnumerable();
                }
            },

        // *****************five tests above this line************************

            

            new TestRule
            {
                Id = "math-consecutive-mn-merge",
                Description = "Detect two adjacent <mn> elements that represent a single numeric token (e.g. <mn>1</mn><mn>0</mn> -> <mn>10</mn>). Skip cases where the direct parent is one of the excluded layout containers (mfrac, msup, msub, mover, munder, mroot, msubsup, munderover, mmultiscripts). Allow detection when the <mn> pair is wrapped in an <mrow> even if the mrow itself is inside msup or another excluded ancestor.",

                Checker = doc =>
                {
                    // containers where we DO NOT flag consecutive mn when they are the direct parent
                    var skipDirectParents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "mfrac","msup","msub","mover","munder","mroot","msubsup","munderover" ,"mmultiscripts"
                    };

                    // helper: true if the direct parent is in the skip list AND is not an mrow.
                    // This implements the rule: if the direct parent is an mrow, allow detection even if that mrow is inside an excluded ancestor.
                    bool HasSkippedDirectParent(XElement el)
                    {
                        var p = el?.Parent;
                        if (p == null) return false;
                        // if direct parent is mrow, we allow detection here (even if mrow's ancestor is excluded)
                        if (string.Equals(p.Name.LocalName, "mrow", StringComparison.OrdinalIgnoreCase)) return false;
                        return skipDirectParents.Contains(p.Name.LocalName);
                    }

                    // Choose the context ancestor: if the selected node itself is math -> use it,
                    // otherwise choose nearest math ancestor, otherwise nearest p, otherwise immediate parent.
                    XElement GetContextAncestor(XElement node)
                    {
                        if (node == null) return null;
                        if (string.Equals(node.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase)) return node;
                        var mathAnc = node.Ancestors().FirstOrDefault(a => string.Equals(a.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase));
                        if (mathAnc != null) return mathAnc;
                        var pAnc = node.Ancestors().FirstOrDefault(a => string.Equals(a.Name.LocalName, "p", StringComparison.OrdinalIgnoreCase));
                        if (pAnc != null) return pAnc;
                        return node.Parent;
                    }

                    var offenders = new List<XElement>();

                    // iterate candidate first <mn> nodes
                    foreach (var firstMn in doc.Descendants().Where(e => string.Equals(e.Name.LocalName, "mn", StringComparison.OrdinalIgnoreCase)))
                    {
                        // If the direct parent is an excluded layout container (and not an mrow), skip
                        if (HasSkippedDirectParent(firstMn)) continue;

                        var parent = firstMn.Parent;
                        if (parent == null) continue;

                        // gather element children of the parent in document order
                        var children = parent.Elements().ToList();
                        var idx = children.IndexOf(firstMn);
                        if (idx < 0) continue;

                        // need a following sibling that is an <mn>
                        if (idx + 1 >= children.Count) continue;
                        var secondMn = children[idx + 1];
                        if (!string.Equals(secondMn.Name.LocalName, "mn", StringComparison.OrdinalIgnoreCase)) continue;

                        // skip if the second mn's direct parent is an excluded layout container (and not an mrow)
                        if (HasSkippedDirectParent(secondMn)) continue;

                        // ensure both mn values look like integer digit sequences
                        var v1 = (firstMn.Value ?? string.Empty).Trim();
                        var v2 = (secondMn.Value ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(v1) || string.IsNullOrEmpty(v2)) continue;
                        if (!Regex.IsMatch(v1, @"^\d+$", RegexOptions.CultureInvariant)) continue;
                        if (!Regex.IsMatch(v2, @"^\d+$", RegexOptions.CultureInvariant)) continue;

                        // Exclude longer digit runs (3+ mn siblings) to avoid false positives
                        if (idx - 1 >= 0 && string.Equals(children[idx - 1].Name.LocalName, "mn", StringComparison.OrdinalIgnoreCase)) continue;
                        if (idx + 2 < children.Count && string.Equals(children[idx + 2].Name.LocalName, "mn", StringComparison.OrdinalIgnoreCase)) continue;

                        // Now, we must also allow the situation where the pair is wrapped in an mrow and that mrow
                        // is placed inside an excluded ancestor (msup etc). The HasSkippedDirectParent logic above
                        // allows such detection because the direct parent is mrow (not skipped) even if mrow's ancestor is skipped.

                        // Determine line/column from original if available (use firstMn)
                        int line = -1, column = -1;
                        if (firstMn is IXmlLineInfo li && li.HasLineInfo())
                        {
                            line = li.LineNumber;
                            column = li.LinePosition;
                        }

                        // Build ExactNode wrapper that contains the two mn nodes (clone them so result is independent)
                        var pair = new XElement("wrapper",
                            new XElement(firstMn),
                            new XElement(secondMn)
                        );

                        //if (line > 0) pair.SetAttributeValue("line", line);
                        //if (column > 0) pair.SetAttributeValue("column", column);
                    

                        // Build Snippet: choose context ancestor (prefer math, then p, then parent)
                        var ctxAnc = GetContextAncestor(firstMn) ?? parent;
                        XElement contextClone = ctxAnc != null ? new XElement(ctxAnc) : null;

                        // Compose wrapper <Context> with explicit <Snippet> and <ExactNode> children
                        var wrapper = new XElement("Context",
                            contextClone != null ? new XElement("Snippet", contextClone) : null,
                            new XElement("ExactNode", pair)
                        );

                        if (line > 0) wrapper.SetAttributeValue("line", line);
                        if (column > 0) wrapper.SetAttributeValue("column", column);

                        offenders.Add(wrapper);
                    }

                    return offenders.AsEnumerable();
                }
            },

            new TestRule
                {
                    Id = "math-operator-in-mtext",
                    Description = "Detect any <mtext> that contains one or more mathematical operators (+,  − (U+2212), ⋅ (U+22C5), × (U+00D7), =) which should be represented with <mo> elements instead.",
                    Checker = doc =>
                    {
                        // match any of the listed operator characters anywhere in the mtext content
                        var operatorChars = new Regex(@"[+\u2212\u22C5\u00D7=]",
                                                        RegexOptions.CultureInvariant);

                        return doc.Descendants()
                                    .Where(el => el.Name.LocalName == "mtext")
                                    .Where(mi =>
                                    {
                                        var txt = mi.Value ?? string.Empty;
                                        if (string.IsNullOrWhiteSpace(txt)) return false;
                                        return operatorChars.IsMatch(txt);
                                    });
                    }
            },

            new TestRule
        {
            Id = "math-units-in-mtext",
            Description = "Detect units or prefixed units inside <mtext> only when a number appears immediately before the unit inside the <mtext>, or when a numeric <mn> is immediately followed by an <mtext> that is a unit (e.g., <mn>10</mn><mtext>m</mtext>)",
            Checker = doc =>
            {
                var units = new HashSet<string>(StringComparer.Ordinal)
                {
                    // SI and common units
                    "A","cd","K","\u212A","g","m","mol","s","sek","sec","Bq","C","°C","\u00B0C","\u2103","F",
                    "Gy","H","Hz","J","kat","lm","lx","N","Ω","\u03A9","\u2126","Pa","S","Sv","T","V","W",
                    "Wb","l","L","\u2113","t","u","Da","Np","eV","rad","sr","as","b","B",

                    // Additional requested units (minimal additions)
                    "min","h","hr","Hr","\u00B0","°","arcmin","am","arcsec","au","AU","ly","pc",
                    "Å","\u212B","ha","dB","bar","cal",
                    "in","ft","mi","hp","hk","\u00B0F","\u2109","℉"
                };

                // Units that must NOT be combined with SI prefixes
                var nonPrefixableUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "min","h","hr","Hr","\u00B0","°","arcmin","am","arcsec","au","AU","ly","pc",
                    "Å","\u212B","ha","dB","bar","cal",
                    "in","ft","mi","hp","hk","\u00B0F","\u2109","℉"
                };

                var prefixes = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Q","R","Y","Z","E","P","T","G","M","k","h","da","d","c","m","\u00B5","µ","n","p","f","a","z","y","r","q"
                };

                // Build allowed tokens: base units plus prefixed units (skip prefixing non-prefixable units)
                var allowedUnitTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var u in units) allowedUnitTokens.Add(u);
                foreach (var p in prefixes)
                    foreach (var u in units)
                        if (!nonPrefixableUnits.Contains(u))
                            allowedUnitTokens.Add(p + u);

                // Alternation of unit tokens, ordered by length to prefer longest matches
                var unitAlternation = string.Join("|", allowedUnitTokens.OrderByDescending(s => s.Length).Select(Regex.Escape));

                // Require unit tokens to be standalone (not embedded in words): use Unicode letter/number boundaries
                var unitTokenWithBoundaries = $@"(?<![\p{{L}}\p{{N}}])(?:{unitAlternation})(?![\p{{L}}\p{{N}}])";

                // number token (integer or decimal with comma or dot)
                var numberToken = @"\d+(?:[.,]\d+)?";

                // unit or unit/unit (e.g., m, km, m/s, km/h) using bounded unit tokens
                var unitOrFrac = $@"(?:{unitTokenWithBoundaries})(?:\s*/\s*(?:{unitTokenWithBoundaries}))?";

                // 1) Exact number+unit anchored to the whole mtext (number must be in front)
                var numberPlusUnitPattern = new Regex($@"^\s*{numberToken}\s*{unitOrFrac}\s*$",
                                                      RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

                // 2) Number immediately before a standalone unit anywhere in the mtext (number must be before unit)
                var numberBeforeUnitPattern = new Regex($@"{numberToken}\s*{unitTokenWithBoundaries}",
                                                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

                // 3) Unit-only pattern for sibling <mn> check (unit token only)
                var unitOnlyPattern = new Regex($@"^\s*{unitOrFrac}\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

                // Skip common list/label tokens like "a)" or "(3.1)"
                var isListLabel = new Regex(@"^\s*[A-Za-z]\s*[\)\.]$|^\s*\(\s*\d+(?:[.,]\d+)?\s*\)\s*$", RegexOptions.CultureInvariant);

                return doc.Descendants()
                          .Where(e => string.Equals(e.Name.LocalName, "mtext", StringComparison.OrdinalIgnoreCase))
                          .Where(mtext =>
                          {
                              var txt = (mtext.Value ?? string.Empty).Trim();
                              if (string.IsNullOrEmpty(txt)) return false;

                              if (isListLabel.IsMatch(txt)) return false;

                              // 1) If the mtext itself is exactly number+unit (number must be in front), flag it
                              if (numberPlusUnitPattern.IsMatch(txt)) return true;

                              // 2) If the mtext contains a number that appears before a standalone unit token, flag it
                              // This avoids matching digits that are part of words (e.g., "converges to 0") or unit-like letters embedded in words.
                              if (numberBeforeUnitPattern.IsMatch(txt)) return true;

                              // 3) Flag a unit-only <mtext> that a numeric <mn> precedes, either directly
                              // (<mn>10</mn><mtext>m</mtext>) or separated by the invisible-times operator
                              // used to mark up the space between a number and a unit
                              // (<mn>10</mn><mo rspace="0.25em">&#x2062;</mo><mtext>m</mtext>).
                              if (unitOnlyPattern.IsMatch(txt))
                              {
                                  // Compound units such as m/s are wrapped in an <mrow>, so anchor the
                                  // sibling walk on that <mrow> when this <mtext> is its first child.
                                  var anchor = mtext;
                                  if (mtext.Parent != null &&
                                      string.Equals(mtext.Parent.Name.LocalName, "mrow", StringComparison.OrdinalIgnoreCase) &&
                                      mtext.Parent.Elements().FirstOrDefault() == mtext)
                                  {
                                      anchor = mtext.Parent;
                                  }

                                  var prev = anchor.ElementsBeforeSelf().LastOrDefault();

                                  // Step over the invisible-times <mo> marking the space between number and unit
                                  if (prev != null &&
                                      string.Equals(prev.Name.LocalName, "mo", StringComparison.OrdinalIgnoreCase) &&
                                      (prev.Value ?? string.Empty).Trim() == "\u2062")
                                  {
                                      prev = prev.ElementsBeforeSelf().LastOrDefault();
                                  }

                                  if (prev != null && string.Equals(prev.Name.LocalName, "mn", StringComparison.OrdinalIgnoreCase))
                                      return true;
                              }

                              // Otherwise do not flag (covers cases like "converges to 0" and "A0")
                              return false;
                          });
            }
        },







        new TestRule {
        Id = "math-variable-mtext",
        Description = "Detect single letters (Norwegian, English, Greek) in <mtext>.",
        Checker = doc => {
            var setSymbols = new[] { "\u2102", "\u2115", "\u211A", "\u211D", "\u2124" }; // ℂ ℕ ℚ ℝ ℤ
            var openDelims = new[] { "(", "{", "[" };

            XNamespace xmlNs = "http://www.w3.org/XML/1998/namespace";

            // 24 modern Greek letters (uppercase and lowercase)
            var modernGreekLetters = new HashSet<string> {
                "Α","α","Β","β","Γ","γ","Δ","δ","Ε","ε","Ζ","ζ","Η","η","Θ","θ",
                "Ι","ι","Κ","κ","Λ","λ","Μ","μ","Ν","ν","Ξ","ξ","Ο","ο","Π","π",
                "Ρ","ρ","Σ","σ","Τ","τ","Υ","υ","Φ","φ","Χ","χ","Ψ","ψ","Ω","ω"
            };

            // Norwegian letters (uppercase and lowercase)
            var norwegianLetters = new HashSet<string> {
                "Æ","æ","Ø","ø","Å","å"
            };

            bool IsNorwegian(XElement root) {
                if (root == null) return false;
                string GetLang(XElement el) {
                    var a = el.Attribute(xmlNs + "lang") ?? el.Attribute("lang");
                    return a?.Value;
                }
                var cand = GetLang(root) ?? root.AncestorsAndSelf().Select(GetLang).FirstOrDefault(v => !string.IsNullOrEmpty(v));
                if (string.IsNullOrEmpty(cand)) return false;
                cand = cand.Trim().ToLowerInvariant();
                return cand.StartsWith("no") || cand.StartsWith("nb") || cand.StartsWith("nn");
            }

            // Accept only Basic Latin, explicit Norwegian letters, and the 24 modern Greek letters.
            bool IsSingleAllowedLetter(XElement mtext, out string letter) {
                letter = null;
                if (mtext == null) return false;
                var raw = mtext.Value ?? string.Empty;
                var stripped = new string(raw.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
                if (stripped.Length != 1) return false;
                var ch = stripped[0];
                int code = (int)ch;

                // Basic ASCII Latin letters A-Z, a-z
                if ((code >= 0x0041 && code <= 0x005A) || (code >= 0x0061 && code <= 0x007A)) {
                    letter = stripped;
                    return true;
                }

                // Explicit Norwegian letters (Æ æ Ø ø Å å)
                if (norwegianLetters.Contains(stripped)) {
                    letter = stripped;
                    return true;
                }

                // Explicit modern Greek letters (both uppercase and lowercase)
                if (modernGreekLetters.Contains(stripped)) {
                    letter = stripped;
                    return true;
                }

                // Do not accept letters from other scripts or other Latin-1 Supplement letters
                return false;
            }

            XElement GetAdjacentNextElement(XElement mtext) {
                foreach (var node in mtext.NodesAfterSelf()) {
                    if (node is XText txt) {
                        if (string.IsNullOrWhiteSpace(txt.Value)) continue;
                        return null;
                    }
                    if (node is XElement el) return el;
                }
                return null;
            }

            bool docIsNorwegian = IsNorwegian(doc.Root);

            var candidates = doc.Descendants()
                                .Where(el => string.Equals(el.Name.LocalName, "mtext", StringComparison.OrdinalIgnoreCase));

            if (!docIsNorwegian) {
                return candidates.Where(mtext => {
                    string letter;
                    return IsSingleAllowedLetter(mtext, out letter);
                });
            }

            // Norwegian documents: apply exceptions for <mtext>i</mtext>
            return candidates.Where(mtext => {
                string letter;
                if (!IsSingleAllowedLetter(mtext, out letter)) return false;

                // If the single letter is not lowercase 'i', always flag
                if (!string.Equals(letter, "i", StringComparison.Ordinal)) return true;

                var next = GetAdjacentNextElement(mtext);
                if (next == null) return true; // no adjacent element -> flag

                // Exception 1: <mi> with one of the blackboard symbols
                if (string.Equals(next.Name.LocalName, "mi", StringComparison.OrdinalIgnoreCase)) {
                    var content = (next.Value ?? string.Empty).Trim();
                    if (setSymbols.Contains(content)) return false; // do not flag
                }

                // Exception 2: <msup> with base <mi> being blackboard symbol and exponent <mn> (e.g., 2) or <mi> 'n'
                if (string.Equals(next.Name.LocalName, "msup", StringComparison.OrdinalIgnoreCase)) {
                    var children = next.Elements().ToList();
                    if (children.Count >= 2) {
                        var baseElem = children[0];
                        var expElem = children[1];
                        if (string.Equals(baseElem.Name.LocalName, "mi", StringComparison.OrdinalIgnoreCase)) {
                            var baseText = (baseElem.Value ?? string.Empty).Trim();
                            if (setSymbols.Contains(baseText)) {
                                if (string.Equals(expElem.Name.LocalName, "mn", StringComparison.OrdinalIgnoreCase)) return false;
                                if (string.Equals(expElem.Name.LocalName, "mi", StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals((expElem.Value ?? string.Empty).Trim(), "n", StringComparison.Ordinal)) return false;
                            }
                        }
                    }
                }

                // Exception 3: <mo> that is opening delimiter ( ( { [ )
                if (string.Equals(next.Name.LocalName, "mo", StringComparison.OrdinalIgnoreCase)) {
                    var moText = (next.Value ?? string.Empty).Trim();
                    if (openDelims.Contains(moText)) return false;
                }

                // None of the exceptions matched -> flag
                return true;
            });
        }
    },




                // *****************ten tests above this line************************

            new TestRule {
                Id = "math-functions-as-mtext",
                Description = "Function names sin cos tan ln log lim found in <mtext> (should be <mi>).",
                Checker = doc => {
                    var functions = new[] { "sin", "cos", "tan", "ln", "log", "lim" };
                    var funcPattern = new Regex(@"\b(?:" + string.Join("|", functions) + @")\b",
                                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                    return doc.Descendants()
                              .Where(el => el.Name.LocalName == "mtext")
                              .Where(el => {
                                  var txt = (el.Value ?? string.Empty).Trim();
                                  if (string.IsNullOrEmpty(txt)) return false;
                                  // flag when element text equals a function token or contains a short token that matches
                                  if (funcPattern.IsMatch(txt) && txt.Length <= 6) return true;
                                  return false;
                              });
                }
            },

            new TestRule
            {
                Id = "math-function-and-value-in-mi",
                Description = "Detect <mi> elements where a function name (sin cos tan ln log lim) appears together with an identifier or token (e.g. 'lnx', 'ln x', 'sin2x').",
                Checker = doc =>
                {
                    // match function name optionally followed by whitespace and then a token:
                    // - a letter-starting identifier (x, var, x1)
                    // - a number (2) or number+identifier (2x)
                    // - any non-space run (covers unusual tokens)
                    var funcPrefixAttachedPattern = new Regex(
                        @"^\s*(?:sin|cos|tan|ln|log|lim)\s*(?:\d+[a-zA-Z]*|[a-zA-Z]\w*|[^\s]+)\s*$",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.CultureInvariant);

                    return doc.Descendants()
                              .Where(el => el.Name.LocalName == "mi")
                              .Where(mi =>
                              {
                                  var txt = (mi.Value ?? string.Empty).Trim();
                                  if (string.IsNullOrEmpty(txt)) return false;
                                  return funcPrefixAttachedPattern.IsMatch(txt);
                              });
                }
            },


            new TestRule
            {
                Id = "math-empty-symbols",
                Description = "Detect empty or whitespace-only <mi>, <mo>,<mn>, and <mtext> elements.",
                Checker = doc =>
                {
                    var targetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mi", "mo", "mn", "mtext" };

                    return doc.Descendants()
                                .Where(el => targetNames.Contains(el.Name.LocalName))
                                .Where(el =>
                                {
                                    // Treat the element as empty if its string value is null, empty, or only whitespace
                                    var value = el.Value ?? string.Empty;
                                    if (!string.IsNullOrWhiteSpace(value)) return false;

                                    // Also treat as empty if it contains no child nodes (defensive, Value check above covers text-only)
                                    if (!el.Nodes().Any()) return true;

                                    // If it has children but the concatenated Value is whitespace, it's still empty for our purposes
                                    return true;
                                })
                                .Distinct();
                }
            },

            new TestRule
            {
                Id = "math-ocr-lnloglim",
                Description = "Detect OCR errors where l (lowercase L) was recognized as 1 or I producing 1n/In, 1og/Iog, 1im/Iim",
                Checker = doc => {
                    // match 1n or In, 1og or Iog, 1im or Iim (case-insensitive)
                    var pattern = new Regex(@"^(?:[1I]n|[1I]og|[1I]im)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                    // element local names to inspect: mi and mtext are the usual places for identifiers/text
                    var targetLocalNames = new[] { "mi", "mtext" };

                    return doc.Descendants()
                              .Where(el => targetLocalNames.Contains(el.Name.LocalName))
                              .Where(el => {
                                  var text = (el.Value ?? string.Empty).Trim();
                                  return !string.IsNullOrEmpty(text) && pattern.IsMatch(text);
                              });
                }
            },

            new TestRule
            {
                Id = "math-hyphen-minus-used",
                Description = "Detect uses of the hyphen-minus U+002D  where the proper mathematical minus U+2212 should be used.",
                Checker = doc =>
                {
                    // exact hyphen-minus character (U+002D) possibly surrounded by whitespace
                    var hyphenMinusPattern = new Regex(@"^\s*\-\s*$", RegexOptions.CultureInvariant);

                    return doc.Descendants()
                              .Where(el => string.Equals(el.Name.LocalName, "mo", StringComparison.OrdinalIgnoreCase))
                              .Where(el =>
                              {
                                  var txt = (el.Value ?? string.Empty);
                                  if (string.IsNullOrWhiteSpace(txt)) return false;
                                  return hyphenMinusPattern.IsMatch(txt);
                              });
                }
            },

            // ***************** fifteen tests above this line ************************

             new TestRule {
                Id = "math-adjacent-mtext-merge",
                Description = "Adjacent <mtext> elements that probably should be merged.",
                Checker = doc => {
                    // works with MathML with or without namespace by checking LocalName
                    var punctuationOnly = new Regex(@"^\p{P}+$", RegexOptions.CultureInvariant);

                    return doc.Descendants()
                              .Where(e => e.Name.LocalName == "math")
                              .SelectMany(math =>
                                  math.Elements() // only direct children, to detect adjacent siblings
                                      .Select((el, idx) => new { el, idx })
                                      .Where(x => x.el.Name.LocalName == "mtext" &&
                                                  // check next sibling exists and is also mtext
                                                  (x.idx + 1) < math.Elements().Count() &&
                                                  math.Elements().ElementAt(x.idx + 1).Name.LocalName == "mtext" &&
                                                  // do not flag when the next <mtext> is only punctuation (e.g., ".")
                                                  !punctuationOnly.IsMatch((math.Elements().ElementAt(x.idx + 1).Value ?? string.Empty).Trim()))
                                      .Select(x => x.el) // return the first element of each adjacent pair
                              );
                }
             },


            new TestRule
            {
                Id = "math-mrow-only-child",
                Description = "math with a single <mrow> child.",
                Checker = doc => doc
                    .Descendants()                                    // search all elements
                    .Where(e => e.Name.LocalName == "math")          // match <math> regardless of namespace
                    .Where(x => x.Elements().Count() == 1
                                && x.Elements().First().Name.LocalName == "mrow") // single child named <mrow>
            },

            new TestRule {
                Id = "math-mrow-with-one-child",
                Description = "<mrow> with only one child.",
                Checker = doc => doc
                    .Descendants()                                       // search all elements
                    .Where(e => e.Name.LocalName == "math")             // find <math>
                    .SelectMany(math => math.Descendants()               // look inside each <math>
                                          .Where(n => n.Name.LocalName == "mrow"
                                                      && n.Elements().Count() == 1))
            },

            new TestRule
            {
                Id = "math-reference-labels-in-math",
                Description = "Detect math expressions used for task/item references like \"a) - b)\" or \"a. - b.\" which should be plain text, not MathML.",
                Checker = doc =>
                {
                    // Matches a single reference label token used in lists: e.g. "a)", "A)", "1)", "a.", "A.", "1."
                    var labelToken = new Regex(@"^\s*[A-Za-z0-9]\s*[\)\.]\s*$", RegexOptions.CultureInvariant);

                    // Matches dash/minus characters used between labels: ASCII hyphen-minus or Unicode minus
                    var dashMo = new Regex(@"^\s*[\-\u2212]\s*$", RegexOptions.CultureInvariant);

                    // Helper: is this an <mtext> that looks like a single list label token
                    bool IsLabelMtext(XElement e)
                    {
                        if (e == null || e.Name.LocalName != "mtext") return false;
                        var t = (e.Value ?? string.Empty);
                        return labelToken.IsMatch(t);
                    }

                    // Helper: is this an <mo> that is a simple dash/minus used as a separator
                    bool IsDashMo(XElement e)
                    {
                        if (e == null || e.Name.LocalName != "mo") return false;
                        var t = (e.Value ?? string.Empty);
                        return dashMo.IsMatch(t);
                    }

                    // Find math elements that contain the pattern: <mtext>label</mtext> <mo>-</mo> <mtext>label</mtext>
                    return doc.Descendants()
                              .Where(m => m.Name.LocalName == "math")
                              .Where(math =>
                              {
                                  // consider direct child elements (allow intervening whitespace text nodes)
                                  var children = math.Elements().ToList();
                                  for (int i = 0; i + 2 < children.Count; i++)
                                  {
                                      var first = children[i];
                                      var middle = children[i + 1];
                                      var second = children[i + 2];

                                      if (IsLabelMtext(first) && IsDashMo(middle) && IsLabelMtext(second))
                                      {
                                          // Exclude likely genuine math sequences: if labels are numeric and math context suggests coordinates etc.
                                          // Heuristic: if any of the label tokens contain digits followed by ')' or '.' AND math contains other math tokens (mi,mn) treat conservatively.
                                          // For now, flag the math node for review.
                                          return true;
                                      }
                                  }
                                  return false;
                              });
                }
            },
            // ***************** 19 tests above this line ************************

            //******************* 2 extra tests below this line ***********************

           

            new TestRule
        {
            Id = "math-punctuation-outside",
            Description = "Detect punctuation that immediately follows a <math> element but is placed outside the MathML. Punctuation should be inside the math expression (for example as <mtext>.) so screen readers do not read it aloud separately.",
            Checker = doc =>
            {
                var punctPattern = new Regex(@"^[\.\,\:\;\?\!]", RegexOptions.CultureInvariant);

                // Choose the context ancestor: if the selected node itself is math -> use it,
                // otherwise choose nearest math ancestor, otherwise nearest p, otherwise immediate parent.
                XElement GetContextAncestor(XElement node)
                {
                    if (node == null) return null;
                    if (string.Equals(node.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase)) return node;
                    var mathAnc = node.Ancestors().FirstOrDefault(a => string.Equals(a.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase));
                    if (mathAnc != null) return mathAnc;
                    var pAnc = node.Ancestors().FirstOrDefault(a => string.Equals(a.Name.LocalName, "p", StringComparison.OrdinalIgnoreCase));
                    if (pAnc != null) return pAnc;
                    return node.Parent;
                }

                var offenders = new List<XElement>();

                foreach (var math in doc.Descendants().Where(e => string.Equals(e.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase)))
                {
                    // if math has ending punctuation inside it (an mtext that is just punctuation) then it's fine
                    var lastChild = math.Nodes().Reverse().FirstOrDefault();
                    if (lastChild is XElement lastEl && string.Equals(lastEl.Name.LocalName, "mtext", StringComparison.OrdinalIgnoreCase))
                    {
                        var lastTxt = (lastEl.Value ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(lastTxt) && punctPattern.IsMatch(lastTxt)) continue;
                    }

                    var parent = math.Parent;
                    if (parent == null) continue;

                    // find math's position among parent's nodes (including text nodes)
                    var nodes = parent.Nodes().ToList();
                    var idx = nodes.IndexOf(math);
                    if (idx < 0) continue;
                    if (idx + 1 >= nodes.Count) continue;

                    // skip whitespace-only text nodes until we find the first non-whitespace sibling
                    var j = idx + 1;
                    XNode candidate = null;
                    while (j < nodes.Count)
                    {
                        var n = nodes[j];
                        if (n is XText xt)
                        {
                            if (!string.IsNullOrWhiteSpace(xt.Value))
                            {
                                candidate = n;
                                break;
                            }
                        }
                        else
                        {
                            candidate = n;
                            break;
                        }
                        j++;
                    }
                    if (candidate == null) continue;

                    // helper checks
                    bool IsTextNodeStartingWithPunct(XNode node)
                    {
                        if (node is XText t)
                        {
                            var v = (t.Value ?? string.Empty);
                            return punctPattern.IsMatch(v.TrimStart());
                        }
                        return false;
                    }

                    bool IsElementStartingWithPunct(XNode node)
                    {
                        if (node is XElement e)
                        {
                            var v = (e.Value ?? string.Empty);
                            return punctPattern.IsMatch(v.TrimStart());
                        }
                        return false;
                    }

                    // If candidate starts with punctuation, build a Context wrapper and attach exact line/column from the offending node
                    if (IsTextNodeStartingWithPunct(candidate) || IsElementStartingWithPunct(candidate))
                    {
                        int line = -1, column = -1;
                        XElement exactNodeClone = null;
                        XElement ctxAnc = null;

                        if (candidate is XElement ce)
                        {
                            // offending element itself
                            if (ce is IXmlLineInfo eli && eli.HasLineInfo())
                            {
                                line = eli.LineNumber;
                                column = eli.LinePosition;
                            }
                            exactNodeClone = new XElement(ce);
                            ctxAnc = GetContextAncestor(ce) ?? parent;
                        }
                        else if (candidate is XText ct)
                        {
                            // offending text node: prefer to return its parent element as ExactNode clone if available,
                            // but capture line/column from the text node itself so we report the exact punctuation location.
                            if (ct is IXmlLineInfo tli && tli.HasLineInfo())
                            {
                                line = tli.LineNumber;
                                column = tli.LinePosition;
                            }

                            if (ct.Parent is XElement p)
                            {
                                // clone the parent element so the snippet shows the element that contains the text
                                exactNodeClone = new XElement(p);
                                ctxAnc = GetContextAncestor(p) ?? parent;
                            }
                            else
                            {
                                // no parent element: wrap the text in a small element so ExactNode is an XElement
                                exactNodeClone = new XElement("text", ct.Value);
                                ctxAnc = GetContextAncestor(math) ?? parent;
                            }
                        }

                        // Compose wrapper <Context> with explicit <Snippet> and <ExactNode> children
                        XElement contextClone = ctxAnc != null ? new XElement(ctxAnc) : null;
                        var wrapper = new XElement("Context",
                            contextClone != null ? new XElement("Snippet", contextClone) : null,
                            new XElement("ExactNode", exactNodeClone)
                        );

                        if (line > 0) wrapper.SetAttributeValue("line", line);
                        if (column > 0) wrapper.SetAttributeValue("column", column);

                        offenders.Add(wrapper);
                    }
                }

                return offenders.AsEnumerable();
            }
        },



            new TestRule {
                Id = "math-space-mn-mo-mi",
                Description = "Flag <mn>, <mo>, and <mi> elements that contain specific Unicode space characters (U+2005, U+2003).",
                Checker = doc => {
                    // target Unicode space characters: U+2005 (four-per-em space), U+2003 (em space)
                    var targetChars = new[] { '\u2005', '\u2003' };

                    return doc.Descendants()
                              .Where(e =>
                              {
                                  var local = e.Name.LocalName;
                                  if (!string.Equals(local, "mn", StringComparison.OrdinalIgnoreCase) &&
                                      !string.Equals(local, "mo", StringComparison.OrdinalIgnoreCase) &&
                                      !string.Equals(local, "mi", StringComparison.OrdinalIgnoreCase))
                                      return false;

                                  var txt = e.Value ?? string.Empty;
                                  foreach (var ch in targetChars)
                                      if (txt.IndexOf(ch) >= 0)
                                          return true;

                                  return false;
                              });
                }
            },



        }.ToList();
    }// end class Tests


}// end namespace Math_ML_Validator
