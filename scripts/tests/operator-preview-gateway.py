"""Loopback-only preview gateway. Forwards real API requests without logging bodies."""
import http.server
import urllib.request
import urllib.error

class Gateway(http.server.BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'
    def log_message(self, *args): pass
    def forward(self):
        upstream = 'http://127.0.0.1:5055' if self.path.startswith('/api/') else 'http://127.0.0.1:18730'
        body = self.rfile.read(int(self.headers.get('Content-Length', '0')))
        excluded = {'host', 'connection', 'content-length', 'transfer-encoding', 'upgrade'}
        headers = {k:v for k,v in self.headers.items() if k.lower() not in excluded}
        request = urllib.request.Request(upstream+self.path, data=body if body else None, headers=headers, method=self.command)
        try:
            response = urllib.request.urlopen(request, timeout=60)
        except urllib.error.HTTPError as error:
            response = error
        except (urllib.error.URLError, TimeoutError):
            self.send_error(502, 'Local preview upstream unavailable')
            return
        with response:
            data = response.read()
            self.send_response(response.status)
            for key,value in response.headers.items():
                if key.lower() not in {'connection', 'content-length', 'transfer-encoding'}:
                    self.send_header(key,value)
            self.send_header('Content-Length',str(len(data)))
            self.end_headers()
            if self.command != 'HEAD': self.wfile.write(data)
    do_GET = do_POST = do_PUT = do_PATCH = do_DELETE = do_OPTIONS = do_HEAD = forward

if __name__ == '__main__':
    print('Operator preview: http://127.0.0.1:18731', flush=True)
    http.server.ThreadingHTTPServer(('127.0.0.1',18731),Gateway).serve_forever()
