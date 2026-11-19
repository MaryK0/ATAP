"""Keep identical tests from different developers distinct in Allure."""
import hashlib
import json
import sys
from pathlib import Path

root, developer = Path(sys.argv[1]), sys.argv[2]
for path in root.glob("*-result.json"):
    result = json.loads(path.read_text())
    result.setdefault("parameters", []).append({"name": "developer", "value": developer})
    result["historyId"] = hashlib.md5((result.get("historyId", result["fullName"]) + developer).encode()).hexdigest()
    result.setdefault("labels", []).append({"name": "host", "value": developer})
    path.write_text(json.dumps(result, ensure_ascii=False))
