// tools/comfy/comfy-graph.mjs
// -----------------------------------------------------------------------------
// One description of a ComfyUI graph, two outputs:
//   toUi(graph, defs)  → the workflow the ComfyUI editor opens (litegraph 0.4: positions, groups, notes)
//   toApi(graph, defs) → the "API format" that POST /prompt runs (only what executes)
// so the workflow the user sees in Comfy and the one Claude runs are the same graph, never two copies.
//
//   graph = { nodes: [...], groups: [...] }
//   node  = { id, type, pos: [x, y], size: [w, h], title?, bypass?, color?,
//             values: { widgetName: value },              // anything not given takes the node's default
//             links:  { inputName: [fromNodeId, outputIndex] } }
//   group = { title, bounding: [x, y, w, h], color? }
//   A node of type 'MarkdownNote' is editor-only: { text } and it never reaches the API.
//
// `defs` = GET /object_info of the running ComfyUI (node definitions): inputs, their order, which are
// widgets, defaults. Nothing about a node is hardcoded here except two editor conventions:
// a `seed` widget is followed by its "control after generate" widget, and LoadImage has an upload button.
//
// A bypassed node (bypass: true) stays in the editor, switched off (Ctrl+B turns it on). In the API
// format it is removed: what it fed gets its first input of the same type instead, or nothing.

const WIDGET_TYPES = new Set(['INT', 'FLOAT', 'STRING', 'BOOLEAN', 'COMBO', 'COLORCODE', 'IMAGEUPLOAD']);

function inputsOf(def) {
  const list = [];
  for (const section of ['required', 'optional']) {
    for (const [name, spec] of Object.entries(def.input?.[section] || {})) {
      const [kind, options = {}] = spec;
      const combo = Array.isArray(kind);
      const type = combo ? 'COMBO' : kind;
      const choices = combo ? kind : options.options;
      list.push({ name, type, options, choices, optional: section === 'optional', widget: WIDGET_TYPES.has(type), autogrow: type === 'COMFY_AUTOGROW_V3' });
    }
  }
  return list;
}

function defaultOf(input) {
  if (input.options.default !== undefined) return input.options.default;
  if (input.choices?.length) return input.choices[0];
  return { INT: 0, FLOAT: 0, STRING: '', BOOLEAN: false }[input.type] ?? null;
}

function defOf(defs, node) {
  const def = defs[node.type];
  if (!def) throw new Error(`ComfyUI has no node '${node.type}' (node ${node.id}): a custom node is missing`);
  return def;
}

/** Widget values of a node, by name, checked against the node's choices. */
function valuesOf(node, def) {
  const values = {};
  for (const input of inputsOf(def)) {
    if (!input.widget) continue;
    const value = node.values?.[input.name] ?? defaultOf(input);
    if (input.type === 'COMBO' && input.choices?.length && !input.choices.includes(value) && !String(value).endsWith('[output]')) {
      throw new Error(`${node.type}.${input.name}: '${value}' is not installed (have: ${input.choices.slice(0, 8).join(', ') || 'nothing'})`);
    }
    values[input.name] = value;
  }
  for (const name of Object.keys(node.values || {})) {
    if (!(name in values)) throw new Error(`${node.type} has no widget '${name}'`);
  }
  return values;
}

// ============================================================ editor format

