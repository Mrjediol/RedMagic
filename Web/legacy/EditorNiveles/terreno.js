// EditorNiveles — vista previa: terreno (roca + borde de hielo) y fondo de parallax, con los valores del bioma que
// exporta Unity en datos-unity.js. Es una aproximación en canvas de Level.cs / ParallaxBackgroundLayer.cs (mismos
// parámetros y fórmulas), no un render exacto. Coordenadas: celdas con la fila 0 ARRIBA (como el .levelsketch.json).
'use strict';
const Terreno = (() => {
  const lerp = (a, b, t) => a + (b - a) * t, clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  const css = (c, a) => `rgba(${c.r * 255 | 0},${c.g * 255 | 0},${c.b * 255 | 0},${a ?? c.a})`;
  const mixC = (a, b, t) => ({ r: lerp(a.r, b.r, t), g: lerp(a.g, b.g, t), b: lerp(a.b, b.b, t), a: 1 });
  const mk = (w, h) => { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; };

  // ruido de valor 2D (mismo papel que Level.Noise: bamboleo del grosor del borde)
  const P = new Float32Array(65536);
  { let s = 11; for (let i = 0; i < P.length; i++) { s = s + 0x6D2B79F5 | 0; let t = Math.imul(s ^ s >>> 15, 1 | s); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; P[i] = ((t ^ t >>> 14) >>> 0) / 4294967296; } }
  const sm = t => t * t * (3 - 2 * t), hsh = (a, b) => P[((a & 255) << 8) | (b & 255)];
  function noise(x, y) {
    const xi = Math.floor(x), yi = Math.floor(y), u = sm(x - xi), v = sm(y - yi);
    return lerp(lerp(hsh(xi, yi), hsh(xi + 1, yi), u), lerp(hsh(xi, yi + 1), hsh(xi + 1, yi + 1), u), v);
  }

  // Contornos de las bolsas de aire (roca a la derecha, en pantalla). En un vértice "pellizco" (dos rocas que se
  // tocan en diagonal) se gira a la izquierda para que cada bolsa conserve su propio contorno, como LevelShape.
  function loops(solid, W, H) {
    const edges = [], at = new Map(), key = (x, y) => x + ',' + y;
    const add = (x0, y0, x1, y1) => { const i = edges.length; edges.push({ x0, y0, x1, y1, dx: x1 - x0, dy: y1 - y0, used: false }); const k = key(x0, y0); (at.get(k) || at.set(k, []).get(k)).push(i); };
    for (let r = 0; r < H; r++) for (let c = 0; c < W; c++) if (solid(c, r)) {
      if (!solid(c, r - 1)) add(c, r, c + 1, r);
      if (!solid(c + 1, r)) add(c + 1, r, c + 1, r + 1);
      if (!solid(c, r + 1)) add(c + 1, r + 1, c, r + 1);
      if (!solid(c - 1, r)) add(c, r + 1, c, r);
    }
    const choose = (e) => {
      const list = at.get(key(e.x1, e.y1)) || [];
      if (list.length <= 1) return list[0];
      const lx = e.dy, ly = -e.dx; // izquierda en pantalla (y hacia abajo)
      return list.find(i => edges[i].dx === lx && edges[i].dy === ly) ?? list[0];
    };
    const out = [];
    for (let s = 0; s < edges.length; s++) {
      if (edges[s].used) continue;
      const pts = []; let i = s, guard = 0;
      do { const e = edges[i]; e.used = true; pts.push([e.x0, e.y0]); i = choose(e); }
      while (i !== undefined && i !== s && !edges[i].used && ++guard < 1e6);
      const clean = pts.filter((p, k) => { const a = pts[(k + pts.length - 1) % pts.length], b = pts[(k + 1) % pts.length]; return (p[0] - a[0]) * (b[1] - p[1]) - (p[1] - a[1]) * (b[0] - p[0]) !== 0; });
      if (clean.length >= 3) out.push(clean);
    }
    return out;
  }

  // Contorno con cada esquina redondeada (curva cuadrática de radio R px), muestreado; con tangente, normal hacia la
  // roca y distancia recorrida (s, en px).
  function rounded(pts, ppu, R) {
    const Pp = pts.map(([x, y]) => [x * ppu, y * ppu]), n = Pp.length, C = [];
    for (let i = 0; i < n; i++) {
      const a = Pp[(i + n - 1) % n], b = Pp[i], c = Pp[(i + 1) % n], l1 = Math.hypot(b[0] - a[0], b[1] - a[1]), l2 = Math.hypot(c[0] - b[0], c[1] - b[1]);
      const d1 = [(b[0] - a[0]) / l1, (b[1] - a[1]) / l1], d2 = [(c[0] - b[0]) / l2, (c[1] - b[1]) / l2], r = Math.min(R, l1 / 2, l2 / 2);
      C.push({ b, p1: [b[0] - d1[0] * r, b[1] - d1[1] * r], p2: [b[0] + d2[0] * r, b[1] + d2[1] * r] });
    }
    const out = [], step = Math.max(2, ppu / 10);
    for (let i = 0; i < n; i++) {
      const cur = C[i], pr = C[(i + n - 1) % n], dx = cur.p1[0] - pr.p2[0], dy = cur.p1[1] - pr.p2[1], k = Math.max(1, Math.round(Math.hypot(dx, dy) / step));
      for (let j = 0; j < k; j++) out.push({ x: pr.p2[0] + dx * j / k, y: pr.p2[1] + dy * j / k });
      for (let j = 0; j <= 8; j++) { const t = j / 8, u = 1 - t; out.push({ x: u * u * cur.p1[0] + 2 * u * t * cur.b[0] + t * t * cur.p2[0], y: u * u * cur.p1[1] + 2 * u * t * cur.b[1] + t * t * cur.p2[1] }); }
    }
    let s = 0; const m = out.length;
    out.forEach((q, i) => {
      const a = out[(i + m - 2) % m], b = out[(i + 2) % m]; let tx = b.x - a.x, ty = b.y - a.y; const l = Math.hypot(tx, ty) || 1;
      q.nx = -ty / l; q.ny = tx / l; const p = out[(i + m - 1) % m]; s += Math.hypot(q.x - p.x, q.y - p.y); q.s = s;
    });
    return out;
  }

  // Canvas del nivel entero a "ppu" px por celda: roca con la textura del bioma, sombra y brillo interior, las 4
  // bandas del borde y el contorno oscuro. solid(c, r) = roca o plataforma (en el juego se ven igual).
  function build(solid, W, H, biome, rockImg, ppu) {
    const cv = mk(Math.ceil(W * ppu), Math.ceil(H * ppu)), g = cv.getContext('2d');
    const polys = loops(solid, W, H).map(l => rounded(l, ppu, biome.cornerRadius * ppu));
    const air = new Path2D();
    polys.forEach(p => { p.forEach((q, i) => i ? air.lineTo(q.x, q.y) : air.moveTo(q.x, q.y)); air.closePath(); });
    const rock = new Path2D(); rock.rect(0, 0, cv.width, cv.height); rock.addPath(air);

    g.save(); g.clip(rock, 'evenodd');
    if (rockImg) {
      const pat = g.createPattern(rockImg, 'repeat'), k = biome.rockTileUnits * ppu / rockImg.width;
      pat.setTransform(new DOMMatrix([k, 0, 0, k, 0, cv.height % (biome.rockTileUnits * ppu)]));
      g.fillStyle = pat;
    } else g.fillStyle = '#35506e';
    g.fillRect(0, 0, cv.width, cv.height);
    g.fillStyle = css(biome.rockShadow); g.fillRect(0, 0, cv.width, cv.height);
    // brillo interior: el contorno del aire difuminado, sólo dentro de la roca
    const gw = biome.innerGlowWidth * ppu;
    if (gw > 0 && biome.innerGlow.a > 0) {
      g.save(); g.globalCompositeOperation = 'screen'; g.filter = `blur(${gw * 0.35}px)`;
      g.strokeStyle = css(biome.innerGlow, Math.min(1, biome.innerGlow.a * 2)); g.lineWidth = gw; g.stroke(air);
      g.restore();
    }
    const bands = biome.floorBands.length, frac = biome.bandFractions || [1, .68, .4, .17];
    polys.forEach(Q => {
      Q.forEach(q => {
        const fl = clamp(q.ny, 0, 1), ce = clamp(-q.ny, 0, 1), s = q.s / ppu;
        const base = biome.wallWidth + (biome.floorWidth - biome.wallWidth) * fl + (biome.ceilingWidth - biome.wallWidth) * ce;
        const wob = 1 - biome.widthVariation * 0.44 + biome.widthVariation * noise(s / 0.6, 3.3);
        q.w = (base * wob + (noise(s / 0.175, 9) - 0.5) * 0.075) * ppu; q.t = (q.ny + 1) / 2;
      });
      const n = Q.length;
      for (let b = 0; b < bands; b++) for (let i = 0; i < n; i++) {
        const A = Q[i], B = Q[(i + 1) % n], f = frac[b] ?? 1 - b * 0.25;
        const col = css(mixC(biome.ceilingBands[b], biome.floorBands[b], (A.t + B.t) / 2), 1);
        g.beginPath(); g.moveTo(A.x, A.y); g.lineTo(B.x, B.y); g.lineTo(B.x + B.nx * B.w * f, B.y + B.ny * B.w * f); g.lineTo(A.x + A.nx * A.w * f, A.y + A.ny * A.w * f); g.closePath();
        g.fillStyle = col; g.strokeStyle = col; g.lineWidth = 1; g.fill(); g.stroke();
      }
    });
    g.restore();
    g.lineJoin = 'round'; g.strokeStyle = css(biome.outline); g.lineWidth = biome.outlineWidth * ppu; g.stroke(air);
    return cv;
  }

  // ---- fondo: misma lógica que ParallaxBackgroundLayer.LateUpdate (todo en unidades, vista de viewH de alto) ----
  const faded = new Map(); // imagen + niebla precalculadas (no se lee la imagen: file:// no deja)
  function fadedImage(img, fog, fade) {
    if (!fade) return img;
    const k = img.src + '|' + fade; if (faded.has(k)) return faded.get(k);
    const c = mk(img.width, img.height), x = c.getContext('2d'); x.drawImage(img, 0, 0);
    x.globalCompositeOperation = 'source-atop'; x.fillStyle = css(fog, fade); x.fillRect(0, 0, c.width, c.height);
    faded.set(k, c); return c;
  }

  // cam = {x, y} centro de la vista en unidades (y hacia arriba, origen abajo a la izquierda); rect = vista en pantalla.
  function drawLayer(g, L, img, cam, view, level, groundTop) {
    if (!img) return;
    const { viewW, viewH, ppu, x0, y0 } = view, viewLeft = cam.x - viewW / 2, viewTop = cam.y + viewH / 2;
    const camLeft = viewLeft, camDown = level.h - viewTop, camMax = Math.max(0, level.h - viewH);
    const h = viewH * L.size / 100, w = h * img.width / img.height, yOff = -L.heightOffset * viewH / 200;
    let y;
    if (L.anchor === 'Ceiling') y = yOff - camDown * L.verticalSpeed;
    else if (L.anchor === 'Floor') y = viewH - h + yOff + (camMax - camDown) * L.verticalSpeed;
    else {
      const k = camMax > 0 ? camDown / camMax : 0, d = viewH - h;
      y = L.align === 'Top' ? yOff + d * k * L.travel : L.align === 'Bottom' ? d + yOff - d * (1 - k) * L.travel : d * 0.5 + yOff + d * (k - 0.5) * L.travel;
    }
    const src = fadedImage(img, L.fogColor, L.fade), sx = wx => x0 + (wx - viewLeft) * ppu, sy = wy => y0 + (viewTop - wy) * ppu; // en unidades del mundo
    g.globalAlpha = L.opacity;
    const one = (left, bottom, mirror) => {
      g.save(); g.translate(sx(left) + (mirror ? w * ppu : 0), sy(bottom + h)); if (mirror) g.scale(-1, 1);
      g.drawImage(src, 0, 0, w * ppu + 1, h * ppu + 1); g.restore();
    };
    if (L.repeat === 'LevelEnds') {
      const left = viewLeft - camLeft * L.speed + L.positionX;
      const right = viewLeft + viewW + (level.w - viewW) * L.speed - camLeft * L.speed - L.positionX;
      let bl = viewTop - y - h, br = bl;
      if (L.sitOnGround) {
        const fine = L.heightOffset * viewH / 200;
        bl = groundTop(Math.floor(left + w * 0.5)) + fine; br = groundTop(Math.floor(right - w * 0.5)) + fine;
      }
      one(left, bl, false); one(right - w, br, true);
    } else {
      const u0 = (camLeft * L.speed - L.positionX) / w, i0 = Math.floor(u0), bottom = viewTop - y - h;
      for (let i = i0; (i - u0) * w < viewW; i++) {
        const left = viewLeft + (i - u0) * w;
        one(left, bottom, L.repeat === 'Mirror' && (i & 1) === 1);
      }
    }
    g.globalAlpha = 1;
  }

  return { build, drawLayer, loops };
})();
