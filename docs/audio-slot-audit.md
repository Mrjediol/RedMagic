# Auditoría de huecos de sonido (fase 5, paso 1)

Documento de trabajo del paso 1. **Se borra en el paso 6**, cuando el registro y la pestaña Sounds
existan: a partir de ahí la única fuente es el registro.

Leyenda: **✅ el gancho existe** (hay hueco y el evento ya se dispara; puede faltar poner el
`SoundEmitter` en el prefab, que es cableado del paso 2) · **🔧 hay que construir el gancho** ·
**📝 declarado** (el evento aún no existe en el juego).

## (a) Lo que ya estaba marcado

| Dónde | Qué dice |
|---|---|
| `docs/audio-pending.md` | 3 "migrado pero hay que sustituir" (Player OnHit, Player OnAttack, música de menú) + 10 huecos vacíos (5 props OnInteract, clic de la cinemática, Shop deny/reroll, Player RangedAttack, SectionClear ×13 escenas). Todos entran abajo. |
| `Scripts/Hub/MirrorInteractable.cs:128` | Gancho `OnMirrorInteract` pensado para "VFX, SFX…" del espejo. |
| `Scripts/Hub/AnvilInteractable.cs:92,97` | TODO: la mejora de arma real no existe (el sonido de usar sí). |
| `Scripts/Hub/WardrobeLootContainer.cs:23,27` | TODO: la recompensa (3 items) no existe (abrir/recoger sí suenan). |
| `Scripts/Economy/LegendaryPassiveEffects.cs:22,89,91` | TODO: Tomo del Destino sin implementar; Páginas del Eco nivel 2 sin implementar. |
| `Scripts/Combat/Health.cs:301` | `Drain` es daño "sin sonido" a propósito (sangrado de items). |
| `docs/*.md`, `_Pipeline/*.md` | Ninguna mención de sonidos planeados o que falten. |

No hay más TODO/FIXME de audio en `Assets/Scripts`, `Assets/Ui`, `Assets/Audio`.

## (b) Lista completa

### Player ▸ Movement — 5
| Sonido | Gancho |
|---|---|
| Paso (Footstep) | ✅ |
| Salto | ✅ |
| Doble salto | ✅ |
| Dash | ✅ |
| Aterrizar | 🔧 no hay evento de aterrizaje |

*Pisotón*: fuera — la mecánica se quitó en la fase 4 a petición tuya.

### Player ▸ Attack / combate — 5 + armas 8
| Sonido | Gancho |
|---|---|
| Ataque cuerpo a cuerpo (`PlayerAttack`, sin arma) | ✅ OnAttack |
| Ataque a distancia (`RangedAttack`, bola de fuego antigua) | 🔧 comparte el hueco OnAttack con el cuerpo a cuerpo en el mismo emisor: necesita su propio momento |
| Recibir daño | ✅ OnHit |
| Morir | ✅ OnDeath |
| Recoger item del suelo | ✅ OnLoot |
| Disparo, por arma ×7 (BolaDeFuego, EscopetaEspectral, GranadaRunica, LanzaAstral, ProyectilRecto, RafagaArcana, RayoArcano) | ✅ `WeaponDefinition.fireSound` |
| Carga (sólo RayoArcano tiene carga) | ✅ `chargeSound` |

### Projectiles — 41
| Sonido | Gancho |
|---|---|
| Proyectiles de enemigo/jefe ×5 (`Fx_IceSpiritShoot`, `Fx_ColosodeHielo_Boulder`, `Fx_ColosodeHielo_PunchWave`, `Fx_TreeBoss_Leaf`, `Fx_TreeBoss_Bullet`): lanzar | ✅ OnSpawn (×5) |
| …impacto en objetivo | ✅ OnHit (×5) |
| …impacto en terreno | 🔧 hoy cae en OnDeath junto a todo lo demás (×5) |
| …desaparecer (fin de vida) | ✅ OnDeath (×5), pasa a ser "sólo fin de vida" al separar el terreno |
| Disparos del jugador, por arma ×6 de proyectil: impacto en objetivo / en terreno / fin de vida | 🔧 `ShotProjectile` no avisa de nada (×18) |
| RayoArcano (haz): impacto en objetivo + bucle del haz | 🔧 ×2 |
| GranadaRunica: explosión | 🔧 ×1 |

### Interactables — 9
| Sonido | Gancho |
|---|---|
| GoldChest: abrir / sacar el arma | ✅ OnInteract / OnLoot |
| Wardrobe: abrir / coger el item | ✅ OnInteract / OnLoot (la recompensa en sí es TODO) |
| BookLectern: abrir / consultar | ✅ OnInteract / OnLoot |
| Anvil: usar | ✅ OnInteract |
| Mirror: usar | ✅ OnInteract |
| Mirror: mejora de pasiva hecha | 🔧 hoy es el clic genérico de UI |

La navegación del menú del espejo es la de la UI (abajo).

### UI — 23
| Sonido | Gancho |
|---|---|
| Hover | ✅ global (AudioManager) |
| Foco (mando/teclado) | 🔧 hoy reutiliza el hover; necesita su propia cue |
| Clic | ✅ global |
| Atrás | ✅ global |
| Rechazo (deny) | 🔧 la cue existe pero nada la llama: compras fallidas en Mejoras/Forja/Espejo son mudas |
| Abrir menú ×8 (Main, Pause, Options, Items, Upgrades, Mirror, WeaponForge, WeaponChoice) | 🔧 ×8 |
| Cerrar menú ×8 | 🔧 ×8 |
| Popup "Arma/Item obtenido" (`RewardPopupUi`) | 🔧 |
| Cinemática de pasiva: aterriza | ✅ `landSound` |

