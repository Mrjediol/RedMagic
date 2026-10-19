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
| 0 | Cimientos: delivery + kits, mundo de entidades, reglas comunes, 2.º hueco + cambio, menú DEV, escena de prueba | hecho |
| 1 | Abanico (Erizo / Lanza / Caleidoscopio) | hecho |
| 2 | Singularidad · Grieta (Fauces / Errante / Arsenal) + captura | hecho |
| 3 | Rayo (Sobrecarga / Prisma / Faro) | hecho |
| 4 | Media luna (Sierra / Satélite / Malabar) | hecho |
| 5 | Semilla (Campo de minas / Racimo / Madura) | hecho |
| 6 | Golpe estelar (Bombardeo / Cazador / Tormenta) + marcas | hecho |
| 7 | Sonidos: piezas `SND.*` a WAV con el mismo código y que el usuario los califique | a medias (ver RETOMAR) |
| 8 | Items de la web (Duplicador, Gatillo, Constelación…) frente a los del juego: decidir con el usuario | pendiente |

## ▶ RETOMAR AQUÍ (fin de sesión 2026-10-19)

**Estado**: las 6 armas portadas, compilan y se ven bien en renders fuera de pantalla (`ArsenalPreviewShots.Fan/Sing/Ray/Moon/Seed/Star`,
`unity command eval --code "return RedMagic.Arsenal.Editor.ArsenalPreviewShots.Ray(@\"<carpeta>\");"`). **Nada probado en Play**:
lo prueba el usuario. Rama `task/armas-unity` (sin fusionar).

**Cómo probar**: Tools ▸ RedMagic ▸ Test ▸ Escena de armas (arsenal) → abrir `Assets/Test/Scenes/ArsenalTest.unity` → Play.
Q dispara (mantener = repetir / canalizar / madurar), Tab cambia de arma, F10 (o botón «ARMAS») = menú DEV: arma de cada hueco,
evolución, mejoras, elemento forzado (hielo/fuego), Duplicador, reacciones.

**Siguiente**:
1. Recoger la prueba del usuario y arreglar (un arma cada vez).
2. Sonidos: el script está hecho (`docs/bocetos/habilidades/tools/render-sonidos-armas.cjs`, 54 ids → WAV en
   `Assets/Audio/Sounds/Player/Arsenal/`), pero falta `node-web-audio-api`: la instalación del cometa
   (`%TEMP%/cometaudio`) está incompleta. Con permiso del usuario: `npm i node-web-audio-api@1.0.4` en una carpeta temporal,
   `WEB_AUDIO_API=<ruta>/node_modules/node-web-audio-api node … Assets/Audio/Sounds/Player/Arsenal`, luego Tools ▸ RedMagic ▸
   Arsenal ▸ Preparar (mete cada WAV en ArsenalAssets ▸ Sonidos por su nombre) y que el usuario califique cada sonido
   (regla del registro de sonido).
3. Fase 8 (items) y pasar las armas de `Assets/Test/Arsenal/` a `Resources/Items/Weapons/` cuando el usuario las dé por buenas.

**Trampas aprendidas**: `Bump(1)` = sin(π) negativo → `Pow` daba NaN y Unity descartaba el lienzo entero (arreglado;
`FxCanvas` avisa de NaN). El mundo devolvía al pool entidades antes de quitarlas de la lista (arreglado). Ediciones a .cs con
CRLF: usar un script que normalice saltos de línea. El banco de renders aísla todo en la capa 31.

## Cómo se verifica

Compilar con el editor abierto (`unity command recompile`). Aspecto: render fuera de pantalla de 2-3 instantes por efecto
(`docs/fx-proceso-bocetos.md` ▸ 4). El juego lo prueba el usuario con la escena de prueba y el menú DEV.

## Historial

- 2026-10-19 · Fases 0-6 hechas en una sesión; sonido a medias (falta instalar node-web-audio-api).
- 2026-10-19 · Empieza la tarea. Antes, en la web: Malabar exige timing (pulsar antes de tiempo = fallo).
