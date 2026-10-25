# Tax rules in the Tax App

> **Illustrative only.** This is a demo of software modernization, not a tax calculator. It is not tax advice and not an ATO service.
> It deliberately simplifies the rules. Check [ato.gov.au](https://www.ato.gov.au/) for real figures.

## What is implemented

An Australian **resident individual** who is resident for the full year and entitled to the tax-free threshold.

| Taxable income | Tax on this income (2026–27) |
| --- | --- |
| $0 – $18,200 | Nil |
| $18,201 – $45,000 | 15c for each $1 over $18,200 |
| $45,001 – $135,000 | $4,020 plus 30c for each $1 over $45,000 |
| $135,001 – $190,000 | $31,020 plus 37c for each $1 over $135,000 |
| $190,001 and over | $51,370 plus 45c for each $1 over $190,000 |

| Taxable income | Tax on this income (2025–26) |
| --- | --- |
| $0 – $18,200 | Nil |
| $18,201 – $45,000 | 16c for each $1 over $18,200 |
| $45,001 – $135,000 | $4,288 plus 30c for each $1 over $45,000 |
| $135,001 – $190,000 | $31,288 plus 37c for each $1 over $135,000 |
| $190,001 and over | $51,638 plus 45c for each $1 over $190,000 |

Source: [Tax rates – Australian residents](https://www.ato.gov.au/tax-rates-and-codes/tax-rates-australian-residents), ATO, retrieved
2026-10-26. The ATO states these rates do not include the Medicare levy of 2%.

## The calculation

```text
taxable income = max(0, gross income - deductions)
income tax     = round(tax from the scale above)
medicare levy  = round(taxable income x 2%)
total tax      = income tax + medicare levy
income after tax = gross income - total tax
effective rate = round(total tax x 100 / gross income)           (0 when income is 0)
```

`round` means 2 decimal places, **midpoint away from zero** (`Math.Round(value, 2, MidpointRounding.AwayFromZero)`), applied to each
component before adding. This matters: .NET's default is round-half-to-even, so `0.045` rounds to `0.04` by default but to `0.05`
here. The golden master has two vectors that fail if a rewrite changes this.

**Return summary:** `balance = tax withheld - total tax`. A balance of zero or more is a **refund**, below zero is **payable**. The
deductions come from the saved deductions list.

**GST** (rate 10%): *add GST* gives `gst = round(price x 10%)`; *extract GST* from a GST-inclusive price gives
`gst = round(price x 10% / 110%)`, which is one eleventh.

## Worked examples (hand-checkable)

| Case (2026–27) | Working | Result |
| --- | --- | --- |
| Income $95,000, no deductions | 4,020 + (95,000 − 45,000) × 30% = 19,020; Medicare 95,000 × 2% = 1,900 | Total tax **$20,920.00** |
| Income $95,000, deductions $5,000 | Taxable 90,000: 4,020 + 45,000 × 30% = 17,520; Medicare 1,800 | Total tax **$19,320.00** |
| Income $18,200 | Under the threshold: income tax 0; Medicare 18,200 × 2% = 364 | Total tax **$364.00** |
| Add GST to $100 | 100 × 10% = 10 | **$110.00** |
| Extract GST from $110 | 110 × 10% / 110% = 10 | GST **$10.00**, price **$100.00** |

## What is deliberately left out

Tax offsets (for example the low income tax offset), Medicare levy low-income thresholds and the surcharge, HELP/HECS repayments,
part-year residency, non-residents, capital gains, super, and anything about GST registration. A 2% Medicare levy is charged on
all taxable income here, which is **wrong** for low incomes in real life.

## How the expected values were produced (and why you can trust them)

`legacy/TaxApp.Tests/Golden/tax-vectors.json` is the golden master. It was **not** recorded from the legacy code. It is written by
[`scripts/golden/new_tax_vectors.py`](../scripts/golden/new_tax_vectors.py), an independent reference calculation in Python's `decimal`
module that uses a different algorithm (progressive slices, no base-tax constants) and asserts the ATO's published base amounts
($4,020, $31,020, $51,370 and the 2025–26 equivalents) before it writes anything. The legacy tests then assert that the legacy code
**agrees with it**. `./go.ps1 -Check` re-runs the generator in check mode, so the file cannot drift.

The same file is the oracle for the modern app (its tests read it) and for the HTTP contract, which reads the figures back out of
the rendered pages and the JSON endpoint.