export function toUi(graph, defs, { id = 'claude-workflow' } = {}) {
  const byId = new Map(graph.nodes.map((n) => [n.id, n]));
  const links = [];
  const outLinks = new Map();   // "nodeId:slot" → [link ids]
  let nextLink = 1;

  const nodes = graph.nodes.map((node, order) => {
    if (node.type === 'MarkdownNote') {
      return { id: node.id, type: 'MarkdownNote', pos: node.pos, size: node.size, flags: {}, order, mode: 0, inputs: [], outputs: [],
        title: node.title || 'Note', properties: {}, widgets_values: [node.text], color: '#222', bgcolor: '#000' };
    }
    const def = defOf(defs, node);
    const values = valuesOf(node, def);
    const inputs = [];
    const widgetValues = [];
    const named = {};

    const connect = (name, type) => {
      const from = node.links?.[name];
      if (!from) return { link: null, type };
      const [fromId, slot] = from;
      const source = byId.get(fromId);
      if (!source) throw new Error(`${node.type}.${name}: links to missing node ${fromId}`);
      const outType = defOf(defs, source).output[slot];
      const real = outType === 'COMFY_MATCHTYPE_V3' ? type : outType;
      const linkId = nextLink++;
      links.push([linkId, fromId, slot, node.id, inputs.length, real]);
      const key = `${fromId}:${slot}`;
      outLinks.set(key, [...(outLinks.get(key) || []), linkId]);
      return { link: linkId, type: real };
    };

    for (const input of inputsOf(def)) {
      if (input.autogrow) {
        // The slots in use, plus one free so another image can be plugged in.
        const names = input.options.template.names;
        const used = names.filter((n) => node.links?.[`${input.name}.${n}`]);
        for (const n of [...used, names[used.length]].filter(Boolean)) {
          const { link } = connect(`${input.name}.${n}`, 'IMAGE');
          inputs.push({ name: `${input.name}.${n}`, shape: 7, type: 'IMAGE', link });
        }
        continue;
      }
      if (input.widget) {
        inputs.push({ name: input.name, type: input.type, ...(input.optional ? { shape: 7 } : {}), widget: { name: input.name }, link: null });
        widgetValues.push(values[input.name]);
        named[input.name] = values[input.name];
        if (input.name === 'seed' && input.options.control_after_generate) {
          widgetValues.push(node.seedControl || 'fixed');
          named.control_after_generate = node.seedControl || 'fixed';
        }
        continue;
      }
      const matched = input.type === 'COMFY_MATCHTYPE_V3' ? (node.matchType || '*') : input.type;
      const { link, type } = connect(input.name, matched);
      inputs.push({ name: input.name, type, ...(input.optional ? { shape: 7 } : {}), link });
    }
    if (node.type === 'LoadImage') {
      inputs.push({ name: 'upload', type: 'IMAGEUPLOAD', widget: { name: 'upload' }, link: null });
      widgetValues.push('image');
      named.upload = 'image';
    }

    return {
      id: node.id, type: node.type, pos: node.pos, size: node.size, flags: {}, order, mode: node.bypass ? 4 : 0,
      inputs,
      outputs: def.output.map((type, i) => ({ name: def.output_name?.[i] || type, type: type === 'COMFY_MATCHTYPE_V3' ? (node.matchType || '*') : type, links: null })),
      ...(node.title ? { title: node.title } : {}),
      ...(node.color ? { color: node.color.fg, bgcolor: node.color.bg } : {}),
      properties: { 'Node name for S&R': node.type },
      widgets_values: widgetValues,
      widgets_values_named: named,
    };
  });

  for (const node of nodes) {
    node.outputs.forEach((out, slot) => { out.links = outLinks.get(`${node.id}:${slot}`) || (slot === 0 ? [] : null); });
  }

  return {
    id, revision: 0,
    last_node_id: Math.max(...graph.nodes.map((n) => n.id)),
    last_link_id: nextLink - 1,
    nodes, links,
    groups: (graph.groups || []).map((g, i) => ({ id: i + 1, title: g.title, bounding: g.bounding, color: g.color || '#3f789e', font_size: 24, flags: {} })),
    config: {},
    extra: { ds: { scale: 0.5, offset: [60, 60] } },
    version: 0.4,
  };
}

// ============================================================ API format

export function toApi(graph, defs) {
  const byId = new Map(graph.nodes.map((n) => [n.id, n]));
  const live = (node) => node.type !== 'MarkdownNote' && !node.bypass;

  /** Where a link really comes from once bypassed nodes are taken out. Null = from nowhere. */
  const resolve = ([fromId, slot]) => {
    const source = byId.get(fromId);
    if (!source) return null;
    if (live(source)) return [String(fromId), slot];
    const def = defOf(defs, source);
    const outType = def.output[slot];
    // Bypass = pass through the first input of the type that output has.
    const through = inputsOf(def).find((i) => !i.widget && (i.type === outType || i.type === 'COMFY_MATCHTYPE_V3') && source.links?.[i.name]);
    return through ? resolve(source.links[through.name]) : null;
  };

  const prompt = {};
  for (const node of graph.nodes) {
    if (!live(node)) continue;
    const def = defOf(defs, node);
    const inputs = { ...valuesOf(node, def) };
    if (node.type === 'LoadImage') delete inputs.upload;
    for (const [name, link] of Object.entries(node.links || {})) {
      const from = resolve(link);
      if (from) inputs[name] = from;
    }
    prompt[String(node.id)] = { class_type: node.type, inputs, _meta: { title: node.title || node.type } };
  }
  return prompt;
}
