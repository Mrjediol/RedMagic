# Plataformas y puertas: lo que queda por pulir

Pendiente desde 2026-10-13 (ver también `objetos-editor-web.md`).

- **Más tipos de puerta**: barrotes de hierro que bajan del techo (arte Gemini de la sesión: barrotes, losa rúnica, energía; sólo se usó la de energía).
  Mismo componente `LevelDoor` con otra variante de arte; la animación es el movimiento por código, no frames.
- **Plataformas que se caen / que se mueven**: sobre `LevelPlatform` (plataforma-objeto con sprite, ya en `Assets/Scripts/Test/`); las pintables por
  tile son estáticas. Las cinco imágenes de Gemini están en `Hielo/Objetos/Plataformas` (las gruesas descartadas por ahora).
- **Plataformas gruesas como tiles** (si se quieren): tiles de varias celdas de alto.
- **Colisión**: ajustar `colliderThickness` (0,12) en los 3 `ArtTile_*` tras probar bajadas rápidas (dash/picado).
- **Fondo/nieve vs objetos**: decisión del usuario pendiente (ver `objetos-editor-web.md`).
- Puertas: probar el aspecto del hueco real (techo/suelo con nieve) y decidir si hace falta arte de "umbral".
- Mover lo de `Assets/Scripts/Test` y `Assets/Test` a su sitio cuando se validen (regla del proyecto: lo nuevo va a Test primero).
