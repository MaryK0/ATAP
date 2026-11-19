"""Build a readable HTML copy of README; expose only the diagram assets."""
import re
from pathlib import Path

import markdown
from markdown.extensions.toc import slugify_unicode

source = Path("README.md").read_text(encoding="utf-8-sig")
body = markdown.markdown(source, extensions=["tables", "fenced_code", "toc", "sane_lists"],
                         extension_configs={"toc": {"slugify": slugify_unicode}})
body = re.sub(r'(src|href)="(?:\./)?(web_lr1/diagrams/[^"?#]+)',
              r'\1="/documentation/assets/\2', body)
# Other repository files are linked to their repository page, not exposed locally.
body = re.sub(r'href="(?!https?://|mailto:|#|/)([^"]+)"',
              r'href="https://github.com/MaryK0/ATAP/blob/main/\1"', body)
Path("documentation.html").write_text(
    '<!doctype html><html lang="ru"><meta charset="utf-8">'
    '<meta name="viewport" content="width=device-width,initial-scale=1">'
    '<title>ATAP — документация</title><link rel="stylesheet" href="/static/site.css">'
    '<main><a href="/management">Навигация</a>' + body + '</main></html>', encoding="utf-8")
