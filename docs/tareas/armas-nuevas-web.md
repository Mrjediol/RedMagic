# Armas nuevas de la web (12, hechas el 2026-10-20 sin el usuario) — pendiente: su feedback

**Frase para retomar**: «feedback de las armas nuevas» / «retoma las armas nuevas». Leer este doc entero primero.

## Estado

12 armas en el prototipo jugable (`docs/bocetos/habilidades/jugable.html`), cada una con 3 evoluciones × 3 mejoras, elemento
(hielo/fuego con 4 items) y Duplicador. Marcadas `fresh: true` → «✦ nueva» en el desplegable. **Ninguna jugada por el usuario.**
Commits en la rama `task/supervivencia` (sin push), uno por arma.

| Arma (`id`) | Código | Ficha | Evoluciones |
|---|---|---|---|
| Reloj de arena (`time`) | `g-reloj.js` | `docs/armas/reloj.md` | Estasis · Eco · Aceleración |
| Ráfaga (`gust`) | `g-rafaga.js` | `docs/armas/rafaga.md` | Tornado · Corriente · Martillo |
| Látigo de luz (`whip`) | `g-latigo.js` | `docs/armas/latigo.md` | Cadena · Garfio · Remolino |
| Jabalina (`spear`) | `g-jabalina.js` | `docs/armas/jabalina.md` | Pararrayos · Empalar · Destello |
| Enjambre (`swarm`) | `g-enjambre.js` | `docs/armas/enjambre.md` | Colmena · Plaga · Faroles |
| Tótem de cristal (`totem`) | `g-totem.js` | `docs/armas/totem.md` | Resonador · Centinela · Señuelo |
| Espejo (`mirror`) | `g-espejo.js` | `docs/armas/espejo.md` | Facetas · Lupa · Portal |
| Acumulador (`volt`) | `g-acumulador.js` | `docs/armas/acumulador.md` | Sobrecarga · Arco · Celdas |
| Pacto de sangre (`blood`) | `g-pacto.js` | `docs/armas/pacto.md` | Hemorragia · Vampiro · Mártir |
| Mazo de cristal (`hammer`) | `g-mazo.js` | `docs/armas/mazo.md` | Terremoto · Meteoro · Sismo |
| Égida (`aegis`) | `g-egida.js` | `docs/armas/egida.md` | Reflejo · Bastión · Embestida (probar con «Los enemigos disparan») |
| Ballesta de cristal (`bow`) | `g-ballesta.js` | `docs/armas/ballesta.md` | Perforador · Rebote · Marcador |

Resumen de los árboles y de cómo expresa cada una el elemento: `docs/armas/progresion.md` ▸ «Armas 7+».

## Dónde está cada cosa

- Dibujo de las armas nuevas: `docs/bocetos/habilidades/fx-armas2.js` (lo que se porta a Unity; nunca dibujar en los `g-*.js`).
  Paletas nuevas en `core.js`: `chrono`, `wind`, `ember`, `storm`, `volt`, `blood`.
- Sonidos: bloque `SND_<ARMA>` al principio de cada `g-*.js` (`sound(id, grupo, nombre, fn)`).
- Reglas comunes añadidas al motor (`game.js`), documentadas en `docs/armas/README.md` ▸ reglas comunes:
  - `inst.onAnyHit(e, o, dmg)` / `inst.onAnyKill(e, o)`: el motor los llama para todas las armas equipadas, también la que no
    está en la mano. Los golpes derivados llevan `proc: true` y no los vuelven a disparar (sin bucles).
  - `inst.outMul(e, o)`: multiplica tus golpes de arma (Mártir, Marcador). `inst.onHurt(d)` / `inst.onLethal()` en `hurtPlayer`.
  - Estados de los enemigos:
    - `e.timeK`: tiempo propio, 0 = parado.
    - `e.stasis` + `o.release`: daño guardado.
    - `makeVuln(e, k, s)`: vulnerable.
    - `o.launch` / `o.pins`: lanzado, choca (`launchCollide`); cada pareja choca una vez.
    - `e.litT` / `e.litChance`: iluminado, crítico ×2.
    - `e.bleedN`, `e.weakN`, `e.infest`.
  - `o.raw`: el daño ya lleva la build aplicada.
  - Espejos: `G.mirrors`, `mirrorCross`, `segSeg`; `g-rayo.js ▸ rayPath` rebota en ellos (`gap` = salto de portal).
  - Señuelos: `G.decoys`; las balas enemigas van a por ellos.
  - Un rayo o proyectil que empieza dentro de un enemigo le da (`raycastEnemy`).
