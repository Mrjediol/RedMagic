// modules/enemy-form-fields.js
// -----------------------------------------------------------------------------
// Generic, reusable input widgets — one builder per JSON-Schema type the Enemy Creator form
// needs (string/number/bool/enum/vector2/color/layerMask/assetRef). Each builder is DOM-coupled
// (they build real elements) but knows nothing about EnemyConfig specifically: modules/
// enemy-form.js is what maps schema fields onto these. Keeping the split here means a new field
// added to the schema is "call an existing builder with a new key", not new widget code.
//
// Every builder returns { row, setError(msg|null) }: `row` is a ready-to-append element, and
// `setError` shows/hides a small red message under the field (used by modules/enemy-form.js when
// per-field Ajv errors come back from the live validator).

function fieldRow(label, hint, controlEl) {
  const row = document.createElement('div');
  row.className = 'efRow';

  const labelEl = document.createElement('label');
  labelEl.textContent = label;
  row.appendChild(labelEl);

  row.appendChild(controlEl);

  if (hint) {
    const hintEl = document.createElement('p');
    hintEl.className = 'efHint';
    hintEl.textContent = hint;
    row.appendChild(hintEl);
  }

  const errorEl = document.createElement('p');
  errorEl.className = 'efError';
  errorEl.hidden = true;
  row.appendChild(errorEl);

  const setError = (msg) => {
    errorEl.hidden = !msg;
    errorEl.textContent = msg || '';
    row.classList.toggle('efRow-invalid', !!msg);
  };

  return { row, setError };
}

export function textField({ label, value, placeholder, hint, onChange }) {
  const input = document.createElement('input');
  input.type = 'text';
  input.value = value ?? '';
  if (placeholder) input.placeholder = placeholder;
  input.addEventListener('input', () => onChange(input.value));

  return { ...fieldRow(label, hint, input), input };
}

export function numberField({ label, value, step = 'any', min, max, hint, onChange }) {
  const input = document.createElement('input');
  input.type = 'number';
  input.step = step;
  if (min !== undefined) input.min = min;
  if (max !== undefined) input.max = max;
  input.value = value;
  input.addEventListener('input', () => {
    const n = parseFloat(input.value);
    onChange(Number.isFinite(n) ? n : 0);
  });

  return { ...fieldRow(label, hint, input), input };
}

export function boolField({ label, value, hint, onChange }) {
  const wrap = document.createElement('label');
  wrap.className = 'efCheckboxRow';
  const input = document.createElement('input');
  input.type = 'checkbox';
  input.checked = !!value;
  input.addEventListener('change', () => onChange(input.checked));
  wrap.appendChild(input);
  wrap.appendChild(document.createTextNode(label));

  const row = document.createElement('div');
  row.className = 'efRow';
  row.appendChild(wrap);
  if (hint) {
    const hintEl = document.createElement('p');
    hintEl.className = 'efHint';
    hintEl.textContent = hint;
    row.appendChild(hintEl);
  }
  const errorEl = document.createElement('p');
  errorEl.className = 'efError';
  errorEl.hidden = true;
  row.appendChild(errorEl);

  const setError = (msg) => {
    errorEl.hidden = !msg;
    errorEl.textContent = msg || '';
    row.classList.toggle('efRow-invalid', !!msg);
  };

  return { row, input, setError };
}

/** options: Array<{value, label}> */
export function enumField({ label, value, options, hint, onChange }) {
  const select = document.createElement('select');
  options.forEach((opt) => {
    const optEl = document.createElement('option');
    optEl.value = opt.value;
    optEl.textContent = opt.label ?? opt.value;
    if (opt.value === value) optEl.selected = true;
    select.appendChild(optEl);
  });
  select.addEventListener('change', () => onChange(select.value));

  return { ...fieldRow(label, hint, select), input: select };
}

export function vector2Field({ label, value, hint, onChange }) {
  const wrap = document.createElement('div');
  wrap.className = 'efVector2';

  const xInput = document.createElement('input');
  xInput.type = 'number'; xInput.step = 'any'; xInput.value = value.x;
  xInput.placeholder = 'x';
  const yInput = document.createElement('input');
  yInput.type = 'number'; yInput.step = 'any'; yInput.value = value.y;
  yInput.placeholder = 'y';

  const emit = () => onChange({
    x: Number.isFinite(parseFloat(xInput.value)) ? parseFloat(xInput.value) : 0,
    y: Number.isFinite(parseFloat(yInput.value)) ? parseFloat(yInput.value) : 0,
  });
  xInput.addEventListener('input', emit);
  yInput.addEventListener('input', emit);

  wrap.appendChild(xInput);
  wrap.appendChild(yInput);

  return { ...fieldRow(label, hint, wrap), xInput, yInput };
}

