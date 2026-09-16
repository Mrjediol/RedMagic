// modules/library-panel.js
// -----------------------------------------------------------------------------
// Renders a grid of saved-enemy cards (thumbnail/name/date/badges) into any container, with a
// caller-supplied action list per card — this is the ONE rendering implementation shared by the
// Sprites tab's "Enemigos guardados" panel (cargar/exportar zip/eliminar) and the full Biblioteca
// tab (adds editar en Enemy Creator / exportar combinado). No DOM logic lives twice; only which
// actions to show differs per call site (see app.js).

import { listEnemies } from './enemy-library.js';

/**
 * @param {HTMLElement} container
 * @param {object} opts
 * @param {(entry: object) => Array<{label: string, className?: string, disabled?: boolean, title?: string, onClick: (entry: object) => void}>} opts.getActions
 * @returns {Promise<void>}
 */
export async function renderLibraryCards(container, { getActions }) {
  const entries = await listEnemies();
  container.innerHTML = '';

  if (entries.length === 0) {
    container.innerHTML = '<p class="hint">Todavía no hay enemigos guardados. Usa "💾 Guardar como enemigo" en la pestaña Sprites.</p>';
    return;
  }

  entries.forEach((entry) => {
    const card = document.createElement('div');
    card.className = 'libCard';

    const thumb = document.createElement('img');
    thumb.className = 'libThumb';
    thumb.src = entry.thumbnail || '';
    thumb.alt = entry.enemyName;
    card.appendChild(thumb);

    const info = document.createElement('div');
    info.className = 'libInfo';
    const animCount = (entry.sprite?.lanes || []).filter((l) => l.type === 'animation' && l.frameBoxIndices.length > 0).length;
    info.innerHTML = `
      <div class="libName">${entry.enemyName}</div>
      <div class="libDate">${new Date(entry.updatedAt).toLocaleString()}</div>
      <div class="libBadges">
        <span class="libBadge">${animCount} anim.</span>
        ${entry.config ? '<span class="libBadge libBadgeConfig">config</span>' : ''}
      </div>
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
