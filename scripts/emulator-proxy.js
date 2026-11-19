// Dev helper for Windows machines where the Android emulator's built-in DNS proxy
// (10.0.2.3) does not answer, which breaks every hostname lookup inside the emulator.
// The guest can still reach the internet by IP, so route its traffic through this
// local proxy instead: the proxy resolves hostnames with the host DNS.
//
// Usage:
//   node scripts/emulator-proxy.js
//   adb shell settings put global http_proxy 10.0.2.2:8899
//   (clear with: adb shell settings put global http_proxy :0)
//
// Inside the emulator 10.0.2.2 is the host loopback, so the proxy binds 127.0.0.1 only.

const http = require('node:http');
const net = require('node:net');

const PORT = 8899;
const EMULATOR_HOST_ALIAS = '10.0.2.2';

const resolveTarget = (host) => (host === EMULATOR_HOST_ALIAS ? '127.0.0.1' : host);

const server = http.createServer((req, res) => {
  let target;
  try {
    target = new URL(req.url);
  } catch {
    res.writeHead(400);
    res.end('bad request');
    return;
  }

  const proxyReq = http.request(
    {
      host: resolveTarget(target.hostname),
      port: target.port || 80,
      path: target.pathname + target.search,
      method: req.method,
      headers: req.headers,
    },
    (proxyRes) => {
      res.writeHead(proxyRes.statusCode ?? 502, proxyRes.headers);
      proxyRes.pipe(res);
    },
  );

  proxyReq.on('error', () => {
    if (!res.headersSent) res.writeHead(502);
    res.end();
  });
  req.pipe(proxyReq);
});

server.on('connect', (req, clientSocket, head) => {
  const [host, port] = req.url.split(':');
  const upstream = net.connect(Number(port) || 443, resolveTarget(host), () => {
    clientSocket.write('HTTP/1.1 200 Connection Established\r\n\r\n');
    if (head.length > 0) upstream.write(head);
    upstream.pipe(clientSocket);
    clientSocket.pipe(upstream);
  });

  upstream.on('error', () => clientSocket.destroy());
  clientSocket.on('error', () => upstream.destroy());
});

server.listen(PORT, '127.0.0.1', () => console.log(`emulator proxy listening on 127.0.0.1:${PORT}`));
