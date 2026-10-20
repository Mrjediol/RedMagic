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
  for (let i = 0; i < n; i++) list.push({ type, x: 900 + i * 150, y: def.fly ? 130 + (i % 2) * 50 : undefined });
  return { id: 'duelo:' + type + ':' + n, name: 'Duelo · ' + def.name + (n > 1 ? ' ×' + n : ''), desc: def.question, list };
}
