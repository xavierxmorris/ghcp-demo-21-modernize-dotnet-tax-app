#!/usr/bin/env python3
"""Independent reference calculator that authors the Tax App golden master.

Why this exists: the golden master must not be "whatever the legacy code printed".
This script re-derives every expected value from the ATO's published resident tax scale
using a different algorithm from the app (progressive slices, no base-tax constants) and
Python's decimal module. The legacy tests then assert that the legacy code agrees with it.

Usage:
    python scripts/golden/new_tax_vectors.py            # rewrite legacy/TaxApp.Tests/Golden/tax-vectors.json
    python scripts/golden/new_tax_vectors.py --check    # fail if the committed file differs (used by go.ps1 -Check)

Rates: https://www.ato.gov.au/tax-rates-and-codes/tax-rates-australian-residents (retrieved 2026-10-26).
Illustrative only: no offsets, no Medicare levy thresholds, no HELP. Not tax advice.
"""
from __future__ import annotations

import argparse
import json
import sys
from decimal import ROUND_HALF_UP, Decimal as D
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / "legacy" / "TaxApp.Tests" / "Golden" / "tax-vectors.json"

MEDICARE_RATE = D("0.02")
GST_RATE = D("0.10")

# (lower bound, upper bound or None, marginal rate)
BANDS = {
    "2025-26": [
        (D(0), D(18200), D("0")),
        (D(18200), D(45000), D("0.16")),
        (D(45000), D(135000), D("0.30")),
        (D(135000), D(190000), D("0.37")),
        (D(190000), None, D("0.45")),
    ],
    "2026-27": [
        (D(0), D(18200), D("0")),
        (D(18200), D(45000), D("0.15")),
        (D(45000), D(135000), D("0.30")),
        (D(135000), D(190000), D("0.37")),
        (D(190000), None, D("0.45")),
    ],
}

# Figures copied from the ATO page, used only as cross-checks on the slice algorithm.
ATO_PUBLISHED_TAX_AT_BAND_STARTS = {
    "2025-26": {D(45000): D(4288), D(135000): D(31288), D(190000): D(51638)},
    "2026-27": {D(45000): D(4020), D(135000): D(31020), D(190000): D(51370)},
}


def q(value: D) -> D:
    return value.quantize(D("0.01"), rounding=ROUND_HALF_UP)


def s(value: D) -> str:
    return format(q(value), "f")


def tax_on(year: str, taxable: D) -> D:
    total = D(0)
    for lower, upper, rate in BANDS[year]:
        if taxable <= lower:
            break
        top = taxable if upper is None else min(taxable, upper)
        total += (top - lower) * rate
    return total


def estimate(year: str, income: D, deductions: D) -> dict:
    taxable = max(D(0), income - deductions)
    income_tax = q(tax_on(year, taxable))
    medicare = q(taxable * MEDICARE_RATE)
    total = income_tax + medicare
    effective = q(total * 100 / income) if income > 0 else D(0)
    return {
        "taxableIncome": s(taxable),
        "incomeTax": s(income_tax),
        "medicareLevy": s(medicare),
        "totalTax": s(total),
        "netIncome": s(income - total),
        "effectiveRate": s(effective),
    }


def gst(mode: str, amount: D) -> dict:
    if mode == "AddGst":
        tax = q(amount * GST_RATE)
        return {"gstAmount": s(tax), "amountExclGst": s(amount), "amountInclGst": s(amount + tax)}
    tax = q(amount * GST_RATE / (1 + GST_RATE))
    return {"gstAmount": s(tax), "amountExclGst": s(amount - tax), "amountInclGst": s(amount)}


