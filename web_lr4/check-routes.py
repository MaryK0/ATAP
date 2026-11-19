"""Read-only checks of the actual NGINX routes, auth boundary and Blazor WebSocket."""
import base64
import hashlib
import json
import os
import socket
from html.parser import HTMLParser
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import quote, urljoin, urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener

root = Path(__file__).resolve().parent
settings = dict(line.split("=", 1) for line in (root / ".env").read_text().splitlines()
                if line and not line.startswith("#"))
base = f"http://127.0.0.1:{settings.get('WEB_PORT', '8088')}"
lines = (root / "secrets/access.txt").read_text().splitlines()
password = next(line.removeprefix("Password: ") for line in lines if line.startswith("Password: "))
username = settings.get("NGINX_USER", lines[0].rsplit(": ", 1)[-1])
credentials = base64.b64encode(f"{username}:{password}".encode()).decode()
requests = 0


class LocalRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, newurl):
        assert urlsplit(newurl).netloc == urlsplit(base).netloc, "Redirect changed host or port"
        assert urlsplit(newurl).scheme == urlsplit(base).scheme, "Redirect changed scheme"
        return super().redirect_request(request, response, code, message, headers, newurl)


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


class Links(HTMLParser):
    def __init__(self, body):
        super().__init__()
        self.urls = []
        self.feed(body.decode("utf-8"))

    def handle_starttag(self, tag, attrs):
        self.urls.extend(value for name, value in attrs
                         if name in ("href", "src", "action") and value)


opener = build_opener(LocalRedirect())
direct = build_opener(NoRedirect())

def check(path, expected=200, contains=None, auth=False, method="GET", follow=True,
          media=None, prefix=None, expected_headers=None, invalid_password=False):
    global requests
    credential = credentials if not invalid_password else base64.b64encode(f"{username}:incorrect-password".encode()).decode()
    headers = {"Authorization": "Basic " + credential} if auth or invalid_password else {}
    request = Request(base + path, headers=headers, method=method)
    try:
        response = (opener if follow else direct).open(request, timeout=10)
    except HTTPError as error:
        response = error
    with response:
        requests += 1
        body = response.read()
        assert response.code == expected, f"{path}: expected {expected}, got {response.code}"
        if contains is not None:
            assert contains in body, f"{path}: expected content missing"
        if media:
            assert response.headers.get_content_type() == media, f"{path}: wrong Content-Type"
        if prefix:
            assert urlsplit(response.geturl()).path.startswith(prefix), f"{path}: lost prefix"
        for name, value in (expected_headers or {}).items():
            assert value in response.headers.get(name, ""), f"{path}: incorrect {name}"
        if expected == 308:
            assert urljoin(base, response.headers["Location"]) == base + path + "/", \
                f"{path}: redirect lost external origin, port or path"
    print(f"PASS {path} -> {expected}" + (" (authenticated)" if auth else ""))
    return body

home = check("/", contains=b"/static/img/map.png", media="text/html")
assert check("/reserved/", follow=False) == home
for source in ("/reserved", "/legacy", "/api/v1/docs"):
    check(source, expected=308, follow=False)
menu = check("/management", contains=b"/admin/", media="text/html")
menu_links = set(Links(menu).urls)
for required in ("/api/v1/airports", "/api/v1/docs/", "/legacy/", "/documentation",
                 "/", "/reserved/", "/admin/", "/status"):
    assert required in menu_links, f"Missing management link: {required}"
assert all(not urlsplit(link).username for link in menu_links), "Credentials in menu links"
docs = check("/documentation", contains=b"Air Traffic Analytics Platform", media="text/html")
assets = {url for url in Links(home).urls if url.startswith("/static/")}
assert len(assets) >= 4, "Home page sketches or stylesheet are missing"
for asset in sorted(assets):
    if asset.endswith(".png"):
        check(asset, contains=b"\x89PNG\r\n\x1a\n", media="image/png")
    elif asset.endswith(".css"):
        check(asset, media="text/css")
