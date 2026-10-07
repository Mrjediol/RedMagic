// modules/shell-tools.js
// -----------------------------------------------------------------------------
// The shell's conveniences — things that belong to the whole tool, not to one tab:
//
//   Ctrl+S   saves whatever the open tab saves           (saveActions[tab])
//   Ctrl+K   a finder over everything: tabs, effects, saved entries, actions   (paletteItems())
//   Space    "▶ Probar" of the open tab, when the tab has one                  (playActions[tab])
//   ●        a dot on a tab with changes that were not saved, a "● Sin guardar" chip in the tab bar listing
//            WHAT was touched in each (field + section + when), and the browser's "leave?" question when
//            closing with any pending (answering "stay" opens that list)
//   toast()  a short message at the bottom (what a shortcut did, since nothing else says it)
//
// It knows nothing about the tabs: app.js hands it one function per tab for each thing. A tab with
// no entry simply has no save / no Probar. DOM-coupled (it builds the finder and the toast).

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

/** Typing in a field must keep typing: Space and the like are only shortcuts outside of one. */
function typingIn(target) {
  if (!target || !target.closest) return false;
  if (target.isContentEditable) return true;
  const field = target.closest('input, textarea, select, button, summary, a');
  if (!field) return false;
  if (field.tagName !== 'INPUT') return true;
  return !['checkbox', 'radio', 'range', 'color'].includes(field.type);
}

/**
 * @param {object} o
 * @param {() => string} o.activeTab                 id of the open tab
 * @param {(tab: string) => HTMLElement} o.tabButton the tab's button (the dot goes on it)
 * @param {(tab: string) => HTMLElement} o.tabPanel  the tab's panel (its edits are watched)
 * @param {Record<string, () => (string|false|void|Promise<string|false|void>)>} o.saveActions
 *        a string = the message to show; false = there was nothing to save
 * @param {Record<string, () => boolean>} o.playActions  true = something was played / stopped
 * @param {() => Promise<Array<{label: string, hint?: string, group: string, run: () => void}>>} o.paletteItems
 * @param {string[]} o.dirtyTabs                     tabs whose edits count as "not saved"
 */
