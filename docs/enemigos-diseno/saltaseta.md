# Saltaseta

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Saltaseta)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** bosque · **Rol:** Saltarina de grupo (ritmo) · **Cuerpo:** de suelo, hitbox 36×40 px (≈ 0.9×1.0 u) · código `e-*.js` id `shroom`

**Pregunta al jugador:** Leer el ritmo: tres saltitos y un gran salto que cae donde estás (también en tu plataforma). En grupo, cada una lleva su cuenta: apartarte a tiempo de la que va a caer.

**Movimientos**

- **Saltitos** — Avanza hacia ti a saltitos, siempre con el mismo compás; cerca de ti salta de lado a lado. No hacen daño (salvo el contacto).
- **Gran salto** — Tras unos saltitos se aplasta (se enciende) y salta alto hacia donde estás: marca en el suelo (o en tu plataforma) dónde cae; al caer, ¡PLAF!, nube de esporas que hace daño alrededor. Después se queda un momento aturdida.

**Combina con:** En grupos de 2-3 (ritmos desfasados). Con Zarza: saltar los muros de espinas sin caer en un ¡PLAF!. Con el Oso: te obliga a moverte mientras te persigue.

**Arte (idea, sin decidir):** Seta de sombrero rojo pardo con motas crema, pie blanco hueso con dos ojillos oscuros bajo el sombrero y musgo en la base; algunas motas son cristales turquesa que brillan. Muy elástica: se estira al saltar y se aplasta al caer (fácil de animar por escala). Partes: sombrero, pie, ojos. ~0,9×1 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_shroom`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 45 | 5 – 1500 (paso 5) | Vida |
| `poise` | 15 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 1.3 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 4 | 0 – 40 (paso 1) | Daño por contacto |
| `hopV` | 300 | 100 – 600 (paso 10) | Saltito: impulso |
| `hopX` | 120 | 0 – 300 (paso 5) | Saltito: avance |
| `hopRest` | 0.28 | 0.05 – 1 (paso 0.01) | Saltito: pausa en el suelo (s) |
| `keep` | 110 | 0 – 400 (paso 5) | Deja de acercarse a |
| `bigEvery` | 3 | 1 – 8 (paso 1) | Gran salto: cada N saltitos |
| `bigWindup` | 0.45 | 0.1 – 1.5 (paso 0.05) | Gran salto: preparación (s) |
| `bigH` | 150 | 40 – 300 (paso 5) | Gran salto: altura |
| `lead` | 0.25 | 0 – 1 (paso 0.05) | Gran salto: adelanta tu movimiento (s) |
| `slamR` | 58 | 15 – 160 (paso 1) | Gran salto: radio |
| `slamDmg` | 14 | 0 – 60 (paso 1) | Gran salto: daño |
| `rest` | 0.6 | 0 – 2 (paso 0.05) | Tras el gran salto: aturdida (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
