# Modo Supervivencia (idea del usuario, 2026-10-19)

Nuevo modo de juego tipo *survivor*: **un solo mapa**, oleadas de enemigos que aparecen progresivamente y se hacen más fuertes.
Mismos enemigos y mismas armas/items que el modo normal. Una run de roguelite concentrada en una sala: sólo combate y progresión.

## Para qué sirve (palabras del usuario)
- Modo divertido por sí mismo.
- **Campo de pruebas de combate y de balance**: cada oleada (o grupo de oleadas) representa un nivel de poder de una run normal.
  Sirve para decidir cada cuánto y cuánto subir la vida de los enemigos, cada cuánto dar al player más objetos, etc.

## Ideas para el diseño (sin decidir; preguntar al usuario)
- Oleadas definidas por datos (Inspector): enemigos, cantidad, multiplicador de vida/daño, ritmo de aparición.
- Recompensas entre oleadas que imiten la run: subidas de arma (evolución/mejoras del arsenal), cofres de items, oro/tienda.
- Registro de métricas por oleada (tiempo, daño recibido, DPS, objetos tenidos) para comparar builds y ajustar la curva.
- Reutilizar la escena de prueba de armas (`Assets/Test/Scenes/ArsenalTest.unity`) como primer mapa.

## Estado
Sólo la idea. Relacionado: `docs/tareas/armas-unity.md` (las armas que se probarán aquí), `docs/armas/progresion.md` (curva deseada).
