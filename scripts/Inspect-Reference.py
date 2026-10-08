"""Read current official component descriptions/headings into ignored verification artifacts."""
import concurrent.futures
import json
import pathlib
import re
import urllib.request
import sys

sys.stdout.reconfigure(encoding="utf-8")

SLUGS = "accordion alert alert-dialog aspect-ratio attachment avatar badge breadcrumb bubble button button-group calendar card carousel chart checkbox collapsible combobox command context-menu data-table date-picker dialog direction drawer dropdown-menu empty field hover-card input input-group input-otp item kbd label marker menubar message message-scroller native-select navigation-menu pagination popover progress questionnaire radio-group resizable scroll-area select separator sheet sidebar skeleton slider spinner switch table tabs textarea toast toggle toggle-group tooltip typography".split()

def read(slug):
    url = f"https://ui.shadcn.com/docs/components/base/{slug}"
    with urllib.request.urlopen(url + ".md", timeout=30) as response:
        document = response.read().decode("utf-8")
    description = re.search(r"^description:\s*(.+)$", document, re.M)
    headings = re.findall(r"^#{2,4}\s+(.+)$", document, re.M)
    return {"slug": slug, "url": url, "description": description.group(1) if description else "", "sections": headings}

if __name__ == "__main__":
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as executor:
        results = list(executor.map(read, SLUGS))
    output = pathlib.Path(__file__).resolve().parents[1] / "artifacts" / "reference"
    output.mkdir(parents=True, exist_ok=True)
    (output / "shadcn-components.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
    for result in results:
        print(result["slug"] + ": " + result["description"] + " | " + ", ".join(result["sections"]))
    print(f"Verified {len(results)} official component pages.")
