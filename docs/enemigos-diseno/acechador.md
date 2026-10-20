# Acechador

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Acechador)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Rol:** Cazador ágil de manada · **Cuerpo:** de suelo, hitbox 60×40 px (≈ 1.5×1.0 u) · código `e-*.js` id `stalker`

**Pregunta al jugador:** Leer la finta (el agachado no siempre acaba en salto) y esquivar por debajo del arco; no dejarse rodear.

**Movimientos**

- **Abalanzarse** — Se agacha (cuerpo naranja, ojos blancos) y salta en arco a donde vas a estar (adelanta tu movimiento). Marca en el suelo de dónde va a caer; si estás en una plataforma, salta a la plataforma (es su respuesta si te subes). Al caer se queda un momento recuperándose: ahí se le castiga.
- **Finta** — A veces el mismo agachado acaba en un salto atrás. Castiga gastar el dash por reflejo.
- **Rodear** — Se mueve a tu alrededor a media distancia y salta atrás si te pegas. En manada se reparten a los dos lados (uno a cada lado) y se turnan para atacar.

**Combina con:** Manada de 3: siempre hay uno a tu espalda. Con Tejedora: el ralentizado de la telaraña hace que el salto acierte.

**Arte (idea, sin decidir):** Cuadrúpedo esbelto y bajo, orejas puntiagudas, cola larga, púas de cristal en el lomo. Lobo de escarcha, zorro de musgo.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_stalker`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 90 | 5 – 1500 (paso 5) | Vida |
| `poise` | 25 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 1.1 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 3 | 0 – 40 (paso 1) | Daño por contacto |
| `ringMin` | 170 | 40 – 500 (paso 10) | Rodea a: distancia mínima |
| `ringMax` | 280 | 60 – 700 (paso 10) | Rodea a: distancia máxima |
| `run` | 230 | 50 – 500 (paso 5) | Velocidad corriendo |
| `crouch` | 0.45 | 0.1 – 1.5 (paso 0.05) | Abalanzarse: preparación (s) |
| `leapH` | 110 | 20 – 300 (paso 5) | Abalanzarse: altura del arco |
| `lead` | 0.25 | 0 – 1 (paso 0.05) | Abalanzarse: adelanta tu movimiento (s) |
| `leapDmg` | 12 | 0 – 60 (paso 1) | Abalanzarse: daño |
| `landRecover` | 0.6 | 0 – 2 (paso 0.05) | Al caer: recuperación (s, castigo) |
| `feint` | 0.3 | 0 – 1 (paso 0.05) | Probabilidad de finta |
| `hopV` | 330 | 100 – 700 (paso 10) | Salto atrás: impulso |
| `flank` | 1 | 0 – 1 (paso 1) | Manada: repartirse a los dos lados (1 = sí) |
| `cooldown` | 1 | 0 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
- 2026-10-20 · Respuesta hacia arriba (el usuario no veía cómo encajan los de suelo con tantas plataformas): ver «Movimientos»; ajustes nuevos en la tabla.
