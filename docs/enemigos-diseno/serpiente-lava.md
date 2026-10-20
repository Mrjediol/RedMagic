# Serpiente de lava

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Serpiente de lava)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** volcan · **Rol:** Saltadora en arco desde el suelo · **Cuerpo:** volador, hitbox 40×36 px (≈ 1.0×0.9 u) · código `e-*.js` id `lavaserpent`

**Pregunta al jugador:** Su salto pasa por donde estás (marcadas la entrada, la salida y la curva): saltar por encima de su cuerpo justo cuando pasa, o salirte del arco. Al caer se retuerce fuera un momento: castígala antes de que se hunda.

**Movimientos**

- **Nadar** — Bajo el suelo no se le puede dar; burbujas de lava delatan dónde está. Se coloca a un lado de ti.
- **Salto en arco** — Burbujea en la entrada y en la salida (las dos marcadas) y se marca la curva; salta desde un lado, pasa a la altura de tu cuerpo y se zambulle al otro. Encadena dos saltos (ida y vuelta) antes de descansar.
- **Fuera** — Tras el último salto se queda fuera retorciéndose un momento (recibe más daño) y se vuelve a hundir.

**Combina con:** Con la Salamandra: los rastros te quitan suelo para esquivar. Con Ave brasa: dos pasadas por encima de ti.

**Arte (idea, sin decidir):** Serpiente o anguila larga de roca volcánica negra con escamas que brillan por dentro (naranja), cabeza de dragón pequeña con cuernos de obsidiana; deja gotas de lava al salir. Partes: cabeza, 6-8 segmentos de cuerpo, cola. ~0,9 u de grosor, ~4 u de largo.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_lavaserpent`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 170 | 5 – 1500 (paso 5) | Vida |
| `poise` | 60 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.3 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `tunnelSpeed` | 240 | 50 – 600 (paso 10) | Nadar: velocidad |
| `span` | 420 | 150 – 900 (paso 10) | Salto: ancho del arco |
| `leapWindup` | 0.8 | 0.2 – 2 (paso 0.05) | Salto: aviso (s) |
| `leapTime` | 0.9 | 0.3 – 2 (paso 0.05) | Salto: duración (s) |
| `leapDmg` | 16 | 0 – 50 (paso 1) | Salto: daño |
| `leaps` | 2 | 1 – 5 (paso 1) | Saltos seguidos |
| `exposed` | 1.3 | 0 – 4 (paso 0.1) | Fuera tras el último salto (s, castigo) |
| `exposedVuln` | 1.4 | 1 – 3 (paso 0.05) | Fuera: × daño recibido |
| `cooldown` | 1.2 | 0 – 5 (paso 0.1) | Descanso bajo tierra (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
