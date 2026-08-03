#!/usr/bin/env python3
"""build_pdf.py — Markdown → styled PDF in one command.

Takes any Markdown file (defaults to docs/Documentation.md) and produces a
vector PDF with: print-optimized CSS, code blocks that wrap (never clip),
clickable internal TOC links, page breaks before each H1, and proper metadata.

Pipeline (all in this one script):
  1. Markdown → HTML fragment          (Python `markdown` lib)
  2. Inject id= on every heading       (so #anchor TOC links resolve)
  3. Wrap in styled HTML doc           (self-contained, no external assets)
  4. HTML → PDF                         (Playwright via the pdf skill's html2pdf-next.js;
                                          Chromium emits correct, clickable GoTo-named-destination
                                          link annotations for every #anchor natively — no
                                          post-processing needed, see set_pdf_metadata's doc for
                                          the bug history here)
  5. Set PDF metadata                   (title, author, subject)

Usage:
  .pdf-venv/bin/python docs/build_pdf.py                       # default: docs/Documentation.{md,pdf}
  .pdf-venv/bin/python docs/build_pdf.py path/to/input.md      # custom input, output alongside it
  .pdf-venv/bin/python docs/build_pdf.py input.md output.pdf   # both explicit

Requirements (already set up in this repo):
  - .pdf-venv/       : project-local venv with `markdown` + `pymupdf`
  - Playwright + chromium (npm-global, ms-playwright cache)
  - the pdf skill's html2pdf-next.js at $PDF_SKILL_DIR/scripts/

Output: a vector PDF (selectable text, sharp at any zoom) alongside the input
by default, plus the intermediate HTML (kept for browser viewing / re-printing).
"""
from __future__ import annotations

import html as html_lib
import markdown
import os
import re
import subprocess
import sys
import pymupdf
from pathlib import Path

# ── Paths ────────────────────────────────────────────────────────────────────
REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_INPUT = REPO_ROOT / "docs" / "Documentation.md"
VENV_PYTHON = REPO_ROOT / ".pdf-venv" / "bin" / "python"
PDF_SKILL_DIR = Path(
    os.environ.get("PDF_SKILL_DIR")
    or Path.home() / ".zcode/cli/plugins/cache/zcode-plugins-official"
    "/document-skills/0.1.0/skills/pdf"
)
HTML2PDF_JS = PDF_SKILL_DIR / "scripts" / "html2pdf-next.js"

# Metadata applied to the final PDF. Override by editing these constants if
# you build a different document (e.g. a SPECIFICATION.pdf).
DOC_TITLE = "Topos — Documentation"
DOC_AUTHOR = "Nasser Towfigh"
DOC_SUBJECT = (
    "Combined user-facing documentation for the Topos typed-property "
    "hypergraph library for C#."
)
DOC_KEYWORDS = "topos, hypergraph, csharp, ai-memory, agent-memory, knowledge-graph"


# ── Step 1+2: Markdown → HTML with heading IDs ───────────────────────────────
def slug(text: str) -> str:
    """GitHub's heading-slug algorithm (lowercase, strip punctuation, spaces→hyphens)."""
    text = re.sub(r"[`*_~]", "", text).lower()
    text = re.sub(r"[^\w\s-]", "", text)
    return re.sub(r"[\s]+", "-", text.strip())


def add_heading_ids(html: str) -> str:
    """Inject id= on every <hN>, including ones whose content has nested <code> etc.
    The slug is computed from visible text (HTML-escaped entities unescaped first,
    tags stripped) so it matches the (#anchor) links in the source Markdown."""
    seen: dict[str, int] = {}

    def strip_tags(s: str) -> str:
        # Unescape entities FIRST so '&amp;' → '&' → stripped, not '...amp...'.
        return re.sub(r"<[^>]+>", "", html_lib.unescape(s))

    def repl(m: re.Match) -> str:
        level, inner = m.group(1), m.group(2)
        s = slug(strip_tags(inner))
        if s in seen:
            seen[s] += 1
            s = f"{s}-{seen[s]}"
        else:
            seen[s] = 0
        return f'<h{level} id="{s}">{inner}</h{level}>'

    return re.sub(r"<h([1-6])>(.*?)</h\1>", repl, html, flags=re.DOTALL)


def markdown_to_html_body(md_text: str) -> str:
    """Convert Markdown source to an HTML fragment with heading IDs."""
    body = markdown.markdown(
        md_text,
        extensions=["fenced_code", "tables", "sane_lists", "attr_list"],
    )
    return add_heading_ids(body)


