# Parkour: obstáculos en medio del mapa

Pendiente desde 2026-10-17 (el usuario: "pensar en algún obstáculo que pueda estar en medio del mapa, no sólo en bordes como los pinchos").

## Estado

- Hecho: salas de parkour (`ParkourRoom`), pinchos de cristal pintables que salen de suelo / techo / pared (`SpikeTile` +
  `SpikeTilemap`, 2×2 = cristal grande), cofre de oro y plataforma de reaparición. Ver `docs/architecture/levels.md` ▸ Painted objects.
- Falta: un obstáculo que no dependa de una superficie, colocable en mitad del aire.

## Ideas a proponer (sin decidir)

- Cristal flotante que gira o pulsa (daño al tocar, vuelve a la plataforma como los pinchos).
- Péndulo / sierra de hielo que va y viene entre dos puntos.
- Bloque que cae al pisarlo / estalactita que cae al pasar por debajo.
- Viento o corriente que empuja en una zona.

## Decisiones

- Cualquiera reutiliza el golpe de los pinchos (daño + sacudida + hit-stop + vuelta a la sala): sacar ese golpe a un componente común.
