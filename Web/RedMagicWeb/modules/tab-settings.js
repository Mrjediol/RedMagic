// modules/tab-settings.js
// -----------------------------------------------------------------------------
// ⚙️ Opciones: qué pestañas se ven y en qué orden (localStorage, por navegador). Reordena los botones de #tabBar
// (y el separador, que se mueve como una pestaña más) y oculta los que estén apagados. ⚙️ Opciones va siempre al
// final y no se puede ocultar. Alt+1…9 sigue el orden visible (app.js ▸ tabShortcuts usa visibleTabs()).
const STORE = 'redmagic.tabs.v1';
const SEP = 'sep';
const FIXED = 'options';
/** Pestañas de otros proyectos o en desuso: ocultas de serie (el usuario las puede volver a encender). */
const DEFAULT_HIDDEN = ['sprites', 'enemy', 'fx', 'map'];

/**
 * @param {{ onChange?: () => void }} o  onChange: tras cada cambio (renumerar atajos).
 * @returns {{ visibleTabs: () => HTMLButtonElement[], isHidden: (tab: string) => boolean }}
 */
export function initTabSettings({ onChange } = {}) {
  const bar = document.getElementById('tabBar');
  const list = document.getElementById('optTabList');
  const sep = bar.querySelector('.tabSep');
  if (sep) sep.dataset.tab = SEP;
  const items = () => [...bar.querySelectorAll('.tabBtn, .tabSep')].filter((el) => el.dataset.tab !== FIXED);
  const byId = (id) => bar.querySelector(`[data-tab="${id}"]`);
  const fixed = byId(FIXED);

  const defaultOrder = items().map((el) => el.dataset.tab); // el de index.html
  let cfg = load();
  function load() {
    let saved = null;
    try { saved = JSON.parse(localStorage.getItem(STORE) || 'null'); } catch { /* sin almacenamiento */ }
    const ids = items().map((el) => el.dataset.tab);
    const order = (saved?.order || []).filter((id) => ids.includes(id));
    ids.forEach((id) => { if (!order.includes(id)) order.push(id); }); // pestañas nuevas: al final
    return { order, hidden: saved?.hidden ?? DEFAULT_HIDDEN.slice() };
  }
  function store() { try { localStorage.setItem(STORE, JSON.stringify(cfg)); } catch { /* sin almacenamiento */ } }

  function apply() {
    cfg.order.forEach((id) => { const el = byId(id); if (el) bar.insertBefore(el, fixed); });
    items().forEach((el) => { el.hidden = el.dataset.tab !== SEP && cfg.hidden.includes(el.dataset.tab); });
    // si la pestaña abierta se acaba de ocultar, abre la primera visible
    const active = bar.querySelector('.tabBtn.active');
    if (active && active.hidden) visibleTabs()[0]?.click();
    render();
    onChange?.();
  }

  function render() {
    list.innerHTML = '';
    cfg.order.forEach((id, i) => {
      const el = byId(id);
      if (!el) return;
      const row = document.createElement('div');
      row.className = 'optTabRow';
      const isSep = id === SEP;
      const name = isSep ? '── separador ──' : el.firstChild.textContent.trim();
      row.innerHTML = `<label class="optTabName">${isSep ? '' : `<input type="checkbox" ${cfg.hidden.includes(id) ? '' : 'checked'}>`}<span>${name}</span></label>`
        + `<button class="small" data-dir="-1" ${i === 0 ? 'disabled' : ''} title="Subir">▲</button>`
        + `<button class="small" data-dir="1" ${i === cfg.order.length - 1 ? 'disabled' : ''} title="Bajar">▼</button>`;
      row.querySelector('input')?.addEventListener('change', (e) => {
        cfg.hidden = e.target.checked ? cfg.hidden.filter((h) => h !== id) : [...cfg.hidden, id];
        store(); apply();
      });
      row.querySelectorAll('button').forEach((b) => b.addEventListener('click', () => {
        const j = i + Number(b.dataset.dir);
        [cfg.order[i], cfg.order[j]] = [cfg.order[j], cfg.order[i]];
        store(); apply();
      }));
      list.appendChild(row);
    });
  }

  document.getElementById('optTabsReset').addEventListener('click', () => {
    cfg = { order: defaultOrder.slice(), hidden: DEFAULT_HIDDEN.slice() };
    store(); apply();
  });

  function visibleTabs() { return [...bar.querySelectorAll('.tabBtn')].filter((b) => !b.hidden); }

  apply();
  return { visibleTabs, isHidden: (tab) => cfg.hidden.includes(tab) };
}
