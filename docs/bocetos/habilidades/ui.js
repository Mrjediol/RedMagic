// ui.js — pestañas, filtro por habilidad, paneles con barra de tiempo, sonido por clip y grabación a vídeo.
'use strict';
(function () {
  const $ = id => document.getElementById(id);
  const store = { get(k, d) { try { const v = localStorage.getItem('hab.' + k); return v === null ? d : v; } catch (e) { return d; } },
                  set(k, v) { try { localStorage.setItem('hab.' + k, v); } catch (e) { /* sin almacenamiento */ } } };

  $('speed').oninput = e => { speed = +e.target.value; $('speedv').textContent = speed.toFixed(2) + '×'; };
  $('layers').onchange = e => showLayers = e.target.checked;
  $('frm').onchange = e => framesOn = e.target.checked;
  $('snd').onchange = e => { soundOn = e.target.checked; if (soundOn) audioInit(); };

  // ------------------------------------------------ clips
  const views = [];
  let tab = store.get('tab', 'wip'), filter = store.get('filter', 'all');
  const grids = { saved: $('grid-saved'), wip: $('grid-wip'), arc: $('grid-arc'), snd: $('grid-snd'), sys: $('grid-sys') };

  function badge(tag) {
    const kind = /sistema nuevo/i.test(tag) ? 'new' : (/base/i.test(tag) ? 'base' : 'tag');
    return `<span class="badge ${kind}">${tag}</span>`;
  }
  for (const A of ABIL) {
    for (const tb of ['saved', 'wip', 'arc']) {
      const clips = A.clips.filter(c => (c.tab || 'wip') === tb);
      if (!clips.length) continue;
      const intro = document.createElement('section'); intro.className = 'intro'; intro.dataset.abil = A.id;
      intro.innerHTML = `<h2>${A.num} · ${A.name}</h2><p>${A.pitch}</p>` +
        (A.fit ? `<p class="fit"><b>Cómo encaja:</b> ${A.fit}</p>` : '') +
        (A.ideas && A.ideas.length ? `<details><summary>Más ideas de build para esta habilidad (sin animar)</summary><ul>${A.ideas.map(i => `<li>${i}</li>`).join('')}</ul></details>` : '');
      grids[tb].appendChild(intro);
      clips.forEach((c, ci) => {
        const R = A.build(c.P || {});
        const sec = document.createElement('section'); sec.dataset.abil = A.id;
        sec.innerHTML = `<h2>${c.title} ${badge(c.tag || 'Base')}</h2><p>${c.text}</p>`;
        const cv = document.createElement('canvas'); cv.width = W; cv.height = H; sec.appendChild(cv);
        const bar = document.createElement('div'); bar.className = 'bar';
        const mkB = (txt, title) => { const b = document.createElement('button'); b.textContent = txt; if (title) b.title = title; return b; };
        const btn = mkB('Pausa'), back = mkB('◀', 'Paso atrás'), fwd = mkB('▶', 'Paso adelante'), spk = mkB('🔊', 'Oír este clip (sólo uno a la vez)'), rec = mkB('⬇ vídeo', 'Graba una vuelta del clip a la velocidad actual (con sonido si 🔊 está activo)');
        const sl = document.createElement('input'); sl.type = 'range'; sl.min = 0; sl.max = 1000; sl.value = 0;
        const tm = document.createElement('span'); tm.className = 'tm';
        bar.append(btn, back, sl, fwd, tm, spk, rec); sec.appendChild(bar); grids[tb].appendChild(sec);
        const v = { A, c, R, cv, ctx: cv.getContext('2d'), t: 0, playing: true, sl, btn, tm, spk, audio: false, sec, tab: tb };
        v.setPlay = p => { v.playing = p; btn.textContent = p ? 'Pausa' : 'Seguir'; };
        btn.onclick = () => v.setPlay(!v.playing);
        sl.oninput = () => { v.setPlay(false); v.t = sl.value / 1000 * R.total; };
        back.onclick = () => { v.setPlay(false); v.t = Math.max(0, v.t - 0.01); };
        fwd.onclick = () => { v.setPlay(false); v.t = Math.min(R.total, v.t + 0.01); };
        spk.onclick = () => {
          const on = !v.audio; views.forEach(x => { x.audio = false; x.spk.classList.remove('on'); });
          v.audio = on; spk.classList.toggle('on', on); if (on) { $('snd').checked = true; soundOn = true; audioInit(); }
        };
        rec.onclick = () => record(v, rec, `${A.id}-${ci + 1}`);
        views.push(v);
      });
    }
  }

  // ------------------------------------------------ grabar una vuelta a webm
  function record(v, btn, name) {
    if (!window.MediaRecorder || !v.cv.captureStream) { btn.textContent = 'sin soporte'; return; }
    const stream = v.cv.captureStream(60);
    if (v.audio && AC && recDest) recDest.stream.getAudioTracks().forEach(tr => stream.addTrack(tr));
    const mime = ['video/webm;codecs=vp9,opus', 'video/webm;codecs=vp8,opus', 'video/webm'].find(m => MediaRecorder.isTypeSupported(m));
    const mr = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: 6e6 }), chunks = [];
    mr.ondataavailable = e => e.data.size && chunks.push(e.data);
    mr.onstop = () => {
      const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob(chunks, { type: 'video/webm' })); a.download = `habilidad-${name}.webm`; a.click();
      btn.textContent = '⬇ vídeo'; btn.disabled = false;
    };
    v.t = 0; v.setPlay(true); btn.disabled = true; btn.textContent = '● grabando';
    mr.start(); setTimeout(() => mr.stop(), v.R.total / speed * 1000 + 120);
  }

  // ------------------------------------------------ filtro por habilidad
  const chips = $('chips');
  const mkChip = (id, txt) => { const b = document.createElement('button'); b.textContent = txt; b.dataset.f = id; b.onclick = () => setFilter(id); chips.appendChild(b); };
  mkChip('all', 'Todas');
  for (const A of ABIL) mkChip(A.id, `${A.num} · ${A.short || A.name}`);
  function setFilter(f) {
    filter = f; store.set('filter', f);
    chips.querySelectorAll('button').forEach(b => b.classList.toggle('on', b.dataset.f === f));
    document.querySelectorAll('section[data-abil]').forEach(s => s.style.display = (f === 'all' || s.dataset.abil === f) ? '' : 'none');
  }
  function setTab(t) {
    tab = t; store.set('tab', t);
    document.querySelectorAll('[data-tab]').forEach(x => x.classList.toggle('on', x.dataset.tab === t));
    for (const k in grids) grids[k].style.display = k === t ? '' : 'none';
    chips.style.display = (t === 'snd' || t === 'sys') ? 'none' : '';
    if (!grids[t].children.length && t !== 'snd' && t !== 'sys') grids[t].innerHTML = '<section class="intro"><p>Nada aquí todavía.</p></section>';
  }
  document.querySelectorAll('[data-tab]').forEach(b => b.onclick = () => setTab(b.dataset.tab));

  let allPaused = false;
  $('pause').onclick = e => { allPaused = !allPaused; e.target.textContent = allPaused ? 'Seguir todo' : 'Pausa todo'; views.forEach(v => v.setPlay(!allPaused)); };

  // ------------------------------------------------ pestaña sonidos (agrupada por habilidad)
  {
    const groups = {};
    for (const id in SND) (groups[SND[id].group] = groups[SND[id].group] || []).push(id);
    for (const g in groups) {
      const sec = document.createElement('section'); sec.innerHTML = `<h2>${g}</h2>`;
      const row = document.createElement('div'); row.className = 'lab'; sec.appendChild(row);
      for (const id of groups[g]) { const b = document.createElement('button'); b.textContent = '▶ ' + SND[id].name; b.onclick = () => { audioInit(); SND[id].fn(1); }; row.appendChild(b); }
      grids.snd.appendChild(sec);
    }
  }
  // ------------------------------------------------ pestaña sistemas de build
  if (typeof SYSTEMS !== 'undefined') for (const s of SYSTEMS) {
    const sec = document.createElement('section'); sec.className = 'sys';
    sec.innerHTML = `<h2>${s.name} ${badge(s.status)}</h2><p>${s.what}</p>` +
      `<p class="fit"><b>Cambia cómo juegas:</b> ${s.play}</p>` +
      (s.items ? `<ul>${s.items.map(i => `<li>${i}</li>`).join('')}</ul>` : '') +
      (s.seen ? `<p class="seen">Se ve en: ${s.seen}</p>` : '');
    grids.sys.appendChild(sec);
  }

  setTab(tab); setFilter(filter);
  window.__views = views;                 // para revisar fotogramas desde la consola: __views.forEach(v => { v.setPlay(false); v.t = 0.5; })
  // hoja de contactos: __sheet('fan', [0.3, 0.4, 0.6], 0.6) → capa fija con cada clip (filas) en esos instantes (columnas); __sheet() la quita
  // crop = [x, y, w, h] en px del boceto (amplía esa zona); rows = índices de clip a mostrar
  window.__sheet = (id, times, sc = 0.5, crop = null, rows = null) => {
    document.getElementById('dbg-sheet')?.remove(); if (!id) return;
    const vs = views.filter(v => v.A.id === id && (!v.tab || v.tab === 'wip')).filter((v, i) => !rows || rows.includes(i));
    const [cx, cy, cw, ch] = crop || [0, 0, W, H];
    const cv = document.createElement('canvas'); cv.id = 'dbg-sheet'; cv.width = times.length * W * sc; cv.height = vs.length * H * sc;
    Object.assign(cv.style, { position: 'fixed', left: 0, top: 0, zIndex: 99, background: '#000', width: cv.width + 'px', height: cv.height + 'px' });
    const g = cv.getContext('2d'), off = document.createElement('canvas'); off.width = W; off.height = H; const oc = off.getContext('2d');
    vs.forEach((v, r) => times.forEach((t, c) => {
      scene(oc, Math.min(t, v.R.total), v.R);
      g.fillStyle = '#4a4a4a'; g.fillRect(c * W * sc, r * H * sc, W * sc - 2, H * sc - 2);
      g.drawImage(off, cx, cy, cw, ch, c * W * sc, r * H * sc, W * sc, H * sc);
      g.fillStyle = '#fff'; g.font = '12px monospace'; g.fillText(`${v.c.title.slice(0, 26)}  t=${t}`, c * W * sc + 4, r * H * sc + 13);
    }));
    document.body.appendChild(cv); return vs.length;
  };

  // ------------------------------------------------ bucle
  let last = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000); last = now;
    for (const v of views) {
      if (v.tab !== tab || v.sec.style.display === 'none') continue;
      const total = v.R.total;
      if (v.playing) {
        const prev = v.t; v.t += dt * speed;
        if (v.t >= total) v.t = 0;
        else if (soundOn && v.audio && AC) { const k = Math.min(4, 1 / speed); for (const e of v.R.sfx || []) if (prev < e.t && e.t <= v.t) e.f(k); }
        v.sl.value = v.t / total * 1000;
      }
      v.tm.textContent = v.t.toFixed(2) + 's';
      scene(v.ctx, v.t, v.R);
    }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
})();
