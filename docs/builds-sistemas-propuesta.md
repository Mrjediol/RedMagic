# Sistemas de build — propuesta (2026-10-19)

Salen de los prototipos de habilidades (`docs/bocetos/habilidades/`, pestaña 🧩). Hoy el juego tiene el pipeline de disparo
(Trayectoria → Forma → Elemento), 9 huecos de items y etiquetas con umbrales 2/4/6 que **cuentan pero no hacen nada**
(`docs/architecture/weapons-items.md`). Objetivo del usuario: builds que cambien **cómo** se usa un arma, estilo Isaac pero
sin miles de items.

## Principios

- **Pocos sistemas, muchas combinaciones.** Cada sistema es una regla genérica que vale para cualquier arma (como el pipeline
  actual), no un efecto escrito para un arma concreta.
- **Cada item cambia una decisión del jugador**, no sólo un número (+daño queda para las mejoras de nivel del arma).
- **Legible en pantalla**: cada sistema tiene una firma visual (estado en el enemigo, marca, estela, color).
- Tamaño orientativo para el prototipo: 6 armas × 3-4 items de sistema + 5 transformaciones ≈ 40 items.

## Sistemas (prioridad sugerida)

| # | Sistema | Qué cambia en el juego | Encaje técnico | Se ve en |
|---|---|---|---|---|
| 1 | **Reacciones elementales** | Un arma aplica estado (congelado / quemado), otra lo detona (añicos, vapor, deshielo) | Estados en el enemigo + tabla de reacciones; el Elemento del pipeline ya existe | 1·Hielo, 2·Fuego, 3·Hielo, 5·Hielo |
| 2 | **Transformaciones** | 3+ items de una familia transforman al player (visual + efecto global) | Dar efecto a los umbrales del `SynergyTracker` | ideas de 2 y 6 |
| 3 | **Retorno** | Proyectiles que vuelven y golpean otra vez; efectos «al volver» (recarga, escudo) | Modificador de Trayectoria nuevo | 1·Retorno, 4 |
| 4 | **Rebote** | Rebotan en suelo/enemigos hacia el siguiente | Modificador de Trayectoria nuevo | 1·Rebote |
| 5 | **Marcas** | Unos golpes marcan, otras armas consumen marcas (caen solas, persiguen, detonan) | Estado «marcado» + consumidores | 6·Marcas (y el homing del cometa) |
| 6 | **Segunda arma / sinergias entre armas** | Dos armas que interactúan por reglas genéricas («proyectil que entra en tu zona hereda su efecto») | Segundo hueco de arma en `WeaponInventory` | 2·Catapulta |
| 7 | **Estelas y zonas** | Los ataques dejan fuego/hielo en el mundo un tiempo | Componente de zona con pool | 4·Fuego, 6·Fuego |
| 8 | **Trampas / detonador** | Proyectiles que se quedan como minas | Trayectoria «lanzada» + estado mina | 5·Proximidad |
| 9 | División al tocar / racimo | Un objetivo → zona | Amplía el modificador de Forma | 3·Prisma, 5·Racimo |
| 10 | Copias y eco | Duplicar o repetir el ataque | Forma | 4·Doble |
| 11 | Canalización / sobrecarga | Armas de mantener con etapas | Nuevo modo de disparo (hoy: tocar o cargar) | ideas del 3 |
| 12 | Robo de vida por contacto | Lifesteal legible en armas de canalización | La etiqueta existe | 3·Robo de vida |

**Recomendación**: empezar por 1 + 2 (son las que más multiplican: dan sentido a las etiquetas que ya existen) y por 3/4, que
encajan directamente como Trayectorias del pipeline actual. 6 (segunda arma) es el cambio más grande de diseño: decidir antes de
diseñar más armas, porque condiciona si las armas nuevas son «principales» o «habilidades».

## Transformaciones propuestas (familias)

- **Constelación** (astral): cada 5º disparo invoca un golpe estelar pequeño.
- **Glaciar** (hielo): los congelados estallan al morir → añicos en cadena.
- **Vacío** (gravedad): las muertes dejan una mini singularidad que atrae 0,5 s.
- **Forja** (oro): cada moneda recogida carga el siguiente disparo.
- **Brasa** (fuego): los quemados propagan el fuego al tocar a otro enemigo.

## Preguntas abiertas para el usuario

1. ¿Dos armas a la vez (principal + secundaria) o una arma + habilidades con recarga? (decide el sistema 6 y cómo se diseñan las armas nuevas).
2. ¿Las transformaciones cambian el aspecto del player (como Isaac) o sólo el del cristal/disparos?
3. Reacciones: ¿sólo Hielo y Fuego para el prototipo, o entra un tercero (Eléctrico está reservado en `BuildTag`)?
