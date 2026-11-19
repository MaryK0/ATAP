"""Save redacted copies in mitmproxy; leave the live HTTP exchange intact."""
import json
from pathlib import Path
from mitmproxy import http, io


def redact(value):
    if isinstance(value, dict):
        return {key: "[REDACTED]" if key.lower() in {"password", "accesstoken", "refreshtoken"}
                else redact(item) for key, item in value.items()}
    if isinstance(value, list):
        return [redact(item) for item in value]
    return value


class Capture:
    def __init__(self):
        self.stream = Path('/out/e2e.flow').open('wb')
        self.writer = io.FlowWriter(self.stream)
        self.log = Path('/out/e2e-traffic.log').open('w', encoding='utf-8')

    def response(self, flow: http.HTTPFlow):
        saved = flow.copy()
        for message in (saved.request, saved.response):
            for name in ('Authorization', 'Cookie', 'Set-Cookie'):
                if name in message.headers:
                    message.headers[name] = '[REDACTED]'
            if 'json' in message.headers.get('content-type', ''):
                try:
                    message.text = json.dumps(redact(json.loads(message.text)), ensure_ascii=False)
                except (ValueError, TypeError):
                    pass
        self.writer.add(saved)
        self.stream.flush()
        self.log.write(f"developer={flow.request.headers.get('X-ATAP-Developer', 'probe')} {flow.request.method} {flow.request.path} -> {flow.response.status_code}\n")
        self.log.flush()

    def done(self):
        self.stream.close()
        self.log.close()


addons = [Capture()]
