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
| 7 | Sonidos: piezas `SND.*` a WAV con el mismo código y que el usuario los califique | WAV hechos y asignados; falta calificar |
| 8 | Items de la web (Duplicador, Gatillo, Constelación…) frente a los del juego: decidir con el usuario | pendiente |

## ▶ RETOMAR AQUÍ — «continúa con las armas» (fin de sesión 2026-10-19)

**Primera prueba del usuario (2026-10-19)**: «a priori funciona la mayoría de cosas aparentemente bien, aunque tocará retocar y
mejorar muchas cosas»; **la Semilla parece incompleta** (sin detalle aún: preguntarle qué echa en falta antes de tocarla).
**Permiso dado** para `npm i node-web-audio-api@1.0.4` (sonidos), en una carpeta temporal fuera del repo.

**Estado**: las 6 armas portadas, compilan y se ven bien en renders fuera de pantalla (`ArsenalPreviewShots.Fan/Sing/Ray/Moon/Seed/Star`,
`unity command eval --code "return RedMagic.Arsenal.Editor.ArsenalPreviewShots.Ray(@\"<carpeta>\");"`). **Nada probado en Play**:
lo prueba el usuario. Rama `task/armas-unity` (sin fusionar).

**Cómo probar**: Tools ▸ RedMagic ▸ Test ▸ Escena de armas (arsenal) → abrir `Assets/Test/Scenes/ArsenalTest.unity` → Play.
Q dispara (mantener = repetir / canalizar / madurar), Tab cambia de arma, F10 (o botón «ARMAS») = menú DEV: arma de cada hueco,
evolución, mejoras, elemento forzado (hielo/fuego), Duplicador, reacciones.

**Sonidos (2026-10-20)**: los 56 WAV renderizados y asignados en `ArsenalAssets ▸ Sonidos`; en el registro salen como
`Player/Arsenal/<arma>` ▸ `<id>`. **Falta que el usuario los califique** (pestaña Sounds). Re-renderizar: punto 2 de abajo.

**Siguiente (en este orden)**:
1. Calificación de los 56 sonidos del arsenal (la hace el usuario; apuntar en `SoundStatus.json`).
2. Preguntar al usuario qué falla de la Semilla y arreglarlo; luego el resto de retoques que vaya pidiendo, un arma cada vez.
3. Lo que quedó sin portar o a medias (lista de abajo).

**Sin terminar / sin portar (2026-10-19)**:
- HUD: hecho con teclado/mando (insignia de reserva en la casilla del arma, iconos en `Assets/Test/Arsenal/Icons/` desde
  `docs/bocetos/habilidades/iconos-armas.html` + `tools/exportar-iconos-armas.cjs`). Falta el botón táctil de cambiar
  (en táctil la fila de casillas no se ve).
- Items de la web sin portar: Gatillo de cambio, Constelación, Corazón de vacío, Cristal afilado, Sangre de cristal…; el Duplicador sólo
  existe como interruptor DEV. Tramos 6 de Fuego (la quemadura salta al morir) y de Hielo (congelado +50 %) no aplicados.
- Feel de la web no portado: impact frames (pantalla oscura + enemigo blanco en golpes fuertes), estiramiento del enemigo atrapado por la
  grieta (aquí hay hebras y brillo), suma de números pequeños seguidos (aquí cada tick saca su número).
- Balance: los números son los de la web (enemigos de 120 de vida); con los enemigos reales habrá que reajustar (ver modo Supervivencia).
- Las armas viven en `Assets/Test/Arsenal/`: moverlas a `Resources/Items/Weapons/` cuando el usuario las dé por buenas; los nombres
  de evoluciones/mejoras (`ArsenalTree`) están sin localizar (sólo los ve el DEV; localizarlos antes de enseñarlos al jugador).
- Rama `task/armas-unity` sin fusionar: el usuario la fusiona cuando quiera.

**Siguiente (detalle)**:
1. Recoger la prueba del usuario y arreglar (un arma cada vez).
2. Re-renderizar sonidos (`docs/bocetos/habilidades/tools/render-sonidos-armas.cjs`, 56 ids → WAV en
   `Assets/Audio/Sounds/Player/Arsenal/`): `npm i node-web-audio-api@1.0.4` en una carpeta temporal,
   `WEB_AUDIO_API=<ruta>/node_modules/node-web-audio-api/index.cjs node … Assets/Audio/Sounds/Player/Arsenal`, luego Tools ▸
   RedMagic ▸ Arsenal ▸ Preparar (mete cada WAV en ArsenalAssets ▸ Sonidos por su nombre; lo que ya tiene clip no se toca) y
   regenerar el registro. Piezas de la web con `setTimeout` (no corre offline) se programan a mano en el script (`seed_trigger`).
3. Fase 8 (items) y pasar las armas de `Assets/Test/Arsenal/` a `Resources/Items/Weapons/` cuando el usuario las dé por buenas.

**Trampas aprendidas**: `Bump(1)` = sin(π) negativo → `Pow` daba NaN y Unity descartaba el lienzo entero (arreglado;
`FxCanvas` avisa de NaN). El mundo devolvía al pool entidades antes de quitarlas de la lista (arreglado). Ediciones a .cs con
CRLF: usar un script que normalice saltos de línea. El banco de renders aísla todo en la capa 31.

## Cómo se verifica

Compilar con el editor abierto (`unity command recompile`). Aspecto: render fuera de pantalla de 2-3 instantes por efecto
(`docs/fx-proceso-bocetos.md` ▸ 4). El juego lo prueba el usuario con la escena de prueba y el menú DEV.

## Historial

- 2026-10-20 · Barras de vida en los muñecos (`IHudEnemy`), insignia del arma de reserva en el HUD e iconos de las 6 armas.
- 2026-10-20 · Sonidos: 56 WAV asignados y en el registro (`Player/Arsenal/*`); quitado el hueco fantasma `OnRangedAttack`
  de PlayerNuevo (bloqueaba el registro; el disparo suena por `WeaponDefinition.FireSound`).
- 2026-10-19 · Fases 0-6 hechas en una sesión; sonido a medias (falta instalar node-web-audio-api).
- 2026-10-19 · Empieza la tarea. Antes, en la web: Malabar exige timing (pulsar antes de tiempo = fallo).
