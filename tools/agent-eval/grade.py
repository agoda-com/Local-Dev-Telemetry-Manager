"""Grades an agent-eval answer: every planted problem found, no noise reported.

Usage: grade.py answer.md [forbidden-requests.txt]. The second file lists requests the run must not have made
(analysis exports, for the data-tools-only run); any line in it fails the grade.
"""
import json, re, sys

text = open(sys.argv[1], encoding="utf-8").read()
blocks = re.findall(r"```json\s*(\[.*?\])\s*```", text, re.S)
if not blocks:
    print("FAIL: no ```json block in the answer"); sys.exit(1)
findings = json.loads(blocks[-1])
blob = [(f.get("kind", ""), (f.get("subject", "") + " " + f.get("evidence", ""))) for f in findings]

def found(kinds, *needles):
    return any(k in kinds and any(n.lower() in s.lower() for n in needles) for k, s in blob)

planted = {
    "flaky CheckoutTests.PlacesOrder": found({"flaky_test"}, "PlacesOrder"),
    "broken CartTests.AppliesVoucher": found({"broken_test"}, "AppliesVoucher"),
    "slow InventoryTests (class setup)": found({"slow_tests"}, "InventoryTests"),
    ".Net compile regression, tied to 10.0.200": any(
        k in {"build_regression", "slow_build"} and ("Shop.Api" in s or ".Net" in s) and "10.0.200" in s for k, s in blob),
}
noise = {
    "TDD LoginTests.RejectsBadPassword": any("RejectsBadPassword" in f.get("subject", "") for f in findings),
    "rare ReportTests.ExportsYearEnd": any("ExportsYearEnd" in f.get("subject", "") for f in findings),
    "vitehmr with 4 samples": any("vitehmr" in f.get("subject", "").lower() or "shop-web" in f.get("subject", "").lower() for f in findings),
}
# The data-tools-only run must not have read analysis results through the export endpoint either.
leaks = [line.strip() for line in open(sys.argv[2], encoding="utf-8")] if len(sys.argv) > 2 else []
leaks = [l for l in leaks if l]
ok = all(planted.values()) and not any(noise.values()) and not leaks
for name, hit in planted.items(): print(f"{'found ' if hit else 'MISSED'}  {name}")
for name, hit in noise.items(): print(f"{'REPORTED' if hit else 'ignored '}  noise: {name}")
for l in leaks: print(f"LEAK  analysis export fetched: {l}")
print("PASS" if ok else "FAIL")
sys.exit(0 if ok else 1)
