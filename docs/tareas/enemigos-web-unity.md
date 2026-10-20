# Llevar los enemigos de prueba (web) a Unity — pendiente

**Objetivo:** encontrar un buen camino, repetible, para pasar un enemigo diseñado en la jugable web de enemigos
(`docs/bocetos/enemigos/jugable.html`, fichas en `docs/enemigos-diseno/`) a un enemigo jugable en Unity: comportamiento
+ arte por piezas + animación. Pedido por el usuario al cerrar la sesión del 2026-10-20.

## Estado (2026-10-20)
- **Diseño hecho en la web:** 31 enemigos (genéricos, hielo, bosque, volcán; comunes y élites), cada uno con ficha
  generada (`docs/bocetos/enemigos/tools/gen-fichas.cjs`) y pruebas sin navegador (`tools/test.cjs`, 225 casos).
  Sin probar en Unity ninguno. Feedback del usuario: le gustan los sets de bosque y volcán; el Erizo le encantó.
- **Arte, primera prueba:** Golem de escoria. Piezas acabadas en `docs/enemigos-diseno/arte/golem-escoria/` (README con
  huecos de plantilla, rótulas y orden de dibujo). El usuario lo va a montar y animar él en 🧬 Enemy Studio.
  Las animaciones/poses hechas por Claude no le convencieron: **no animar por él; preparar piezas.**
- Lo aprendido del arte ya está en la receta `.claude/skills/make-enemy/` (SKILL §2 «Cut for MOVEMENT», `pose-preview.mjs`)
  y `base-prompt-gemini.md` (bloque VIEW de perfil, hombros con hueco, descargar la imagen).

## Lo que falta decidir / construir
1. **Comportamiento → Unity.** Cada pieza genérica del motor web tiene que existir como movimiento / ataque genérico de
   Unity (`docs/architecture/enemies.md`: `EnemyBrain`, baraja `BossAttack`, `EnemyDefence`). Lista de lo que falta en
   `docs/enemigos-diseno/README.md` ▸ «Cómo se pasa un enemigo a Unity» (aguante/interrupción, turnos de ataque, no atacar
   fuera de pantalla, `roam`/`dart` entre ataques, escudo frontal, madriguera, teletransporte, invocar, kamikaze que daña a
   enemigos, charco, salto a plataformas, congelar 1 s, pincho que se queda, viento, arrastrar al player, posarse, guardia
   con contraataque, objetos que tapan disparos, renacer de un huevo…). Decidir el orden: empezar por las que sirven a más
   enemigos.
2. **Decisión abierta del usuario:** dash invulnerable (los diseños cuentan con él; el player de Unity no lo tiene).
3. **Arte:** base de más resolución (el JPEG de 1024 de Gemini se ve pixelado al subirlo); plantillas de rig que faltan
   para cuadrúpedos (Escorpión, Salamandra, lobos), cuerpos en cadena (Serpiente) y voladores con alas.
4. **Datos:** los `tuning` de cada `e-*.js` pasan con el mismo nombre (px × 0,025 = u). Valorar un exportador
   ficha/tuning → brief de `make-enemy.mjs` (`Web/RedMagicWeb/tools/comfy/enemies/<Name>.json`) para no copiar a mano.
5. **Primer enemigo de punta a punta:** el Golem de escoria (piezas listas + animación del usuario + sus 3 ataques:
   alzar pilar, reventar pilar, golpe al suelo; el pilar es un objeto nuevo). Medir cuánto cuesta y escribir el flujo.
