# Progresión de armas: mejoras permanentes + evolución y núcleos en la run (tarea `armas-meta-upgrades`)

Rama `task/supervivencia`. Diseño y números: `docs/armas/progresion.md ▸ En Unity`. Arquitectura:
`docs/architecture/weapons-items.md ▸ Arsenal` y `docs/architecture/survival.md ▸ Weapons in survival`.

## Hecho (2026-10-20)
- **Mejoras permanentes por arma** (`WeaponMastery`, `Resources/WeaponMasteryConfig.asset`): pestaña por arma en el libro de
  mejoras del hub (y en Supervivencia: preparación y pantalla final). Poder (Daño → Recarga), Firma (Abanico 3 → 5 esquirlas…),
  desbloquear la mejora 2 y 3 de cada evolución (la 1 viene de serie).
- **En la run** (`WeaponRunProgress`, `Resources/WeaponProgressionConfig.asset`): las armas empiezan en base. Prisma = evolución
  segura (clásico: cada jefe; Supervivencia: rondas 5 y 10). Núcleo = lo suelta un **portador** (enemigo raro que se escapa a
  los 20 s) y hoy **sube un nivel el arma al momento** (evolución si no la tiene, si no una mejora). Con las dos armas al tope
  el portador suelta 30 fragmentos de alma.
- Portador: aspecto blanco radiante + halo + columna de luz + icono de lo que suelta + flecha en el borde de la pantalla.
- **`testEase` ×8** en `WeaponProgressionConfig` (portadores 8 veces más probables para probar).

## Feedback del usuario (2026-10-20, fin de sesión) — PENDIENTE, en este orden
1. **El núcleo NO debe evolucionar el arma.** Hoy, si cae un núcleo pronto, evoluciona el arma demasiado temprano. Debe
   **guardarse como moneda** (contador de núcleos) hasta que el arma esté evolucionada, y **gastarse desde el menú de inventario
   (tecla I)** para mejorar el arma cuando el jugador quiera. La evolución sólo llega por Prisma.
   - Implica: quitar `ShowEvolutions(…, fromCore: true)` / `LevelUp` de `ArsenalProgressUi`; el núcleo ya no abre la pantalla
     solo; un apartado en el menú de items (`ItemMenuController`, I) con los núcleos guardados y, por arma evolucionada, sus
     mejoras disponibles para comprar con un núcleo. `NothingLeft` debe seguir contando núcleos guardados.
   - Revisar entonces la regla "al tope → almas" (con núcleos guardados que aún no se pueden gastar, ¿cuenta como tope?).
2. **Los efectos del portador son exagerados** (halo, columna de luz, pulso): bajarlos hasta que se distinga sin pasarse.
   Números en `CoreCarrier.LateUpdate` (escala del halo 2,2 + 0,35·pulso, alfa 0,55-0,9; columna 16 u, alfa 0,3-0,4) y el color
   en `WeaponProgressionConfig.carrierGlow`. Quizá pasarlos al config para afinarlos sin código.
3. **UI fea** (pestañas y árbol de armas en el menú de mejoras, pantalla de elegir evolución/mejora, flecha del borde):
   funciona; pulido visual más adelante.
4. **Sin probar todavía**: portador con las armas al tope → suelta almas y su icono es el del alma.
5. Al acabar las pruebas: `testEase` a 1.

## Cómo se prueba
Supervivencia (menú → Jugar → Supervivencia), `SurvivalConfig ▸ Evolution After Waves` = [1, 2] para evolucionar pronto.
Almas para el menú: `CurrencyManager` en el Inspector (en Play).
