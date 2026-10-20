# Atrapamoscas

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Atrapamoscas)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** bosque · **Rol:** Trampa plantada que te arrastra · **Cuerpo:** de suelo, hitbox 52×60 px (≈ 1.3×1.5 u) · código `e-*.js` id `flytrap`

**Pregunta al jugador:** Apartarte de la línea de la liana; si te atrapa, soltarte antes de llegar a la boca: con el dash, o haciéndole daño suficiente mientras tira de ti (puedes seguir disparando). De cerca, cuidado con el mordisco.

**Movimientos**

- **Liana** — Se encoge y apunta: una línea fina va de su boca a ti y se fija (blanca) justo antes de lanzar. La liana sale recta y rápida por esa línea; si falla, la recoge despacio (castigo).
- **Arrastrar** — Si la liana te pilla, tira de ti hacia su boca (no puedes andar ni saltar; sí disparar). Te sueltas con el dash o si le haces daño bastante mientras tira (su aguante). Si llegas a la boca, mordisco fuerte que te lanza.
- **Mordisco** — Si estás pegado a ella, cierra la boca de golpe (preparación corta).

**Combina con:** Con Zarza: te deja quieto delante del muro de espinas. Con Saltasetas: te arrastra debajo de un gran salto. Plantada: es una planta, no se mueve (pero te sigue con la cabeza).

**Arte (idea, sin decidir):** Planta atrapamoscas grande: dos lóbulos de boca con dientes de cristal turquesa y el interior rojo; tallo grueso, hojas anchas en la base y raíces; la liana es un zarcillo verde con espinitas. Partes: base (hojas), tallo, boca (abierta / cerrada), liana (segmentos). ~1,3×1,5 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_flytrap`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 130 | 5 – 1500 (paso 5) | Vida |
| `poise` | 45 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `snapRange` | 80 | 20 – 200 (paso 5) | Mordisco: si estás a menos de |
| `snapWindup` | 0.35 | 0.1 – 1 (paso 0.05) | Mordisco: preparación (s) |
| `snapDmg` | 12 | 0 – 50 (paso 1) | Mordisco: daño |
| `vineRange` | 330 | 80 – 700 (paso 10) | Liana: alcance |
| `vineWindup` | 0.7 | 0.2 – 2 (paso 0.05) | Liana: apuntar (s) |
| `lockAt` | 0.75 | 0 – 1 (paso 0.05) | Liana: fija la línea a esta fracción |
| `vineSpeed` | 950 | 200 – 2000 (paso 10) | Liana: velocidad |
| `reelSpeed` | 260 | 50 – 600 (paso 10) | Arrastrar: velocidad |
| `biteDmg` | 16 | 0 – 60 (paso 1) | Arrastrar: mordisco al llegar |
| `missRec` | 0.8 | 0 – 3 (paso 0.05) | Liana fallida: recogiendo (s, castigo) |
| `cooldown` | 2.2 | 0.2 – 6 (paso 0.1) | Pausa entre lianas (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
