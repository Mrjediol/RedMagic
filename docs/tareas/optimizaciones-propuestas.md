# Optimizaciones: proponer, probar, aprobar

**Regla del usuario (2026-10-13):** tener presentes las optimizaciones posibles y **proponerlas**. Las que puedan empeorar el aspecto (baking)
se **prueban primero** en una variante aislada (todo en una carpeta de prueba con menú para borrarla sin restos) para que el usuario vea si nota
diferencia; sólo con su visto bueno se adoptan.

Resultados de la sesión (la prueba se borró):
- **Energía de la puerta horneada (flipbook)**: se nota algo peor (sobre todo la puerta ancha). No adoptada.
- **Proyectiles de enemigo en una hoja** (IceCannonOrb, CinderKettleBot): ya eran animaciones de sprites sueltos; con 100-400 a la vez sin diferencia
  en FPS, batches ni SetPass en el PC. No adoptada. Si algún día hace falta: un **Sprite Atlas** (sin tocar prefabs).

Candidatas a proponer cuando toque (no probadas):
- **Sprite Atlas** de los sprites de proyectiles/FX (cero riesgo visual; medir en móvil).
- Quitar `com.unity.ai.assistant` / `com.unity.ai.inference` si no se usan: meten shaders Sentis en la build y warnings (decide el usuario: es su asistente).
- Dash y melee: el tajo ya no reconstruye capas por golpe y la estela no reserva memoria por frame; falta medir en móvil.
- Medir siempre en el móvil (el editor de PC no es representativo).