export function initShellTools({ activeTab, tabButton, tabPanel, saveActions, playActions, paletteItems, dirtyTabs }) {
  // ============================================================ toast

  const toastEl = el('div', 'shellToast');
  toastEl.hidden = true;
  document.body.appendChild(toastEl);
  let toastTimer = 0;
  function toast(message, kind = 'ok') {
    toastEl.textContent = message;
    toastEl.dataset.kind = kind;
    toastEl.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { toastEl.hidden = true; }, 2600);
  }

  // ============================================================ not saved

  // tab → Map(what was edited → when). The browser's "leave?" box cannot say what: the 🟠 chip in the tab bar
  // and its list can (also opened by itself when you answer "stay" to the browser's box).
  const dirty = new Map();
  const tabName = (tab) => tabButton(tab)?.firstChild?.textContent.trim() || tab;

  const chip = el('button', 'shellUnsaved');
  chip.type = 'button';
  chip.hidden = true;
  chip.title = 'Qué cambios no se han guardado';
  const bar = tabButton(dirtyTabs[0])?.parentElement;
  if (bar) bar.appendChild(chip); else document.body.appendChild(chip);
  const pop = el('div', 'shellUnsavedPop');
  pop.hidden = true;
  document.body.appendChild(pop);

  function setDirty(tab, on, what) {
    if (on) {
      if (!dirty.has(tab)) dirty.set(tab, new Map());
      const edits = dirty.get(tab);
      const key = what || 'un cambio';
      edits.delete(key);             // newest last
      edits.set(key, Date.now());
    } else if (!dirty.delete(tab)) return;
    const btn = tabButton(tab);
    if (btn) {
      btn.classList.toggle('dirty', dirty.has(tab));
      btn.dataset.dirtyTitle = dirty.has(tab) ? 'Tiene cambios sin guardar' : '';
    }
    chip.hidden = !dirty.size;
    chip.textContent = `● Sin guardar: ${[...dirty.keys()].map(tabName).join(', ')}`;
    if (!pop.hidden) renderPop();
  }

  /** "Escala (Partes)": the field's own label, then the section it is in. */
  function describe(t) {
    const clean = (s) => (s || '').replace(/\s+/g, ' ').trim().slice(0, 48);
    let label = t.getAttribute?.('aria-label') || '';
    if (!label && t.id) label = t.ownerDocument.querySelector(`label[for="${CSS.escape(t.id)}"]`)?.textContent;
    if (!label) {
      const wrap = t.closest('label');
      if (wrap) label = [...wrap.childNodes].filter((n) => n !== t && !n.contains?.(t)).map((n) => n.textContent).join(' ');
    }
    if (!label && t.previousElementSibling && /^(LABEL|SPAN|B|STRONG)$/.test(t.previousElementSibling.tagName)) label = t.previousElementSibling.textContent;
    if (!label) label = t.placeholder || t.title || t.name || t.id || '';
    if (t.tagName === 'CANVAS') label = 'arrastre en el lienzo';
    const section = t.closest('details')?.querySelector(':scope > summary')?.textContent
      || t.closest('fieldset')?.querySelector(':scope > legend')?.textContent
      || t.closest('section')?.querySelector('h2, h3, h4')?.textContent || '';
    label = clean(label) || (t.tagName === 'SELECT' ? 'un desplegable' : t.type === 'checkbox' ? 'una casilla' : 'un campo');
    return section && clean(section) !== label ? `${label} (${clean(section)})` : label;
  }

  const ago = (ms) => { const m = Math.round((Date.now() - ms) / 60000); return m < 1 ? 'ahora' : m < 60 ? `hace ${m} min` : `hace ${Math.floor(m / 60)} h ${m % 60} min`; };

  function renderPop() {
    pop.innerHTML = '';
    pop.appendChild(el('h3', null, 'Cambios sin guardar'));
    if (!dirty.size) { pop.appendChild(el('p', 'shellUnsavedEmpty', 'Nada: puedes cerrar sin perder nada.')); }
    dirty.forEach((edits, tab) => {
      const box = el('div', 'shellUnsavedTab');
      const head = el('div', 'shellUnsavedHead');
      head.appendChild(el('b', null, tabName(tab)));
      const go = el('button', 'small', 'Ir');
      go.onclick = () => { closePop(); tabButton(tab)?.click(); };
      const save = saveActions[tab] ? el('button', 'small primary', '💾 Guardar') : null;
      if (save) save.onclick = async () => { tabButton(tab)?.click(); await saveTab(tab); };
      const drop = el('button', 'small', 'No es nada');
      drop.title = 'Quitar el aviso: lo que tocaste no hace falta guardarlo';
      drop.onclick = () => setDirty(tab, false);
      head.append(go, ...(save ? [save] : []), drop);
      const list = el('ul');
      [...edits].reverse().slice(0, 8).forEach(([what, at]) => {
        const li = el('li', null, what);
        li.appendChild(el('span', 'shellUnsavedAgo', ` · ${ago(at)}`));
        list.appendChild(li);
      });
      if (edits.size > 8) list.appendChild(el('li', 'shellUnsavedAgo', `…y ${edits.size - 8} más`));
      box.append(head, list);
      pop.appendChild(box);
    });
    pop.appendChild(el('p', 'shellUnsavedEmpty', 'Es lo que has tocado desde la última vez que guardaste o exportaste esa pestaña. Si sabes que no importa, «No es nada» quita el aviso.'));
  }
  function openPop() { renderPop(); pop.hidden = false; }
  function closePop() { pop.hidden = true; }
  chip.addEventListener('click', () => { if (pop.hidden) openPop(); else closePop(); });
  document.addEventListener('mousedown', (e) => { if (!pop.hidden && !pop.contains(e.target) && e.target !== chip) closePop(); });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && !pop.hidden) closePop(); });

  dirtyTabs.forEach((tab) => {
    const panel = tabPanel(tab);
    if (!panel) return;
    const edited = (e) => {
      const t = e.target;
      // Looking is not editing: search boxes, file pickers and anything marked as view-only.
      if (!t || !t.closest || t.closest('[data-nodirty]') || t.type === 'file' || t.type === 'search') return;
      setDirty(tab, true, describe(t));
    };
    panel.addEventListener('input', edited);
    panel.addEventListener('change', edited);
    // A drag on an editing canvas (moving a part, a pivot) is an edit too; a click on it (selecting) is not.
    let down = null;
    panel.addEventListener('pointerdown', (e) => { down = e.target.tagName === 'CANVAS' ? { x: e.clientX, y: e.clientY } : null; });
    panel.addEventListener('pointerup', (e) => {
      const moved = down && Math.hypot(e.clientX - down.x, e.clientY - down.y) > 4;
      down = null;
      if (moved && e.target.tagName === 'CANVAS' && !e.target.closest('[data-nodirty]')) setDirty(tab, true, describe(e.target));
    });
    // Saving or exporting with the mouse clears it, the same as Ctrl+S.
    panel.addEventListener('click', (e) => {
      const btn = e.target.closest ? e.target.closest('button') : null;
      if (btn && !btn.disabled && /^\s*(💾|⬇)/u.test(btn.textContent)) setTimeout(() => setDirty(tab, false), 350);
    });
  });

  window.addEventListener('beforeunload', (e) => {
    if (!dirty.size) return;
    e.preventDefault();
    e.returnValue = '';      // the browser shows its own "leave this page?" text, which cannot be changed
    // Timers wait while that box is up: this runs only if you chose to stay — then it says what was not saved.
    setTimeout(openPop, 0);
  });

  // ============================================================ Ctrl+S

  const save = () => saveTab(activeTab());

  async function saveTab(tab) {
    const action = saveActions[tab];
    if (!action) { toast('Esta pestaña no tiene nada que guardar.', 'info'); return; }
    try {
      const result = await action();
      if (result === false) { toast('No hay nada que guardar todavía.', 'info'); return; }
      if (typeof result === 'string') { toast(result, 'info'); return; }
      setDirty(tab, false);
      toast('Guardado.');
    } catch (err) {
      toast(`No se pudo guardar: ${err.message}`, 'error');
    }
  }

  // ============================================================ Ctrl+K — the finder

  const overlay = el('div', 'shellPalette');
  overlay.hidden = true;
  const box = el('div', 'shellPaletteBox');
  const input = el('input');
  input.type = 'text';
  input.placeholder = 'Ir a una pestaña, un efecto, algo guardado…';
  input.setAttribute('aria-label', 'Buscar');
  const list = el('div', 'shellPaletteList');
  const foot = el('div', 'shellPaletteFoot', '↑ ↓ para moverse · Enter para abrir · Esc para cerrar');
  box.append(input, list, foot);
  overlay.appendChild(box);
  document.body.appendChild(overlay);

  let items = [], shown = [], cursor = 0;

  function renderPalette() {
    const words = input.value.toLowerCase().split(/\s+/).filter(Boolean);
    shown = items.filter((it) => words.every((w) => it.haystack.includes(w))).slice(0, 60);
    cursor = Math.min(cursor, Math.max(0, shown.length - 1));
    list.innerHTML = '';
    if (!shown.length) { list.appendChild(el('p', 'shellPaletteEmpty', 'Nada con ese nombre.')); return; }
    let group = null;
    shown.forEach((it, i) => {
      if (it.group !== group) { group = it.group; list.appendChild(el('div', 'shellPaletteGroup', group)); }
      const row = el('div', `shellPaletteRow${i === cursor ? ' active' : ''}`);
      row.appendChild(el('span', 'shellPaletteLabel', it.label));
      if (it.hint) row.appendChild(el('span', 'shellPaletteHint', it.hint));
      row.onmousemove = () => { if (cursor !== i) { cursor = i; renderPalette(); } };
      row.onclick = () => choose(i);
      list.appendChild(row);
    });
    list.querySelector('.shellPaletteRow.active')?.scrollIntoView?.({ block: 'nearest' });
  }

  function closePalette() { overlay.hidden = true; }

  function choose(i) {
    const it = shown[i];
    if (!it) return;
    closePalette();
    try { it.run(); } catch (err) { toast(err.message, 'error'); }
  }

  async function openPalette() {
    overlay.hidden = false;
    input.value = '';
    cursor = 0;
    input.focus();
    items = (await paletteItems()).map((it) => ({ ...it, haystack: `${it.label} ${it.hint || ''} ${it.group}`.toLowerCase() }));
    renderPalette();
  }

  input.addEventListener('input', () => { cursor = 0; renderPalette(); });
  input.addEventListener('keydown', (e) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); cursor = Math.min(shown.length - 1, cursor + 1); renderPalette(); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); cursor = Math.max(0, cursor - 1); renderPalette(); }
    else if (e.key === 'Enter') { e.preventDefault(); choose(cursor); }
    else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); closePalette(); }
  });
  overlay.addEventListener('mousedown', (e) => { if (e.target === overlay) closePalette(); });

  // ============================================================ keys

  document.addEventListener('keydown', (e) => {
    const mod = (e.ctrlKey || e.metaKey) && !e.altKey;
    if (mod && !e.shiftKey && e.code === 'KeyS') { e.preventDefault(); save(); return; }
    if (mod && !e.shiftKey && e.code === 'KeyK') { e.preventDefault(); if (overlay.hidden) openPalette(); else closePalette(); return; }
    if (!overlay.hidden) return;
    if (e.code === 'Space' && !e.ctrlKey && !e.metaKey && !e.altKey && !e.repeat && !typingIn(e.target)) {
      const action = playActions[activeTab()];
      if (action && action()) e.preventDefault();
    }
  });

  return { toast, save, openPalette, setDirty, isDirty: (tab) => dirty.has(tab) };
}
