// modules/shell-tools.js
// -----------------------------------------------------------------------------
// The shell's conveniences — things that belong to the whole tool, not to one tab:
//
//   Ctrl+S   saves whatever the open tab saves           (saveActions[tab])
//   Ctrl+K   a finder over everything: tabs, effects, saved entries, actions   (paletteItems())
//   Space    "▶ Probar" of the open tab, when the tab has one                  (playActions[tab])
//   ●        a dot on a tab with changes that were not saved, and the browser's "leave?" question
//            when closing with any pending
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

  const dirty = new Set();
  function setDirty(tab, on) {
    if (on === dirty.has(tab)) return;
    if (on) dirty.add(tab); else dirty.delete(tab);
    const btn = tabButton(tab);
    if (btn) {
      btn.classList.toggle('dirty', on);
      btn.dataset.dirtyTitle = on ? 'Tiene cambios sin guardar' : '';
    }
  }

  dirtyTabs.forEach((tab) => {
    const panel = tabPanel(tab);
    if (!panel) return;
    const edited = (e) => {
      const t = e.target;
      // Looking is not editing: search boxes, file pickers and anything marked as view-only.
      if (!t || !t.closest || t.closest('[data-nodirty]') || t.type === 'file' || t.type === 'search') return;
      setDirty(tab, true);
    };
    panel.addEventListener('input', edited);
    panel.addEventListener('change', edited);
    // A drag on an editing canvas (moving a part, a pivot) is an edit too.
    panel.addEventListener('pointerup', (e) => { if (e.target.tagName === 'CANVAS' && !e.target.closest('[data-nodirty]')) setDirty(tab, true); });
    // Saving or exporting with the mouse clears it, the same as Ctrl+S.
    panel.addEventListener('click', (e) => {
      const btn = e.target.closest ? e.target.closest('button') : null;
      if (btn && !btn.disabled && /^\s*(💾|⬇)/u.test(btn.textContent)) setTimeout(() => setDirty(tab, false), 350);
    });
  });

  window.addEventListener('beforeunload', (e) => {
    if (!dirty.size) return;
    e.preventDefault();
    e.returnValue = '';      // the browser shows its own "leave this page?" text
  });

  // ============================================================ Ctrl+S

  async function save() {
    const tab = activeTab();
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
