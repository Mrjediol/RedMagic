# Prompts de enemigos por zona + modo "Prompt para Gemini" en 🎨 Diseños

Estado: **terminada, pendiente de que el usuario la pruebe** (2026-10-05). Reiniciar el taller-server (start.cmd) para que cargue el código nuevo.

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

## Hecho también (pasos 1-5)
- Servidor: request({zone, generator prompt|comfy}), GET /api/disenos devuelve zonas, POST /api/disenos/imagen, aceptar exige imagen y guarda zone; buildPrompt (▶ Montar) copia una imagen no-Comfy a Comfy input.
- check-design-batch acepta items con prompt (bloques REQUIRED de enemy-prompt.mjs).
- Web: selector Zona + Cómo (Prompt / ComfyUI), tarjeta con prompt (📋 Copiar, ↗ Abrir Gemini), 🖼 / Ctrl+V para la imagen.
- Skill design-proposals (modos prompt / comfy con enemy-prompt.mjs), STYLE de claude-workflow.mjs = estilo del juego, make-enemy §0/§1.
- Quitado tools/gemini (API) y su línea de .gitignore; designs.md al día.
- Probado con stubs: request→lote con zone, aceptar sin imagen da error claro, attachImage, aceptar con imagen guarda zone. NO probado en el navegador.

## Falta
- Que el usuario pruebe la web (lista en el chat) y diga qué ajustar.
- Aún sin hacer: cortar el robot de hielo con metralletas (imagen de Gemini del usuario) siguiendo la skill (1 seed por pieza).

## Decisiones
- Un solo generador de texto (`enemy-prompt.mjs`) para Gemini y ComfyUI: lo variable (criatura, diseño, piezas) lo
  escribe la tarea (Claude), el resto es fijo.
- Cristales por zona: hielo cian, volcán naranja-magma, bosque esmeralda-turquesa.
- El robot de hielo con ametralladoras (imagen de Gemini del usuario, `Assets/Art/Test` o su carpeta) está aprobado de
  estilo; aún no se ha cortado.
