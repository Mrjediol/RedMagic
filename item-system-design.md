# Sistema de Items y Builds — Documento de Diseño

## 1. Visión general

Cada run, el jugador equipa un **arma** con hasta **9 slots de items**:

- **3 slots dedicados** (uno de cada, siempre exactamente 1 item activo, "último equipado gana"):
  - `Elemento`
  - `Trayectoria`
  - `Forma`
- **6 slots libres** (pool general, sin restricción de tipo).

El objetivo del sistema es que el arma base + los 3 slots dedicados definan **cómo juega el arma** (comportamiento), y los 6 slots libres definan **la identidad de build** (sinergias elementales/universales) mediante un sistema de tags y umbrales, similar a Skull the Hero Slayer pero con doble-tag por item.

**Todo elemento equipable lleva exactamente 2 tags** (arma base y los 3 slots dedicados incluidos): arma, slot Elemento y pool libre llevan 1 elemental + 1 universal; Trayectoria y Forma llevan 2 universales. Así el comportamiento mecánico también alimenta las sinergias universales. Ver sección 4.1.

## 2. Pipeline de resolución del disparo

Cuando el arma dispara, el resultado se calcula en cascada, en este orden fijo:

```
1. TRAYECTORIA  → cómo viaja el proyectil/hitbox (recto, auto-aim/curva, rebote, etc.)
2. FORMA        → cuántas veces / dónde aplica el daño (split al impacto, multi-shot, auto-repetición, etc.)
3. ELEMENTO     → qué tipo de daño/status aplica — se hereda automáticamente por CUALQUIER
                   proyectil generado en el paso 2 (ej. los 5 rayos de un split son todos del
                   elemento actual del arma, sin necesidad de re-propagarlo a mano)
```

Regla clave: el elemento se resuelve **al final**, como una propiedad que "pinta" el resultado de los pasos anteriores, no como un paso que genera sus propios proyectiles.

Cada arma define un "shot base" (lo que dispara sin ningún modificador equipado); los modificadores de trayectoria/forma se aplican como funciones que transforman ese shot base o sus proyectiles resultantes.

## 3. Slots dedicados

### 3.1 Elemento
- Exactamente 1 activo a la vez (o "ninguno" = daño físico/neutro, si el arma no trae elemento propio).
- "Último equipado gana": si el jugador ya tenía Hielo y equipa Fuego, el arma pasa a ser Fuego.
- Algunas armas pueden nacer con un elemento por defecto (ej. arma que ya es de hielo de base); ese elemento cuenta igualmente para el conteo de tags de sinergia (ver sección 5).
- **Los items del slot Elemento llevan 2 tags: 1 elemental + 1 universal** (igual que el pool libre).

### 3.2 Trayectoria
- Define cómo se mueve el proyectil/hitbox tras ser generado.
- Solo 1 modificador de trayectoria activo a la vez (último gana).
- **Lleva 2 tags, ambos universales — nunca elementales.** Su comportamiento es mecánico, pero contribuye a sinergias universales como cualquier otro item.

### 3.3 Forma
- Define cantidad/patrón de aplicación de daño (split, multi-shot, auto-repetición en el aire, área, etc.).
- Solo 1 modificador de forma activo a la vez (último gana).
- **Lleva 2 tags, ambos universales — nunca elementales.** Igual que Trayectoria: mecánico en efecto, pero cuenta para sinergias universales.

## 4. Slots libres (pool general)

- 6 slots, sin restricción de tipo — pueden repetirse categorías, no hay exclusividad entre ellos.
- **Todo item de este pool lleva exactamente 2 tags: 1 elemental + 1 universal (random/definido en diseño).**
- Los items `Reset` no son una excepción: son items normales del pool con `Reset` como su tag universal (+ 1 elemental, como el resto). Su mecánica especial (sección 7) es de comportamiento, no de estructura de tags.

### 4.1 Regla global de tags (todos los slots)

**Cada elemento equipable lleva siempre exactamente 2 tags.** Qué tipos admite depende del slot:

