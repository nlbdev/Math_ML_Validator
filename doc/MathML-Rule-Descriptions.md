# MathML Rule Descriptions

This document explains, in plain English, what each rule in src/Tests.cs checks for.

## Rule Tags and Filtering

A rule may optionally be tagged with a library, a language and a MathML version:

```csharp
new TestRule {
    Id = "math-wrong-number-of-children-3",
    Library = "tibi",
    Language = "no",
    MathML_version = "MathML3",
    ...
}
```

A run can then be narrowed to a given tag by setting `settings.txt` next to the EPUB files:

```
library        = tibi
language       = no
mathml_version = MathML3
```

Notes:

- Every tag is optional. A rule without tags is generic and runs no matter what the filter says.
- Each tag may list several values, for example `Library = "tibi,statped"`. Matching is case-insensitive.
- The keys in `settings.txt` combine with AND: a rule must match every key that is set.
- If `settings.txt` is missing, or all its keys are empty or commented out, every active rule runs.
- This filter is applied after the rule ID filter in `rules.txt`, so the two can be combined.

## Rule Overview

### math-wrong-number-of-children-2

Checks for MathML elements that should have exactly two children but currently have only one.

Applies to:

- `<mfrac>`
- `<msup>`
- `<msub>`
- `<mroot>`
- `<mover>`
- `<munder>`

### math-wrong-number-of-children-3

Checks for MathML elements that should have exactly three children but currently have fewer than three.

Applies to:

- `<munderover>`
- `<msubsup>`

### math-number-in-mtext

Checks for numbers inside `<mtext>` elements, that should be in `<mn>` elements (or split so numeric parts use `<mn>`).

It does not flag:

- `<mtext>` inside `<mtd>` with intent="equation-label", which is used for labeling equations
- simple parenthesized labels such as $(3)$ or $(3.1)$

### math-single-mn

Checks for `<math>` elements that contain only one child, and that child is `<mn>`.

### math-decimal-comma-split

Checks for decimals written with comma that are incorrectly split into three nodes:

- `<mn>`
- `<mo>` containing comma
- `<mn>`

Example pattern:

`<mn>1</mn><mo>,</mo><mn>2</mn>`

It only flags two-number comma pairs. It avoids false positives by excluding:

- cases in brackets, parentheses or braces (for example vectors)
- longer comma-separated lists (for example coordinates or sets)

### math-consecutive-mn-merge

Checks for two adjacent `<mn>` elements that likely represent one single number and should be merged.

Example:

`<mn>1</mn><mn>0</mn>` should likely be `<mn>10</mn>`

It excludes cases where the parent is `<mfrac>`, `<msup>`, `<msub>`, `<mover>`, `<munder>`, `<mroot>`, `<msubsup>`, `<munderover>` or `<mmultiscripts>`. However, it detects cases inside `<mrow>`s inside the mentioned elements.

### math-operator-in-mtext

Checks for mathematical operators placed inside `<mtext>` instead of `<mo>`.

Operators detected include:

- pluss (+)
- minus sign (U+2212)
- dot operator (U+22C5)
- multiplication sign (U+00D7)
- equal sign (=)

### math-units-in-mtext

Detects units or prefixed units inside `<mtext>` elements.

It detects the following cases:

- number + unit inside the same `<mtext>` element, where the number is immediately before the unit
- `<mn>` followed immediately by `<mtext>` containing a unit
- `<mn>` + `<mo rspace="0.25em">&#x2062;</mo>` + `<mtext>` containing a unit
- `<mn>` + `<mo rspace="0.25em">&#x2062;</mo>` + `<mrow>` whose first child is an `<mtext>` containing a unit (compound units, like m/s, are wrapped in an `<mrow>`)

If there is space between a number and a unit in a book, this space should be marked up with `<mo rspace="0.25em">&#x2062;</mo>`. The invisible times operator between the number and the unit therefore does not stop the rule from flagging the unit.

The rule only detects units and prefixed units defined in the file [Units and prefixes](Units_and_prefixes.md). The file contains the most common units and all SI prefixes. Decided to not include all units defined in MathCAT to avoid false positives.

### math-variable-mtext

Checks for single-letter variables in `<mtext>` element that should be represented as `<mi>`.

It detects:

- English letters
- Norwegian letters
- Modern Greek letters

For Norwegian- and Swedish-language documents, the rule does not flag the letter "i" when it stands directly in front of:

- one of the set symbols ℂ, ℕ, ℚ, ℝ, ℤ
- an `<msup>` element with one of those symbols in the base (any exponent)
- an opening delimiter `(`, `{` or `[`

The language is read from the `xml:lang`/`lang` attribute of the document, so a book must be marked as Norwegian (`no`, `nb`, `nn`) or Swedish (`sv`) for these exceptions to apply.

### math-functions-as-mtext

Checks for common math function names written in `<mtext>` instead of `<mi>`.

Functions detected:

- sin
- cos
- tan
- ln
- log
- lim

### math-function-and-value-in-mi

Checks for `<mi>` elements containing both a function name and a variable/number.

Examples:

- $\text{ln} x$
- $\text{ln} \hspace{0.25em} x$
- $\text{sin} 2x$

### math-empty-symbols

Checks for empty or whitespace-only symbol/text elements.

Applies to:

- `<mi>`
- `<mo>`
- `<mn>`
- `<mtext>`

### math-ocr-lnloglim

Checks for OCR errors where l (lowercase L) in function names was recognized as 1 (one) or I (capital i).

Patterns detected:

- 1n or In (instead of ln)
- 1og or Iog (instead of log)
- 1im or Iim (instead of lim)

### math-hyphen-minus-used

Checks for hyphen-minus character U+002D in a `<mo>` element, where U+2212 should be used instead.

### math-adjacent-mtext-merge

Checks for adjacent `<mtext>` siblings that likely should be merged into one `<mtext>`.

It does not flag pairs where the second `<mtext>` is only punctuation.

### math-mrow-only-child

Checks for `<math>` elements that have exactly one child and that child is `<mrow>`.

### math-mrow-with-one-child

Checks for `<mrow>` elements that contain only one child element.

### math-reference-labels-in-math

Checks for list/reference label patterns written as MathML expressions. These should likely be in plain text. 

The rule checks the following cases:

- a)-b)
- a)−b)
- a.-b.
- a.−b.

### math-punctuation-outside

Checks for punctuation immediately following a `<math>` element when punctuation is outside MathML.

Punctuation right after a `<math>` element needs to be inside the `<math>` element for assisitve technology to parse it correctly.

### math-space-mn-mo-mi

Checks for U+2005 (four-per-em-space) or U+2003 (em-space) inside token elements `<mn>`, `<mo>` and `<mi>` elements.
