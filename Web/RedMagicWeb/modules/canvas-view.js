// modules/canvas-view.js
// -----------------------------------------------------------------------------
// NEW module — didn't exist in the old single-file tool. Gives the sprite
// canvas Unity-Scene-view-like navigation: the canvas sits centered inside its
// wrap, the scroll wheel zooms toward the cursor, and a middle-mouse drag pans.
//
// Implementation choice: the canvas element's OWN pixel buffer always stays at
// native image resolution (1 canvas px = 1 image px) — zoom/pan are pure CSS
// (`left`/`top` for pan, `transform: scale()` from a top-left origin for zoom)
// applied to the canvas element itself. This keeps every box coordinate
// (detection, manual boxes, drag handles, crops) in plain image-pixel space;
// only screenToCanvas() below has to know the transform exists.

export class CanvasView {
  /**
   * @param {HTMLCanvasElement} canvas
   * @param {HTMLElement} wrap - the positioned container the canvas pans/zooms within.
   * @param {{onChange?: (view: CanvasView) => void}} [opts] - onChange fires after every
   *   scale/pan update (e.g. app.js uses it to keep a "100%" zoom readout in sync).
   */
  constructor(canvas, wrap, opts = {}) {
    this.canvas = canvas;
    this.wrap = wrap;
    this.onChange = opts.onChange || null;
    this.scale = 1;
    this.panX = 0;
    this.panY = 0;
    this.minScale = 0.05;
    this.maxScale = 16;

    this._panning = false;
    this._panStartClientX = 0;
    this._panStartClientY = 0;
    this._panOrigX = 0;
    this._panOrigY = 0;

    canvas.style.position = 'absolute';
    canvas.style.transformOrigin = '0 0';

    this._onWheel = this._onWheel.bind(this);
    this._onMouseDown = this._onMouseDown.bind(this);
    this._onMouseMove = this._onMouseMove.bind(this);
    this._onMouseUp = this._onMouseUp.bind(this);

    wrap.addEventListener('wheel', this._onWheel, { passive: false });
    wrap.addEventListener('mousedown', this._onMouseDown);
    window.addEventListener('mousemove', this._onMouseMove);
    window.addEventListener('mouseup', this._onMouseUp);
    // Stop the browser's default middle-click autoscroll cursor/behavior.
    wrap.addEventListener('auxclick', (e) => { if (e.button === 1) e.preventDefault(); });
  }

  /** Centers the canvas in the wrap and picks a scale that fits it, like "Frame Selected". */
  frameToFit() {
    const wrapW = this.wrap.clientWidth, wrapH = this.wrap.clientHeight;
    const imgW = this.canvas.width, imgH = this.canvas.height;
    if (imgW === 0 || imgH === 0 || wrapW === 0 || wrapH === 0) { this.reset(); return; }

    const fit = Math.min(wrapW / imgW, wrapH / imgH);
    this.scale = this._clampScale(fit);
    this.panX = (wrapW - imgW * this.scale) / 2;
    this.panY = (wrapH - imgH * this.scale) / 2;
    this._apply();
  }

  reset() {
    this.scale = 1;
    this.panX = 0;
    this.panY = 0;
    this._apply();
  }

  /** Screen (clientX/clientY) → canvas-native pixel coordinates, accounting for the current pan/zoom. */
  screenToCanvas(clientX, clientY) {
    const wrapRect = this.wrap.getBoundingClientRect();
    const localX = clientX - wrapRect.left - this.panX;
    const localY = clientY - wrapRect.top - this.panY;
    return { x: localX / this.scale, y: localY / this.scale };
  }

  _clampScale(s) {
    return Math.min(this.maxScale, Math.max(this.minScale, s));
  }

  _apply() {
    this.canvas.style.left = `${this.panX}px`;
    this.canvas.style.top = `${this.panY}px`;
    this.canvas.style.transform = `scale(${this.scale})`;
    this.canvas.style.imageRendering = this.scale >= 1 ? 'pixelated' : 'auto';
    if (this.onChange) this.onChange(this);
  }

  _onWheel(e) {
    if (this.canvas.width === 0) return;
    e.preventDefault();

    const wrapRect = this.wrap.getBoundingClientRect();
    const cursorX = e.clientX - wrapRect.left;
    const cursorY = e.clientY - wrapRect.top;

    // Canvas-space point currently under the cursor — kept fixed on screen after the zoom.
    const canvasX = (cursorX - this.panX) / this.scale;
    const canvasY = (cursorY - this.panY) / this.scale;

    const zoomFactor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
    this.scale = this._clampScale(this.scale * zoomFactor);

    this.panX = cursorX - canvasX * this.scale;
    this.panY = cursorY - canvasY * this.scale;
    this._apply();
  }

  _onMouseDown(e) {
    if (e.button !== 1) return; // middle mouse only
    e.preventDefault();
    this._panning = true;
    this._panStartClientX = e.clientX;
    this._panStartClientY = e.clientY;
    this._panOrigX = this.panX;
    this._panOrigY = this.panY;
  }

  _onMouseMove(e) {
    if (!this._panning) return;
    this.panX = this._panOrigX + (e.clientX - this._panStartClientX);
    this.panY = this._panOrigY + (e.clientY - this._panStartClientY);
    this._apply();
  }

  _onMouseUp(e) {
    if (e.button !== 1) return;
    this._panning = false;
  }
}
