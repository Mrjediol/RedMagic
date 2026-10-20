# Fatuo

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Fatuo)

## Diseño
<!-- gen:diseno -->
**Rol:** Kamikaze flotante (bomba de doble filo) · **Cuerpo:** volador, hitbox 32×32 px (≈ 0.8×0.8 u) · código `e-*.js` id `wisp`

**Pregunta al jugador:** Matarlo antes de que llegue, apartarte cuando se hincha, o aprovecharlo: su explosión también daña (mucho) a los otros enemigos.

**Movimientos**

- **Deriva** — Flota hacia ti despacio haciendo eses. Los empujones le afectan mucho (Ráfaga, Égida, explosiones lo mandan lejos).
- **Hincharse** — A poca distancia se hincha (se pone naranja y parpadea cada vez más rápido; anillo del tamaño exacto de la explosión) y revienta. No se puede interrumpir.
- **Doble filo** — Si lo matas mientras se hincha, revienta al momento donde esté. Si lo matas antes, se apaga sin explotar. Congelado tampoco explota. La explosión daña a los demás enemigos.

**Combina con:** Con Embestidor o Caparazón: déjalo explotar junto a ellos. Con Colmena: un fatuo en el enjambre limpia zánganos.

**Arte (idea, sin decidir):** Llama o esfera de luz con cola, ojos sencillos; al hincharse cambia de color. Fuego fatuo, espora luminosa.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_wisp`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 35 | 5 – 1500 (paso 5) | Vida |
| `poise` | 0 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 1.6 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `speed` | 95 | 20 – 300 (paso 5) | Velocidad |
| `zigzag` | 50 | 0 – 150 (paso 5) | Eses: amplitud |
| `triggerR` | 90 | 30 – 250 (paso 5) | Se hincha a esta distancia |
| `fuse` | 0.9 | 0.2 – 3 (paso 0.05) | Hincharse: tiempo hasta reventar (s) |
| `blastR` | 95 | 30 – 250 (paso 5) | Explosión: radio |
| `blastDmg` | 22 | 0 – 80 (paso 1) | Explosión: daño al player |
| `foeDmg` | 30 | 0 – 200 (paso 1) | Explosión: daño a otros enemigos |
| `chain` | 1 | 0 – 1 (paso 1) | Revienta si lo matas hinchándose (1 = sí) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
