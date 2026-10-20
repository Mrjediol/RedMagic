# Excavador

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Excavador)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Rol:** Emboscador subterráneo · **Cuerpo:** de suelo, hitbox 52×72 px (≈ 1.3×1.8 u) · código `e-*.js` id `burrower`

**Pregunta al jugador:** No quedarte quieto: el montículo te persigue (más lento que tú corriendo) y sale justo debajo. Castigarle cuando está fuera y al hundirse; las plataformas te salvan de la salida.

**Movimientos**

- **Túnel** — Bajo tierra no se le puede dar. Se ve un montículo de tierra que avanza hacia ti.
- **Emerger** — Se para debajo de ti: grietas y zona del ancho de su cuerpo en el suelo; sale disparado hacia arriba y te lanza por los aires. La columna de tierra atraviesa las plataformas: subirte no te salva (también se marca la plataforma).
- **Escupitajo** — Ya fuera, escupe 3 rocas en parábola alrededor de ti (cada una marca dónde cae).
- **Hundirse** — Al rato se vuelve a meter: tarda un poco (ventana de castigo). Un golpe fuerte mientras está fuera lo aturde; mucho daño mientras se hunde lo interrumpe y se queda fuera.

**Combina con:** Con Tejedora o Artilleros: tienes que moverte por dos motivos a la vez. En una arena con plataformas, menos peligroso.

**Arte (idea, sin decidir):** Gusano o topo segmentado con mandíbulas de cristal que asoma erguido de un agujero. Gusano de raíz, topo de piedra.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_burrower`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 160 | 5 – 1500 (paso 5) | Vida |
| `poise` | 45 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.2 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 4 | 0 – 40 (paso 1) | Daño por contacto |
| `tunnelSpeed` | 190 | 50 – 500 (paso 5) | Túnel: velocidad |
| `maxTunnel` | 4 | 0.5 – 10 (paso 0.5) | Túnel: sale como mucho a los (s) |
| `emergeWindup` | 0.65 | 0.1 – 2 (paso 0.05) | Emerger: aviso (s) |
| `emergeR` | 34 | 10 – 120 (paso 1) | Emerger: radio |
| `emergeDmg` | 16 | 0 – 60 (paso 1) | Emerger: daño |
| `emergeReach` | 230 | 40 – 340 (paso 5) | Emerger: altura de la columna (atraviesa plataformas) |
| `outTime` | 2.6 | 0.5 – 8 (paso 0.1) | Fuera: tiempo (s) |
| `spitWindup` | 0.5 | 0.1 – 2 (paso 0.05) | Escupitajo: preparación (s) |
| `spitN` | 3 | 1 – 7 (paso 1) | Escupitajo: rocas |
| `spitSpread` | 70 | 0 – 250 (paso 5) | Escupitajo: separación |
| `spitFlight` | 0.9 | 0.3 – 2.5 (paso 0.05) | Escupitajo: vuelo (s) |
| `spitDmg` | 10 | 0 – 60 (paso 1) | Escupitajo: daño |
| `digWindup` | 0.45 | 0.1 – 2 (paso 0.05) | Hundirse: tiempo (s, castigo) |
| `heavyStun` | 1.2 | 0 – 4 (paso 0.1) | Golpe fuerte fuera: aturdido (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
- 2026-10-20 · Respuesta hacia arriba (el usuario no veía cómo encajan los de suelo con tantas plataformas): ver «Movimientos»; ajustes nuevos en la tabla.
