// modules/parallax-data.js
// -----------------------------------------------------------------------------
// Fondos de parallax: la forma de una capa (los mismos campos que ParallaxBackgroundLayer.cs en Unity, para que la
// vista previa y el importador no traduzcan nada), el procesado de su imagen (quitar magenta, recortar bordes) y el
// archivo que va a Unity (*.parallax.json, menú EditorNiveles ▸ Importar fondo…). Lo usan 🏞️ Fondos y 🧱 Niveles.
// En la Biblioteca un fondo es una entrada kind 'parallax' con `draft = { layers, levelW, levelH }`.
import { listEntries } from './enemy-library.js';

export const KIND = 'parallax';
export const REPEATS = [['Repeat', 'Normal'], ['Mirror', 'Espejo'], ['LevelEnds', 'Extremos del nivel']];
export const ANCHORS = [['Proportional', 'Proporcional (fondo)'], ['Ceiling', 'Techo del nivel'], ['Floor', 'Suelo del nivel']];
export const ALIGNS = [['Top', 'Arriba'], ['Center', 'Centro'], ['Bottom', 'Abajo']];

/** Una capa nueva con los valores de serie de ParallaxBackgroundLayer. */
export function newLayer(name) {
  return {
    name, on: true, image: null, key: false, crop: 0,
    speed: 0.1, repeat: 'Repeat', positionX: 0, size: 100, heightOffset: 0,
    anchor: 'Proportional', align: 'Center', travel: 1, verticalSpeed: 0.9,
    opacity: 1, fade: 0, fogColor: { r: 18 / 255, g: 52 / 255, b: 92 / 255, a: 1 },
    inFront: false, overTerrain: false, sitOnGround: false,
  };
}

/** Completa una capa guardada por una versión anterior (sólo lectura). */
export function normalizeLayer(l, i) { return { ...newLayer(`Capa ${i + 1}`), ...l }; }

/** Imagen lista para dibujar: la original con magenta quitado y bordes recortados (canvas), o null. */
export function processImage(img, layer) {
  if (!img) return null;
  if (!layer.key && !layer.crop) return img;
  const cr = Math.max(0, Math.min(layer.crop | 0, Math.floor(img.width / 2) - 1));
  const c = document.createElement('canvas');
  c.width = img.width - 2 * cr; c.height = img.height;
  const x = c.getContext('2d');
  x.drawImage(img, -cr, 0);
  if (layer.key) {
    const d = x.getImageData(0, 0, c.width, c.height), p = d.data;
    for (let i = 0; i < p.length; i += 4) {
      const m = (p[i] + p[i + 2]) / 2 - p[i + 1];
      if (m > 35) {
        p[i + 3] = p[i + 3] * Math.max(0, Math.min(1, 1 - (m - 35) / 90));
        const k = 0.85 * Math.min(1, m / 120);
        p[i] = Math.max(0, p[i] - m * k); p[i + 2] = Math.max(0, p[i + 2] - m * k);
      }
    }
    x.putImageData(d, 0, 0);
  }
  return c;
}

export function loadImage(src) {
  return new Promise((resolve) => {
    if (!src) return resolve(null);
    const i = new Image();
    i.onload = () => resolve(i);
    i.onerror = () => resolve(null);
    i.src = src;
  });
}

/** Las capas de un fondo con su imagen ya procesada (`img`), para dibujar con level-terrain.drawLayer. */
export async function readyLayers(layers) {
  return Promise.all(layers.map(async (l, i) => {
    const layer = normalizeLayer(l, i);
    return { ...layer, img: processImage(await loadImage(layer.image), layer) };
  }));
}

let unityDataPromise = null, serverPromise = null;
/** assets/niveles/datos-unity.json (lo escribe Unity: tiles y, por bioma, valores y capas del fondo), o null. */
export function unityData() {
  unityDataPromise ??= fetch('assets/niveles/datos-unity.json', { cache: 'no-store' }).then((r) => (r.ok ? r.json() : null)).catch(() => null);
  return unityDataPromise;
}
/** { token } si la página viene del servidor del taller (start.cmd), o null. */
export function tallerServer() {
  serverPromise ??= fetch('/api/hello').then((r) => (r.ok ? r.json() : null)).then((h) => (h?.token ? { token: h.token } : null)).catch(() => null);
  return serverPromise;
}
/** Un archivo de arte de Assets/Levels servido por el taller. */
export const artUrl = (p) => `/api/niveles/arte?path=${encodeURIComponent(p)}`;

/** Fondos guardados en la Biblioteca, el más reciente primero. */
export const listBackgrounds = () => listEntries(KIND);

/** PNG si tiene transparencia, JPEG si no (pesa mucho menos). */
function bake(canvasOrImg) {
  const c = document.createElement('canvas');
  c.width = canvasOrImg.width; c.height = canvasOrImg.height;
  const x = c.getContext('2d');
  x.drawImage(canvasOrImg, 0, 0);
  const p = x.getImageData(0, 0, c.width, c.height).data;
  let alpha = false;
  for (let i = 3; i < p.length; i += 16) if (p[i] < 250) { alpha = true; break; }
  return alpha ? c.toDataURL('image/png') : c.toDataURL('image/jpeg', 0.95);
}

/**
 * El archivo para Unity: { format: 'redmagic-parallax', version: 1, name, layers: [{ name, image, …campos }] }, con
 * el magenta y el recorte ya aplicados a cada imagen. Las capas apagadas no van.
 */
export async function toUnityFile(name, layers) {
  const ready = await readyLayers(layers);
  const out = [];
  for (const l of ready) {
    if (!l.on || !l.img) continue;
    const { img, image, key, crop, on, ...fields } = l;
    out.push({ ...fields, image: bake(img) });
  }
  return { format: 'redmagic-parallax', version: 1, name, layers: out };
}
