// tools/exportar-iconos-armas.cjs — pasa a PNG (128×128, fondo transparente) los iconos de iconos-armas.html.
// Uso: node docs/bocetos/habilidades/tools/exportar-iconos-armas.cjs <carpeta de salida> [vista.png]
//   Abre la página en Chrome/Edge headless con --dump-dom y guarda los dataURL de #out (icon_<id>.png, el id es el de
//   ArsenalKit.WeaponId). Con un segundo argumento guarda además una captura de la página (vista previa).
const fs = require('fs'), path = require('path'), { execFileSync } = require('child_process');
const out = process.argv[2], shot = process.argv[3];
if (!out) { console.log('falta la carpeta de salida'); process.exit(1); }
const browsers = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'];
const exe = browsers.find(b => fs.existsSync(b));
if (!exe) { console.log('no hay Chrome ni Edge'); process.exit(1); }
const page = 'file:///' + path.resolve(__dirname, '..', 'iconos-armas.html').replace(/\\/g, '/');
const base = ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--allow-file-access-from-files'];

const dom = execFileSync(exe, [...base, '--dump-dom', page], { encoding: 'utf8', maxBuffer: 64 << 20 });
const pre = /<pre id="out">([\s\S]*?)<\/pre>/.exec(dom);
if (!pre) { console.log('la página no ha escrito #out'); process.exit(1); }
fs.mkdirSync(out, { recursive: true });
for (const line of pre[1].trim().split('\n')) {
  const [id, url] = [line.slice(0, line.indexOf(':')), line.slice(line.indexOf(':') + 1)];
  const file = path.join(out, `icon_${id}.png`);
  fs.writeFileSync(file, Buffer.from(url.replace(/^data:image\/png;base64,/, ''), 'base64'));
  console.log(file);
}
if (shot) execFileSync(exe, [...base, '--window-size=1100,1000', '--screenshot=' + path.resolve(shot), page]);
