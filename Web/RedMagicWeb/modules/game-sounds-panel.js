// modules/game-sounds-panel.js
// -----------------------------------------------------------------------------
// "🔊 Sonidos del juego": the palette of modules/game-sounds.js to listen to before (and after) it
// goes into the game. One dialog, a section per family, a row per sound: ▶ to hear it, its file name
// (what to look for in Biblioteca Jere ▸ Sounds), whether the rules assign it or it is an option to
// try by hand, and ⬇ to download the .wav. Nothing here edits anything: it is a listening room.

import { GAME_SOUNDS, GAME_FAMILIES, renderGameSound, assignedSoundIds } from './game-sounds.js';
import { playSound, downloadWav } from './vfx-stage.js';

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

export function initGameSoundsPanel({ button }) {
  if (!button) return;
  let dialog = null;
  let stopCurrent = () => {};

  function hear(id, def) {
    stopCurrent();
    const stopIt = playSound(id, { loop: !!def.loop, gain: def.loop ? 0.45 : 0.8, samples: renderGameSound(id) });
    // A loop is held for two turns, enough to hear the seam.
    const timer = setTimeout(stopIt, (def.loop ? def.seconds * 2 : def.seconds) * 1000 + 200);
    stopCurrent = () => { clearTimeout(timer); stopIt(); };
  }

  function build() {
    dialog = el('dialog', 'gameSounds');
    dialog.dataset.nodirty = '1';
    const assigned = assignedSoundIds();

    const head = el('div', 'gameSoundsHead');
    head.appendChild(el('h2', '', '🔊 Sonidos del juego'));
    const search = el('input');
    search.type = 'search';
    search.placeholder = 'Buscar (nombre o descripción)…';
    const count = el('span', 'efHint', '');
    const close = el('button', 'small', '✕ Cerrar');
    close.onclick = () => dialog.close();
    head.append(search, count, close);

    const body = el('div', 'gameSoundsBody');
    body.appendChild(el('p', 'hint',
      'Sintetizados: marcadores con la forma correcta, para sustituir por grabaciones cuando las haya. '
      + '"asignado" = las reglas lo ponen en algún hueco (Unity ▸ Tools ▸ RedMagic ▸ Audio ▸ 4). '
      + '"opción" = no está puesto en nada: para probarlo, asígnalo a mano en Biblioteca Jere ▸ Sounds '
      + '(Assets/Audio/Sfx/Game/<familia>/<nombre>.wav).'));

    const rows = [];
    Object.entries(GAME_FAMILIES).forEach(([family, info]) => {
      const ids = Object.keys(GAME_SOUNDS).filter((id) => GAME_SOUNDS[id].family === family);
      if (!ids.length) return;
      const section = el('details');
      section.open = true;
      section.appendChild(el('summary', '', `${info.label} · ${ids.length}`));
      const list = el('div', 'vfxSounds');
      ids.forEach((id) => {
        const def = GAME_SOUNDS[id];
        const row = el('div', 'vfxSound');
        const play = el('button', 'small', '▶');
        play.title = 'Oír';
        play.onclick = () => hear(id, def);
        const name = el('span', 'gameSoundId', id);
        name.title = `${family}/${id}.wav`;
        const tag = el('span', `gameSoundTag${assigned.has(id) ? ' assigned' : ''}`, assigned.has(id) ? 'asignado' : 'opción');
        const save = el('button', 'small', '⬇');
        save.title = 'Descargar .wav';
        save.onclick = () => downloadWav(id, renderGameSound(id));
        row.append(play, name, el('span', 'vfxSoundName', `${def.label} · ${def.seconds.toFixed(2)} s${def.loop ? ' · bucle' : ''}`), tag, save);
        list.appendChild(row);
        rows.push({ row, section, text: `${id} ${def.label} ${info.label}`.toLowerCase() });
      });
      section.appendChild(list);
      body.appendChild(section);
    });

    const filter = () => {
      const q = search.value.trim().toLowerCase();
      let shown = 0;
      const sections = new Map();
      rows.forEach(({ row, section, text }) => {
        const hit = !q || text.includes(q);
        row.style.display = hit ? '' : 'none';
        if (hit) shown++;
        sections.set(section, (sections.get(section) || 0) + (hit ? 1 : 0));
      });
      sections.forEach((n, section) => { section.style.display = n ? '' : 'none'; });
      count.textContent = `${shown} de ${rows.length}`;
    };
    search.oninput = filter;
    filter();

    dialog.addEventListener('close', () => stopCurrent());
    // Space here presses the focused ▶, it is not the tab's "Probar" shortcut.
    dialog.addEventListener('keydown', (e) => { if (e.code === 'Space') e.stopPropagation(); });
    // A click on the backdrop (the dialog element itself, outside its content) closes it.
    dialog.addEventListener('click', (e) => { if (e.target === dialog) dialog.close(); });
    dialog.append(head, body);
    document.body.appendChild(dialog);
  }

  button.onclick = () => {
    if (!dialog) build();
    dialog.showModal();
  };
}
