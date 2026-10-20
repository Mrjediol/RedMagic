# Golem de escoria — piezas para animar (prueba de arte, 2026-10-20)

Piezas acabadas del [Golem de escoria](../../golem-escoria.md), listas para montarlo y animarlo en 🧬 **Enemy Studio** (web).
Todas comparten el mismo lienzo de 2048×2048 (fondo transparente): cada una está en su sitio, así que se colocan solas.

| Archivo | Hueco de la plantilla `groundHumanoid` | Qué es | Articulación (px del lienzo 2048) |
|---|---|---|---|
| `Head.png` | Head | cabeza flotante | base de la cabeza |
| `Torso.png` | Torso | pecho, núcleo de cristal, cintura **y la hombrera con cristal** (no se mueve con el brazo) | cintura |
| `WeaponHand.png` | WeaponHand | brazo de delante (el puño a la derecha), con rótula en el hombro | rótula ≈ (1252, 690) |
| `BackHand.png` | BackHand | brazo de atrás (izquierda), sale de una rótula escondida bajo la hombrera | rótula ≈ (705, 705) |
| `FrontFoot.png` | FrontFoot | pierna de la **izquierda** de la imagen (la de delante) | rótula de la cadera ≈ (852, 1120) |
| `BackFoot.png` | BackFoot | pierna de la **derecha** de la imagen (la del fondo) | cadera ≈ (1270, 1160) |

**Orden de dibujo** (de atrás adelante, decidido con el usuario): BackHand → BackFoot → FrontFoot → WeaponHand → Torso → Head.
Los dos brazos y las dos piernas van **detrás del torso** (la cintura tapa los muslos). Ojo: la plantilla pone por defecto
WeaponHand y FrontFoot delante del torso; hay que cambiar ese orden en el Studio.

Otros archivos: `_montado.png` (todas juntas), `_base_gemini.jpg` (la base de Gemini de la que salen).

## Cómo se hicieron (receta en `.claude/skills/make-enemy/`)
1. Base en Gemini (prompt con bloque `VIEW` de perfil a la derecha; la primera salió de frente).
2. Recorte por pieza en ComfyUI (`claude-workflow.mjs run --keep-size --ref base_slaggolem.jpg`), una edición por pieza.
   El brazo de delante y el torso se confundían (el modelo pegaba el brazo al pecho): se arreglaron **editando el
   resultado** («borra sólo la hombrera», «borra sólo el brazo»), no recortando otra vez de la base.
3. Rótulas redondas en los extremos que giran (el primer brazo acababa en un gancho que asomaría al girar).
4. Acabado: `claude-workflow.mjs finish` (×2 → 2048, fondo recortado) + `finish-parts.mjs` (una isla, sin halo blanco).

## Lo que queda flojo (para la próxima)
- **Resolución:** la base de Gemini es un JPEG de 1024 px; subirla a 2048 no añade detalle (el usuario lo nota
  pixelado). Para el juego bastan ~100-128 px por unidad (el golem mide ~2,3 u → ~300 px de alto), así que la
  textura final irá reducida; pero hay que conseguir una base más limpia (PNG, más resolución, o regenerarla en ComfyUI).
- La rótula del brazo de delante es grande y asoma un poco por la derecha del pecho.
- Versiones extra de las piezas (núcleo al rojo / apagado, puño al rojo, cabeza alerta / dolorida): sin hacer.
- Las poses de prueba (`Web/RedMagicWeb/tools/comfy/enemies/SlagGolem.poses.json` + `pose-preview.mjs`) no convencieron
  al usuario: la animación la hace él en Enemy Studio.
