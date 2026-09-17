// modules/library-panel.js
// -----------------------------------------------------------------------------
// Renders a grid of saved-entry cards (thumbnail/name/date/badges) into any container, with a
// caller-supplied action list per card — this is the ONE rendering implementation shared by the
// Sprites tab's "Enemigos guardados" panel (cargar/exportar zip/eliminar), the full Biblioteca
// tab (adds editar en Enemy Creator / exportar combinado), and the Map Tracer tab's saved-maps
// and saved-pieces lists. No DOM logic lives twice; only which entries to fetch, how to title/
// badge a card, and which actions to show differs per call site (see app.js).
//
// Defaults (`fetchEntries`, `getTitle`, `getBadges`) reproduce the original enemy-library-only
// behavior exactly, so every pre-existing call site (which only ever passed `getActions`) needs
// no changes.

import { listEnemies } from './enemy-library.js';

function defaultBadges(entry) {
  const animCount = (entry.sprite?.lanes || []).filter((l) => l.type === 'animation' && l.frameBoxIndices.length > 0).length;
  return [`${animCount} anim.`, ...(entry.config ? [{ text: 'config', accent: true }] : [])];
}

/**
 * @param {HTMLElement} container
 * @param {object} opts
 * @param {(entry: object) => Array<{label: string, className?: string, disabled?: boolean, title?: string, onClick: (entry: object) => void}>} opts.getActions
 * @param {() => Promise<object[]>} [opts.fetchEntries] - defaults to the shared enemy library.
 * @param {(entry: object) => string} [opts.getTitle] - defaults to `entry.enemyName`.
 * @param {(entry: object) => Array<string|{text:string,accent?:boolean}>} [opts.getBadges] - defaults to the enemy anim-count/config badges.
 * @param {string} [opts.emptyMessage]
 * @param {(entry: object) => boolean} [opts.isSelected] - when given, the card gets selectable
 *   styling and a `.selected` class reflects this. Opt-in only — omitted call sites (every
 *   pre-existing one) render exactly as before.
 * @param {(entry: object, index: number, ctrlKey: boolean, shiftKey: boolean, orderedEntries: object[]) => void} [opts.onCardClick] -
 *   fired on a click anywhere on the card OUTSIDE its action buttons. `index`/`orderedEntries` are
 *   the card's position and the full fetched list in render order, so a caller can implement
 *   ctrl-toggle/shift-range multi-select the same way app.js's `applySelectionClick` already does
 *   for the Sprites tab's box grid, without this module needing to own selection state itself.
 * @returns {Promise<void>}
 */
export async function renderLibraryCards(container, {
  getActions,
  fetchEntries = listEnemies,
  getTitle = (entry) => entry.enemyName,
  getBadges = defaultBadges,
  emptyMessage = 'Todavía no hay enemigos guardados. Usa "💾 Guardar como enemigo" en la pestaña Sprites.',
  isSelected = null,
  onCardClick = null,
}) {
  const entries = await fetchEntries();
  container.innerHTML = '';

  if (entries.length === 0) {
    container.innerHTML = `<p class="hint">${emptyMessage}</p>`;
    return;
  }

  entries.forEach((entry, index) => {
    const card = document.createElement('div');
    card.className = 'libCard';
    if (isSelected) {
      card.classList.add('libCardSelectable');
      card.classList.toggle('selected', isSelected(entry));
    }
    if (onCardClick) {
      card.addEventListener('click', (e) => {
        if (e.target.closest('.libActions')) return; // let action buttons behave normally
        onCardClick(entry, index, e.ctrlKey || e.metaKey, e.shiftKey, entries);
      });
    }

    const thumb = document.createElement('img');
    thumb.className = 'libThumb';
    thumb.src = entry.thumbnail || '';
    thumb.alt = getTitle(entry);
    card.appendChild(thumb);

    const info = document.createElement('div');
    info.className = 'libInfo';
    const badgesHtml = getBadges(entry).map((b) => {
      const badge = typeof b === 'string' ? { text: b } : b;
      return `<span class="libBadge${badge.accent ? ' libBadgeConfig' : ''}">${badge.text}</span>`;
    }).join('');
    info.innerHTML = `
      <div class="libName">${getTitle(entry)}</div>
      <div class="libDate">${new Date(entry.updatedAt).toLocaleString()}</div>
      <div class="libBadges">${badgesHtml}</div>
    `;
    card.appendChild(info);

    const actionsEl = document.createElement('div');
    actionsEl.className = 'libActions';
    getActions(entry).forEach(({ label, className, disabled, title, onClick }) => {
      const btn = document.createElement('button');
      btn.className = `small ${className || ''}`;
      btn.textContent = label;
      if (title) btn.title = title;
      if (disabled) btn.disabled = true;
      btn.onclick = () => onClick(entry);
      actionsEl.appendChild(btn);
    });
    card.appendChild(actionsEl);

    container.appendChild(card);
  });
}
