const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const { URL } = require('node:url');

const port = Number(process.env.SMARTX_WEB_PORT || 4200);
const apiOrigin = process.env.SMARTX_API_ORIGIN || 'http://localhost:5163';
const webRoot = process.env.SMARTX_WEB_ROOT || path.resolve(
  __dirname,
  '..',
  '.artifacts',
  'web-dist',
  'omnibusiness-web',
  'browser',
);
const requestLogPath = path.resolve(__dirname, '..', '.artifacts', 'local-web-proxy-requests.log');

const contentTypes = {
  '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8',
  '.ico': 'image/x-icon',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
};

function proxyToApi(request, response) {
  const target = new URL(request.url, apiOrigin);
  const upstream = http.request(target, {
    method: request.method,
    headers: { ...request.headers, host: target.host },
  }, (upstreamResponse) => {
    fs.appendFileSync(
      requestLogPath,
      `${new Date().toISOString()} ${request.method} ${request.url} -> ${upstreamResponse.statusCode}\n`,
    );
    response.writeHead(upstreamResponse.statusCode || 502, upstreamResponse.headers);
    upstreamResponse.pipe(response);
  });

  upstream.on('error', () => {
    response.writeHead(502, { 'content-type': 'application/json; charset=utf-8' });
    response.end(JSON.stringify({ message: 'SmartX API is unavailable. Start run-api.cmd first.' }));
  });

  request.pipe(upstream);
}

function serveSpa(request, response) {
  const requestedPath = decodeURIComponent(new URL(request.url, `http://localhost:${port}`).pathname);
  const relativePath = requestedPath === '/' ? 'index.html' : requestedPath.replace(/^[/\\]+/, '');
  const candidate = path.resolve(webRoot, relativePath);
  const safePath = candidate.startsWith(webRoot) ? candidate : path.join(webRoot, 'index.html');
  const filePath = fs.existsSync(safePath) && fs.statSync(safePath).isFile()
    ? safePath
    : path.join(webRoot, 'index.html');

  fs.readFile(filePath, (error, content) => {
    if (error) {
      response.writeHead(503, { 'content-type': 'text/plain; charset=utf-8' });
      response.end('SmartX web build was not found. Run the Angular build when available.');
      return;
    }

    response.writeHead(200, {
      'content-type': contentTypes[path.extname(filePath)] || 'application/octet-stream',
      'cache-control': 'no-store',
    });
    response.end(content);
  });
}

http.createServer((request, response) => {
  if (request.url?.startsWith('/api/') || request.url === '/health' || request.url === '/ready') {
    proxyToApi(request, response);
    return;
  }

  serveSpa(request, response);
}).listen(port, '127.0.0.1', () => {
  console.log(`SmartX web is available at http://localhost:${port}`);
});
