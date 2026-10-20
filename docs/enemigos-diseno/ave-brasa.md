# Ave brasa

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Ave brasa)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** volcan · **Rol:** Voladora de pasadas; renace si no la rematas · **Cuerpo:** volador, hitbox 36×28 px (≈ 0.9×0.7 u) · código `e-*.js` id `emberbird`

**Pregunta al jugador:** Salir de la franja marcada cuando hace la pasada (las brasas caen a lo largo de ella) y, al matarla, romper el huevo de brasa antes de que eclosione: si no, vuelve.

**Movimientos**

- **Pasada** — Sube a un lado, se encoge (franja marcada en el suelo por donde va a pasar) y cruza en línea recta soltando brasas que caen. Tocarla también duele.
- **Renacer** — Al morir cae al suelo como un huevo de brasa que late cada vez más deprisa; si no lo rompes en unos segundos, el ave renace (con menos vida).

**Combina con:** Con el Escorpión: esperar su guardia mientras caen brasas. Con la Serpiente: dos cosas que te pasan por encima.

**Arte (idea, sin decidir):** Pájaro pequeño de plumas de fuego naranja y amarillo con el cuerpo de carbón negro agrietado; cola larga de llamas, ojos blancos. El huevo: bola de carbón con grietas de magma que laten. Partes: cuerpo, 2 alas, cola (llamas), huevo. ~0,9×0,7 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_emberbird`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 50 | 5 – 1500 (paso 5) | Vida |
| `poise` | 0 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 1.2 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `height` | 150 | 40 – 260 (paso 5) | Altura sobre ti |
| `speed` | 220 | 40 – 500 (paso 10) | Velocidad |
| `runWindup` | 0.55 | 0.1 – 2 (paso 0.05) | Pasada: preparación (s) |
| `runSpeed` | 520 | 150 – 1000 (paso 10) | Pasada: velocidad |
| `runDist` | 700 | 200 – 1300 (paso 10) | Pasada: recorrido |
| `dropEvery` | 0.11 | 0.03 – 0.5 (paso 0.01) | Pasada: brasa cada (s) |
| `emberDmg` | 7 | 0 – 30 (paso 1) | Brasa: daño |
| `runDmg` | 10 | 0 – 40 (paso 1) | Pasada: daño al tocarla |
| `eggHp` | 20 | 1 – 200 (paso 1) | Huevo: vida |
| `reviveTime` | 3 | 0.5 – 10 (paso 0.1) | Huevo: renace a los (s) |
| `reviveHp` | 0.6 | 0.1 – 1 (paso 0.05) | Al renacer: fracción de vida |
| `cooldown` | 1.6 | 0.2 – 5 (paso 0.1) | Pausa entre pasadas (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
