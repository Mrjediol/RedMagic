# Escorpión de obsidiana

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Escorpión de obsidiana)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** volcan · **Rol:** Duelista: guardia y contraataque · **Cuerpo:** de suelo, hitbox 68×40 px (≈ 1.7×1.0 u) · código `e-*.js` id `scorpion`

**Pregunta al jugador:** Paciencia: cuando cruza las pinzas en guardia (brillo claro, «GUARDIA»), no le pegues o te contraataca con la cola. Esquiva el picotazo largo (línea marcada) y castígale mientras la cola está clavada.

**Movimientos**

- **Picotazo** — Levanta la cola y marca una línea hacia ti que se fija (blanca) justo antes; la cola sale disparada por esa línea, muy lejos. Después se queda clavada un momento (castigo).
- **Guardia** — Cruza las pinzas delante un rato: tus golpes no le hacen nada y, en cuanto le das, contraataca al instante con un picotazo rápido.
- **Pinza** — Si estás pegado, pinzazo rápido delante.

**Combina con:** Con Ave brasa: te obliga a esperar mientras llueven brasas. Con Salamandra: esperar con el suelo lleno de lava.

**Arte (idea, sin decidir):** Escorpión grande de caparazón de obsidiana negra y brillante con filos de cristal violáceo y vetas de magma; aguijón de cristal ardiente; pinzas grandes que cruza como un escudo. Partes: cuerpo, cola (3-4 segmentos + aguijón), 2 pinzas, 6 patas. ~1,7×1 u (cola aparte).
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_scorpion`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 150 | 5 – 1500 (paso 5) | Vida |
| `poise` | 50 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.4 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 4 | 0 – 40 (paso 1) | Daño por contacto |
| `speed` | 90 | 20 – 300 (paso 5) | Velocidad |
| `keepMin` | 130 | 0 – 400 (paso 10) | Le gusta estar a (mín.) |
| `keepMax` | 230 | 50 – 500 (paso 10) | Le gusta estar a (máx.) |
| `stingRange` | 280 | 80 – 500 (paso 10) | Picotazo: alcance |
| `stingWindup` | 0.6 | 0.15 – 2 (paso 0.05) | Picotazo: preparación (s) |
| `stingDmg` | 14 | 0 – 50 (paso 1) | Picotazo: daño |
| `stingRec` | 0.7 | 0 – 2 (paso 0.05) | Picotazo: cola clavada (s, castigo) |
| `guardChance` | 0.45 | 0 – 1 (paso 0.05) | Probabilidad de guardia |
| `guardTime` | 1.6 | 0.3 – 5 (paso 0.1) | Guardia: dura (s) |
| `counterDmg` | 16 | 0 – 60 (paso 1) | Contraataque: daño |
| `pinchRange` | 70 | 20 – 200 (paso 5) | Pinza: si estás a menos de |
| `pinchWindup` | 0.3 | 0.1 – 1 (paso 0.05) | Pinza: preparación (s) |
| `pinchDmg` | 10 | 0 – 40 (paso 1) | Pinza: daño |
| `cooldown` | 1.3 | 0.2 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
