#!/usr/bin/env python3
"""Disposable loopback OTLP dev sink. Counts requests; never persists or prints telemetry bodies."""
import argparse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


class Sink(BaseHTTPRequestHandler):
    def do_POST(self):
        if self.path not in ("/v1/traces", "/v1/metrics", "/v1/logs"):
            self.send_error(404)
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            length = 0
        if not 0 < length <= 1048576 or self.headers.get("Content-Type") != "application/x-protobuf":
            self.send_error(400)
            return
        self.connection.settimeout(3)
        body = self.rfile.read(length)
        if len(body) != length:
            self.send_error(400)
            return
        # No body, IDs, headers or attributes retained. Closing the process resets all state.
        print(f"Accepted {self.path[4:]} protobuf bytes={length}", flush=True)
        self.send_response(200)
        self.send_header("Content-Type", "application/x-protobuf")
        self.send_header("Content-Length", "0")
        self.end_headers()

    def log_message(self, *_):
        pass


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=4318, choices=range(1024, 65536), metavar="PORT")
    args = parser.parse_args()
    print(f"Disposable OTLP sink on 127.0.0.1:{args.port}; Ctrl+C to stop. No forwarding or disk retention.", flush=True)
    try:
        with ThreadingHTTPServer(("127.0.0.1", args.port), Sink) as server:
            server.serve_forever()
    except KeyboardInterrupt:
        pass
