# Progresión de armas y builds en la run (propuesta en prueba, 2026-10-19)

Pedido del usuario (2026-10-19): las mejoras de un arma no pueden ser items generales (un item «Lluvia» no hace nada en el
Abanico). Quiere **tres vías** de mejora, un árbol de arma **profundo** (varias formas avanzadas de la misma arma, no «3 flechas
→ 10 flechas que rebotan») y la curva de un roguelite: **difícil de base**, fácil con una run con algo de suerte, y rota con un
combo muy bueno. Se experimenta primero con **Abanico** y **Golpe estelar**.

## Las tres vías

| Vía | Qué es | Dónde se consigue (propuesta) | Regla de diseño |
|---|---|---|---|
| **1 · Árbol del arma** | Evolución (forma avanzada) + mejoras de esa evolución | Forja con Skulls (`WeaponLevelManager`, niveles) | Profundidad de UNA arma; cada evolución cambia cómo se juega |
| **2 · Sinergias generales** | Reglas del mundo: reacciones elementales, captura, marcas | No se compran: salen al combinar | Funcionan con cualquier pareja de armas |
| **3 · Items y sinergias** | Items con 2 etiquetas (elemental + universal) que suman tramos 2/4/6; efecto propio opcional | Tienda (oro) / cofres | **Sólo usan eventos que tienen todas las armas** (golpear, golpe fuerte, matar, cambiar de arma) o un parámetro que todas definen (nº de proyectiles, elemento) → nunca un item muerto |

## 1 · Árbol del arma

- **Nivel 1**: el arma base.
- **Nivel 2 — Evolución**: 1 de 3 formas avanzadas. En la run se ofrecen **2 al azar** (no siempre puedes ir a tu favorita).
- **Niveles 3, 4, 5 — Mejoras**: cada vez se ofrecen **2 al azar** de las 3 mejoras de tu evolución que aún no tienes (la
  última subida puede no darte a elegir). Máximo = evolución + sus 3 mejoras.
- Las armas aún no rediseñadas (Media luna, Semilla) tienen de momento una lista plana de mejoras provisionales. Árboles de Singularidad y
  Rayo: en sus fichas.

### Abanico

| Evolución | Idea | Mejoras |
|---|---|---|
| **Erizo** | Las esquirlas se clavan (enemigos y suelo) y **estallan al cambiar de arma** (o solas al rato): premia alternar armas | Retorno (vuelven atravesando y recargan el arma de reserva) · Metralla (cada estallido suelta esquirlas pequeñas) · Hemorragia (cada clavada hace daño continuo) |
| **Lanza** | Ráfaga en **línea**, rápida y lejana, que perfora: el abanico de lejos | Perforación (atraviesa todo, +daño por enemigo atravesado) · Aceleración (manteniendo, cada ráfaga sale antes) · Cristal mayor (la última de cada ráfaga es una lanza grande, golpe fuerte) |
| **Caleidoscopio** | Rebotes (suelo, plataformas, enemigos) que **ganan daño** con cada rebote | Buscador (todo rebote busca al enemigo más cercano) · Prisma (al rebotar en un enemigo se divide en dos) · Eterno (+3 rebotes y no caducan mientras rebotan) |

### Golpe estelar

| Evolución | Idea | Mejoras |
|---|---|---|
| **Bombardeo** | Lanzas más pequeñas en **fila** desde el punto marcado hacia delante | Salva (+2 lanzas) · Final (la última es enorme) · Rodante (la fila sigue al ratón mientras cae) |
| **Cazador** | Tus otras armas **marcan**; las lanzas caen solas sobre los marcados | Acumulación (marcas apilables hasta 3: más daño por marca) · Ejecución (remata a los que quedan con poca vida y marca al siguiente) · Vigilancia (cada 4 s cae una lanza sola sobre un marcado, aunque lleves la otra arma) |
| **Tormenta** | Lluvia de agujas sobre una zona | Diluvio (más agujas, dura el doble y sigue al ratón) · Agujas pesadas (golpes fuertes: rompen congelados) · Ojo de la tormenta (atrae hacia el centro y acaba con una lanza) |

### Rayo

| Evolución | Idea | Mejoras |
|---|---|---|
| **Sobrecarga** | El daño crece con el calor; al soltar **descarga** por toda la línea; si llegas al máximo se recalienta | Punto justo (soltar en la franja final = doble y golpe fuerte) · Perforante (con medio calor atraviesa a todos) · Onda (la descarga estalla en área) |
| **Prisma** | Se divide en haces dorados tras el primero: contra grupos | Cadena (cada haz salta a otro) · Foco (los haces sin objetivo refuerzan el principal) · Reflejo (rebota en el suelo) |
| **Faro** | Al soltar, el cristal se queda disparando solo: premia cambiar de arma | Vínculo (hilo que daña y cura) · Estallido (al apagarse descarga en área) · Vigía (cada baja alarga el faro) |

