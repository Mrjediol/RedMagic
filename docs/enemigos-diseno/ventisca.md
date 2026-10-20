# Ventisca

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Ventisca)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** hielo · **Rol:** Voladora de control: te empuja · **Cuerpo:** volador, hitbox 44×52 px (≈ 1.1×1.3 u) · código `e-*.js` id `gale`

**Pregunta al jugador:** Posicionarte contra el viento: el soplo te empuja (físicamente: sí te saca de una plataforma) hacia lo que tengas detrás. El dash lo atraviesa. Y esquivar sus bolas de nieve, que revientan en trozos.

**Movimientos**

- **Soplo** — Se hincha y sopla en un cono hacia ti (cono marcado en rojo). Dentro del cono te empuja con fuerza en esa dirección mientras dura; sin daño. Con el dash no te mueve.
- **Bola de nieve** — Lanza una bola en parábola que, al caer, revienta y suelta tres trozos pequeños hacia arriba y a los lados.

**Combina con:** Con Morsa (te empuja a sus pinchos), con Colgantes (te empuja debajo), con Erizos erizados. Sola es fácil: es un enemigo de combinación.

**Arte (idea, sin decidir):** Espíritu de ventisca: un remolino de nieve con forma de lágrima y capucha, dentro de la capucha oscuridad con dos ojos azul claro. Dos brazos de nieve deshilachada y una cola de copos que se deshace. Al soplar la capucha se abre en una boca redonda con anillos de viento. Partes: cuerpo (remolino), capucha/cara, 2 brazos, cola (partículas). ~1,1×1,3 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_gale`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 70 | 5 – 1500 (paso 5) | Vida |
| `poise` | 20 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 1.2 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `distMin` | 200 | 50 – 500 (paso 10) | Distancia mínima (huye) |
| `distMax` | 340 | 100 – 700 (paso 10) | Distancia máxima |
| `height` | 110 | 0 – 260 (paso 5) | Altura sobre ti |
| `speed` | 140 | 30 – 400 (paso 5) | Velocidad |
| `gustChance` | 0.55 | 0 – 1 (paso 0.05) | Probabilidad de soplo (si no, bola) |
| `gustWindup` | 0.6 | 0.1 – 2 (paso 0.05) | Soplo: preparación (s) |
| `gustTime` | 1.4 | 0.2 – 4 (paso 0.1) | Soplo: duración (s) |
| `gustRange` | 440 | 100 – 900 (paso 10) | Soplo: alcance |
| `gustHalf` | 0.45 | 0.1 – 1.2 (paso 0.05) | Soplo: medio ángulo (rad) |
| `gustPush` | 300 | 0 – 900 (paso 10) | Soplo: empuje (px/s) |
| `snowWindup` | 0.5 | 0.1 – 2 (paso 0.05) | Bola: preparación (s) |
| `snowFlight` | 0.9 | 0.3 – 2 (paso 0.05) | Bola: vuelo (s) |
| `snowDmg` | 10 | 0 – 40 (paso 1) | Bola: daño |
| `snowR` | 30 | 10 – 90 (paso 1) | Bola: radio al reventar |
| `fragN` | 3 | 0 – 7 (paso 1) | Bola: trozos |
| `fragDmg` | 5 | 0 – 30 (paso 1) | Bola: daño de cada trozo |
| `cooldown` | 1.6 | 0.2 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
