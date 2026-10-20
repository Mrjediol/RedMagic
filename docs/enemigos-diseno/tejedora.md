# Tejedora

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Tejedora)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Rol:** Voladora a distancia que se teletransporta · **Cuerpo:** volador, hitbox 36×44 px (≈ 0.9×1.1 u) · código `e-*.js` id `weaver`

**Pregunta al jugador:** Apartarte justo cuando el hilo de puntería se queda fijo (blanco), no antes; perseguirla o derribarla con armas a distancia porque huye y reaparece.

**Movimientos**

- **Aguja** — Traza un hilo fino hacia ti que te sigue; un instante antes de disparar se queda fijo y se pone blanco. Luego, aguja muy rápida por esa línea.
- **Telaraña** — Lanza 3 nudos de tela lentos en abanico. No hacen daño: si te tocan te ENREDAN (vas a menos de la mitad de velocidad un rato). Se pueden devolver con Égida, Espejo, Ráfaga…
- **Parpadeo** — Tras unos ataques, o si le haces mucho daño seguido, desaparece y reaparece en otro sitio (al otro lado de ti). Antes de aparecer marca el sitio.

**Combina con:** Con Acechadores (el enredo deja que el salto acierte). Con Excavador: la aguja te obliga a moverte mientras buscas dónde va a salir.

**Arte (idea, sin decidir):** Cuerpo pequeño de hada o polilla con cuatro alas translúcidas y abdomen brillante; cuelga un hilo. Polilla de cristal, araña voladora.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_weaver`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 80 | 5 – 1500 (paso 5) | Vida |
| `poise` | 20 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 1.4 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `distMin` | 230 | 50 – 600 (paso 10) | Distancia mínima (huye) |
| `distMax` | 380 | 100 – 800 (paso 10) | Distancia máxima |
| `height` | 140 | 0 – 280 (paso 5) | Altura sobre ti |
| `speed` | 160 | 30 – 400 (paso 5) | Velocidad |
| `aimTime` | 0.8 | 0.2 – 2 (paso 0.05) | Aguja: apuntar (s) |
| `lockTime` | 0.25 | 0 – 1 (paso 0.05) | Aguja: hilo fijo antes de disparar (s) |
| `needleSpeed` | 1050 | 300 – 2000 (paso 10) | Aguja: velocidad |
| `needleDmg` | 14 | 0 – 60 (paso 1) | Aguja: daño |
| `webChance` | 0.35 | 0 – 1 (paso 0.05) | Probabilidad de telaraña |
| `webN` | 3 | 1 – 7 (paso 1) | Telaraña: nudos |
| `webSpeed` | 150 | 40 – 400 (paso 5) | Telaraña: velocidad |
| `slowTime` | 1.6 | 0 – 5 (paso 0.1) | Enredado: duración (s) |
| `slowK` | 0.45 | 0.1 – 1 (paso 0.05) | Enredado: × velocidad |
| `blinkAfter` | 3 | 1 – 10 (paso 1) | Parpadeo: cada N ataques |
| `blinkHurt` | 28 | 0 – 200 (paso 1) | Parpadeo: si recibe este daño en 1,5 s |
| `blinkCd` | 4 | 0 – 15 (paso 0.5) | Parpadeo: pausa mínima (s) |
| `blinkMark` | 0.5 | 0.1 – 2 (paso 0.05) | Parpadeo: aviso antes de aparecer (s) |
| `cooldown` | 1.5 | 0.2 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