def build() -> dict:
    for year, checks in ATO_PUBLISHED_TAX_AT_BAND_STARTS.items():
        for income, published in checks.items():
            assert tax_on(year, income) == published, (year, income, tax_on(year, income), published)

    estimate_cases = [
        ("zero income", "2026-27", "0", "0"),
        ("at the tax-free threshold", "2026-27", "18200", "0"),
        ("one dollar over the threshold", "2026-27", "18201", "0"),
        ("low income", "2026-27", "30000", "0"),
        ("top of the 15% band", "2026-27", "45000", "0"),
        ("middle income", "2026-27", "95000", "0"),
        ("top of the 30% band", "2026-27", "135000", "0"),
        ("top of the 37% band", "2026-27", "190000", "0"),
        ("high income", "2026-27", "250000", "0"),
        ("middle income with deductions", "2026-27", "95000", "5000"),
        ("deductions larger than income", "2026-27", "20000", "25000"),
        ("rounding: income tax .045 rounds up", "2026-27", "18200.30", "0"),
        ("rounding: Medicare levy .905 rounds up", "2026-27", "12345.25", "0"),
        ("cents in income", "2026-27", "87654.32", "1234.56"),
        ("2025-26 just over the threshold", "2025-26", "18201", "0"),
        ("2025-26 top of the lowest taxed band", "2025-26", "45000", "0"),
        ("2025-26 middle income", "2025-26", "95000", "0"),
        ("2025-26 top of the 37% band", "2025-26", "190000", "0"),
        ("2025-26 high income with deductions", "2025-26", "210000", "10000"),
    ]
    gst_cases = [
        ("add GST to a round price", "AddGst", "100"),
        ("extract GST from a round price", "ExtractGst", "110"),
        ("add GST, rounds up", "AddGst", "19.99"),
        ("extract GST, rounds down", "ExtractGst", "19.99"),
        ("rounding: add GST .005 rounds up", "AddGst", "0.05"),
        ("zero amount", "ExtractGst", "0"),
        ("large amount", "AddGst", "1234567.89"),
    ]
    return_cases = [
        ("refund with deductions", "2026-27", "95000", "5000", "20000"),
        ("amount payable", "2026-27", "95000", "0", "18000"),
        ("below threshold, Medicare only", "2026-27", "18000", "0", "0"),
        ("2025-26 with seeded deductions", "2025-26", "60000", "2250.50", "10000"),
        ("withheld exactly equals tax", "2026-27", "95000", "0", "20920"),
    ]

    estimates = []
    for name, year, income, deductions in estimate_cases:
        estimates.append({
            "name": name,
            "taxYear": year,
            "income": s(D(income)),
            "deductions": s(D(deductions)),
            "expected": estimate(year, D(income), D(deductions)),
        })

    gsts = [
        {"name": name, "mode": mode, "amount": s(D(amount)), "expected": gst(mode, D(amount))}
        for name, mode, amount in gst_cases
    ]

    returns = []
    for name, year, income, deductions_total, withheld in return_cases:
        e = estimate(year, D(income), D(deductions_total))
        balance = D(withheld) - D(e["totalTax"])
        returns.append({
            "name": name,
            "taxYear": year,
            "income": s(D(income)),
            "deductionsTotal": s(D(deductions_total)),
            "withheld": s(D(withheld)),
            "expected": {
                "totalTax": e["totalTax"],
                "isRefund": balance >= 0,
                "resultAmount": s(abs(balance)),
            },
        })

    return {
        "description": "Golden master for the Tax App. Expected values come from an independent reference calculation, "
                       "not from the application code. Do not hand-edit; regenerate with scripts/golden/new_tax_vectors.py.",
        "source": {
            "ratesUrl": "https://www.ato.gov.au/tax-rates-and-codes/tax-rates-australian-residents",
            "retrieved": "2026-10-26",
            "medicareLevyRate": "0.02",
            "gstRate": "0.10",
            "rounding": "2 decimal places, midpoint away from zero",
            "generator": "scripts/golden/new_tax_vectors.py",
            "disclaimer": "Illustrative only. Not tax advice and not an ATO service.",
        },
        "estimate": estimates,
        "gst": gsts,
        "return": returns,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="fail if the committed file differs from a fresh run")
    parser.add_argument("--out", type=Path, default=OUT)
    args = parser.parse_args()

    document = build()
    text = json.dumps(document, indent=2, ensure_ascii=False) + "\n"

    if args.check:
        current = args.out.read_text(encoding="utf-8") if args.out.exists() else ""
        if current.replace("\r\n", "\n") != text:
            print(f"DIFFERENT: {args.out} does not match the reference calculation. Run without --check to regenerate.")
            return 1
        print(f"OK: {args.out.name} matches the independent reference calculation "
              f"({len(document['estimate'])} estimate, {len(document['gst'])} GST, {len(document['return'])} return vectors)")
        return 0

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
