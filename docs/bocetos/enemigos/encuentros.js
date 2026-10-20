// encuentros.js — combinaciones predefinidas. Un enemigo se juzga solo (duelo) y, sobre todo, mezclado: cada encuentro prueba
// una pregunta doble (moverte por dos motivos a la vez, elegir prioridad, usar a un enemigo contra otro).
// El player empieza en x = 200; la arena va de 60 a 1460.
'use strict';
addEncounter({ id: 'manada', name: 'Manada', desc: '3 Acechadores: siempre hay uno a tu espalda; se turnan para saltar.',
  list: [{ type: 'stalker', x: 900 }, { type: 'stalker', x: 1050 }, { type: 'stalker', x: 1200 }] });
addEncounter({ id: 'muro', name: 'Muro y cañón', desc: 'Caparazón delante, dos Artilleros detrás: rodea el escudo mientras caen morteros.',
  list: [{ type: 'shell', x: 820 }, { type: 'mortar', x: 1180 }, { type: 'mortar', x: 1340 }] });
addEncounter({ id: 'plaga', name: 'Plaga', desc: 'Colmena + 2 Fatuos: muchos objetivos; un fatuo bien usado limpia zánganos.',
  list: [{ type: 'hive', x: 1050, y: 150 }, { type: 'wisp', x: 1200, y: 170 }, { type: 'wisp', x: 1350, y: 220 }] });
addEncounter({ id: 'emboscada', name: 'Emboscada', desc: 'Excavador + Tejedora: muévete por el montículo y por la aguja a la vez.',
  list: [{ type: 'burrower', x: 900 }, { type: 'weaver', x: 1100, y: 140 }] });
addEncounter({ id: 'estampida', name: 'Estampida', desc: '2 Embestidores + 1 Fatuo: haz que se estampen contra la pared o que carguen hacia el fatuo.',
  list: [{ type: 'charger', x: 900 }, { type: 'charger', x: 1300 }, { type: 'wisp', x: 1100, y: 200 }] });
addEncounter({ id: 'cielo', name: 'Cielo cubierto', desc: '2 Tejedoras + Colmena: todo vuela; armas a distancia o subir a las plataformas.',
  list: [{ type: 'weaver', x: 900, y: 130 }, { type: 'weaver', x: 1300, y: 160 }, { type: 'hive', x: 1150, y: 150 }] });
addEncounter({ id: 'todo', name: 'Todos', desc: 'Uno de cada (sin zánganos sueltos): caos para probar el máximo de atacantes a la vez.',
  list: [{ type: 'charger', x: 1350 }, { type: 'stalker', x: 800 }, { type: 'mortar', x: 1420 }, { type: 'weaver', x: 1000, y: 140 },
         { type: 'shell', x: 1150 }, { type: 'hive', x: 1250, y: 140 }, { type: 'wisp', x: 950, y: 200 }, { type: 'burrower', x: 700 }] });
// duelo: lo monta el panel (tipo × cantidad)
function duelEncounter(type, n = 1) {
  const def = FOES[type], list = [];
  for (let i = 0; i < n; i++) list.push({ type, x: 640 + i * 110, y: def.fly ? 130 + (i % 2) * 50 : undefined });   // a la vista (no piensan hasta verte)
  return { id: 'duelo:' + type + ':' + n, name: 'Duelo · ' + def.name + (n > 1 ? ' ×' + n : ''), desc: def.question, list, world: def.world };
}
// élites (1 por encuentro, con comunes alrededor)
addEncounter({ id: 'alfa', name: 'Jefe de manada', desc: 'Acechador alfa + 2 Acechadores: mátalo pronto o aúlla (y si quedan pocos, llama a más).',
  list: [{ type: 'stalkerAlpha', x: 1100 }, { type: 'stalker', x: 900 }, { type: 'stalker', x: 1300 }] });
addEncounter({ id: 'toro', name: 'Toro y cañón', desc: 'Embestidor alfa + Artillero: cargas encadenadas mientras caen morteros.',
  list: [{ type: 'chargerAlpha', x: 1000 }, { type: 'mortar', x: 1380 }] });