# ── Step 3: wrap in a styled HTML document ───────────────────────────────────
CSS = """
@page {
  size: Letter;          /* US Letter */
  margin: 18mm 16mm 18mm 16mm;
}
html, body {
  margin: 0;
  padding: 0;
  background: #ffffff;
  font-family: "Charter", "Georgia", "Iowan Old Style", "Palatino Linotype", "Book Antiqua", serif;
  font-size: 10.5pt;
  line-height: 1.55;
  color: #1a1a1a;
  -webkit-print-color-adjust: exact;
  print-color-adjust: exact;
}
h1, h2, h3, h4, h5, h6 {
  font-family: "Helvetica Neue", "Inter", "Helvetica", "Arial", sans-serif;
  color: #0f172a;
  line-height: 1.25;
  font-weight: 600;
}
h1 {
  font-size: 22pt;
  margin: 0 0 14pt 0;
  padding-bottom: 6pt;
  border-bottom: 2pt solid #0f172a;
  break-before: page;            /* each H1 starts a new page */
  page-break-before: always;
}
h1:first-of-type { break-before: avoid; page-break-before: avoid; }  /* title needs no leading blank page */
h2 {
  font-size: 15pt;
  margin: 22pt 0 8pt 0;
  padding-bottom: 3pt;
  border-bottom: 0.5pt solid #cbd5e1;
  break-after: avoid; page-break-after: avoid;
}
h3 { font-size: 12.5pt; margin: 16pt 0 6pt 0; color: #1e293b; break-after: avoid; page-break-after: avoid; }
h4 { font-size: 11pt; margin: 12pt 0 4pt 0; color: #334155; break-after: avoid; page-break-after: avoid; }
p { margin: 0 0 8pt 0; orphans: 3; widows: 3; }
ul, ol { margin: 0 0 8pt 0; padding-left: 22pt; }
li { margin: 2pt 0; }
li > p { margin: 2pt 0; }
code {
  font-family: "JetBrains Mono", "SF Mono", "Menlo", "Consolas", monospace;
  font-size: 8.8pt;
}
p code, li code, td code {
  background: #f1f5f9; padding: 0.5pt 3pt; border-radius: 2pt;
  color: #be123c; word-break: break-word;
}
pre {
  background: #f8fafc;
  border: 0.5pt solid #e2e8f0;
  border-radius: 3pt;
  padding: 8pt 10pt;
  margin: 8pt 0 12pt 0;
  overflow-wrap: break-word; word-wrap: break-word;
  white-space: pre-wrap;        /* wrap long lines instead of clipping */
  break-inside: avoid; page-break-inside: avoid;
}
pre code { background: transparent; color: #0f172a; padding: 0; font-size: 8.6pt; line-height: 1.4; }
blockquote {
  margin: 8pt 0 12pt 0; padding: 6pt 12pt;
  border-left: 3pt solid #94a3b8; background: #f8fafc;
  color: #334155; font-size: 9.8pt;
  break-inside: avoid; page-break-inside: avoid;
}
blockquote p { margin: 3pt 0; }
table {
  border-collapse: collapse; width: 100%; margin: 8pt 0 12pt 0;
  font-size: 9.2pt; break-inside: avoid; page-break-inside: avoid;
}
th, td { border: 0.5pt solid #cbd5e1; padding: 4pt 7pt; text-align: left; vertical-align: top; }
th { background: #f1f5f9; font-weight: 600; font-family: "Helvetica Neue", sans-serif; }
tbody tr:nth-child(even) { background: #fafbfc; }
a { color: #1d4ed8; text-decoration: none; }
a:hover { text-decoration: underline; }
hr { border: none; border-top: 0.5pt solid #cbd5e1; margin: 16pt 0; }
body > h1:first-child {
  font-size: 26pt; border-bottom: 3pt solid #0f172a; margin-bottom: 6pt;
}
strong { color: #0f172a; font-weight: 600; }
em { color: #1e293b; }
"""


