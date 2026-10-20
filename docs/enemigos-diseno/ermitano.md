# Ermitaño de hielo

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Ermitaño de hielo)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** hielo · **Rol:** Blindado en dos fases (coraza → cangrejo) · **Cuerpo:** de suelo, hitbox 68×60 px (≈ 1.7×1.5 u) · código `e-*.js` id `hermit`

**Pregunta al jugador:** Romperle la coraza (los golpes fuertes la agrietan mucho más) sin ponerte a tiro de su pinza, que te congela un segundo y te remata. Roto, cambia de ritmo: pequeño, rápido y frágil.

**Movimientos**

- **Coraza** — Dentro del bloque de hielo recibe ×0,3. El daño que le haces lo agrieta (3 grietas = rota); un golpe fuerte cuenta como una grieta entera.
- **Pinza helada** — De cerca levanta la pinza (brilla) y da un tirón hacia delante. Si te pilla te CONGELA 1 s (no te mueves ni disparas) y al momento te da un pellizco fuerte. Si falla, se queda vendido un instante.
- **Al descubierto** — Con la coraza rota sale el cangrejo: corre de lado alrededor de ti y pellizca rápido (sin congelar). Poca vida.

**Combina con:** Con Ventisca: el viento te empuja a su pinza. Con Erizo: la lluvia te obliga a acercarte. En pareja: uno te congela y el otro remata (cuidado con el ajuste de turnos).

**Arte (idea, sin decidir):** Cangrejo ermitaño que lleva por concha un bloque de hielo facetado (transparente, con burbujas y vetas blancas, se ven sombras del cangrejo dentro); asoman 4 patas articuladas gris-rosado, dos ojos en pedúnculo y una pinza grande con punta de cristal azul. Al romperse: cangrejo pequeño y blando, rosado. Partes: concha (3 estados de grietas), cuerpo, pinza (abierta / cerrada), patas. ~1,7×1,5 u (con concha), 0,9×0,7 u (sin ella).
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_hermit`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 160 | 5 – 1500 (paso 5) | Vida |
| `poise` | 50 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.3 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 4 | 0 – 40 (paso 1) | Daño por contacto |
| `shellMul` | 0.3 | 0 – 1 (paso 0.05) | Coraza: × daño recibido |
| `crackDmg` | 45 | 5 – 300 (paso 5) | Coraza: daño (sin reducir) por grieta |
| `cracks` | 3 | 1 – 6 (paso 1) | Coraza: grietas para romperse |
| `walk` | 40 | 0 – 200 (paso 5) | Velocidad (con coraza) |
| `grabRange` | 120 | 30 – 300 (paso 5) | Pinza: si estás a menos de |
| `grabWindup` | 0.55 | 0.1 – 2 (paso 0.05) | Pinza: preparación (s) |
| `grabLunge` | 110 | 0 – 300 (paso 5) | Pinza: tirón (px) |
| `freezeTime` | 1 | 0 – 3 (paso 0.1) | Pinza: te congela (s) |
| `grabDmg` | 4 | 0 – 30 (paso 1) | Pinza: daño al pillarte |
| `pinchDelay` | 0.75 | 0.2 – 2 (paso 0.05) | Pinza: pellizco tras congelarte (s) |
| `pinchDmg` | 12 | 0 – 60 (paso 1) | Pinza: daño del pellizco |
| `missRec` | 0.7 | 0 – 2 (paso 0.05) | Pinza fallida: vendido (s) |
| `cooldown` | 1.6 | 0.2 – 6 (paso 0.1) | Pausa entre ataques (s) |
| `coreHp` | 45 | 5 – 300 (paso 5) | Al descubierto: vida máxima |
| `scuttle` | 210 | 50 – 500 (paso 5) | Al descubierto: velocidad |
| `nipWindup` | 0.25 | 0.05 – 1 (paso 0.05) | Al descubierto: preparación del pellizco (s) |
| `nipDmg` | 6 | 0 – 40 (paso 1) | Al descubierto: daño del pellizco |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