// ---- mundo de hielo (aspecto de hielo: fondo y techo de carámbanos; las mecánicas son las de cada enemigo)
addEncounter({ id: 'cueva', world: 'hielo', name: 'Cueva de carámbanos', desc: '2 Colgantes + Erizo de escarcha: todo cae desde arriba; mira al techo y al suelo.',
  list: [{ type: 'icicle', x: 700 }, { type: 'icicle', x: 1150 }, { type: 'urchin', x: 1000 }] });
addEncounter({ id: 'tundra', world: 'hielo', name: 'Tundra', desc: 'Morsa + 2 Ventiscas: el viento te empuja contra los muros de carámbanos.',
  list: [{ type: 'walrus', x: 1000 }, { type: 'gale', x: 800, y: 140 }, { type: 'gale', x: 1300, y: 160 }] });
addEncounter({ id: 'orilla', world: 'hielo', name: 'Orilla helada', desc: '2 Ermitaños + Erizo: no te dejes agarrar mientras esquivas la lluvia.',
  list: [{ type: 'hermit', x: 850 }, { type: 'hermit', x: 1250 }, { type: 'urchin', x: 1050 }] });
addEncounter({ id: 'matriarca', world: 'hielo', name: 'Matriarca', desc: 'Morsa matriarca + Colgante: tobogán y muros mientras algo cuelga encima.',
  list: [{ type: 'walrusAlpha', x: 1050 }, { type: 'icicle', x: 800 }] });
addEncounter({ id: 'hielo_todo', world: 'hielo', name: 'Todo el hielo', desc: 'Uno de cada común del hielo, para ver cómo se mezclan.',
  list: [{ type: 'icicle', x: 900 }, { type: 'urchin', x: 1300 }, { type: 'gale', x: 1100, y: 150 }, { type: 'hermit', x: 800 }, { type: 'walrus', x: 1200 }] });
// ---- bosque (aspecto: troncos, copa de hojas con ramas para posarse, plataformas-rama)
addEncounter({ id: 'claro', world: 'bosque', name: 'Claro de setas', desc: '3 Saltasetas + Zarza: tres ritmos a la vez y un muro con hueco.',
  list: [{ type: 'shroom', x: 650 }, { type: 'shroom', x: 760 }, { type: 'shroom', x: 900 }, { type: 'bramble', x: 1100 }] });
addEncounter({ id: 'zarzal', world: 'bosque', name: 'Zarzal', desc: '2 Zarzas + Atrapamoscas: la liana te deja quieto delante del muro.',
  list: [{ type: 'bramble', x: 800 }, { type: 'flytrap', x: 1000 }, { type: 'bramble', x: 1250 }] });
addEncounter({ id: 'osera', world: 'bosque', name: 'Osera', desc: 'Oso de musgo + Búho de cristal: abajo el oso, arriba el búho; subir no te salva.',
  list: [{ type: 'bear', x: 800 }, { type: 'owl', x: 1000, y: 120 }] });
addEncounter({ id: 'espesura', world: 'bosque', name: 'Espesura', desc: 'Atrapamoscas + Búho + 2 Saltasetas: te arrastra debajo del picado.',
  list: [{ type: 'flytrap', x: 900 }, { type: 'owl', x: 1100, y: 120 }, { type: 'shroom', x: 700 }, { type: 'shroom', x: 1200 }] });
addEncounter({ id: 'setamadre', world: 'bosque', name: 'Seta madre', desc: 'Seta madre + Zarza: ondas, crías y muros.',
  list: [{ type: 'shroomAlpha', x: 750 }, { type: 'bramble', x: 1150 }] });
addEncounter({ id: 'bosque_todo', world: 'bosque', name: 'Todo el bosque', desc: 'Uno de cada común del bosque.',
  list: [{ type: 'shroom', x: 700 }, { type: 'bramble', x: 1250 }, { type: 'bear', x: 950 }, { type: 'owl', x: 1100, y: 120 }, { type: 'flytrap', x: 1400 }] });
