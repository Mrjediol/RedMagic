# Zángano

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Zángano)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Rol:** Cría de la Colmena: frágil, en grupo · **Cuerpo:** volador, hitbox 20×18 px (≈ 0.5×0.5 u) · código `e-*.js` id `drone`

**Pregunta al jugador:** Muchos objetivos pequeños que se lanzan en línea: aparta o barre (un golpe los mata) sin perder de vista lo grande.

**Movimientos**

- **Rondar** — Vuelan en círculo alrededor de ti, separándose entre ellos.
- **Picado** — Se para, tiembla (línea corta del recorrido) y se lanza en línea recta. Si falla, vuelve a rondar. Como todos los ataques, cuentan para el máximo de atacantes a la vez.

**Combina con:** Con su Colmena (la fuente). Sueltos, en grupo de 4-6, como relleno de una oleada.

**Arte (idea, sin decidir):** Insecto pequeño y redondo con rayas de ámbar, aguijón y alas borrosas. Avispa de corteza, mosca de luz.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_drone`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 12 | 5 – 1500 (paso 5) | Vida |
| `poise` | 0 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 2 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `orbitR` | 80 | 30 – 250 (paso 5) | Rondar: radio |
| `speed` | 190 | 40 – 500 (paso 5) | Velocidad |
| `diveWindup` | 0.4 | 0.1 – 1.5 (paso 0.05) | Picado: preparación (s) |
| `diveSpeed` | 540 | 100 – 1200 (paso 10) | Picado: velocidad |
| `diveDist` | 230 | 50 – 600 (paso 10) | Picado: recorrido |
| `dmg` | 6 | 0 – 40 (paso 1) | Picado: daño |
| `cooldown` | 1.4 | 0.2 – 6 (paso 0.1) | Pausa entre picados (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
