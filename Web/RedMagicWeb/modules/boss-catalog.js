// GENERADO por Unity (BossCatalogExporter.cs) — no editar a mano.
// Se regenera al compilar y desde Tools > Web > Exportar catálogo de jefes.
export const BOSS_CATALOG = {
  "version": 1,
  "common": [
    {
      "name": "displayName",
      "label": "Display Name",
      "header": "Identidad",
      "type": "string",
      "default": "Ataque"
    },
    {
      "name": "description",
      "label": "Description",
      "type": "string",
      "default": ""
    },
    {
      "name": "accent",
      "label": "Accent",
      "tooltip": "Color del aviso y de los efectos de este ataque.",
      "type": "color",
      "default": "#8ce666ff"
    },
    {
      "name": "telegraph",
      "label": "Telegraph",
      "tooltip": "Segundos de aviso antes de que el ataque haga daño. Es lo que lo hace esquivable: súbelo si el patrón es difícil de leer, bájalo para agobiar.",
      "header": "Ritmo",
      "min": 0.0,
      "type": "float",
      "default": 0.8
    },
    {
      "name": "recovery",
      "label": "Recovery",
      "tooltip": "Segundos que el jefe se queda quieto al terminar. Es la ventana de daño del jugador: cuanto más brutal el ataque, más larga.",
      "min": 0.0,
      "type": "float",
      "default": 1.1
    },
    {
      "name": "weight",
      "label": "Weight",
      "tooltip": "Peso en el sorteo del siguiente ataque dentro de la fase.",
      "min": 0.01,
      "type": "float",
      "default": 1.0
    },
    {
      "name": "cooldownInAttacks",
      "label": "Cooldown In Attacks",
      "tooltip": "Ataques que tienen que pasar antes de que este pueda repetirse. 1 = nunca dos seguidos. Evita que el sorteo encadene tres veces el mismo patrón.",
      "min": 0.0,
      "type": "int",
      "default": 1
    },
    {
      "name": "cooldownSeconds",
      "label": "Cooldown Seconds",
      "tooltip": "Segundos desde que se lanzó antes de que pueda volver a salir en el sorteo. Se suma a 'cooldownInAttacks' (tienen que cumplirse los dos). 0 = sin espera por tiempo.",
      "min": 0.0,
      "type": "float",
      "default": 0.0
    },
    {
      "name": "minPhase",
      "label": "Min Phase",
      "tooltip": "Fase (1 = la primera) a partir de la cual este ataque puede salir en el sorteo. Por debajo nunca se elige, aunque esté en la baraja de esa fase. 1 = siempre.",
      "header": "Fases",
      "min": 1.0,
      "type": "int",
      "default": 1
    },
    {
      "name": "damage",
      "label": "Damage",
      "header": "Daño",
      "min": 0.0,
      "type": "float",
      "default": 18.0
    },
    {
      "name": "knockbackMultiplier",
      "label": "Knockback Multiplier",
      "tooltip": "Cuánto empuja: multiplica el retroceso configurado en el Knockback de la víctima.",
      "min": 0.0,
      "type": "float",
      "default": 1.0
    },
    {
      "name": "vulnerableSeconds",
      "label": "Vulnerable Seconds",
      "tooltip": "Segundos que el jefe queda EXPUESTO al acabar este ataque, durante su recuperación. 0 = no abre ventana.\n\nEs lo que convierte 'esquiva y espera' en 'esquiva y castiga': con un jefe acorazado (la armadura de la fase), pegarle fuera de la ventana casi no vale, así que el ataque más peligroso pasa a ser también la oportunidad.",
      "header": "Castigo",
      "min": 0.0,
      "type": "float",
      "default": 0.0
    },
    {
      "name": "vulnerableMultiplier",
      "label": "Vulnerable Multiplier",
      "tooltip": "Cuánto multiplica el daño recibido durante la ventana, por encima de la armadura de la fase.",
      "min": 1.0,
      "type": "float",
      "default": 2.5
    },
    {
      "name": "gesture",
      "label": "Gesture",
      "tooltip": "Estado del Animator del jefe que hace de aviso de este ataque ('Charge', 'Slam'…). Vacío = sin gesto: el aviso es sólo el aura y las marcas del ataque.\n\nCon gesto (y un BossAnimator en el jefe) el cuerpo ES el aviso: el clip se acelera o frena para que su frame de suelta — el evento OnAttackRelease que el pipeline planta en el 'releaseFrame' de esa fila — caiga justo al acabar 'telegraph', y el ataque arranca en ese frame exacto. 'telegraph' sigue siendo el único mando de tiempo, así que la fase 2, al acortarlo con speedScale, acelera también el gesto.",
      "header": "Gesto",
      "type": "string",
      "default": ""
    },
    {
      "name": "sfxId",
      "label": "Sfx Id",
      "tooltip": "id de sonido del AudioManager al lanzar el ataque. Vacío = sin sonido.",
      "header": "Presencia",
      "type": "string",
      "default": ""
    },
    {
      "name": "shakeAmplitude",
      "label": "Shake Amplitude",
      "tooltip": "Sacudida de cámara al lanzar el ataque. 0 = ninguna.",
      "min": 0.0,
      "type": "float",
      "default": 0.0
    },
    {
      "name": "shakeDuration",
      "label": "Shake Duration",
      "min": 0.0,
      "type": "float",
      "default": 0.25
    }
  ],
  "attacks": [
    {
      "type": "AnchorRitualAttack",
      "label": "Anchor Ritual",
      "params": [
        {
          "name": "anchorCount",
          "label": "Anchor Count",
          "header": "Anclas",
          "min": 1.0,
          "type": "int",
          "default": 3
        },
        {
          "name": "anchorHealth",
          "label": "Anchor Health",
          "tooltip": "Vida de cada ancla. Es el mando principal: mide cuánto daño hay que apartar del jefe para salvar el ritual.",
          "min": 1.0,
          "type": "float",
          "default": 120.0
        },
        {
          "name": "anchorSize",
          "label": "Anchor Size",
          "type": "vector2",
          "default": {
            "x": 1.3,
            "y": 1.6
          }
        },
        {
          "name": "anchorSprite",
          "label": "Anchor Sprite",
          "tooltip": "Sprite del ancla. Vacío = el del jefe.",
          "type": "unity",
          "unityType": "Sprite"
        },
        {
          "name": "anchorHeight",
          "label": "Anchor Height",
          "tooltip": "Altura sobre el suelo a la que se plantan.",
          "min": 0.0,
          "type": "float",
          "default": 0.9
        },
        {
          "name": "minSeparation",
          "label": "Min Separation",
          "tooltip": "Separación mínima entre anclas. Cuanto más grande, más hay que correr para llegar a todas y menos vale un arma de área.",
          "min": 0.0,
          "type": "float",
          "default": 6.0
        },
        {
          "name": "edgeMargin",
          "label": "Edge Margin",
          "min": 0.0,
          "type": "float",
          "default": 2.5
        },
        {
          "name": "ritualSeconds",
          "label": "Ritual Seconds",
          "tooltip": "Segundos para romperlas todas.",
          "header": "Ritual",
          "min": 1.0,
          "type": "float",
          "default": 9.0
        },
        {
          "name": "damageTakenDuringRitual",
          "label": "Damage Taken During Ritual",
          "tooltip": "Daño que recibe el jefe mientras el ritual está en pie. Prácticamente nada: es lo que empuja a ir a por las anclas.",
          "min": 0.0,
          "max": 1.0,
          "type": "float",
          "default": 0.05
        },
        {
          "name": "dischargeDamage",
          "label": "Discharge Damage",
          "tooltip": "Daño del estallido que cubre la arena si no se rompen a tiempo.",
          "header": "Si se completa",
          "min": 0.0,
          "type": "float",
          "default": 34.0
        },
        {
          "name": "dischargeKnockback",
          "label": "Discharge Knockback",
          "min": 0.0,
          "type": "float",
          "default": 2.0
        },
        {
          "name": "dischargeWarning",
          "label": "Discharge Warning",
          "tooltip": "Segundos de aviso entre que el ritual se completa y estalla. Corto: es un castigo, no otro patrón que esquivar.",
          "min": 0.1,
          "type": "float",
          "default": 0.7
        }
      ]
    },
    {
      "type": "BulletHellAttack",
      "label": "Bullet Hell",
      "params": [
        {
          "name": "pattern",
          "label": "Pattern",
          "header": "Patrón",
          "type": "enum",
          "options": [
            "Radial",
            "Fan",
            "Rain",
            "Storm"
          ],
          "default": "Radial"
        },
        {
          "name": "projectile",
          "label": "Projectile",
          "tooltip": "Cómo vuela cada proyectil. El mismo bloque que usan las habilidades del jugador.",
          "type": "object",
          "fields": [
            {
              "name": "prefab",
              "label": "Prefab",
              "tooltip": "Prefab con componente Projectile. Vacío = se construye uno en código con el sprite y el color de la habilidad.",
              "type": "art",
              "artKind": "projectile",
              "artLabel": "Projectile",
              "placeholder": "bola de color de la fase",
              "default": null
            },
            {
              "name": "speed",
              "label": "Speed",
              "min": 0.1,
              "type": "float",
              "default": 12.0
            },
            {
              "name": "lifetime",
              "label": "Lifetime",
              "tooltip": "Segundos de vuelo antes de desaparecer solo.",
              "min": 0.05,
              "type": "float",
              "default": 2.5
            },
            {
              "name": "size",
              "label": "Size",
              "tooltip": "Tamaño del proyectil en unidades del mundo. Con arte del importador de jefes (FxArtSize) manda el ANCHO (X) y el collider escala con él. Un prefab hecho a mano sin FxArtSize usa su propia escala.",
              "type": "vector2",
              "default": {
                "x": 0.35,
                "y": 0.35
              }
            },
            {
              "name": "muzzleOffset",
              "label": "Muzzle Offset",
              "tooltip": "Salida del disparo respecto al lanzador. La X se invierte según hacia dónde mira.",
              "type": "vector2",
              "default": {
                "x": 0.65,
                "y": 0.1
              }
            },
            {
              "name": "pierce",
              "label": "Pierce",
              "tooltip": "Objetivos que atraviesa antes de desaparecer.",
              "header": "Comportamiento",
              "min": 0.0,
              "type": "int",
              "default": 0
            },
            {
              "name": "homingTurnRate",
              "label": "Homing Turn Rate",
              "tooltip": "Grados/segundo de giro hacia el objetivo más cercano. 0 = va recto.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "homingRange",
              "label": "Homing Range",
              "min": 0.0,
              "type": "float",
              "default": 9.0
            },
            {
              "name": "arcGravity",
              "label": "Arc Gravity",
              "tooltip": "Caída en unidades/s². >0 = granada con trayectoria parabólica.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "impactRadius",
              "label": "Impact Radius",
              "header": "Explosión al terminar",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "impactDamage",
              "label": "Impact Damage",
              "tooltip": "Daño de la explosión. Se suma al impacto directo sólo si el radio es > 0.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "faceDirection",
              "label": "Face Direction",
              "tooltip": "Rota el proyectil para que su punta (Facing Axis) mire hacia donde vuela, al salir y en cada paso (autoguiado, parábola). Apagado = decide el prefab (su casilla 'Face Travel Direction'), así los proyectiles que ya existían no cambian.",
              "header": "Aiming",
              "type": "bool",
              "default": false
            },
            {
              "name": "facingAxis",
              "label": "Facing Axis",
              "tooltip": "Lado del sprite que es la punta. Right = arte dibujado mirando a +X (convención).",
              "type": "enum",
              "options": [
                "Right",
                "Left",
                "Up",
                "Down"
              ],
              "default": "Right"
            },
            {
              "name": "aimMode",
              "label": "Aim Mode",
              "tooltip": "Fixed = la dirección que da quien dispara. MouseDirection / NearestEnemy sólo para el jugador (cursor / enemigo más cercano al salir, sin perseguir); en un enemigo equivalen a Fixed.",
              "type": "enum",
              "options": [
                "Fixed",
                "MouseDirection",
                "NearestEnemy"
              ],
              "default": "Fixed"
            }
          ],
          "spec": "projectile"
        },
        {
          "name": "volleys",
          "label": "Volleys",
          "header": "Oleadas",
          "min": 1.0,
          "type": "int",
          "default": 3
        },
        {
          "name": "timeBetweenVolleys",
          "label": "Time Between Volleys",
          "min": 0.02,
          "type": "float",
          "default": 0.35
        },
        {
          "name": "bulletsPerVolley",
          "label": "Bullets Per Volley",
          "min": 1.0,
          "type": "int",
          "default": 12
        },
        {
          "name": "arcDegrees",
          "label": "Arc Degrees",
          "tooltip": "Apertura total en grados. 360 en Radial = anillo completo.",
          "header": "Forma (Radial / Fan)",
          "min": 0.0,
          "max": 360.0,
          "type": "float",
          "default": 360.0
        },
        {
          "name": "startAngle",
          "label": "Start Angle",
          "tooltip": "Ángulo base cuando no se apunta al jugador (0 = a la derecha).",
          "type": "float",
          "default": 0.0
        },
        {
          "name": "spinPerVolley",
          "label": "Spin Per Volley",
          "tooltip": "Grados que rota el patrón en cada oleada. Es lo que convierte un anillo en una espiral: con un valor que NO divida a 360 el hueco se desplaza y hay que seguirlo.",
          "type": "float",
          "default": 11.0
        },
        {
          "name": "alternateSpin",
          "label": "Alternate Spin",
          "tooltip": "Invierte el giro en cada oleada. El patrón se cruza consigo mismo y deja huecos que abren y cierran, en vez de una espiral previsible.",
          "type": "bool",
          "default": false
        },
        {
          "name": "randomSpread",
          "label": "Random Spread",
          "tooltip": "Desviación aleatoria por proyectil, en grados.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "aimAtPlayer",
          "label": "Aim At Player",
          "tooltip": "Centra el patrón en el jugador. En Fan es lo normal; en Radial gira el anillo entero.",
          "type": "bool",
          "default": true
        },
        {
          "name": "originOffset",
          "label": "Origin Offset",
          "tooltip": "Desplazamiento del centro del patrón respecto al jefe. No se voltea con la mirada.",
          "header": "Origen (Radial / Fan)",
          "type": "vector2",
          "default": {
            "x": 0.0,
            "y": 2.2
          }
        },
        {
          "name": "spawnRadius",
          "label": "Spawn Radius",
          "tooltip": "Radio del círculo sobre el que nacen los proyectiles. Hace que el anillo se lea como algo que emana de la copa, y no como un punto que escupe.",
          "min": 0.0,
          "type": "float",
          "default": 1.2
        },
        {
          "name": "rainSpan",
          "label": "Rain Span",
          "tooltip": "Fracción de la anchura de la arena que cubre cada oleada. 1 = de lado a lado; por encima de 1 se sale un poco de la arena (para cubrir una plataforma que asoma).",
          "header": "Lluvia",
          "min": 0.1,
          "max": 2.0,
          "type": "float",
          "default": 1.0
        },
        {
          "name": "rainCenterOffset",
          "label": "Rain Center Offset",
          "tooltip": "Desplaza en X el centro de la franja de lluvia respecto al jefe, en unidades. 0 = centrada en el jefe.",
          "type": "float",
          "default": 0.0
        },
        {
          "name": "rainRandomX",
          "label": "Rain Random X",
          "tooltip": "Cada gota en una X al azar dentro de la franja, en vez de repartidas en rejilla (+ 'rainJitter').",
          "type": "bool",
          "default": false
        },
        {
          "name": "rainDropStagger",
          "label": "Rain Drop Stagger",
          "tooltip": "Segundos entre una gota y la siguiente dentro de la misma oleada (en rejilla, de izquierda a derecha). 0 = todas a la vez. Cada marca dura hasta que cae su gota.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "rainMarkerSeconds",
          "label": "Rain Marker Seconds",
          "tooltip": "Segundos que la marca del suelo está visible antes de que caiga el proyectil.",
          "min": 0.05,
          "type": "float",
          "default": 0.5
        },
        {
          "name": "rainJitter",
          "label": "Rain Jitter",
          "tooltip": "Desorden horizontal de cada gota respecto a su hueco. 0 = rejilla perfecta (fácil de leer), alto = caos.",
          "min": 0.0,
          "type": "float",
          "default": 0.8
        },
        {
          "name": "rainMarkerSize",
          "label": "Rain Marker Size",
          "tooltip": "Tamaño de la marca de aviso en el suelo.",
          "type": "vector2",
          "default": {
            "x": 1.0,
            "y": 0.35
          }
        },
        {
          "name": "stormSeconds",
          "label": "Storm Seconds",
          "tooltip": "Segundos que dura la tormenta. NO lo acorta el ritmo de la fase: es lo que dura.",
          "header": "Tormenta (Storm) — usa también rainSpan / rainCenterOffset / rainMarkerSize",
          "min": 0.1,
          "type": "float",
          "default": 5.0
        },
        {
          "name": "stormPerSecond",
          "label": "Storm Per Second",
          "tooltip": "Proyectiles por segundo mientras dura. Tampoco lo cambia el ritmo de la fase.",
          "min": 0.1,
          "type": "float",
          "default": 7.0
        },
        {
          "name": "stormAngleRange",
          "label": "Storm Angle Range",
          "tooltip": "Desviación máxima respecto a la vertical, en grados (± al azar por proyectil). 0 = todos rectos hacia abajo. La X al azar es la de LLEGADA, así que las diagonales siguen cubriendo toda la franja. Ojo: projectile.lifetime tiene que cubrir la caída más larga (alto de la arena / cos(ángulo) / speed) o se apagan en el aire.",
          "min": 0.0,
          "max": 60.0,
          "type": "float",
          "default": 18.0
        },
        {
          "name": "stormWind",
          "label": "Storm Wind",
          "tooltip": "Viento: inclinación común a todos, en grados. + = caen hacia la derecha.",
          "min": -45.0,
          "max": 45.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "stormMarkLanding",
          "label": "Storm Mark Landing",
          "tooltip": "Marca en el suelo dónde va a caer cada proyectil, desde que nace hasta que llega.",
          "type": "bool",
          "default": true
        },
        {
          "name": "projectileSprite",
          "label": "Projectile Sprite",
          "tooltip": "Sprite de los proyectiles de ESTE patrón. Vacío = el del jefe (BossController.fxSprite). Está separado del sprite del jefe para que cada ataque pueda tirar de su propio objeto —calabazas, abrojos, piedras— sin que los avisos y las ondas dejen de ser rectángulos legibles.\n\nOjo: los proyectiles NO giran hacia su dirección de vuelo, así que aquí sólo funcionan sprites redondeados; uno alargado (una espada, una lanza) volaría de lado.",
          "header": "Arte",
          "type": "unity",
          "unityType": "Sprite"
        }
      ]
    },
    {
      "type": "ChargeDashAttack",
      "label": "Charge Dash",
      "params": [
        {
          "name": "speed",
          "label": "Speed",
          "tooltip": "Velocidad de la embestida, unidades/s. Se multiplica por el ritmo de la fase.",
          "header": "Recorrido",
          "min": 1.0,
          "type": "float",
          "default": 22.0
        },
        {
          "name": "overshoot",
          "label": "Overshoot",
          "tooltip": "Cuánto sigue después de pasar por donde estaba el jugador. 0 = frena en su sitio.",
          "min": 0.0,
          "type": "float",
          "default": 4.0
        },
        {
          "name": "maxDistance",
          "label": "Max Distance",
          "tooltip": "Recorrido máximo. 0 = hasta donde haga falta (siempre dentro de la arena).",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "arenaMargin",
          "label": "Arena Margin",
          "tooltip": "Distancia a los bordes de la arena donde frena, para no meterse en la pared.",
          "min": 0.0,
          "type": "float",
          "default": 1.5
        },
        {
          "name": "lockSeconds",
          "label": "Lock Seconds",
          "tooltip": "Segundos que la flecha marca la dirección YA FIJADA antes de salir. Es el margen para apartarse: súbelo si es injusto, bájalo para agobiar.",
          "header": "Aviso",
          "min": 0.0,
          "type": "float",
          "default": 0.4
        },
        {
          "name": "warnWidth",
          "label": "Warn Width",
          "tooltip": "Grosor de la flecha / barra de aviso.",
          "min": 0.1,
          "type": "float",
          "default": 1.6
        },
        {
          "name": "hitboxOffset",
          "label": "Hitbox Offset",
          "tooltip": "Caja de daño relativa al jefe mientras viaja (X hacia delante, Y desde los pies).",
          "header": "Golpe",
          "type": "vector2",
          "default": {
            "x": 0.6,
            "y": 1.8
          }
        },
        {
          "name": "hitboxSize",
          "label": "Hitbox Size",
          "type": "vector2",
          "default": {
            "x": 3.2,
            "y": 3.2
          }
        },
        {
          "name": "travelState",
          "label": "Travel State",
          "tooltip": "Estado del Animator que se MANTIENE mientras viaja (una pose de embestida en bucle, p.ej. 'DashLoop'). Vacío = el gesto sigue su curso.",
          "header": "Animación",
          "type": "string",
          "default": ""
        },
        {
          "name": "launchFxPrefab",
          "label": "Launch Fx Prefab",
          "tooltip": "Efecto de un solo uso a los pies al salir (polvo, estela). Vacío = nada.",
          "header": "Arte",
          "type": "art",
          "artKind": "fx",
          "artLabel": "Efecto al arrancar",
          "placeholder": "nada",
          "default": null
        },
        {
          "name": "hitFxPrefab",
          "label": "Hit Fx Prefab",
          "tooltip": "Efecto de un solo uso donde golpea. Vacío = destello del color de la fase.",
          "type": "art",
          "artKind": "fx",
          "artLabel": "Impacto al arrollar",
          "placeholder": "destello de color",
          "default": null
        }
      ]
    },
    {
      "type": "GroundSlamAttack",
      "label": "Ground Slam",
      "params": [
        {
          "name": "aimAtPlayer",
          "label": "Aim At Player",
          "tooltip": "Marca el sitio donde está el jugador. Apagado, golpea siempre a sus pies.",
          "header": "Objetivo",
          "type": "bool",
          "default": true
        },
        {
          "name": "maxReach",
          "label": "Max Reach",
          "tooltip": "Distancia máxima a la que el jefe puede alcanzar desde su sitio. 0 = toda la arena.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "radius",
          "label": "Radius",
          "tooltip": "Radio del cráter.",
          "header": "Golpe",
          "min": 0.5,
          "type": "float",
          "default": 3.4
        },
        {
          "name": "windup",
          "label": "Windup",
          "tooltip": "Segundos entre que se fija la marca y cae el puño. Es TODO el margen que hay para apartarse, así que es el mando de dificultad de este ataque.",
          "min": 0.1,
          "type": "float",
          "default": 0.75
        },
        {
          "name": "height",
          "label": "Height",
          "tooltip": "Altura del cráter sobre el suelo. Cubre a un jugador de pie; subirlo hace que saltar tampoco salve.",
          "min": 0.5,
          "type": "float",
          "default": 3.0
        },
        {
          "name": "slams",
          "label": "Slams",
          "header": "Repetición",
          "min": 1.0,
          "type": "int",
          "default": 1
        },
        {
          "name": "timeBetweenSlams",
          "label": "Time Between Slams",
          "tooltip": "Segundos entre puñetazos. Con varios, cada uno vuelve a marcar dónde estás.",
          "min": 0.1,
          "type": "float",
          "default": 0.9
        },
        {
          "name": "rubbleSeconds",
          "label": "Rubble Seconds",
          "tooltip": "Segundos que el cráter sigue haciendo daño. 0 = no deja nada.",
          "header": "Escombro",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "rubbleDamagePerTick",
          "label": "Rubble Damage Per Tick",
          "min": 0.0,
          "type": "float",
          "default": 6.0
        },
        {
          "name": "rubbleTickInterval",
          "label": "Rubble Tick Interval",
          "min": 0.05,
          "type": "float",
          "default": 0.5
        },
        {
          "name": "impactFxPrefab",
          "label": "Impact Fx Prefab",
          "tooltip": "Efecto de un solo uso (pooled, con VfxOneShot) que sale en el suelo del cráter: la onda de raíces, la púa que brota. Su pivote debe ir abajo (se planta en el suelo). Vacío = el rectángulo de siempre con el color de la fase.",
          "header": "Arte",
          "type": "art",
          "artKind": "fx",
          "artLabel": "Impact Fx Prefab",
          "placeholder": "el de por defecto del ataque",
          "default": null
        },
        {
          "name": "fitFxToRadius",
          "label": "Fit Fx To Radius",
          "tooltip": "Escala el efecto para que su ancho sea el del cráter (2 × radio). Así cambiar el radio no deja el dibujo más grande o más pequeño que lo que duele. Apagado = la escala del prefab tal cual.",
          "type": "bool",
          "default": true
        },
        {
          "name": "fxLeadSeconds",
          "label": "Fx Lead Seconds",
          "tooltip": "Segundos que el efecto sale ANTES del golpe. Para un efecto que tarda en llegar a su pico — una púa que brota —: el daño cae cuando el dibujo está arriba del todo, no mientras aún asoma. Nunca más que 'windup'.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        }
      ]
    },
    {
      "type": "GuardStanceAttack",
      "label": "Guard Stance",
      "params": [
        {
          "name": "guardSeconds",
          "label": "Guard Seconds",
          "tooltip": "Segundos que aguanta cubierto.",
          "header": "Guardia",
          "min": 0.2,
          "type": "float",
          "default": 2.6
        },
        {
          "name": "damageTakenWhileGuarding",
          "label": "Damage Taken While Guarding",
          "tooltip": "Daño que recibe mientras se cubre, como fracción del normal. No se pone a 0 a propósito: el golpe tiene que entrar para poder devolverse, y ver saltar un número ridículo es parte del mensaje.",
          "min": 0.01,
          "max": 1.0,
          "type": "float",
          "default": 0.12
        },
        {
          "name": "reflectDamage",
          "label": "Reflect Damage",
          "tooltip": "Daño fijo que devuelve por cada golpe recibido. Fijo y no proporcional porque así castiga al que machaca el botón, que es justo el comportamiento a corregir.",
          "min": 0.0,
          "type": "float",
          "default": 9.0
        },
        {
          "name": "reflectKnockback",
          "label": "Reflect Knockback",
          "min": 0.0,
          "type": "float",
          "default": 1.2
        },
        {
          "name": "pulseInterval",
          "label": "Pulse Interval",
          "tooltip": "Cada cuánto repinta la marca de 'cubierto' mientras dura.",
          "header": "Aviso",
          "min": 0.05,
          "type": "float",
          "default": 0.25
        },
        {
          "name": "counterAttack",
          "label": "Counter Attack",
          "tooltip": "Al bajar la guardia queda expuesto (los campos 'Castigo' de este mismo asset). Apagado, simplemente vuelve a la normalidad.",
          "type": "bool",
          "default": true
        }
      ]
    },
    {
      "type": "HazardFieldAttack",
      "label": "Hazard Field",
      "params": [
        {
          "name": "zonesPerWave",
          "label": "Zones Per Wave",
          "header": "Trozos",
          "min": 1.0,
          "type": "int",
          "default": 3
        },
        {
          "name": "zoneSize",
          "label": "Zone Size",
          "tooltip": "Tamaño de cada trozo de suelo envenenado.",
          "type": "vector2",
          "default": {
            "x": 3.4,
            "y": 1.0
          }
        },
        {
          "name": "zoneSeconds",
          "label": "Zone Seconds",
          "tooltip": "Segundos que se queda. Cuanto más dure, más se nota que la arena encoge.",
          "min": 0.5,
          "type": "float",
          "default": 9.0
        },
        {
          "name": "damagePerTick",
          "label": "Damage Per Tick",
          "tooltip": "Daño por tic. Bajo a propósito: esto está para estorbar, no para matar.",
          "header": "Daño",
          "min": 0.0,
          "type": "float",
          "default": 7.0
        },
        {
          "name": "tickInterval",
          "label": "Tick Interval",
          "min": 0.05,
          "type": "float",
          "default": 0.5
        },
        {
          "name": "tickKnockbackMultiplier",
          "label": "Tick Knockback Multiplier",
          "min": 0.0,
          "type": "float",
          "default": 0.4
        },
        {
          "name": "spread",
          "label": "Spread",
          "tooltip": "Fracción de la arena que se puede sembrar. 1 = de lado a lado.",
          "header": "Reparto",
          "min": 0.1,
          "max": 1.0,
          "type": "float",
          "default": 1.0
        },
        {
          "name": "minSeparation",
          "label": "Min Separation",
          "tooltip": "Separación mínima entre trozos, para que no se solapen en un muro continuo.",
          "min": 0.0,
          "type": "float",
          "default": 4.0
        },
        {
          "name": "avoidPlayerRadius",
          "label": "Avoid Player Radius",
          "tooltip": "No siembra a menos de esta distancia del jugador. Quitar sitio es justo; aparecer bajo sus pies, no.",
          "min": 0.0,
          "type": "float",
          "default": 2.5
        },
        {
          "name": "waves",
          "label": "Waves",
          "header": "Ritmo",
          "min": 1.0,
          "type": "int",
          "default": 1
        },
        {
          "name": "timeBetweenWaves",
          "label": "Time Between Waves",
          "min": 0.1,
          "type": "float",
          "default": 0.8
        },
        {
          "name": "markerSeconds",
          "label": "Marker Seconds",
          "tooltip": "Segundos que se marca el sitio antes de que aparezca el trozo.",
          "min": 0.05,
          "type": "float",
          "default": 0.6
        }
      ]
    },
    {
      "type": "MeleeStrikeAttack",
      "label": "Melee Strike",
      "params": [
        {
          "name": "hits",
          "label": "Hits",
          "header": "Golpes",
          "min": 1.0,
          "type": "int",
          "default": 1
        },
        {
          "name": "timeBetweenHits",
          "label": "Time Between Hits",
          "tooltip": "Segundos entre golpes (con varios). Cuadra con los frames de golpe del dibujo.",
          "min": 0.02,
          "type": "float",
          "default": 0.25
        },
        {
          "name": "hitboxOffset",
          "label": "Hitbox Offset",
          "tooltip": "Centro de la caja de daño: X hacia delante, Y hacia arriba desde los pies.",
          "header": "Alcance (relativo al jefe, X hacia el jugador)",
          "type": "vector2",
          "default": {
            "x": 2.4,
            "y": 1.8
          }
        },
        {
          "name": "hitboxSize",
          "label": "Hitbox Size",
          "type": "vector2",
          "default": {
            "x": 3.2,
            "y": 2.6
          }
        },
        {
          "name": "warnHitbox",
          "label": "Warn Hitbox",
          "tooltip": "Pinta la caja de daño durante el aviso. Apágalo si el gesto ya se lee solo.",
          "type": "bool",
          "default": true
        },
        {
          "name": "launchProjectile",
          "label": "Launch Projectile",
          "tooltip": "El golpe lanza además un proyectil (la onda del puñetazo).",
          "header": "Proyectil del golpe (opcional)",
          "type": "bool",
          "default": false
        },
        {
          "name": "projectileEveryHit",
          "label": "Projectile Every Hit",
          "tooltip": "Sólo en el primer golpe (apagado) o en cada golpe (encendido).",
          "type": "bool",
          "default": false
        },
        {
          "name": "aimAtPlayer",
          "label": "Aim At Player",
          "tooltip": "Apunta al jugador. Apagado = recto hacia delante.",
          "type": "bool",
          "default": false
        },
        {
          "name": "projectileDamage",
          "label": "Projectile Damage",
          "tooltip": "Daño del proyectil. 0 = el mismo que el golpe.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "projectile",
          "label": "Projectile",
          "tooltip": "Cómo vuela y cómo se ve el proyectil. prefab vacío = bola de color. 'muzzleOffset' es de dónde sale (X hacia delante).",
          "type": "object",
          "fields": [
            {
              "name": "prefab",
              "label": "Prefab",
              "tooltip": "Prefab con componente Projectile. Vacío = se construye uno en código con el sprite y el color de la habilidad.",
              "type": "art",
              "artKind": "projectile",
              "artLabel": "Proyectil del golpe",
              "placeholder": "bola de color de la fase",
              "default": null
            },
            {
              "name": "speed",
              "label": "Speed",
              "min": 0.1,
              "type": "float",
              "default": 14.0
            },
            {
              "name": "lifetime",
              "label": "Lifetime",
              "tooltip": "Segundos de vuelo antes de desaparecer solo.",
              "min": 0.05,
              "type": "float",
              "default": 1.6
            },
            {
              "name": "size",
              "label": "Size",
              "tooltip": "Tamaño del proyectil en unidades del mundo. Con arte del importador de jefes (FxArtSize) manda el ANCHO (X) y el collider escala con él. Un prefab hecho a mano sin FxArtSize usa su propia escala.",
              "type": "vector2",
              "default": {
                "x": 1.4,
                "y": 1.4
              }
            },
            {
              "name": "muzzleOffset",
              "label": "Muzzle Offset",
              "tooltip": "Salida del disparo respecto al lanzador. La X se invierte según hacia dónde mira.",
              "type": "vector2",
              "default": {
                "x": 3.0,
                "y": 1.8
              }
            },
            {
              "name": "pierce",
              "label": "Pierce",
              "tooltip": "Objetivos que atraviesa antes de desaparecer.",
              "header": "Comportamiento",
              "min": 0.0,
              "type": "int",
              "default": 0
            },
            {
              "name": "homingTurnRate",
              "label": "Homing Turn Rate",
              "tooltip": "Grados/segundo de giro hacia el objetivo más cercano. 0 = va recto.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "homingRange",
              "label": "Homing Range",
              "min": 0.0,
              "type": "float",
              "default": 9.0
            },
            {
              "name": "arcGravity",
              "label": "Arc Gravity",
              "tooltip": "Caída en unidades/s². >0 = granada con trayectoria parabólica.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "impactRadius",
              "label": "Impact Radius",
              "header": "Explosión al terminar",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "impactDamage",
              "label": "Impact Damage",
              "tooltip": "Daño de la explosión. Se suma al impacto directo sólo si el radio es > 0.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "faceDirection",
              "label": "Face Direction",
              "tooltip": "Rota el proyectil para que su punta (Facing Axis) mire hacia donde vuela, al salir y en cada paso (autoguiado, parábola). Apagado = decide el prefab (su casilla 'Face Travel Direction'), así los proyectiles que ya existían no cambian.",
              "header": "Aiming",
              "type": "bool",
              "default": false
            },
            {
              "name": "facingAxis",
              "label": "Facing Axis",
              "tooltip": "Lado del sprite que es la punta. Right = arte dibujado mirando a +X (convención).",
              "type": "enum",
              "options": [
                "Right",
                "Left",
                "Up",
                "Down"
              ],
              "default": "Right"
            },
            {
              "name": "aimMode",
              "label": "Aim Mode",
              "tooltip": "Fixed = la dirección que da quien dispara. MouseDirection / NearestEnemy sólo para el jugador (cursor / enemigo más cercano al salir, sin perseguir); en un enemigo equivalen a Fixed.",
              "type": "enum",
              "options": [
                "Fixed",
                "MouseDirection",
                "NearestEnemy"
              ],
              "default": "Fixed"
            }
          ],
          "spec": "projectile",
          "artLabel": "Proyectil del golpe"
        },
        {
          "name": "impactFxPrefab",
          "label": "Impact Fx Prefab",
          "tooltip": "Efecto de un solo uso (VfxOneShot) en el centro de la caja en cada golpe. Vacío = destello del color de la fase.",
          "header": "Arte",
          "type": "art",
          "artKind": "fx",
          "artLabel": "Impacto de cada golpe",
          "placeholder": "destello de color",
          "default": null
        },
        {
          "name": "impactFxWidth",
          "label": "Impact Fx Width",
          "tooltip": "Escala el efecto para que mida esto de ancho. 0 = escala del prefab.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        }
      ]
    },
    {
      "type": "OrbRingAttack",
      "label": "Orb Ring",
      "params": [
        {
          "name": "orbCount",
          "label": "Orb Count",
          "tooltip": "Orbes de la corona. También son los proyectiles que saldrán: más orbes = menos hueco.",
          "header": "Corona",
          "min": 1.0,
          "type": "int",
          "default": 10
        },
        {
          "name": "ringRadius",
          "label": "Ring Radius",
          "tooltip": "Radio de la corona alrededor del jefe, en unidades de mundo.",
          "min": 0.2,
          "type": "float",
          "default": 2.8
        },
        {
          "name": "originOffset",
          "label": "Origin Offset",
          "tooltip": "Centro de la corona respecto al origen del jefe (su base).",
          "type": "vector2",
          "default": {
            "x": 0.0,
            "y": 3.0
          }
        },
        {
          "name": "startAngle",
          "label": "Start Angle",
          "tooltip": "Ángulo del primer orbe. Con 'apuntar al jugador' encendido, se ignora.",
          "type": "float",
          "default": 0.0
        },
        {
          "name": "alignToPlayer",
          "label": "Align To Player",
          "tooltip": "Alinea la corona con el jugador, para que siempre haya un orbe apuntándole (y por tanto un hueco en un sitio predecible).",
          "type": "bool",
          "default": true
        },
        {
          "name": "spinDegreesPerSecond",
          "label": "Spin Degrees Per Second",
          "tooltip": "Grados por segundo que gira la corona mientras carga. El giro es lo que impide memorizar el hueco y quedarse quieto en él desde el principio.",
          "type": "float",
          "default": 35.0
        },
        {
          "name": "startScale",
          "label": "Start Scale",
          "tooltip": "Tamaño del orbe recién creado, como fracción del tamaño autorizado en su prefab. 0.15 = una chispa.",
          "header": "Crecimiento",
          "min": 0.02,
          "type": "float",
          "default": 0.15
        },
        {
          "name": "endScale",
          "label": "End Scale",
          "tooltip": "Tamaño al que se dispara, en las mismas unidades. 1 = el prefab tal cual está.",
          "min": 0.05,
          "type": "float",
          "default": 1.0
        },
        {
          "name": "codeOrbSize",
          "label": "Code Orb Size",
          "tooltip": "Tamaño en unidades de mundo del orbe cuando NO hay prefab y se construye en código. Con prefab se ignora.",
          "min": 0.05,
          "type": "float",
          "default": 0.7
        },
        {
          "name": "growSeconds",
          "label": "Grow Seconds",
          "tooltip": "Segundos que tarda un orbe en crecer del todo.",
          "min": 0.1,
          "type": "float",
          "default": 1.6
        },
        {
          "name": "appearStagger",
          "label": "Appear Stagger",
          "tooltip": "Retraso entre la aparición de un orbe y la del siguiente. Es lo que hace que se lean como creados uno a uno en vez de aparecer de golpe.",
          "min": 0.0,
          "type": "float",
          "default": 0.07
        },
        {
          "name": "holdSeconds",
          "label": "Hold Seconds",
          "tooltip": "Pausa con la corona ya completa y a tamaño máximo, justo antes de soltarla.",
          "min": 0.0,
          "type": "float",
          "default": 0.35
        },
        {
          "name": "recoilFraction",
          "label": "Recoil Fraction",
          "tooltip": "Cuánto se encoge la corona en el último instante antes de disparar. 0 = nada. Un pellizco hacia dentro hace que la salida se sienta como un latigazo.",
          "min": 0.0,
          "max": 0.9,
          "type": "float",
          "default": 0.18
        },
        {
          "name": "volleys",
          "label": "Volleys",
          "tooltip": "Coronas seguidas.",
          "header": "Disparo",
          "min": 1.0,
          "type": "int",
          "default": 1
        },
        {
          "name": "timeBetweenVolleys",
          "label": "Time Between Volleys",
          "tooltip": "Espera entre una corona y la siguiente.",
          "min": 0.05,
          "type": "float",
          "default": 0.9
        },
        {
          "name": "launchSpread",
          "label": "Launch Spread",
          "tooltip": "Dispersión aleatoria en grados al soltar. 0 = radial perfecto.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "launchMode",
          "label": "Launch Mode",
          "tooltip": "Radial = cada orbe sale hacia fuera desde su hueco (el anillo se abre).\nAtPlayer = cada orbe sale hacia el jugador, uno detrás de otro: la corona deja de ser un patrón de huecos y pasa a ser una cola de disparos que hay que ir esquivando al ritmo de 'launchStagger'.",
          "type": "enum",
          "options": [
            "Radial",
            "AtPlayer"
          ],
          "default": "Radial"
        },
        {
          "name": "launchStagger",
          "label": "Launch Stagger",
          "tooltip": "Sólo AtPlayer: segundos entre la salida de un orbe y la del siguiente. Cada uno apunta a donde esté el jugador en SU momento. 0 = todos a la vez.",
          "min": 0.0,
          "type": "float",
          "default": 0.15
        },
        {
          "name": "projectile",
          "label": "Projectile",
          "tooltip": "Cómo vuela cada orbe una vez suelto. Ojo con 'size': si el prefab del proyectil lleva arte de verdad (sin FxPlaceholderStyle) el tamaño lo fija el prefab, así que hay que cuadrarlo a mano con 'endScale' o la bola cambiará de tamaño al salir.",
          "type": "object",
          "fields": [
            {
              "name": "prefab",
              "label": "Prefab",
              "tooltip": "Prefab con componente Projectile. Vacío = se construye uno en código con el sprite y el color de la habilidad.",
              "type": "art",
              "artKind": "projectile",
              "artLabel": "Projectile",
              "placeholder": "bola de color de la fase",
              "default": null
            },
            {
              "name": "speed",
              "label": "Speed",
              "min": 0.1,
              "type": "float",
              "default": 12.0
            },
            {
              "name": "lifetime",
              "label": "Lifetime",
              "tooltip": "Segundos de vuelo antes de desaparecer solo.",
              "min": 0.05,
              "type": "float",
              "default": 2.5
            },
            {
              "name": "size",
              "label": "Size",
              "tooltip": "Tamaño del proyectil en unidades del mundo. Con arte del importador de jefes (FxArtSize) manda el ANCHO (X) y el collider escala con él. Un prefab hecho a mano sin FxArtSize usa su propia escala.",
              "type": "vector2",
              "default": {
                "x": 0.35,
                "y": 0.35
              }
            },
            {
              "name": "muzzleOffset",
              "label": "Muzzle Offset",
              "tooltip": "Salida del disparo respecto al lanzador. La X se invierte según hacia dónde mira.",
              "type": "vector2",
              "default": {
                "x": 0.65,
                "y": 0.1
              }
            },
            {
              "name": "pierce",
              "label": "Pierce",
              "tooltip": "Objetivos que atraviesa antes de desaparecer.",
              "header": "Comportamiento",
              "min": 0.0,
              "type": "int",
              "default": 0
            },
            {
              "name": "homingTurnRate",
              "label": "Homing Turn Rate",
              "tooltip": "Grados/segundo de giro hacia el objetivo más cercano. 0 = va recto.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "homingRange",
              "label": "Homing Range",
              "min": 0.0,
              "type": "float",
              "default": 9.0
            },
            {
              "name": "arcGravity",
              "label": "Arc Gravity",
              "tooltip": "Caída en unidades/s². >0 = granada con trayectoria parabólica.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "impactRadius",
              "label": "Impact Radius",
              "header": "Explosión al terminar",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "impactDamage",
              "label": "Impact Damage",
              "tooltip": "Daño de la explosión. Se suma al impacto directo sólo si el radio es > 0.",
              "min": 0.0,
              "type": "float",
              "default": 0.0
            },
            {
              "name": "faceDirection",
              "label": "Face Direction",
              "tooltip": "Rota el proyectil para que su punta (Facing Axis) mire hacia donde vuela, al salir y en cada paso (autoguiado, parábola). Apagado = decide el prefab (su casilla 'Face Travel Direction'), así los proyectiles que ya existían no cambian.",
              "header": "Aiming",
              "type": "bool",
              "default": false
            },
            {
              "name": "facingAxis",
              "label": "Facing Axis",
              "tooltip": "Lado del sprite que es la punta. Right = arte dibujado mirando a +X (convención).",
              "type": "enum",
              "options": [
                "Right",
                "Left",
                "Up",
                "Down"
              ],
              "default": "Right"
            },
            {
              "name": "aimMode",
              "label": "Aim Mode",
              "tooltip": "Fixed = la dirección que da quien dispara. MouseDirection / NearestEnemy sólo para el jugador (cursor / enemigo más cercano al salir, sin perseguir); en un enemigo equivalen a Fixed.",
              "type": "enum",
              "options": [
                "Fixed",
                "MouseDirection",
                "NearestEnemy"
              ],
              "default": "Fixed"
            }
          ],
          "spec": "projectile"
        },
        {
          "name": "orbPrefab",
          "label": "Orb Prefab",
          "tooltip": "Prefab del orbe de carga (con BossOrb). Vacío = se construye en código.",
          "header": "Arte",
          "type": "art",
          "artKind": "projectile",
          "artLabel": "Orb Prefab",
          "placeholder": "el de por defecto del ataque",
          "default": null
        },
        {
          "name": "orbSprite",
          "label": "Orb Sprite",
          "tooltip": "Sprite del orbe de carga cuando NO hay prefab. Vacío = el sprite FX del jefe.",
          "type": "unity",
          "unityType": "Sprite"
        },
        {
          "name": "tintOrbWithAccent",
          "label": "Tint Orb With Accent",
          "tooltip": "Tiñe el orbe con el color de la fase. Déjalo apagado si el prefab ya lleva arte de verdad: teñirlo le apagaría su propio brillo.",
          "type": "bool",
          "default": false
        },
        {
          "name": "projectileSprite",
          "label": "Projectile Sprite",
          "tooltip": "Sprite del proyectil ya lanzado cuando se construye en código. Vacío = el del jefe.",
          "type": "unity",
          "unityType": "Sprite"
        }
      ]
    },
    {
      "type": "PlatformDenialAttack",
      "label": "Platform Denial",
      "params": [
        {
          "name": "boltPrefab",
          "label": "Bolt Prefab",
          "tooltip": "Prefab del proyectil (BossBolt). PLACEHOLDER: Fx_<Jefe>_FireBolt, un cuadrado teñido. Arte final: edita ese prefab (sprite + SpriteFlipbook, y en FxPlaceholderStyle tint = false). Vacío = cuadrado construido en código.",
          "header": "Proyectil",
          "type": "art",
          "artKind": "projectile",
          "artLabel": "Bolt Prefab",
          "placeholder": "el de por defecto del ataque",
          "default": null
        },
        {
          "name": "boltSpeed",
          "label": "Bolt Speed",
          "tooltip": "Velocidad del proyectil, en unidades por segundo. El ritmo de la fase no la cambia.",
          "min": 0.5,
          "type": "float",
          "default": 12.0
        },
        {
          "name": "boltSize",
          "label": "Bolt Size",
          "tooltip": "Tamaño del proyectil placeholder (lo aplica FxPlaceholderStyle; el arte sin estilo usa la escala de su prefab).",
          "type": "vector2",
          "default": {
            "x": 0.8,
            "y": 0.8
          }
        },
        {
          "name": "launchOffset",
          "label": "Launch Offset",
          "tooltip": "De dónde sale, relativo a la base del jefe.",
          "type": "vector2",
          "default": {
            "x": 0.0,
            "y": 4.5
          }
        },
        {
          "name": "launchStagger",
          "label": "Launch Stagger",
          "tooltip": "Segundos entre un proyectil y el siguiente. 0 = todos a la vez.",
          "min": 0.0,
          "type": "float",
          "default": 0.12
        },
        {
          "name": "fireImpactFxPrefab",
          "label": "Fire Impact Fx Prefab",
          "tooltip": "Ráfaga de un solo uso al llegar el proyectil, antes de que nazca el fuego permanente (un VfxOneShot). Opcional: vacío = sin ráfaga, el fuego nace sin más.",
          "header": "Fuego (hasta que muere el jefe)",
          "type": "art",
          "artKind": "fx",
          "artLabel": "Fire Impact Fx Prefab",
          "placeholder": "el de por defecto del ataque",
          "default": null
        },
        {
          "name": "firePrefab",
          "label": "Fire Prefab",
          "tooltip": "Prefab del fuego (BossHazard). PLACEHOLDER: Fx_<Jefe>_FireHazard, un cuadrado teñido. Arte final: edita ese prefab. Vacío = cuadrado construido en código.",
          "type": "art",
          "artKind": "prop",
          "artLabel": "Fire Prefab",
          "placeholder": "el de por defecto del ataque",
          "default": null
        },
        {
          "name": "fireSize",
          "label": "Fire Size",
          "tooltip": "Ancho × alto del fuego en unidades. Es también su caja de daño.",
          "type": "vector2",
          "default": {
            "x": 2.1,
            "y": 1.0
          }
        },
        {
          "name": "fireTickInterval",
          "label": "Fire Tick Interval",
          "tooltip": "Segundos entre tics de daño. El daño por tic es 'damage' (sección Daño) y el empuje 'knockbackMultiplier'. Los i-frames del jugador ya limitan cuántos entran de verdad.",
          "min": 0.05,
          "type": "float",
          "default": 0.5
        },
        {
          "name": "snapToSurface",
          "label": "Snap To Surface",
          "tooltip": "Asienta el fuego sobre la superficie que haya bajo cada punto (plataforma o suelo), así el punto puede dejarse a ojo un poco por encima.",
          "type": "bool",
          "default": true
        },
        {
          "name": "snapDistance",
          "label": "Snap Distance",
          "min": 0.1,
          "type": "float",
          "default": 4.0
        }
      ]
    },
    {
      "type": "PlatformFloodAttack",
      "label": "Platform Flood",
      "params": [
        {
          "name": "platformCount",
          "label": "Platform Count",
          "header": "Salientes",
          "min": 1.0,
          "type": "int",
          "default": 3
        },
        {
          "name": "platformSize",
          "label": "Platform Size",
          "type": "vector2",
          "default": {
            "x": 4.2,
            "y": 0.7
          }
        },
        {
          "name": "platformHeights",
          "label": "Platform Heights",
          "tooltip": "Alturas sobre el suelo a las que aparecen, repartidas por turnos. La primera tiene que llegarse de un salto; la segunda, saltando desde la primera.",
          "type": "floatList",
          "default": [
            3.2,
            5.6
          ]
        },
        {
          "name": "platformSeconds",
          "label": "Platform Seconds",
          "tooltip": "Segundos que aguantan. Debe ser MÁS que la inundación: bajarse a un suelo que todavía quema sería una muerte que el jugador no ha elegido.",
          "min": 1.0,
          "type": "float",
          "default": 7.0
        },
        {
          "name": "minSeparation",
          "label": "Min Separation",
          "tooltip": "Separación mínima entre salientes para que no salgan pegados.",
          "min": 0.0,
          "type": "float",
          "default": 6.0
        },
        {
          "name": "floodSeconds",
          "label": "Flood Seconds",
          "tooltip": "Segundos que el suelo hace daño.",
          "header": "Inundación",
          "min": 0.5,
          "type": "float",
          "default": 4.5
        },
        {
          "name": "floodSegments",
          "label": "Flood Segments",
          "tooltip": "Trozos en los que se parte el suelo. Más trozos = borde más fino, pero da igual: juntos cubren la arena entera.",
          "min": 1.0,
          "type": "int",
          "default": 6
        },
        {
          "name": "floodHeight",
          "label": "Flood Height",
          "tooltip": "Alto de la lámina que cubre el suelo. Bastante bajo: subirse a un saliente tiene que salvarte, y saltar sin más no.",
          "min": 0.3,
          "type": "float",
          "default": 1.1
        },
        {
          "name": "floodDamagePerTick",
          "label": "Flood Damage Per Tick",
          "min": 0.0,
          "type": "float",
          "default": 11.0
        },
        {
          "name": "floodTickInterval",
          "label": "Flood Tick Interval",
          "min": 0.05,
          "type": "float",
          "default": 0.45
        },
        {
          "name": "climbSeconds",
          "label": "Climb Seconds",
          "tooltip": "Segundos entre que aparecen los salientes y sube la marea. Es el tiempo para llegar a uno y subirse.",
          "header": "Ritmo",
          "min": 0.2,
          "type": "float",
          "default": 1.4
        },
        {
          "name": "markerSeconds",
          "label": "Marker Seconds",
          "min": 0.05,
          "type": "float",
          "default": 0.5
        }
      ]
    },
    {
      "type": "QuakeSlamAttack",
      "label": "Quake Slam",
      "params": [
        {
          "name": "impactDelay",
          "label": "Impact Delay",
          "tooltip": "Segundos desde que empieza el gesto hasta que el golpe toca el suelo (sacudida + daño). Sustituye al 'telegraph' del ataque. Es a ritmo 1: la fase lo divide por su speedScale. Si obliga al clip a ir más rápido o más lento de lo que permite el BossAnimator (gestureSpeedRange), manda el dibujo y el golpe cae en su frame.",
          "header": "Golpe",
          "min": 0.1,
          "type": "float",
          "default": 1.0
        },
        {
          "name": "floorTolerance",
          "label": "Floor Tolerance",
          "tooltip": "Cuánto por encima del suelo de la arena pueden estar los pies del jugador y seguir contando como 'en el suelo'. Deja fuera los salientes sólidos elevados (capa Ground) aunque no sean plataforma; estar en una plataforma (capa Platform) o en el aire salva siempre.",
          "header": "¿Está en el suelo?",
          "min": 0.0,
          "type": "float",
          "default": 0.6
        },
        {
          "name": "warnFloor",
          "label": "Warn Floor",
          "tooltip": "Pinta una franja a ras de suelo en toda la arena mientras dura el gesto.",
          "header": "Aviso",
          "type": "bool",
          "default": true
        },
        {
          "name": "warnHeight",
          "label": "Warn Height",
          "min": 0.05,
          "type": "float",
          "default": 0.5
        },
        {
          "name": "impactFxPrefab",
          "label": "Impact Fx Prefab",
          "tooltip": "Efecto de un solo uso (pooled, VfxOneShot) que sale a los pies del jefe al golpear: la onda de raíces. Vacío = un destello del color de la fase a ras de suelo.",
          "header": "Arte",
          "type": "art",
          "artKind": "fx",
          "artLabel": "Impact Fx Prefab",
          "placeholder": "el de por defecto del ataque",
          "default": null
        },
        {
          "name": "fxWidth",
          "label": "Fx Width",
          "tooltip": "Ancho del efecto en unidades. 0 = la escala del prefab tal cual.",
          "min": 0.0,
          "type": "float",
          "default": 9.0
        }
      ]
    },
    {
      "type": "SafeZoneAttack",
      "label": "Safe Zone",
      "params": [
        {
          "name": "safeSpots",
          "label": "Safe Spots",
          "tooltip": "Cuántos sitios seguros se marcan. Menos refugios = más lejos hay que correr.",
          "header": "Refugios",
          "min": 1.0,
          "type": "int",
          "default": 3
        },
        {
          "name": "spotRadius",
          "label": "Spot Radius",
          "tooltip": "Radio del refugio. Tiene que caber el jugador con holgura: si va justo, el ataque deja de leerse como 'ponte ahí' y pasa a ser 'acierta el píxel'.",
          "min": 0.5,
          "type": "float",
          "default": 2.3
        },
        {
          "name": "minSeparation",
          "label": "Min Separation",
          "tooltip": "Separación mínima entre refugios, para que no salgan dos pegados (que serían uno).",
          "min": 0.0,
          "type": "float",
          "default": 6.0
        },
        {
          "name": "edgeMargin",
          "label": "Edge Margin",
          "tooltip": "Margen que se respeta en los bordes de la arena: un refugio pegado a la pared es medio refugio.",
          "min": 0.0,
          "type": "float",
          "default": 2.5
        },
        {
          "name": "guaranteeReachable",
          "label": "Guarantee Reachable",
          "tooltip": "Coloca SIEMPRE un refugio cerca del jugador. Es lo que hace justo al ataque independientemente de lo grande que sea la arena: sin esto, una mala tirada puede dejar los tres refugios en la otra punta y el golpe pasa a ser daño inevitable.",
          "type": "bool",
          "default": true
        },
        {
          "name": "reachableRadius",
          "label": "Reachable Radius",
          "tooltip": "Radio dentro del que aparece ese refugio garantizado. Debe ser lo que el jugador recorre cómodamente durante la ventana de aviso, no lo máximo que podría correr.",
          "min": 1.0,
          "type": "float",
          "default": 9.0
        },
        {
          "name": "markerSeconds",
          "label": "Marker Seconds",
          "tooltip": "Segundos entre que se marcan los refugios y cae el golpe. Es el tiempo que hay para llegar corriendo: bájalo sólo si también bajas la distancia.",
          "header": "Ritmo",
          "min": 0.2,
          "type": "float",
          "default": 1.6
        },
        {
          "name": "pulses",
          "label": "Pulses",
          "tooltip": "Golpes seguidos del ataque.",
          "min": 1.0,
          "type": "int",
          "default": 1
        },
        {
          "name": "timeBetweenPulses",
          "label": "Time Between Pulses",
          "min": 0.1,
          "type": "float",
          "default": 1.3
        },
        {
          "name": "moveSpotsEachPulse",
          "label": "Move Spots Each Pulse",
          "tooltip": "Cambia los refugios de sitio en cada golpe. Encendido, el ataque es una carrera; apagado, basta con llegar una vez y quedarse.",
          "type": "bool",
          "default": true
        },
        {
          "name": "safeColor",
          "label": "Safe Color",
          "tooltip": "Color del refugio. A propósito NO es el color de la fase: lo que hace daño y lo que salva no pueden pintarse igual.",
          "header": "Presencia",
          "type": "color",
          "default": "#f2edb8d9"
        },
        {
          "name": "blastHeight",
          "label": "Blast Height",
          "tooltip": "Altura que cubre el golpe sobre el suelo. 0 = toda la arena (saltar no salva).",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        }
      ]
    },
    {
      "type": "ShockwaveAttack",
      "label": "Shockwave",
      "params": [
        {
          "name": "bandMin",
          "label": "Band Min",
          "tooltip": "Borde inferior. 0 = pegado al suelo.",
          "header": "Franja de altura (unidades sobre el suelo de la arena)",
          "type": "float",
          "default": 0.0
        },
        {
          "name": "bandMax",
          "label": "Band Max",
          "tooltip": "Borde superior. Con el borde inferior a 0, esto es la altura que hay que saltar.",
          "type": "float",
          "default": 1.9
        },
        {
          "name": "alternateBands",
          "label": "Alternate Bands",
          "tooltip": "Las oleadas impares usan la segunda franja. Con una franja baja y otra alta, el ataque pide saltar, aterrizar y volver a saltar: es el patrón más exigente que se puede montar sin escribir código nuevo.",
          "header": "Franja alterna",
          "type": "bool",
          "default": false
        },
        {
          "name": "bandMinB",
          "label": "Band Min B",
          "type": "float",
          "default": 1.9
        },
        {
          "name": "bandMaxB",
          "label": "Band Max B",
          "type": "float",
          "default": 9.0
        },
        {
          "name": "waves",
          "label": "Waves",
          "tooltip": "Ondas seguidas que lanza el ataque.",
          "header": "Oleadas",
          "min": 1.0,
          "type": "int",
          "default": 1
        },
        {
          "name": "timeBetweenWaves",
          "label": "Time Between Waves",
          "min": 0.05,
          "type": "float",
          "default": 0.5
        },
        {
          "name": "bothDirections",
          "label": "Both Directions",
          "tooltip": "Lanza una onda a cada lado. Apagado = sólo hacia el jugador.",
          "type": "bool",
          "default": true
        },
        {
          "name": "width",
          "label": "Width",
          "tooltip": "Grosor horizontal de la onda: cuánto tiempo te toca si no la esquivas.",
          "header": "Onda",
          "min": 0.2,
          "type": "float",
          "default": 1.6
        },
        {
          "name": "speed",
          "label": "Speed",
          "min": 1.0,
          "type": "float",
          "default": 16.0
        },
        {
          "name": "travelDistance",
          "label": "Travel Distance",
          "tooltip": "Distancia que recorre. 0 = cruza la arena entera.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "spawnInset",
          "label": "Spawn Inset",
          "tooltip": "A qué distancia del jefe nace la onda. Evita que aparezca dentro de su tronco.",
          "min": 0.0,
          "type": "float",
          "default": 1.4
        },
        {
          "name": "speedRampPerWave",
          "label": "Speed Ramp Per Wave",
          "tooltip": "Cada oleada sale un poco más rápida que la anterior. 1 = todas iguales.",
          "header": "Escalonado",
          "min": 0.5,
          "type": "float",
          "default": 1.0
        }
      ]
    },
    {
      "type": "SummonAddsAttack",
      "label": "Summon Adds",
      "params": [
        {
          "name": "prefabs",
          "label": "Prefabs",
          "tooltip": "Prefabs a invocar. Se elige uno al azar por esbirro.",
          "header": "Esbirros",
          "type": "unity",
          "unityType": "GameObject[]"
        },
        {
          "name": "count",
          "label": "Count",
          "min": 1.0,
          "type": "int",
          "default": 3
        },
        {
          "name": "maxAlive",
          "label": "Max Alive",
          "tooltip": "Tope de esbirros vivos. Si ya hay tantos, la invocación no añade más.",
          "min": 1.0,
          "type": "int",
          "default": 6
        },
        {
          "name": "addTag",
          "label": "Add Tag",
          "tooltip": "Etiqueta que se pone al esbirro. Compartirla con el jefe es lo que hace que los proyectiles del jefe no maten a sus propios esbirros.",
          "type": "string",
          "default": "Enemy"
        },
        {
          "name": "distanceRange",
          "label": "Distance Range",
          "tooltip": "Distancia mínima y máxima al jefe. Van alternando izquierda y derecha.",
          "header": "Colocación",
          "type": "vector2",
          "default": {
            "x": 4.0,
            "y": 11.0
          }
        },
        {
          "name": "spawnHeight",
          "label": "Spawn Height",
          "tooltip": "Altura sobre el suelo a la que aparecen (se ajusta por raycast si hay suelo).",
          "type": "float",
          "default": 0.8
        },
        {
          "name": "groundLayers",
          "label": "Ground Layers",
          "tooltip": "Capas que cuentan como suelo al colocarlos.",
          "type": "unity",
          "unityType": "LayerMask"
        },
        {
          "name": "markerSeconds",
          "label": "Marker Seconds",
          "tooltip": "Segundos que la marca del suelo está visible antes de que aparezca el esbirro.",
          "header": "Ritmo",
          "min": 0.05,
          "type": "float",
          "default": 0.55
        },
        {
          "name": "timeBetweenSpawns",
          "label": "Time Between Spawns",
          "min": 0.0,
          "type": "float",
          "default": 0.12
        }
      ]
    },
    {
      "type": "SweepBeamAttack",
      "label": "Sweep Beam",
      "params": [
        {
          "name": "pivotOffset",
          "label": "Pivot Offset",
          "tooltip": "Punto del que sale el brazo, respecto al jefe. La Y es la clave del ataque: cuanto más alto, más tarda la guadaña en llegar al suelo lejos del jefe.",
          "header": "Pivote",
          "type": "vector2",
          "default": {
            "x": 0.0,
            "y": 3.2
          }
        },
        {
          "name": "fromAngle",
          "label": "From Angle",
          "tooltip": "Ángulo de salida. 80 = casi vertical.",
          "header": "Barrido (grados: 0 = horizontal, 90 = arriba)",
          "type": "float",
          "default": 80.0
        },
        {
          "name": "toAngle",
          "label": "To Angle",
          "tooltip": "Ángulo final. Un pelín negativo hace que termine clavada en el suelo.",
          "type": "float",
          "default": -6.0
        },
        {
          "name": "sweepSeconds",
          "label": "Sweep Seconds",
          "tooltip": "Segundos que tarda una pasada. Es el mando de dificultad principal.",
          "min": 0.1,
          "type": "float",
          "default": 0.95
        },
        {
          "name": "sweepTowardPlayer",
          "label": "Sweep Toward Player",
          "tooltip": "Apunta el barrido al lado en el que está el jugador. Apagado, siempre barre a la derecha del jefe.",
          "type": "bool",
          "default": true
        },
        {
          "name": "bothSides",
          "label": "Both Sides",
          "tooltip": "Barre a los dos lados a la vez. Quita la salida fácil de 'ponerse detrás' y convierte el ataque en una pregunta pura de distancia.",
          "type": "bool",
          "default": false
        },
        {
          "name": "sweeps",
          "label": "Sweeps",
          "header": "Pasadas",
          "min": 1.0,
          "type": "int",
          "default": 1
        },
        {
          "name": "timeBetweenSweeps",
          "label": "Time Between Sweeps",
          "min": 0.05,
          "type": "float",
          "default": 0.6
        },
        {
          "name": "returnSweep",
          "label": "Return Sweep",
          "tooltip": "Las pasadas pares vuelven del ángulo final al inicial. Encadenar ida y vuelta castiga quedarse quieto justo donde acaba de pasar el filo.",
          "type": "bool",
          "default": true
        },
        {
          "name": "length",
          "label": "Length",
          "tooltip": "Largo del brazo. 0 = de sobra para cruzar media arena.",
          "header": "Filo",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "width",
          "label": "Width",
          "tooltip": "Grosor del filo: cuánto margen hay al saltarlo o pasarle por debajo.",
          "min": 0.2,
          "type": "float",
          "default": 0.85
        }
      ]
    },
    {
      "type": "ThrowProjectileAttack",
      "label": "Throw Projectile",
      "params": [
        {
          "name": "throwGesture",
          "label": "Throw Gesture",
          "tooltip": "Estado del Animator del gesto de LANZAR. El de COGER va en 'Gesto' (arriba) y hace de aviso. Vacío = sin segundo gesto: se lanza al pasar 'holdSeconds'.",
          "header": "Gesto de lanzar",
          "type": "string",
          "default": "Throw"
        },
        {
          "name": "holdSeconds",
          "label": "Hold Seconds",
          "tooltip": "Segundos con el objeto en alto hasta soltarlo. Con gesto de lanzar, el clip se ajusta para que su frame de suelta caiga aquí.",
          "min": 0.05,
          "type": "float",
          "default": 0.6
        },
        {
          "name": "flightSeconds",
          "label": "Flight Seconds",
          "tooltip": "Segundos desde que lo suelta hasta que cae. Es TODO el margen para salir de la marca: el mando de dificultad de este ataque.",
          "header": "Vuelo",
          "min": 0.2,
          "type": "float",
          "default": 1.1
        },
        {
          "name": "gravity",
          "label": "Gravity",
          "tooltip": "Gravedad de la parábola (u/s²). Más = arco más alto para el mismo tiempo.",
          "min": 1.0,
          "type": "float",
          "default": 30.0
        },
        {
          "name": "landingSpread",
          "label": "Landing Spread",
          "tooltip": "Desvío aleatorio del punto de caída, en X. 0 = justo donde estaba el jugador.",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        },
        {
          "name": "throws",
          "label": "Throws",
          "tooltip": "Lanzamientos seguidos (cada uno repite el gesto de lanzar y vuelve a apuntar).",
          "min": 1.0,
          "type": "int",
          "default": 1
        },
        {
          "name": "timeBetweenThrows",
          "label": "Time Between Throws",
          "min": 0.05,
          "type": "float",
          "default": 0.5
        },
        {
          "name": "projectile",
          "label": "Projectile",
          "tooltip": "Aspecto y explosión del objeto. prefab vacío = bola de color. 'impactRadius' / 'impactDamage' = la explosión al caer. La velocidad y la gravedad las calcula el ataque para caer en la marca.",
          "header": "Proyectil",
          "type": "object",
          "fields": [
            {
              "name": "prefab",
              "label": "Prefab",
              "tooltip": "Prefab con componente Projectile. Vacío = se construye uno en código con el sprite y el color de la habilidad.",
              "type": "art",
              "artKind": "projectile",
              "artLabel": "Proyectil lanzado",
              "placeholder": "bola de color de la fase",
              "default": null
            },
            {
              "name": "lifetime",
              "label": "Lifetime",
              "tooltip": "Segundos de vuelo antes de desaparecer solo.",
              "min": 0.05,
              "type": "float",
              "default": 4.0
            },
            {
              "name": "size",
              "label": "Size",
              "tooltip": "Tamaño del proyectil en unidades del mundo. Con arte del importador de jefes (FxArtSize) manda el ANCHO (X) y el collider escala con él. Un prefab hecho a mano sin FxArtSize usa su propia escala.",
              "type": "vector2",
              "default": {
                "x": 1.6,
                "y": 1.6
              }
            },
            {
              "name": "pierce",
              "label": "Pierce",
              "tooltip": "Objetivos que atraviesa antes de desaparecer.",
              "header": "Comportamiento",
              "min": 0.0,
              "type": "int",
              "default": 0
            },
            {
              "name": "impactRadius",
              "label": "Impact Radius",
              "header": "Explosión al terminar",
              "min": 0.0,
              "type": "float",
              "default": 2.2
            },
            {
              "name": "impactDamage",
              "label": "Impact Damage",
              "tooltip": "Daño de la explosión. Se suma al impacto directo sólo si el radio es > 0.",
              "min": 0.0,
              "type": "float",
              "default": 16.0
            },
            {
              "name": "faceDirection",
              "label": "Face Direction",
              "tooltip": "Rota el proyectil para que su punta (Facing Axis) mire hacia donde vuela, al salir y en cada paso (autoguiado, parábola). Apagado = decide el prefab (su casilla 'Face Travel Direction'), así los proyectiles que ya existían no cambian.",
              "header": "Aiming",
              "type": "bool",
              "default": false
            },
            {
              "name": "facingAxis",
              "label": "Facing Axis",
              "tooltip": "Lado del sprite que es la punta. Right = arte dibujado mirando a +X (convención).",
              "type": "enum",
              "options": [
                "Right",
                "Left",
                "Up",
                "Down"
              ],
              "default": "Right"
            },
            {
              "name": "aimMode",
              "label": "Aim Mode",
              "tooltip": "Fixed = la dirección que da quien dispara. MouseDirection / NearestEnemy sólo para el jugador (cursor / enemigo más cercano al salir, sin perseguir); en un enemigo equivalen a Fixed.",
              "type": "enum",
              "options": [
                "Fixed",
                "MouseDirection",
                "NearestEnemy"
              ],
              "default": "Fixed"
            }
          ],
          "spec": "projectile",
          "artLabel": "Proyectil lanzado"
        },
        {
          "name": "heldPropPrefab",
          "label": "Held Prop Prefab",
          "tooltip": "Lo que se ve en alto antes de lanzarlo (la roca). Vacío = cuadrado del color de la fase. Tip: el mismo arte que el proyectil.",
          "header": "Objeto en la mano",
          "type": "art",
          "artKind": "prop",
          "artLabel": "Objeto sostenido",
          "placeholder": "cuadrado de color",
          "default": null
        },
        {
          "name": "heldOffset",
          "label": "Held Offset",
          "tooltip": "Dónde lo sostiene, relativo a los pies del jefe (X hacia el jugador).",
          "type": "vector2",
          "default": {
            "x": 0.0,
            "y": 5.5
          }
        },
        {
          "name": "heldSize",
          "label": "Held Size",
          "tooltip": "Tamaño del objeto sostenido. Con arte del importador manda el ancho (X).",
          "type": "vector2",
          "default": {
            "x": 1.6,
            "y": 1.6
          }
        },
        {
          "name": "markLanding",
          "label": "Mark Landing",
          "header": "Aviso de caída",
          "type": "bool",
          "default": true
        },
        {
          "name": "markRadius",
          "label": "Mark Radius",
          "tooltip": "Radio de la marca. 0 = el de la explosión (o 1.5 si no explota).",
          "min": 0.0,
          "type": "float",
          "default": 0.0
        }
      ]
    }
  ],
  "movements": [
    {
      "type": "HoverKeepDistance",
      "label": "Flotar a distancia",
      "params": [
        {
          "name": "minDistance",
          "label": "Min Distance",
          "tooltip": "Por debajo de esta distancia horizontal al objetivo, se aparta.",
          "min": 0.0,
          "type": "float",
          "default": 5.0
        },
        {
          "name": "maxDistance",
          "label": "Max Distance",
          "tooltip": "Por encima de esta distancia horizontal al objetivo, se acerca.",
          "min": 0.0,
          "type": "float",
          "default": 9.0
        },
        {
          "name": "speed",
          "label": "Speed",
          "tooltip": "Velocidad máxima, unidades/s.",
          "min": 0.1,
          "type": "float",
          "default": 3.5
        },
        {
          "name": "acceleration",
          "label": "Acceleration",
          "tooltip": "Aceleración, unidades/s². Bajo = arranca y frena con inercia (pesado).",
          "min": 0.1,
          "type": "float",
          "default": 10.0
        },
        {
          "name": "arriveTolerance",
          "label": "Arrive Tolerance",
          "tooltip": "Margen para darse por llegado. Evita que tiemble en el sitio.",
          "min": 0.01,
          "type": "float",
          "default": 0.35
        },
        {
          "name": "followHeight",
          "label": "Follow Height",
          "tooltip": "Además de la distancia, se coloca a una altura fija sobre el objetivo.",
          "header": "Altura (voladores)",
          "type": "bool",
          "default": false
        },
        {
          "name": "heightAboveTarget",
          "label": "Height Above Target",
          "tooltip": "Altura sobre el objetivo cuando 'followHeight' está activo.",
          "type": "float",
          "default": 2.5
        },
        {
          "name": "verticalSpeed",
          "label": "Vertical Speed",
          "min": 0.0,
          "type": "float",
          "default": 2.5
        }
      ]
    }
  ]
};
