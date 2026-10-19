// tools/serve.cjs — servidor estático mínimo para abrir los bocetos desde Claude in Chrome (no acepta file://).
// Uso: node docs/bocetos/habilidades/tools/serve.cjs [carpeta = docs/bocetos] [puerto = 8765]
//      → http://127.0.0.1:8765/habilidades/jugable.html  ·  /habilidades/index.html
const http = require('http'), fs = require('fs'), path = require('path');
const root = path.resolve(process.argv[2] || path.join(__dirname, '..', '..')), port = +process.argv[3] || 8765;
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css', '.png': 'image/png', '.json': 'application/json' };
http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(req.url.split('?')[0]));
  if (!p.startsWith(root)) { res.writeHead(403); return res.end(); }
  fs.readFile(p, (e, d) => { if (e) { res.writeHead(404); return res.end('404'); } res.writeHead(200, { 'Content-Type': types[path.extname(p)] || 'application/octet-stream', 'Cache-Control': 'no-store' }); res.end(d); });
}).listen(port, '127.0.0.1', () => console.log('serving', root, 'on', port));