Los "10 menús" eran 8 vivos + 2 legacy (`AbilityMenu`, `WeaponUpgradeMenu`); los legacy quedan fuera.

### Shop — 4
| Sonido | Gancho |
|---|---|
| Comprar / rechazar / reroll | ✅ ×3 |
| Rechazo por huecos libres llenos | 🔧 hoy mudo (el resto de rechazos suenan) |

### Legendary Passives — 8
| Pasiva | Sonido | Gancho |
|---|---|---|
| Codex Aurum | item gratis al matar al jefe | 🔧 |
| Tomo del Destino | elegir entre 3 items | 📝 efecto sin implementar |
| Grimorio del Umbral | elección de 3 armas en el cofre | 🔧 |
| El Libro Sin Nombre | revivir | 🔧 |
| El Libro Sin Nombre | el escudo absorbe un golpe | 🔧 |
| Anales del Vacío | 1ª oleada ralentizada | 🔧 |
| Manuscrito Eterno | item cada N secciones | 🔧 |
| El Tomo Roto | arma del cofre mejorada | 🔧 |

Páginas del Eco (rerolls) y Volumen Carmesí (+daño) no tienen un momento visible: sin sonido.

### Run Flow — 7
| Sonido | Gancho |
|---|---|
| Empezar run (puerta del hub) | 🔧 `RunStarted` existe, sin sonido |
| Entrar en una sección | 🔧 |
| Empezar oleada | 🔧 `WaveManager.OnWaveStart` existe, sin sonido |
| Sección despejada | ✅ `clearSound` |
| Entrar en la tienda | 🔧 `ShopManager.Entered` existe, sin sonido |
| Muerte → fin de run | 🔧 `RunEnded(false)` |
| Mundo/run completado | 🔧 `RunEnded(true)` |

La activación del jefe va en Bosses.

### Music — 10
Todas por la convención `Resources/Music/<nombre>`: el gancho existe (✅), falta el clip donde se indica.

| Pista | Clip |
|---|---|
| Menú (`MainMenuController.menuMusic`) | placeholder |
| Hub `MainHub` | ✔ |
| Tienda `Shop` | falta |
| `World1-1` | ✔ |
| `World2-1` | ✔ |
| `World2-2` | falta |
| `World3-1` (Mundo 3 = escenas de prueba Fire1) | falta |
| `BossBattle1` | ✔ |
| `BossBattle2` | falta |
| `BossBattle3` | falta |

`World1-2.mp3` existe pero no suena nunca (Mundo 1 = 1 sección por run).

### Enemies — 8
| Enemigo | Herido | Muerte | Movimiento | Ataque |
|---|---|---|---|---|
| IceGolem (cuerpo a cuerpo, camina) | ✅ | ✅ | 🔧 | ✅ |
| IceSpirit (vuela, dispara) | ✅ | ✅ | 🔧 | ✅ (su proyectil va en Projectiles) |

ColosoDeHielo es un **jefe**: está en Bosses.

### Bosses — 21
| Jefe | Activación | Herido | Muerte | Cambio de fase | Ataques |
|---|---|---|---|---|---|
| TreeBoss (Mundo 1) | ✅ | ✅ | ✅ | ✅ ×1 | ✅ ×7: TormentaDeHojas, EspinasDeRaiz, FuegoVerde, OrbesDeSavia, EstallidoDeRaices, BosqueDeEspinas, TormentaDeSavia |
| ColosoDeHielo (Mundo 2) | ✅ | ✅ | ✅ | ✅ ×1 | ✅ ×6: HieloLanzarRoca, HieloLanzarRocas, HieloRafagaPunos, HieloGolpeSuelo, HieloEmbestida, HieloPunoPotente |

Fuera: PruebaGolem (de prueba, en Ice4) y los 6 jefes que no están en ninguna escena
(ArbolAncestral, Espantapájaros, Guardiana, Pozo, ReinaEscarabajo, SelloProfano).

## Total

| Categoría | Huecos | ✅ | 🔧 | 📝 |
|---|---|---|---|---|
| Player ▸ Movement | 5 | 4 | 1 | |
| Player ▸ Attack (+ armas) | 13 | 12 | 1 | |
| Projectiles | 41 | 15 | 26 | |
| Interactables | 9 | 8 | 1 | |
| UI | 23 | 4 | 19 | |
| Shop | 4 | 3 | 1 | |
| Legendary Passives | 8 | | 7 | 1 |
| Run Flow | 7 | 1 | 6 | |
| Music | 10 | 10 | | |
| Enemies | 8 | 6 | 2 | |
| Bosses | 21 | 21 | | |
| **Total** | **149** | **84** | **64** | **1** |

## Candidatos que no estaban en la lista (decide cuáles entran)

Monedas soltadas/recogidas · alcanzar un umbral de sinergia (2/4/6) · equipar item / cambiar de
arma · subir de nivel el arma en la forja · estallido de hielo (IceBurst) · aplicar ralentización ·
aplicar Marca de Oro · curación del robo de vida (motas) · puerta del hub bloqueada (sin arma) ·
aparece el altar de recompensa del jefe · aviso de vida baja · bucle del fuego verde persistente
(BossHazard) · el Coloso coge la roca · despertar de un enemigo dormido.
