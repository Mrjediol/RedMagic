// modules/map-canvas-view.js
// -----------------------------------------------------------------------------
// The Map Tracer canvas reuses modules/canvas-view.js's CanvasView as-is: zoom-to-cursor and
// middle-mouse pan are the exact same math whether the canvas backs a loaded sprite sheet (whose
// size follows the image) or a hand-sized map scene (whose size follows the `mapW`/`mapH`
// inputs) — CanvasView only ever reads `canvas.width`/`canvas.height`, so setting those to the
// configured map size before calling `frameToFit()` just works. Re-exported under this name
// (rather than importing canvas-view.js directly from app.js for the map tab) only so the map
// tab has its own module in the map-* family per ARCHITECTURE.md's module map, and to have one
// place to add map-specific view behavior later if it's ever needed.
export { CanvasView } from './canvas-view.js';