## 3 · Items y sinergias (como el juego: 2 etiquetas por item, tramos 2 / 4 / 6)

Corrección del usuario (2026-10-19): el elemento ya está en el juego como **sinergia**. Cada item lleva **una etiqueta elemental**
(Hielo, Fuego, Oro) y **una universal** (Tanque, Prisa, Robo de vida, Reset) — `BuildTag` + `SynergyConfig` en Unity — y cada
etiqueta activa tramos a 2 / 4 / 6 puntos (6 huecos de items). Propuesta del usuario que se prueba en la web:

- **El elemento se APLICA a los enemigos con 4** de ese elemento (Hielo 4 = los golpes congelan; Fuego 4 = queman). Con 6 huecos
  sólo un elemento puede llegar a 4 → **nunca dos estados elementales a la vez** en un enemigo (no hay que resolver mezclas).
- **El tramo 2 es un bonus compatible**: 2 Hielo = −15 % de recarga, 2 Fuego = +15 % de daño. Se puede ir 4 hielo + 2 fuego o al revés.
- Cada arma decide cómo expresa el elemento (tabla de abajo) y el Duplicador.
- Un item puede además tener **efecto propio** (los 6 de hielo y los de oro copian los del juego; los «generales» de las pruebas —
  Duplicador, Gatillo, Constelación… — reciben etiquetas Fuego + universal).

Tablas completas (generadas del código): [README ▸ Items generales](README.md). Resumen de los tramos propuestos:

| Etiqueta | 2 | 4 | 6 |
|---|---|---|---|
| Hielo | −15 % recarga | aplica hielo | congelado +50 % y añicos ×2 |
| Fuego | +15 % daño | aplica fuego | la quemadura salta al morir |
| Oro | monedas (sin efecto aquí) | 5 % golpes dorados ×2 | botín (sin efecto aquí) |
| Tanque | +20 % vida | regeneración | escudo una vez al 30 % |
| Prisa | recarga ×1,5 con hielo en pantalla | −20 % recarga | −35 % recarga |
| Robo de vida | baja cura 3 | 10 % robo | sobrante → escudo |
| Reset | baja: −2 s arma activa | y −2 s la de reserva | baja recarga del todo la activa |

En Unity hoy el tramo 1 de Hielo es «ralentiza un 40 %» y el de Fuego «aplica quemadura»: si se adopta esto, pasan al tramo 2.

### Cómo expresa cada arma el elemento (con 4 de ese elemento)

| Arma | Hielo | Fuego |
|---|---|---|
| Abanico | congela por esquirla; congelado + golpe fuerte = añicos | quema por esquirla |
| Singularidad | el colapso congela a todos los atrapados (bloque) | el colapso quema y abre llamas |
| Rayo | congela por tick; al soltar, añicos | quema por tick |
| Media luna | la ida congela, **la vuelta rompe** (golpe fuerte) | estela de llamas colgada |
| Semilla | nova de escarcha (picos, congela todo) | charco de fuego donde estalla |
| Golpe estelar | el impacto congela la columna y alrededor | cráter ardiendo |

## Fuera de la run: mejoras permanentes de cada arma (idea del usuario, 2026-10-19 — para Unity, no para la web)

Aprovechar el árbol de mejoras del hub que aún no se usa (`UpgradeTree` / `UpgradeManager`, fragmentos de alma, rejilla de
carriles estilo Skul: `docs/architecture/economy.md`) con **un apartado por arma**:

- **Estadísticas base del arma**: el arma empieza más corta y se sube fuera de la run. Ej.: el Abanico de base con **3 esquirlas**,
  ampliable hasta 5; daño, alcance, recarga.
- **Desbloqueo de mejoras del árbol**: de base cada evolución sólo trae 1 de sus 3 mejoras (p. ej. Erizo → sólo Retorno);
  Metralla y Hemorragia se desbloquean en el hub. Lo mismo podría valer para evoluciones enteras.
- **En la run**: se juega siempre con el arma base hasta que algo desbloquea la evolución (p. ej. matar al primer jefe, u otra
  condición por decidir). Las mejoras no están garantizadas: una run puede no dar ninguna y otra con suerte las 3 → carrilea la run.

Encaja con la curva pedida (difícil de base, la suerte y la meta-progresión la suavizan). Para cuando se porte a Unity: la web
sigue siendo el sitio donde se terminan y afinan las armas; esta capa se diseña al montarlas en el juego.

## Curva de dificultad (para probar)

Panel «Dificultad» de la jugable: vida de los enemigos ×1-×4 y que disparen. Objetivo a comprobar jugando: arma base sola =
cuesta; evolución + 1 mejora + 2 comunes = cómodo; evolución completa + Duplicador + elemento con reacciones = arrasa.

## Abierto

- ¿Skulls para el árbol y oro para items (lo que ya hay), o una sola moneda?
- ¿Cuántas evoluciones por arma en el juego final (3 parece el mínimo para que haya variedad entre runs)?
