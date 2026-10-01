// modules/rig-canvas-view.js
// -----------------------------------------------------------------------------
// The 🦴 Rig Composer's canvas: a camera over Visual space (Unity units, y up), drawing every part
// through its world matrix, pixel-accurate picking (alpha of the part's own art) and the editing
// overlays (pivot, centre, point direction, muzzle, grips, collider, origin).
//
// Pan: middle or right drag. Zoom: wheel, towards the cursor. Owns no document state: the composer
// passes the state (and the preview poses) into render()/partAt() every time.

import { multiply, invert, apply, applyVector, worldMatrix, byUid, variantPivot, isOpenSlot, isEmptyNode, feetY } from './rig-model.js';

const PICK_ALPHA = 10;

export class RigCanvasView {
  constructor(canvas, wrap, { onChange, dpr = null } = {}) {
    this.canvas = canvas;
    this.wrap = wrap;
    this.ctx = canvas.getContext('2d');
    this.zoom = 60;            // screen px per unit
    this.ox = 300;             // screen px of the Visual origin
    this.oy = 400;
    this.onChange = onChange || (() => {});
    this._images = new Map();  // dataURL -> { img, alpha: Uint8ClampedArray|null, w, h }
    this._pan = null;
    this._dpr = dpr;   // fijo (miniaturas) o null = el de la pantalla

    canvas.addEventListener('wheel', (e) => this._onWheel(e), { passive: false });
    canvas.addEventListener('mousedown', (e) => {
      if (e.button !== 1 && e.button !== 2) return;
      e.preventDefault();
      this._pan = { x: e.clientX, y: e.clientY, ox: this.ox, oy: this.oy };
    });
    window.addEventListener('mousemove', (e) => {
      if (!this._pan) return;
      this.ox = this._pan.ox + (e.clientX - this._pan.x);
      this.oy = this._pan.oy + (e.clientY - this._pan.y);
      this.onChange();
    });
    window.addEventListener('mouseup', () => { this._pan = null; });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());