/** value: an 8-digit hex string "#rrggbbaa". Outputs the same shape, always 8-digit. */
export function colorField({ label, value, hint, onChange }) {
  const wrap = document.createElement('div');
  wrap.className = 'efColor';

  const rgb = (value || '#ffffffff').slice(0, 7);
  const alphaHex = (value || '#ffffffff').slice(7, 9) || 'ff';
  const alpha = Math.round((parseInt(alphaHex, 16) / 255) * 100);

  const colorInput = document.createElement('input');
  colorInput.type = 'color';
  colorInput.value = rgb;

  const alphaInput = document.createElement('input');
  alphaInput.type = 'number';
  alphaInput.min = 0; alphaInput.max = 100; alphaInput.step = 1;
  alphaInput.value = Number.isFinite(alpha) ? alpha : 100;
  alphaInput.title = 'Alfa %';

  const emit = () => {
    const a = Math.round((parseInt(alphaInput.value, 10) / 100) * 255);
    const aHex = Math.max(0, Math.min(255, Number.isFinite(a) ? a : 255)).toString(16).padStart(2, '0');
    onChange(`${colorInput.value}${aHex}`);
  };
  colorInput.addEventListener('input', emit);
  alphaInput.addEventListener('input', emit);

  wrap.appendChild(colorInput);
  wrap.appendChild(alphaInput);
  wrap.appendChild(document.createTextNode('% alfa'));

  return { ...fieldRow(label, hint, wrap), colorInput, alphaInput };
}

/**
 * Plain text input for a Unity asset path (assetRef's shorthand string form). Empty = "unset";
 * the caller (enemy-form.js / enemy-export.js) omits the key entirely rather than emitting "".
 */
export function assetRefField({ label, value, hint, onChange }) {
  return textField({ label, value, placeholder: 'Assets/...', hint, onChange });
}

/**
 * layerMask: mode switch between "everything" / "nothing" / known-layer checkboxes (+ a free-text
 * "otras capas" list for anything not in the checkbox set) / a raw advanced fallback (typed
 * exactly as JSON — an integer, or a `["Name", ...]` array) for anything the two structured modes
 * don't cover.
 *
 * @param {string[]} knownLayers - e.g. ['Ground', 'Platform'], the project's documented layers.
 * @param value - current layerMask value: 'everything' | 'nothing' | string[] | number | string.
 */
export function layerMaskField({ label, value, knownLayers, hint, onChange }) {
  const wrap = document.createElement('div');
  wrap.className = 'efLayerMask';

  const modeOf = (v) => {
    if (v === 'everything' || v === 'nothing') return v;
    if (Array.isArray(v)) return 'specific';
    return 'advanced';
  };

  let mode = modeOf(value);
  let specificKnown = Array.isArray(value) ? knownLayers.filter((l) => value.includes(l)) : [];
  let specificExtra = Array.isArray(value) ? value.filter((l) => !knownLayers.includes(l)).join(', ') : '';
  let advancedRaw = mode === 'advanced' ? (typeof value === 'string' ? value : JSON.stringify(value)) : '';

  const modeSelect = document.createElement('select');
  [['everything', 'Todas (everything)'], ['nothing', 'Ninguna (nothing)'],
   ['specific', 'Capas específicas'], ['advanced', 'Avanzado (JSON crudo)']].forEach(([v, l]) => {
    const o = document.createElement('option'); o.value = v; o.textContent = l;
    if (v === mode) o.selected = true;
    modeSelect.appendChild(o);
  });

  const specificBox = document.createElement('div');
  specificBox.className = 'efLayerSpecific';
  const checkboxes = knownLayers.map((layerName) => {
    const cb = document.createElement('label');
    cb.className = 'efCheckboxRow';
    const input = document.createElement('input');
    input.type = 'checkbox';
    input.checked = specificKnown.includes(layerName);
    input.addEventListener('change', emit);
    cb.appendChild(input);
    cb.appendChild(document.createTextNode(layerName));
    specificBox.appendChild(cb);
    return { layerName, input };
  });
  const extraInput = document.createElement('input');
  extraInput.type = 'text';
  extraInput.placeholder = 'Otras capas, separadas por coma';
  extraInput.value = specificExtra;
  extraInput.addEventListener('input', emit);
  specificBox.appendChild(extraInput);

  const advancedBox = document.createElement('div');
  advancedBox.className = 'efLayerAdvanced';
  const advancedInput = document.createElement('input');
  advancedInput.type = 'text';
  advancedInput.placeholder = '["MiCapa"] o un entero';
  advancedInput.value = advancedRaw;
  advancedInput.addEventListener('input', emit);
  advancedBox.appendChild(advancedInput);

  function refreshVisibility() {
    specificBox.hidden = mode !== 'specific';
    advancedBox.hidden = mode !== 'advanced';
  }

  function emit() {
    if (mode === 'everything') { onChange('everything'); return; }
    if (mode === 'nothing') { onChange('nothing'); return; }
    if (mode === 'specific') {
      const known = checkboxes.filter((c) => c.input.checked).map((c) => c.layerName);
      const extra = extraInput.value.split(',').map((s) => s.trim()).filter(Boolean);
      onChange([...known, ...extra]);
      return;
    }
    // advanced: try to parse as JSON (array or number), else pass the raw string through
    // (a single bare layer name is itself valid per the schema's layerMask string branch... no —
    // the schema only allows 'everything'/'nothing' as bare strings; a single custom layer name
    // must be an array. We still forward whatever the user typed so live validation can explain
    // exactly why, rather than silently "fixing" it here.)
    const raw = advancedInput.value.trim();
    try { onChange(JSON.parse(raw)); } catch { onChange(raw); }
  }

  modeSelect.addEventListener('change', () => {
    mode = modeSelect.value;
    refreshVisibility();
    emit();
  });

  refreshVisibility();
  wrap.appendChild(modeSelect);
  wrap.appendChild(specificBox);
  wrap.appendChild(advancedBox);

  return { ...fieldRow(label, hint, wrap) };
}