diagrams = {url for url in Links(docs).urls if url.startswith("/documentation/assets/")}
assert diagrams, "README diagrams are missing"
for diagram in sorted(diagrams):
    check(diagram, media="image/png" if diagram.endswith(".png") else None)
check("/api/v1/docs/", contains=b"swagger-ui", prefix="/api/v1/docs/")
check("/api/v1/docs/index.js", contains=b"/api/v1/docs/openapi.yaml")
check("/api/v1/docs/swagger-ui.css", media="text/css")
check("/api/v1/docs/swagger-ui-bundle.js")
actual = check("/api/v1/docs/openapi.yaml")
assert actual == (root.parent / "web_lr2/openapi.yaml").read_bytes()
page = json.loads(check("/api/v1/airports?limit=1&offset=1", media="application/json"))
assert page["limit"] == 1 and page["offset"] == 1 and len(page["items"]) <= 1, "API query was lost"
problem = json.loads(check("/api/v1/reports", expected=401, media="application/problem+json",
                          expected_headers={"WWW-Authenticate": "Bearer"}))
assert problem["status"] == 401, "Incorrect API error status"
check("/legacy/", contains=b'<base href="/legacy/"', prefix="/legacy/")
for page in ("login", "map", "admin"):
    check("/legacy/" + page, prefix="/legacy/")
check("/legacy/app.css", media="text/css")
check("/legacy/js/map.js")
check("/legacy/_framework/blazor.server.js")
assert check("/health", media="text/plain", follow=False).strip() == b"alive"
assert check("/ready", follow=False, expected_headers={"Cache-Control": "no-store"}).strip() == b"ready"
for path in ("/admin", "/admin/", "/admin/login", "/status"):
    check(path, expected=401, follow=False, expected_headers={"WWW-Authenticate": "Basic"})
check("/admin/", expected=401, invalid_password=True, follow=False)
check("/admin", expected=308, auth=True, follow=False)
admin_page = check("/admin/", contains=b"pgAdmin", auth=True, prefix="/admin/")
assert b"password" in admin_page.lower(), "pgAdmin must require its own login"
for url in Links(admin_page).urls:
    if url.startswith("/"):
        assert url.startswith("/admin/"), "pgAdmin resource lost /admin prefix"
check("/status", contains=b"Active connections", auth=True)
for path in ("/unknown", "/api/v10/airports", "/legacy-other", "/api/v1/unknown",
             "/.env", "/secrets/access.txt", "/static/missing.png"):
    check(path, expected=404, follow=False)

negotiation = json.loads(check("/legacy/_blazor/negotiate?negotiateVersion=1", method="POST"))
target = urlsplit(base)
path = "/legacy/_blazor?id=" + quote(negotiation["connectionToken"], safe="")
key = base64.b64encode(os.urandom(16)).decode()
with socket.create_connection((target.hostname, target.port), timeout=10) as connection:
    connection.sendall((f"GET {path} HTTP/1.1\r\nHost: {target.netloc}\r\n"
        "Upgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\n"
        f"Sec-WebSocket-Key: {key}\r\n\r\n").encode())
    response_headers = b""
    while b"\r\n\r\n" not in response_headers and len(response_headers) < 16384:
        chunk = connection.recv(4096)
        if not chunk:
            break
        response_headers += chunk
    assert response_headers.startswith(b"HTTP/1.1 101 "), "WebSocket upgrade failed"
    accept = base64.b64encode(hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest())
    assert b"sec-websocket-accept: " + accept.lower() in response_headers.lower(), "Invalid WebSocket accept"
    connection.sendall(b"\x88\x80" + os.urandom(4))  # masked close frame
requests += 1
print("PASS /legacy/_blazor -> 101 Switching Protocols")
print(f"All route checks passed ({requests} HTTP checks). Full browser UI interaction is a separate manual check.")