- Jugador: `p.speedMul` (lo pone cada frame quien lo acelera), `p.jumpMul` + `p.jumpUntil`, `G.quickUntil` / `G.quickK`
  (recargas más rápidas).

## Herramientas (sin navegador)

- `node docs/bocetos/habilidades/tools/test-jugable.cjs ["" id]`: matriz arma × árbol × build, con balas enemigas.
  Debe decir «todo ok».
- `node docs/bocetos/habilidades/tools/test-armas2.cjs [id]`: pruebas dirigidas, una por evolución (89). Usa
  `tools/sim.cjs`: carga la jugable en un `vm`, con `setup / aim / press / run / release / swap` y azar con semilla.
  **Ojo**: se canaliza por `| grep -E "MAL|todo|mal"`; mirar la salida antes de hacer commit (el código de salida se pierde
  con `| tail`).
- `node docs/bocetos/habilidades/tools/dps.cjs`: genera `docs/armas/dps.md` (DPS de cada arma y evolución, sola y en pareja
  con el Abanico). Sirve para ver valores muy fuera de rango; las armas cuerpo a cuerpo salen bajas porque el bot no se acerca.
- `node docs/bocetos/habilidades/tools/gen-fichas.cjs`: regenera las tablas de las fichas (árbol y ajustes). Un arma nueva
  necesita su entrada en `FILES` y una ficha con las marcas `<!-- gen:tree -->` y `<!-- gen:tuning -->`.
- Navegador: `node docs/bocetos/habilidades/tools/serve.cjs` → `http://127.0.0.1:8765/habilidades/jugable.html?prueba`.
  - Abrir **una pestaña propia**: la del usuario guarda su estado en `localStorage` y no hay que tocarla.
  - En la consola:
    `setSlot = (i, w, arch, ups = {}) => { const s = G.slots[i]; s.w = w; s.arch = arch; s.ups = ups; s.inst = WEAPONS[w].create(s); }`.
  - Apuntar con `G.mouse.sx` / `G.mouse.sy` (coordenadas de pantalla) y disparar con `G.slots[i].inst.onPress(aimInfo())`.
  - Congelar con `G.timeScale = 0.0001`.

## Qué tiene que probar el usuario (lista que se le dio)

1. Reloj · Estasis + Golpe estelar: abre la burbuja, Q y lanza; al cerrarse, «ESTASIS N».
2. Ráfaga · Martillo: mantén y suelta contra un enemigo con otro detrás; debe salir «CHOQUE».
3. Látigo · Cadena + Golpe estelar: la lanza a uno daña también al encadenado.
4. Espejo + Rayo o Abanico: dispara al espejo con el enemigo fuera de línea.
5. Acumulador · Sobrecarga + Abanico: descarga solo al llenarse.
6. Égida con «Los enemigos disparan»: levántala justo cuando llega la bala («¡PARADA!»).
7. Ballesta · Rebote: apunta al suelo delante del enemigo y mira el recorrido.

## Método para el feedback (el del usuario, de `habilidades-prototipos.md`)

Un arma cada vez:

1. Recoger lo que dice.
2. Arreglarlo en `g-<arma>.js`, con los números en `tuning`.
3. Actualizar su ficha (comportamiento, y en «Decisiones» la fecha y el porqué) y regenerar con `gen-fichas`.
4. Pasar `test-jugable`, `test-armas2 <id>` y, si cambia el daño, `dps.cjs`.
5. Hacer commit.

Lo que apruebe:

- se le quita `fresh: true`;
- pasa a la cola de port a Unity (`docs/armas/README.md` ▸ «Cómo se pasa un arma a Unity»; el port de las 6 primeras sigue en
  `docs/tareas/armas-unity.md`).

Lo que descarte se archiva, no se borra (regla de legacy: mover el `g-*.js` fuera de `jugable.html` y anotarlo en la ficha).

## Ideas sin hacer (por si pide más armas)

- **Alud**: bola de nieve que crece rodando y se traga enemigos.
- **Maldición**: vudú; golpear un muñeco daña a todos los malditos.
- **Sombra**: clon que repite tus disparos usando el `clone()` de los proyectiles.
