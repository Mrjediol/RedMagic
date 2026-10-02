# Editor de gestos y ataques — pruebas pendientes

Todo en la web menos el bloque final. Un único import en Unity, al final. Rama: `task/gesture-attack-editor`.

## A · Línea de tiempo (🧬 Enemy Studio)

1. Abre un enemigo de tu biblioteca y uno de sus gestos. ▶ Probar: ¿se ve igual que antes?
2. Añade un paso, pósalo, arrastra su borde. Clic en el bloque + **Supr**: se quita el paso (no la pieza).
3. Añade una ◆ suelta, arrástrala. Arrastra la diana roja en el lienzo. Clic en la ◆ + **Supr**: se quita.

## B · Ataques por suelta (misma pantalla)

4. Con el gesto abierto, a la derecha: **⚔ Ataques que usan este gesto ▸ ＋ Ataque con este gesto**.
   Sale un hueco por cada ◆ suelta. Añade otra suelta en la línea: ¿aparece su hueco?
5. En un hueco cambia "Qué sale" a abanico, en otro a "Nada". Elige el arte de un proyectil.
6. ▶ Probar: ¿sale cada cosa en su suelta, desde su punto, hacia el objetivo 🎯? Arrastra el objetivo y repite.
7. Abre **koros** o **IceRobot**: su antigua "variante del ataque propio" debe aparecer como un ataque
   "Propio…" con su gesto. ¿Está, con su proyectil?
8. Paso 4 ▸ Defensa: en una defensa elige "Ataque que lanza al apartarse".

## C · Pestaña 🎬 Gestos y ataques

9. Elige un enemigo. ¿Ves sólo gestos, ataques y defensa? Intenta mover una pieza en el lienzo: no debe moverse.

## D · Una vez en Unity (al final)

10. En 🎬: **⬇ Exportar sólo gestos y ataques** de un enemigo ya importado ▸ Tools ▸ Web ▸ Rig ▸ Importar rig.
    El informe debe decir que el cuerpo y lo afinado no se han tocado.
11. Juega contra él: ¿dispara en cada suelta lo que pusiste? ¿La defensa con ataque dispara al apartarse?
12. Juega contra **ShroomShaman**, **koros** e **IceRobot** (convertidos automáticamente): ¿atacan como antes?
    Fíjate sobre todo en si su disparo suena y si apunta igual.

Dime sólo lo que falle o resulte incómodo.
