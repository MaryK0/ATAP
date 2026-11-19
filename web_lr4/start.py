"""Create missing local credentials, build the stack and wait until it is ready."""
import argparse
import secrets
import subprocess
import time
from pathlib import Path
from urllib.error import URLError
from urllib.request import urlopen

root = Path(__file__).resolve().parent


def create_local_config(port):
    """Create local secrets only for a genuinely new deployment."""
    if (root / ".env").exists() or (root / "secrets").exists():
        raise SystemExit("Local configuration already exists; nothing overwritten.")
    passwords = {name: secrets.token_hex(24) for name in ("DB_PASSWORD", "JWT_KEY", "PGADMIN_PASSWORD")}
    admin_password = secrets.token_urlsafe(24)
    result = subprocess.run(
        ["docker", "run", "--rm", "-i", "--entrypoint", "htpasswd", "httpd:2.4-alpine", "-niB", "admin"],
        input=admin_password + "\n", text=True, capture_output=True)
    if result.returncode:
        raise SystemExit("Cannot generate password hash. Start Docker Desktop and retry.\n" + result.stderr)
    secret_dir = root / "secrets"
    secret_dir.mkdir(mode=0o700)
    (secret_dir / "htpasswd").write_text(result.stdout.strip() + "\n", encoding="ascii")
    (root / ".env").write_text(
        f"WEB_PORT={port}\nDB_USER=admin\nNGINX_USER=admin\nPGADMIN_EMAIL=admin@example.com\n" +
        "".join(f"{name}={value}\n" for name, value in passwords.items()), encoding="ascii")
    (secret_dir / "access.txt").write_text(
        "NGINX /admin and /status: admin\nPassword: " + admin_password +
        "\npgAdmin login: admin@example.com\nPassword: " + passwords["PGADMIN_PASSWORD"] +
        "\nDatabase user: admin\nPassword: " + passwords["DB_PASSWORD"] + "\n", encoding="ascii")
    for path in (root / ".env", secret_dir / "htpasswd", secret_dir / "access.txt"):
        path.chmod(0o600)
    print("Created local credentials in web_lr4/.env and web_lr4/secrets/ (not tracked by Git).")


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--port", type=int, help="Port for the first setup; existing settings are not overwritten")
parser.add_argument("--project", help="Optional separate Compose project name")
args = parser.parse_args()
if args.port is not None and not 1 <= args.port <= 65535:
    parser.error("port must be between 1 and 65535")
files = [root / ".env", root / "secrets/htpasswd", root / "secrets/access.txt"]
if not any(path.exists() for path in files):
    create_local_config(args.port if args.port is not None else 8088)
elif not all(path.is_file() for path in files):
    raise SystemExit("Incomplete local configuration. Restore the missing local files; nothing overwritten.")
settings = dict(line.split("=", 1) for line in files[0].read_text(encoding="utf-8-sig").splitlines()
                if line and not line.startswith("#"))
port = int(settings.get("WEB_PORT", "8088"))
if args.port is not None and port != args.port:
    raise SystemExit("Existing WEB_PORT differs from --port; edit .env deliberately. Nothing overwritten.")
compose = ["docker", "compose", "--env-file", str(files[0]), "-f", str(root / "compose.yaml")]
if args.project:
    compose += ["--project-name", args.project]
subprocess.run(compose + ["config", "--quiet"], check=True)
subprocess.run(compose + ["up", "-d", "--build", "--wait", "--wait-timeout", "180"], check=True)
base = f"http://127.0.0.1:{port}"
deadline = time.monotonic() + 90
while time.monotonic() < deadline:
    try:
        with urlopen(base + "/ready", timeout=5) as response:
            if response.status == 200 and response.read().strip() == b"ready":
                print(f"ATAP ready: {base}/management")
                break
    except (URLError, OSError):
        pass
    time.sleep(1)
else:
    raise SystemExit("Readiness timeout; inspect docker compose logs. Data and configuration were preserved.")