def wrap_html(body: str, title: str) -> str:
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{title}</title>
<meta name="author" content="{DOC_AUTHOR}">
<meta name="description" content="{DOC_SUBJECT}">
<meta name="generator" content="Topos docs/build_pdf.py (Markdown + Playwright)">
<style>
{CSS}
</style>
</head>
<body>
{body}
</body>
</html>
"""


# ── Step 5: set PDF metadata ─────────────────────────────────────────────────
def set_pdf_metadata(pdf_path: Path) -> None:
    """Set document metadata (title/author/subject/keywords).

    Internal `#anchor` links do NOT need to be injected here: Chromium's
    page.pdf() already emits correct, working GoTo-named-destination link
    annotations for every `<a href="#anchor">` in the source HTML (verified
    2026-08-03 — all 58 source links resolve to the right page natively).

    An earlier version of this function *also* added a second, overlapping
    set of link annotations on top of Chromium's own, on the theory that
    Chromium's internal links were unreliable. That theory was wrong, and the
    added annotations were themselves buggy (their page-locator searched for
    each heading's text starting from page 0, where the *table of contents*
    itself contains the same heading text — so it matched the TOC entry, not
    the real heading, and pointed almost every link back to page 0, i.e.
    nowhere). Layered on top of Chromium's correct links, these broken
    duplicates were what actually intercepted clicks in at least one PDF
    viewer, making the (real, correct, blue-styled) links look dead. Removed
    entirely rather than patched, since Chromium's native output already does
    this job correctly with no extra code needed.
    """
    doc = pymupdf.open(pdf_path)
    doc.set_metadata({
        "title": DOC_TITLE,
        "author": DOC_AUTHOR,
        "subject": DOC_SUBJECT,
        "creator": "Topos docs/build_pdf.py",
        "keywords": DOC_KEYWORDS,
    })
    doc.saveIncr()
    doc.close()


# ── Orchestration ────────────────────────────────────────────────────────────
def main(argv: list[str]) -> int:
    # Parse args: 0=use defaults, 1=custom input, 2=custom input+output
    if len(argv) == 1:
        md_path = DEFAULT_INPUT
    elif len(argv) == 2:
        md_path = Path(argv[1]).resolve()
    elif len(argv) == 3:
        md_path = Path(argv[1]).resolve()
        pdf_path_explicit = Path(argv[2]).resolve()
    else:
        print(__doc__)
        return 2  # usage error

    if not md_path.exists():
        print(f"✗ Input not found: {md_path}", file=sys.stderr)
        return 1

    # Default outputs sit alongside the input .md (same stem).
    stem = md_path.with_suffix("")
    html_path = stem.with_suffix(".html")
    if len(argv) == 3:
        pdf_path = pdf_path_explicit
    else:
        pdf_path = stem.with_suffix(".pdf")

    # Title: use the default for the canonical Documentation.md; for any other
    # input, derive from the filename so the PDF metadata isn't misleading.
    # (e.g. docs/MCP_SERVER_SPEC.md → "Topos — MCP_SERVER_SPEC")
    global DOC_TITLE, DOC_SUBJECT
    if md_path != DEFAULT_INPUT:
        DOC_TITLE = f"Topos — {md_path.stem}"
        DOC_SUBJECT = f"Source: {md_path.relative_to(REPO_ROOT)}"

    print(f"📖 Reading:   {md_path}")
    md_text = md_path.read_text(encoding="utf-8")

    # Steps 1-3: Markdown → styled HTML
    print(f"📝 Converting Markdown → HTML...")
    body = markdown_to_html_body(md_text)
    html_doc = wrap_html(body, title=DOC_TITLE)
    html_path.write_text(html_doc, encoding="utf-8")
    print(f"   wrote {html_path} ({html_path.stat().st_size:,} bytes)")

    # Sanity check: do all internal #links resolve to a heading id in the HTML?
    ids = set(re.findall(r'<h[1-6] id="([^"]+)"', html_doc))
    links = set(re.findall(r'href="#([^"]+)"', html_doc))
    broken = links - ids
    if broken:
        print(f"   ⚠ {len(broken)} broken anchor(s) in source Markdown (PDF will still build):")
        for b in sorted(broken)[:5]:
            print(f"       #{b}")

    # Step 4: HTML → PDF via the skill's html2pdf-next.js (Playwright + Chromium)
    print(f"📄 Rendering PDF via Playwright...")
    if not HTML2PDF_JS.exists():
        print(f"✗ html2pdf-next.js not found at {HTML2PDF_JS}", file=sys.stderr)
        print("  Install the zcode-plugins-official document-skills plugin.", file=sys.stderr)
        return 1
    # Always start from a clean PDF (saveIncr() below appends to whatever's there).
    if pdf_path.exists():
        pdf_path.unlink()
    cmd = ["node", str(HTML2PDF_JS), str(html_path), "--output", str(pdf_path)]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0 or not pdf_path.exists():
        print(f"✗ PDF render failed:", file=sys.stderr)
        print(result.stderr or result.stdout, file=sys.stderr)
        return 1

    # Step 5: metadata (internal links are already correct — see set_pdf_metadata's doc)
    print(f"🔖 Setting PDF metadata...")
    set_pdf_metadata(pdf_path)

    # Final report
    doc = pymupdf.open(pdf_path)
    n_pages = len(doc)
    internal_links = sum(
        1 for p in range(n_pages) for l in doc[p].get_links()
        if l.get("page") is not None
    )
    meta = doc.metadata
    doc.close()

    size_kb = pdf_path.stat().st_size / 1024
    print()
    print(f"════════════════════════════════════════")
    print(f"  ✓ PDF generated")
    print(f"════════════════════════════════════════")
    print(f"  File:    {pdf_path}")
    print(f"  Pages:   {n_pages}")
    print(f"  Size:    {size_kb:.1f} KB")
    print(f"  Links:   {internal_links} internal clickable (native Chromium named-destination links)")
    print(f"  Title:   {meta.get('title')!r}")
    print(f"  Author:  {meta.get('author')!r}")
    print(f"  HTML:    {html_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
