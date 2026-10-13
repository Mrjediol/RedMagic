# Prueba de baking (flipbook) — se borra entera

Experimento: ¿la energía de la puerta horneada en una textura cuesta menos y se ve igual?
Todo está en esta carpeta; nada del juego depende de ella (la puerta original no se ha tocado).

- `Shaders/DoorEnergyBake.shader`: sólo para hornear (ruido periódico, un fotograma por fase del bucle).
- `Shaders/FxDoorEnergyFlipbook.shader`: variante en vivo: 2 lecturas del flipbook en vez de 9 evaluaciones de ruido.
- `DoorFlipbookBinder.cs`: le pone la textura al material de la energía (LevelDoor crea el material sin saber de texturas).
- `Editor/DoorFlipbookBaker.cs`: menú **Tools ▸ RedMagic ▸ Baking (prueba)**: 1 hornear · 2 poner puertas de prueba en la
  escena (viva vs horneada) · 3 **borrar todo** (quita las puertas de prueba de la escena y esta carpeta).
- `Generado/`: el Texture2DArray (16 fotogramas 128×384 R8) y la puerta variante (Prefab Variant del original).

Qué se hornea y qué no: el ruido y su deformación lenta (lo caro) van en la textura; el desplazamiento hacia arriba, los
bordes, el frente de cierre, la cola y las chispas siguen en vivo (son baratos y dependen de la puerta).