| Fuente | Tag 1 | Tag 2 |
|---|---|---|
| Arma base | 1 elemental | 1 universal |
| Slot Elemento (item) | 1 elemental | 1 universal |
| Slot Trayectoria (item) | 1 universal | 1 universal |
| Slot Forma (item) | 1 universal | 1 universal |
| Pool libre (item) | 1 elemental | 1 universal |

Sin excepciones. Los items `Reset` son pool libre con `Reset` de tag universal.

## 5. Sistema de tags y sinergias

### 5.1 Categorías (v1 — set mínimo para testeo)

**Elementales** (exclusivas entre sí a efectos de identidad, pero no excluyentes de llevarlas — compites por llegar a los umbrales):
- Hielo
- Fuego
- (Arcano y Eléctrico quedan reservados para fases posteriores, no en el set mínimo)

**Universales** (transversales, no compiten con elementales, siempre útiles):
- Tanque
- Rapidez
- Vampirismo
- Reset (naturaleza especial, ver sección 7)

### 5.2 Conteo
- Cada elemento equipable cuenta **+1 punto completo** a CADA una de sus 2 tags (no se divide entre las dos).
- El arma base aporta +1 a su tag elemental y +1 a su tag universal.
- El slot de Elemento (item) aporta +1 a su tag elemental y +1 a su tag universal.
- Los slots de Trayectoria y Forma aportan +1 a cada una de sus 2 tags universales (nunca aportan a tags elementales).
- Los 6 items del pool libre aportan +1 a su tag elemental y +1 a su tag universal.

Implicación: las tags **universales** reciben aportes de más fuentes que las elementales (arma + elemento + trayectoria×2 + forma×2 + hasta 6 del pool), así que su escala de umbrales puede necesitar ajuste aparte (ver 5.3).

### 5.3 Umbrales
- Base estándar para elementales en v1: **2 / 4 / 6**.
- Los universales pueden tener su propia escala (no necesariamente 2/4/6) — a definir por diseño más adelante; en v1 usamos también 2/4/6 por simplicidad, excepto Reset (ver sección 7).
- Cada umbral alcanzado desbloquea un efecto **cualitativamente distinto**, no solo un multiplicador mayor (ver sección 6). El sistema debe soportar 3 efectos independientes por tag (uno por umbral), no un solo efecto que escala en número.

### 5.4 Overflow de cap
- Fuentes máximas por tag **elemental**: arma base (1) + slot Elemento (1) + pool libre (6) = 8.
- Fuentes máximas por tag **universal**: arma base (1) + slot Elemento (1) + Trayectoria (2) + Forma (2) + pool libre (6) = 12.
- El cap de sinergia es 6: si el jugador ya tiene 6+ puntos en una tag, puede seguir llevando items de esa tag sin penalización, pero no obtiene nada extra por superar 6 (queda "completa").
- Esto permite intencionalmente que el jugador se desvíe 1 vez del set "puro" (ej. coger un item muy bueno de otra tag) sin perder el bonus de umbral 6, siempre que mantenga al menos 6 puntos en la tag principal.

### 5.5 Overlap entre elemento del arma y pool libre
- El elemento activo en el slot dedicado sí cuenta para el conteo (ver 5.2), reforzando que elegir el elemento del arma ya es un primer paso hacia esa identidad de build antes de tocar el pool libre.

## 6. Efectos de umbral — v1 (placeholders para testeo, no valores finales)

| Tag | Umbral 2 | Umbral 4 | Umbral 6 |
|---|---|---|---|
| Hielo | -10% velocidad a enemigos golpeados | +daño a enemigos ralentizados | Al recibir golpe letal: 1 vida extra — invulnerabilidad 1.5s en tumba de hielo, daña alrededor, luego rompe |
| Fuego | Aplica quemadura (daño/tiempo) | +duración de quemadura | La quemadura se propaga a enemigos cercanos al morir el objetivo |
| Tanque | +20% vida máxima | +regeneración de vida pasiva | Shield temporal al bajar del 30% de vida (una vez por encuentro/cooldown) |
| Rapidez | -10% cooldown de arma | -20% cooldown de arma | -35% cooldown de arma (umbral final = mayor magnitud, no mecánica nueva, en v1) |
| Vampirismo | 5% lifesteal | 10% lifesteal | El exceso de curación por lifesteal se convierte en shield temporal |