    if (wrap instanceof Element) {
      new ResizeObserver(() => this._resize()).observe(wrap);
      this._resize();
    }
  }

  _resize() {
    const dpr = window.devicePixelRatio || 1;
    const w = this.wrap.clientWidth, h = this.wrap.clientHeight;
    if (!w || !h) return;
    this.canvas.width = Math.round(w * dpr);
    this.canvas.height = Math.round(h * dpr);
    this.canvas.style.width = `${w}px`;
    this.canvas.style.height = `${h}px`;
    this.onChange();
  }

  _onWheel(e) {
    e.preventDefault();
    const r = this.canvas.getBoundingClientRect();
    const sx = e.clientX - r.left, sy = e.clientY - r.top;
    const before = this.screenToVisual(e.clientX, e.clientY);
    this.zoom = Math.min(4000, Math.max(2, this.zoom * (e.deltaY < 0 ? 1.12 : 1 / 1.12)));
    this.ox = sx - before.x * this.zoom;
    this.oy = sy + before.y * this.zoom;
    this.onChange();
  }

  /** Visual space → canvas CSS pixels. */
  get camera() { return [this.zoom, 0, 0, -this.zoom, this.ox, this.oy]; }

  screenToVisual(clientX, clientY) {
    const r = this.canvas.getBoundingClientRect();
    return { x: (clientX - r.left - this.ox) / this.zoom, y: -(clientY - r.top - this.oy) / this.zoom };
  }

  /** Frames every part (or the origin) in view. */
  frame(state) {
    const b = this.bounds(state, null, true);
    const w = this.wrap.clientWidth || 600, h = this.wrap.clientHeight || 400;
    if (!b) { this.zoom = 60; this.ox = w / 2; this.oy = h * 0.7; this.onChange(); return; }
    const bw = Math.max(0.5, b.maxX - b.minX), bh = Math.max(0.5, b.maxY - b.minY);
    this.zoom = Math.min(w / (bw * 1.3), h / (bh * 1.3));
    this.ox = w / 2 - ((b.minX + b.maxX) / 2) * this.zoom;
    this.oy = h / 2 + ((b.minY + b.maxY) / 2) * this.zoom;
    this.onChange();
  }

  /** Visual-space bounds of every part's art (plus, with `withSlots`, the empty template slots), or null. */
  bounds(state, onlyUids = null, withSlots = false) {
    let b = null;
    const add = (p) => {
      b = b ? { minX: Math.min(b.minX, p.x), maxX: Math.max(b.maxX, p.x), minY: Math.min(b.minY, p.y), maxY: Math.max(b.maxY, p.y) }
        : { minX: p.x, maxX: p.x, minY: p.y, maxY: p.y };
    };
    state.parts.forEach((part) => {
      if (onlyUids && !onlyUids.has(part.uid)) return;
      if (withSlots && isOpenSlot(part)) {
        const world = worldMatrix(state, part);
        RigCanvasView.slotCorners(part).forEach((c) => add(apply(world, c)));
        return;
      }
      if (!part.image) return;
      const m = multiply(worldMatrix(state, part), this._imageMatrix(state, part, part.image, part.pivot));
      [[0, 0], [part.image.w, 0], [0, part.image.h], [part.image.w, part.image.h]].forEach(([x, y]) => add(apply(m, { x, y })));
    });
    return b;
  }

  /** Placeholder box of an empty template slot, in the part's local units: its pivot guess sits at 0. */
  static slotCorners(part) {
    const [w, h] = part.slot.size;
    const [nx, ny] = part.slot.pivot;   // x from left, y from top
    const x0 = -nx * w, x1 = (1 - nx) * w, y0 = -(1 - ny) * h, y1 = ny * h;
    return [{ x: x0, y: y1 }, { x: x1, y: y1 }, { x: x1, y: y0 }, { x: x0, y: y0 }];
  }

  _slotScreenCorners(state, part, opts) {
    let m = worldMatrix(state, part, opts.poses);
    if (opts.visualMatrix) m = multiply(opts.visualMatrix, m);
    m = multiply(this.camera, m);
    return RigCanvasView.slotCorners(part).map((c) => apply(m, c));
  }

  /** Topmost empty template slot under the cursor, or null. */
  slotAt(state, clientX, clientY, opts = {}) {
    const r = this.canvas.getBoundingClientRect();
    const sp = { x: clientX - r.left, y: clientY - r.top };
    for (let i = state.parts.length - 1; i >= 0; i--) {
      const part = state.parts[i];
      if (!isOpenSlot(part)) continue;
      const local = apply(invert(multiply(this.camera, worldMatrix(state, part, opts.poses))), sp);
      const [a, , c] = RigCanvasView.slotCorners(part);
      if (local.x >= a.x && local.x <= c.x && local.y >= c.y && local.y <= a.y) return part;
    }
    return null;
  }

  /** Screen position of an empty node (a pivot without art). */
  _nodeScreen(state, part, opts) {
    let m = worldMatrix(state, part, opts.poses);
    if (opts.visualMatrix) m = multiply(opts.visualMatrix, m);
    return apply(multiply(this.camera, m), { x: 0, y: 0 });
  }

  /** Empty node whose handle is under the cursor, or null. */
  nodeAt(state, clientX, clientY, opts = {}) {
    const r = this.canvas.getBoundingClientRect();
    const sx = clientX - r.left, sy = clientY - r.top;
    for (let i = state.parts.length - 1; i >= 0; i--) {
      const part = state.parts[i];
      if (!isEmptyNode(part)) continue;
      const p = this._nodeScreen(state, part, opts);
      if (Math.hypot(p.x - sx, p.y - sy) <= 12) return part;
    }
    return null;
  }

  /** Empty nodes have no art to grab: a diamond handle with their name. */
  _drawNodes(state, opts) {
    const ctx = this.ctx;
    state.parts.forEach((part) => {
      if (!isEmptyNode(part)) return;
      const p = this._nodeScreen(state, part, opts);
      const selected = part.uid === opts.selectedUid;
      ctx.save();
      ctx.strokeStyle = selected ? 'rgba(90,170,255,1)' : 'rgba(255,255,255,0.75)';
      ctx.fillStyle = selected ? 'rgba(90,170,255,0.35)' : 'rgba(255,255,255,0.12)';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(p.x, p.y - 9); ctx.lineTo(p.x + 9, p.y); ctx.lineTo(p.x, p.y + 9); ctx.lineTo(p.x - 9, p.y);
      ctx.closePath();
      ctx.fill();
      ctx.stroke();
      ctx.fillStyle = ctx.strokeStyle;
      ctx.font = '11px sans-serif';
      ctx.fillText(part.name, p.x + 13, p.y + 4);
      ctx.restore();
    });
  }

  /** A boss's feet: the line that is planted on the ground (root.feet). */
  _drawFeet(state) {
    if (state.kind !== 'boss') return;
    const ctx = this.ctx;
    const p = apply(this.camera, { x: 0, y: feetY(state) });
    ctx.save();
    ctx.strokeStyle = ctx.fillStyle = 'rgba(255,140,90,0.9)';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(p.x - 40, p.y); ctx.lineTo(p.x + 40, p.y);
    ctx.stroke();
    ctx.font = '11px sans-serif';
    ctx.fillText('pies (suelo)', p.x + 44, p.y + 4);
    ctx.restore();
  }

  /** Empty template slots: a dashed box where the part goes, with what to drop on it. */
  _drawSlots(state, opts) {
    const ctx = this.ctx;
    state.parts.forEach((part) => {
      if (!isOpenSlot(part)) return;
      const corners = this._slotScreenCorners(state, part, opts);
      const selected = part.uid === opts.selectedUid;
      ctx.save();
      ctx.setLineDash([6, 4]);
      ctx.lineWidth = selected ? 2 : 1.2;
      ctx.strokeStyle = selected ? 'rgba(90,170,255,0.95)' : part.slot.optional ? 'rgba(255,255,255,0.35)' : 'rgba(255,200,80,0.8)';
      ctx.fillStyle = part.slot.optional ? 'rgba(255,255,255,0.03)' : 'rgba(255,200,80,0.06)';
      ctx.beginPath();
      corners.forEach((c, i) => (i ? ctx.lineTo(c.x, c.y) : ctx.moveTo(c.x, c.y)));
      ctx.closePath();
      ctx.fill();
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.fillStyle = ctx.strokeStyle;
      ctx.font = '11px sans-serif';
      ctx.textAlign = 'center';
      const cx = (corners[0].x + corners[2].x) / 2, cy = (corners[0].y + corners[2].y) / 2;
      ctx.fillText(part.slot.label, cx, cy + 4);
      ctx.restore();
    });
  }

  /** Crop pixel (y down) → part local units, for an image drawn with `pivot` (original pixels). */
  _imageMatrix(state, part, image, pivot) {
    const ppu = state.pixelsPerUnit;
    return [1 / ppu, 0, 0, -1 / ppu, (image.cropX - pivot.x) / ppu, -(image.cropY - pivot.y) / ppu];
  }

  _entry(image, requestRender) {
    let e = this._images.get(image.dataURL);
    if (e) return e.ready ? e : null;
    e = { ready: false, img: new Image(), alpha: null };
    this._images.set(image.dataURL, e);
    e.img.onload = () => {
      const c = document.createElement('canvas');
      c.width = e.img.naturalWidth;
      c.height = e.img.naturalHeight;
      const cx = c.getContext('2d');
      cx.drawImage(e.img, 0, 0);
      e.alpha = cx.getImageData(0, 0, c.width, c.height).data;
      e.w = c.width;
      e.h = c.height;
      e.ready = true;
      requestRender?.();
    };
    e.img.src = image.dataURL;
    return null;
  }

  /** Resolves once every part image is decoded (a fresh view loads them lazily). */
  preload(state) {
    // One wait per distinct image: _entry only takes a callback from the caller that starts the load,
    // so two parts sharing a PNG (three equal orbiters) would leave the second one waiting forever.
    const images = [...new Map(state.parts.filter((p) => p.image).map((p) => [p.image.dataURL, p.image])).values()];
    return Promise.all(images.map((image) => new Promise((resolve) => {
      if (this._entry(image, resolve)) resolve();
    })));
  }

  /** The image a part shows right now (a state/stage sprite in the preview) and its pivot. */
  static shownImage(part, spriteState, stage) {
    const v = part.variants || {};
    let variant = null;
    if (stage > 0 && v.stages?.[stage - 1]) variant = v.stages[stage - 1];
    else if (spriteState && v[spriteState]) variant = v[spriteState];
    if (!variant) return { image: part.image, pivot: part.pivot };
    return { image: variant, pivot: variantPivot(part, variant) };
  }

  /** Full matrix crop pixel → screen for a part, with the preview's visual/part poses. */
  _partScreenMatrix(state, part, opts) {
    const { image, pivot } = RigCanvasView.shownImage(part, opts.spriteState, opts.stage);
    if (!image) return null;
    let m = worldMatrix(state, part, opts.poses);
    if (opts.visualMatrix) m = multiply(opts.visualMatrix, m);
    return { m: multiply(this.camera, multiply(m, this._imageMatrix(state, part, image, pivot))), image };
  }

  /** Topmost part under the cursor (by its drawn pixels), or null. */
  partAt(state, clientX, clientY, opts = {}) {
    const r = this.canvas.getBoundingClientRect();
    const sp = { x: clientX - r.left, y: clientY - r.top };
    for (let i = state.parts.length - 1; i >= 0; i--) {
      const part = state.parts[i];
      const pm = this._partScreenMatrix(state, part, opts);
      if (!pm) continue;
      const e = this._entry(pm.image);
      if (!e) continue;
      const px = apply(invert(pm.m), sp);
      const x = Math.floor(px.x), y = Math.floor(px.y);
      if (x < 0 || y < 0 || x >= e.w || y >= e.h) continue;
      if (e.alpha[(y * e.w + x) * 4 + 3] > PICK_ALPHA) return part;
    }
    return null;
  }

  /**
   * @param {object} state
   * @param {object} opts - { selectedUid, poses, visualMatrix, spriteState, stage, showCollider,
   *                          showMarkers, ghostRest, requestRender,
   *                          fx: [{ sheet: dataURL, box: {x,y,w,h}, matrix, alpha, marked }] }
   *                          (gesture effects: `matrix` maps the frame's centred pixels to Visual space)
   */
  render(state, opts = {}) {
    const ctx = this.ctx;
    const dpr = this._dpr || window.devicePixelRatio || 1;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    this._drawGrid();

    const drawParts = (o) => state.parts.forEach((part) => {
      const pm = this._partScreenMatrix(state, part, o);
      if (!pm) return;
      const e = this._entry(pm.image, opts.requestRender);
      if (!e) return;
      ctx.save();
      ctx.transform(...pm.m);
      ctx.drawImage(e.img, 0, 0);
      ctx.restore();
    });

    // Posing a gesture: the rest pose stays underneath as a ghost, to see how far each part moved.
    if (opts.ghostRest) {
      ctx.save();
      ctx.globalAlpha = 0.22;
      drawParts({ ...opts, poses: null, visualMatrix: null });
      ctx.restore();
    }

    drawParts(opts);
    this._drawFx(opts);   // in front of every part, like the driver draws them

    this._drawSlots(state, opts);
    if (opts.showCollider !== false) { this._drawCollider(state, opts); this._drawFeet(state); }
    if (opts.showMarkers !== false) this._drawNodes(state, opts);
    if (opts.showMarkers !== false) this._drawMarkers(state, opts);
  }

  /** Gesture effects (rig-fx.js): one frame of a library sheet each, centred on its anchor point. */
  _drawFx(opts) {
    const ctx = this.ctx;
    (opts.fx || []).forEach((f) => {
      const e = this._entry({ dataURL: f.sheet }, opts.requestRender);
      const m = multiply(this.camera, f.matrix);
      if (e) {
        ctx.save();
        ctx.globalAlpha = f.alpha ?? 1;
        ctx.transform(...m);
        ctx.drawImage(e.img, f.box.x, f.box.y, f.box.w, f.box.h, -f.box.w / 2, -f.box.h / 2, f.box.w, f.box.h);
        ctx.restore();
      }
      if (f.marked) {   // the one being edited: a cross on its anchor point
        const p = apply(m, { x: 0, y: 0 });
        ctx.save();
        ctx.strokeStyle = 'rgba(255,200,80,0.95)';
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(p.x - 7, p.y); ctx.lineTo(p.x + 7, p.y);
        ctx.moveTo(p.x, p.y - 7); ctx.lineTo(p.x, p.y + 7);
        ctx.stroke();
        ctx.restore();
      }
    });
  }

  _drawGrid() {
    const ctx = this.ctx;
    const w = this.canvas.width, h = this.canvas.height;
    ctx.strokeStyle = 'rgba(255,255,255,0.06)';
    ctx.lineWidth = 1;
    const step = this.zoom >= 20 ? this.zoom : this.zoom * Math.ceil(20 / this.zoom);
    ctx.beginPath();
    for (let x = this.ox % step; x < w; x += step) { ctx.moveTo(x, 0); ctx.lineTo(x, h); }
    for (let y = this.oy % step; y < h; y += step) { ctx.moveTo(0, y); ctx.lineTo(w, y); }
    ctx.stroke();

    // Origin = the prefab root: the point the enemy is placed by and its ranges are measured from
    // (the centre of an enemy's body).
    ctx.strokeStyle = 'rgba(255,200,80,0.9)';
    ctx.beginPath();
    ctx.moveTo(this.ox - 12, this.oy); ctx.lineTo(this.ox + 12, this.oy);
    ctx.moveTo(this.ox, this.oy - 12); ctx.lineTo(this.ox, this.oy + 12);
    ctx.stroke();
  }

  _drawCollider(state, opts) {
    const c = state.collider;
    if (!c) return;
    const ctx = this.ctx;
    ctx.save();
    ctx.strokeStyle = 'rgba(120,255,120,0.8)';
    ctx.setLineDash([5, 4]);
    const p = apply(this.camera, c.offset);
    if (c.type === 'circle') {
      ctx.beginPath();
      ctx.arc(p.x, p.y, (c.radius || 0.5) * this.zoom, 0, Math.PI * 2);
      ctx.stroke();
    } else {
      const w = c.size.x * this.zoom, h = c.size.y * this.zoom;
      if (c.type === 'capsule') {
        const r = Math.min(w, h) / 2;
        ctx.beginPath();
        ctx.roundRect(p.x - w / 2, p.y - h / 2, w, h, r);
        ctx.stroke();
      } else ctx.strokeRect(p.x - w / 2, p.y - h / 2, w, h);
    }
    ctx.restore();
  }

  /** Screen point of an original-pixel point on a part. */
  _screenOf(state, part, pixel, opts) {
    let m = worldMatrix(state, part, opts.poses);
    if (opts.visualMatrix) m = multiply(opts.visualMatrix, m);
    const ppu = state.pixelsPerUnit;
    const local = { x: (pixel.x - part.pivot.x) / ppu, y: -(pixel.y - part.pivot.y) / ppu };
    return { p: apply(this.camera, apply(m, local)), m };
  }

  _drawMarkers(state, opts) {
    const ctx = this.ctx;
    const roles = state.roles;

    // Muzzle and grips are always shown: they are easy to forget and cheap to read.
    const aim = byUid(state, roles.aim);
    if (aim && roles.muzzle) this._dot(this._screenOf(state, aim, roles.muzzle, opts).p, '#ff5a5a', 'boca');
    Object.entries(roles.grips || {}).forEach(([uid, pt]) => {
      const limb = byUid(state, uid);
      if (limb && pt) this._dot(this._screenOf(state, limb, pt, opts).p, '#ffb347', 'agarre');
    });

    const part = byUid(state, opts.selectedUid);
    if (!part) return;

    // Outline of the selected art.
    const pm = this._partScreenMatrix(state, part, opts);
    if (pm) {
      ctx.save();
      ctx.strokeStyle = 'rgba(90,170,255,0.9)';
      ctx.lineWidth = 1.5;
      const corners = [[0, 0], [pm.image.w, 0], [pm.image.w, pm.image.h], [0, pm.image.h]].map(([x, y]) => apply(pm.m, { x, y }));
      ctx.beginPath();
      corners.forEach((c, i) => (i ? ctx.lineTo(c.x, c.y) : ctx.moveTo(c.x, c.y)));
      ctx.closePath();
      ctx.stroke();
      ctx.restore();
    }

    const { p: pivot, m } = this._screenOf(state, part, part.pivot, opts);

    // Point direction (RigPart.pointAngle): only matters for parts a boss throws (they fly with
    // their business end first), so it is only drawn on those.
    if (roles.limbs.includes(part.uid) || roles.orbiters.includes(part.uid))
      this._arrow(pivot, this._screenDir(m, part.pointAngle), 46, '#c38bff', 'frente');

    // The aim part: the barrel line (aimArtAngle) from the pivot — the line that is turned to face
    // the player — long enough to reach the muzzle when there is one.
    if (part.uid === roles.aim) {
      const muzzle = roles.muzzle ? this._screenOf(state, part, roles.muzzle, opts).p : null;
      const length = muzzle ? Math.max(40, Math.hypot(muzzle.x - pivot.x, muzzle.y - pivot.y)) : 64;
      this._arrow(pivot, this._screenDir(m, state.rig.aimArtAngle), length, '#5ae0ff', 'cañón');
    }

    if (part.center) this._dot(this._screenOf(state, part, part.center, opts).p, '#7dff9a', 'centro');

    // Pivot = the joint: the crosshair everything rotates around.
    ctx.save();
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(pivot.x, pivot.y, 7, 0, Math.PI * 2);
    ctx.moveTo(pivot.x - 11, pivot.y); ctx.lineTo(pivot.x + 11, pivot.y);
    ctx.moveTo(pivot.x, pivot.y - 11); ctx.lineTo(pivot.x, pivot.y + 11);
    ctx.stroke();
    ctx.restore();
  }

  /** Unit screen direction of a local-space angle (degrees, y up) through matrix `m`. */
  _screenDir(m, degrees) {
    const r = (degrees * Math.PI) / 180;
    const v = applyVector(multiply(this.camera, m), { x: Math.cos(r), y: Math.sin(r) });
    const len = Math.hypot(v.x, v.y) || 1;
    return { x: v.x / len, y: v.y / len };
  }

  _arrow(from, dir, length, color, label) {
    const ctx = this.ctx;
    const to = { x: from.x + dir.x * length, y: from.y + dir.y * length };
    ctx.save();
    ctx.strokeStyle = ctx.fillStyle = color;
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(from.x, from.y);
    ctx.lineTo(to.x, to.y);
    ctx.stroke();
    const a = Math.atan2(dir.y, dir.x);
    ctx.beginPath();
    ctx.moveTo(to.x, to.y);
    ctx.lineTo(to.x - 9 * Math.cos(a - 0.4), to.y - 9 * Math.sin(a - 0.4));
    ctx.lineTo(to.x - 9 * Math.cos(a + 0.4), to.y - 9 * Math.sin(a + 0.4));
    ctx.fill();
    ctx.font = '11px sans-serif';
    ctx.fillText(label, to.x + 4, to.y - 4);
    ctx.restore();
  }

  _dot(p, color, label) {
    const ctx = this.ctx;
    ctx.save();
    ctx.fillStyle = color;
    ctx.strokeStyle = '#000';
    ctx.beginPath();
    ctx.arc(p.x, p.y, 5, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.font = '11px sans-serif';
    ctx.fillText(label, p.x + 7, p.y - 6);
    ctx.restore();
  }
}
