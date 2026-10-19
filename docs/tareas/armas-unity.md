# Port a Unity de las 6 armas de la web (tarea `hp-4`)

Pedido del usuario (2026-10-19): llevar a Unity las seis armas de `docs/bocetos/habilidades/jugable.html` (Abanico, Singularidad,
Rayo, Media luna, Semilla, Golpe estelar) **con todos sus comportamientos activables como en la web** (evolución + 3 mejoras,
elemento, Duplicador) y **mejor aspecto que el prototipo** (más refinado, más detalle). Rama `task/armas-unity`.
Fuente de verdad de cada arma: su ficha en `docs/armas/` + su `g-*.js`.

## Arquitectura (decidida al empezar)

- **Un arma = un `WeaponDefinition`** con `Base Shot ▸ Delivery = Arsenal` y su **kit** en `WeaponDefinition ▸ Arsenal`
  (`[SerializeReference]`, un tipo por arma: `FanKit`, `SingularityKit`…). El kit lleva los ajustes de la web con los mismos
  nombres (px × 0,025 = unidades) y, en el Inspector, la **evolución** (desplegable) y las **mejoras** (casillas, agrupadas por
  evolución). Nada de ScriptableObjects intermedios.
- **Mundo de entidades** (`Arsenal.ArsenalWorld`, singleton que se crea solo): como `G.ents` en la web. Proyectiles, zonas y
  efectos son objetos C# con `Update`/`Draw`; todos se dibujan en **dos lienzos `FxCanvas` compartidos** (detrás orden 2 / delante
  40). Sin GameObjects por disparo → sin Instantiate/Destroy y dos mallas por frame (bien para móvil).
- **Reglas comunes** (genéricas, nunca dentro de un arma): golpe del arsenal (`ArsenalCombat.Hit` → `PlayerHit.Deal`), estados de
  elemento (`ElementStatus`: congelación acumulada → congelado, quemado) y reacciones (añicos, deshielo, vapor), atraer enemigos
  (vía `Knockback.ApplyVelocity`, que la IA ya respeta), captura de proyectiles (Singularidad), marcas (Golpe estelar · Cazador).
- **Dos armas + cambiar** (decisión de la web): `WeaponInventory` gana un segundo hueco; acción `SwapWeapon` (Tab / mando ▲).
  `WeaponUser` delega en un **controlador por arma** (pulsar / mantener / soltar / tick); la recarga del hueco inactivo sigue
  corriendo. Cambiar con el botón pulsado suelta la actual.
- **Elemento y Duplicador**: «Auto» = la sinergia de la run (Hielo 4 / Fuego 4); el menú DEV puede forzarlos.
- **Menú DEV** (F10, sólo editor / dev build, pausa): arma de cada hueco, evolución, mejoras, elemento, Duplicador, reacciones.
  Edita una copia en memoria, nunca el asset.
- **Escena de prueba**: Tools ▸ RedMagic ▸ Test ▸ Escena de armas → `Assets/Test/Scenes/ArsenalTest.unity`, con muñecos como los de
  la web (suelo, andarín, volador) que mueren y reaparecen (Fauces, Vigía, Cosecha necesitan bajas).
- Contenido nuevo en `Assets/Test/Arsenal/` hasta que el usuario lo dé por bueno.

## Fases (una por arma, commit por fase)

| # | Fase | Estado |
|---|---|---|
| 0 | Cimientos: delivery + kits, mundo de entidades, reglas comunes, 2.º hueco + cambio, menú DEV, escena de prueba | en curso |
| 1 | Abanico (Erizo / Lanza / Caleidoscopio) | pendiente |
| 2 | Singularidad · Grieta (Fauces / Errante / Arsenal) + captura | pendiente |
| 3 | Rayo (Sobrecarga / Prisma / Faro) | pendiente |
| 4 | Media luna (Sierra / Satélite / Malabar) | pendiente |
| 5 | Semilla (Campo de minas / Racimo / Madura) | pendiente |
| 6 | Golpe estelar (Bombardeo / Cazador / Tormenta) + marcas | pendiente |
| 7 | Sonidos: piezas `SND.*` a WAV con el mismo código (como `render-sonidos-cometa.cjs`) y que el usuario los califique | pendiente |
| 8 | Items de la web (Duplicador, Gatillo, Constelación…) frente a los del juego: decidir con el usuario | pendiente |

## Cómo se verifica

Compilar con el editor abierto (`unity command recompile`). Aspecto: render fuera de pantalla de 2-3 instantes por efecto
(`docs/fx-proceso-bocetos.md` ▸ 4). El juego lo prueba el usuario con la escena de prueba y el menú DEV.

## Historial

- 2026-10-19 · Empieza la tarea. Antes, en la web: Malabar exige timing (pulsar antes de tiempo = fallo).
