# Prompts de enemigos por zona + modo "Prompt para Gemini" en 🎨 Diseños

Estado: **en curso** (2026-10-05). Actualizar tras cada hito.

## Qué se pidió (palabras del usuario, resumidas)
- En 🎨 Diseños ▸ ＋ Pedir propuestas ▸ Enemigo: elegir **zona** (hielo / volcán / bosque, y las que vengan) y **modo**:
  **Enemigo** (como hasta ahora, por ComfyUI, sin quitar nada) o **Prompt** (devuelve un prompt óptimo para pegar en la
  app de Gemini; ese es el modo por defecto).
- Opción de **pegar una imagen** en la tarjeta (la que usa la web como base para montar el enemigo).
- El prompt se adapta a la zona (paleta, cristales de la zona, estilo del juego: pintado semi-realista, borde fino, sin
  brillos fuera; todo enemigo lleva 1-3 cristales; cian/acento solo en cristales y ojos).
- Actualizar también los prompts que se mandan a ComfyUI con todo lo aprendido (pose sin partes ocultas, nº de piezas,
  fondo blanco, estilo del juego, "COMPLETE" en los cortes) "a ver si puede competirle a Gemini".
- Sin fuentes en el cuaderno de Gemini (copia las criaturas): el estilo va implícito en el prompt.
- **Parar al llegar al 99 % del uso semanal** y dejar aquí el estado (Claude no puede leer ese contador: si el usuario
  avisa, o el límite corta la sesión, retomar desde esta lista).

## Hecho
- `docs/zonas.json` (ice, volcano, forest; una zona nueva = una entrada) y `Web/RedMagicWeb/tools/enemy-prompt.mjs`
  (`buildEnemyPrompt({zone,target:'gemini'|'comfy',creature,design,crystals,pieces,note})`, CLI `--list`).
- Skill `make-enemy`: `base-prompt-gemini.md` v4, regla "ninguna pieza oculta", cláusula COMPLETE, 1 seed por pieza.
- Memorias: enemy-bases-show-every-part-whole, stop-at-first-good-result.
- (Obsoleto, a quitar) `tools/gemini/gemini-image.mjs` = llamada por API a Gemini; el usuario NO tiene plan de pago,
  la vía real es pegar a mano. Hay que quitarlo con su línea de `.gitignore` y su sección en `designs.md`.

## Falta (en este orden)
1. **Servidor** (`tools/taller-server/disenos.mjs`, `design-prompts.mjs`, `server.mjs`): `request({zone, generator:
   'prompt'|'comfy'})` (hoy `generator` es `gemini|comfy`: renombrar a `prompt`); la tarea de modo prompt NO necesita
   ComfyUI; `GET /api/disenos` devuelve también `zonas`; `POST /api/disenos/imagen {loteId,itemId,name,type,data}` guarda
   en `docs/disenos/img/<lote>__<item>.<ext>` y pone `item.image`; `_acceptEnemy` pide imagen (error claro si falta) y
   guarda `zone` en `diseno`.
2. **`tools/check-design-batch.mjs`**: un item enemy puede no tener `image` si trae `prompt` (con los bloques de
   `REQUIRED` de enemy-prompt.mjs); si hay `image`, que exista.
3. **Web `modules/designs-panel.js`**: el formulario (selector de zona + modo, hoy hay `GENERATORS` gemini|comfy),
   tarjeta de item enemy en modo prompt: texto del prompt, 📋 Copiar, "Abrir Gemini" (https://gemini.google.com/app),
   zona de pegado (Ctrl+V sobre la tarjeta + botón de archivo) y "Cambiar imagen" también en las de ComfyUI.
4. **Skill `design-proposals`** (sección enemy): modo prompt = el item lleva `prompt` armado con
   `enemy-prompt.mjs --target gemini`; modo comfy = `enemy-prompt.mjs --target comfy` + `claude-workflow.mjs`.
   Actualizar `STYLE`/`BACKDROP` de `claude-workflow.mjs` y `design-prompts.mjs` (enemigo aprobado → make-enemy: línea de
   zona).
5. Quitar lo obsoleto (arriba), actualizar `docs/architecture/designs.md`, `node --check`, probar el servidor con un lote
   de prueba (curl), commit.
6. Resumen en el chat con la lista de pruebas para el usuario (web: pedir prompt de cada zona, copiar, pegar imagen,
   aceptar; modo ComfyUI sigue igual).

## Decisiones
- Un solo generador de texto (`enemy-prompt.mjs`) para Gemini y ComfyUI: lo variable (criatura, diseño, piezas) lo
  escribe la tarea (Claude), el resto es fijo.
- Cristales por zona: hielo cian, volcán naranja-magma, bosque esmeralda-turquesa.
- El robot de hielo con ametralladoras (imagen de Gemini del usuario, `Assets/Art/Test` o su carpeta) está aprobado de
  estilo; aún no se ha cortado.
