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

`sinh`, `cosh` and `tanh` on their own are function names and are not flagged.

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

### math-units-mathvariant

Checks whether the attribute `mathvariant="normal"` is missing from the `<mi>` tag of single-letter units.

### math-mfenced

Checks for the deprecated `<mfenced>` element. Every occurrence is flagged, with or without `open`, `close` or `separators` attributes.

`<mfenced>` is not supported in MathML Core, and our guidelines no longer allow it. Write the fences as `<mo>` elements inside an `<mrow>` instead, for example `<mrow><mo>[</mo><mn>0</mn><mo>,</mo><mi>∞</mi><mo>]</mo></mrow>`.

### math-alt-attributes

Checks for `<math>` elements with an `alttext` or `altimg` attribute. The guidelines no longer allow these attributes. The related `altimg-width`, `altimg-height` and `altimg-valign` attributes are flagged too.

Each `<math>` element is reported once, even if it has several of these attributes.

### math-semantics

Checks for the `<semantics>`, `<annotation>` and `<annotation-xml>` elements. The guidelines say not to use them unless the order specifically asks for them.

The rule reports:

- every `<semantics>` element, with or without an `<annotation>` or `<annotation-xml>` inside it (reported once, not once per annotation)
- every `<annotation>` or `<annotation-xml>` element that is not inside a `<semantics>` element

If a book is ordered with `<semantics>`, leave `math-semantics` out of `rules.txt` when validating that book.

### math-mtable-one-cell

Checks for `<mtable>` elements that contain exactly one `<mtd>` cell. Such content should probably not be marked up as a table.

Only the table's own cells are counted. Cells of a table nested inside it belong to the nested table, so each table is checked on its own. A row with an equation label (`<mlabeledtr>`, or an `<mtd>` with `intent=":equation-label"`) has two cells and is not flagged.

### math-exponent-o

Checks for `<mi>o</mi>` or `<mi>O</mi>` as the exponent (second child) of an `<msup>` element. This is likely an OCR error for the number 0, for example `10` with exponent `o` instead of `0`.

The `<mi>` is flagged even if it has attributes, for example `<mi mathvariant="normal">o</mi>`. Surrounding whitespace is ignored. A letter o anywhere else, such as the base of `<msup>` or in a subscript, is not flagged.

### math-missing-invisible-times

Checks for a number followed directly by an opening parenthesis, without the invisible times operator `<mo>&#x2062;</mo>` between them.

Flagged:

- `<mn>2</mn><mo>(</mo>...`
- `<mn>2</mn><mrow><mo>(</mo>...<mo>)</mo></mrow>` (the parenthesis group wrapped in `<mrow>`)

Correct: `<mn>2</mn><mo>&#x2062;</mo><mo>(</mo>...`

A number that is a separate argument of a layout element (for example the numerator of `<mfrac>` or the base of `<msup>`) is not checked, because the element after it is not part of the same row. Other places where invisible times may be missing, such as `)(` or between `<mfrac>` and `<msup>`, are deliberately not checked (decided in the GitHub issue).

### math-missing-invisible-function-operator

Checks for a function name that is not followed by the invisible function application operator `<mo>&#x2061;</mo>`.

Function names (lowercase only): sin, cos, tan, ln, lg, log, sinh, cosh, tanh, exp, f, g, h.

Flagged when the `<mi>` with the function name is followed directly by:

- an opening parenthesis: `<mi>sin</mi><mo>(</mo>`, or `<mi>sin</mi><mrow><mo>(</mo>...</mrow>`
- an `<mi>` with one letter: `<mi>sin</mi><mi>x</mi>`

Correct: `<mi>sin</mi><mo>&#x2061;</mo><mo>(</mo>...` and `<mi>f</mi><mo>&#x2061;</mo><mi>x</mi>`

Not flagged:

- a number after the function name, for example `<msup><mi>sin</mi><mn>2</mn></msup><mi>x</mi>`
- a function name that is a separate argument of a layout element, for example the base of `<msup>` or `<msub>` (`<msub><mi>log</mi><mi>a</mi></msub>`)
- uppercase names such as `Sin` or `F`

### math-split-chemical-elements

This rule looks for chemical elements which have been incorrectly captured in separate elements. For example, sodium should be marked up as `<mi>Na</mi>`, not `<mi>N</mi><mi>a</mi>`. The rule is meant for chemistry; in other math, two single-letter variables next to each other are normal, so the rule only checks this exact list of element symbols, with capitalisation:

Ac Ag Al Am Ar As At Au Ba Be Bh Bi Bk Br Ca Cd Ce Cf Cl Cm Cn Co Cr Cs Cu Db Ds Dy Er Es Eu Fe Fl Fm Fr Ga Gd Ge Hf Hg Ho In Ir Kr La Li Lr Lu Lv Mc Md Mg Mn Mo Mt Na Nb Nd Ne Nh Ni No Np Os Pa Pb Pd Pm Po Pr Pt Pu Ra Rb Re Rf Rg Rh Rn Ru Sb Sc Se Sg Si Sm Sn Sr Ta Tb Tc Te Th Ti Tl Tm Ts Xe Yb Zn Zr

Flagged:

- `<mi>N</mi><mi>a</mi>` (an `<mi>` with one uppercase letter followed by an `<mi>` with one lowercase letter)
- `<mi>C</mi><msub><mi>l</mi><mn>2</mn></msub>`: the second letter carries the index or charge (`<msub>`, `<msup>` or `<msubsup>`)

Not flagged:

- letters with an operator between them, for example `<mi>N</mi><mo>&#x2062;</mo><mi>a</mi>`
- letters that are separate arguments of a layout element, for example `<msub><mi>C</mi><mi>l</mi></msub>` (C with subscript l)

### math-inline-no-space

Checks for inline `<math>` elements that are directly preceded or followed by a letter, with no space between the text and the math, for example `expression<math>...</math>` or `<math>...</math>with`. Without a space, TTS audio can be synchronised in the wrong order.

Each `<math>` element is reported once, even if both sides touch a letter.

Not flagged:

- punctuation or a hyphen next to the math, for example `<math>...</math>-coefficient` or `<math>...</math>,`
- digits, spaces and no-break spaces next to the math
- block math (`display="block"`)

Text inside inline elements such as `<em>`, `<strong>` or `<span>` counts as running text. A block element, `<br>`, an image or another `<math>` element counts as a boundary.