*(Estos valores son deliberadamente simples para v1 — se espera iterar tras testeo.)*

## 7. Items con tag Reset — mecánica especial

- Estructura de tags: un item Reset es un item de pool normal (1 elemental + 1 universal), donde su tag universal es `Reset`. No hay slot ni excepción especial.
- Los items Reset **no dependen de los umbrales globales de la misma forma que el resto**.
- Cada item Reset define su propio contador interno y un N base (ej. "cada 3 kills: cura pequeña").
- Cada item Reset escucha el evento global `OnEnemyKilled` de forma independiente y lleva su propio contador — no comparten contador entre sí.
- El "umbral de sinergia Reset" no añade un efecto nuevo: **reduce el N necesario de TODOS los items Reset equipados simultáneamente**:
  - 2 items Reset equipados → cada uno dispara con N-1 (respecto a su N base individual).
  - 3 items Reset equipados → cada uno dispara con N-2.
- Si varios items Reset alcanzan su contador en el mismo kill, todos disparan ese mismo frame — no hay coordinación adicional necesaria.

### Items Reset v1 (mínimo para testeo):
- Reset A: cada 3 kills → cura una pequeña cantidad de vida.
- Reset B: cada 4 kills → el próximo enemigo muerto suelta un proyectil que persigue al enemigo más cercano y explota.

## 8. Lista mínima de contenido — v1 (para testear el sistema, no contenido final)

### Slots dedicados
- **Elemento**: Hielo, Fuego *(+ "ninguno"/físico como default si el arma no trae elemento propio)* — cada item además con 1 tag universal
- **Trayectoria**: Auto-aim (el proyectil curva hacia el enemigo más cercano) — 2 tags universales
- **Forma**: Split en 5 al impacto — 2 tags universales

### Arma base
- Al menos 1 arma de prueba con 1 tag elemental + 1 tag universal asignados.

### Pool libre (6 items mínimo)
1. Hielo + Tanque
2. Hielo + Rapidez
3. Fuego + Vampirismo
4. Fuego + Tanque
5. Reset A (cada 3 kills: cura) — Hielo + Reset
6. Reset B (cada 4 kills: proyectil perseguidor) — Fuego + Reset

## 9. UI

Layout de 3 columnas (referencia: pantalla de items de Skull the Hero Slayer):

- **Columna izquierda**: las sinergias existentes (v1: Hielo, Fuego, Tanque, Rapidez, Vampirismo, Reset), cada una mostrando el conteo actual y el próximo umbral (ej. "2 ▸ 4"). Al llegar al cap (6), se marca visualmente como completa (ej. borde dorado).
- **Columna central**: arriba los 3 slots dedicados (Elemento, Trayectoria, Forma), debajo los 6 slots libres, y en la parte inferior el arma equipada.
- **Columna derecha**: panel de descripción — al pasar el ratón sobre un item o modificador, muestra su efecto y qué tags aporta; al pasar el ratón sobre el arma, muestra sus stats base junto con el comportamiento actual resultante de los 3 slots dedicados equipados (descripción "en vivo", no solo stats base estáticos).
- **Interacción adicional**: al hacer hover sobre un item del pool libre (ej. en tienda o pantalla de recompensa), resaltar en la columna izquierda las 2 sinergias a las que contribuye ese item, para que el jugador vea de un vistazo el impacto antes de decidir.

## 10. Notas de arquitectura (para implementación)

- Los items y modificadores deben representarse como datos (ScriptableObjects en Unity), no como lógica hardcodeada por combinación — el pipeline de resolución (sección 2) debe funcionar por composición genérica, no por casos especiales por arma.
- El synergy tracker debe ser una fuente única de verdad consultable (¿cuántos puntos tiene la tag X ahora mismo?) tanto por la UI como por los propios items/efectos de umbral.
- Los items Reset deben implementarse como componentes/listeners independientes suscritos a un evento global de kill, cada uno con su propio contador interno, consultando el synergy tracker solo para saber cuánto restar a su N base.
