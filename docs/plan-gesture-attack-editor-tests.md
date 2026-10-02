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

## E · El puente Unity ⇄ web (sin exportar ni importar)

Con la web servida en `http://localhost:3000` y Unity abierto.

13. Unity ▸ selecciona el prefab **IceRobot2** ▸ Inspector ▸ EnemyStats ▸ **🌐 Editar gestos y ataques en la web**
    (arriba del todo). Se abre el navegador en 🎬 con IceRobot2 y el recuadro **🔌 Unity · IceRobot2** a la izquierda.
14. Cambia la duración de un paso ▸ **💾 Guardar en Unity** (mismo recuadro): sale "✓ Guardado en Unity" con el
    informe. En Unity, ProceduralRig ▸ Gestures: ¿tiene el valor nuevo y conserva su sonido?
15. En Unity cambia algo de un **gesto** en el Inspector del prefab y guarda. En la web, sin recargar, pulsa
    Guardar en Unity: debe decir **"Cambió en Unity"** con *Recargar desde Unity* / *Sobrescribir*.
16. En el Inspector, despliega **"Abrir la web en…"** y pulsa **✎ web** en un ataque: ¿se abre con ese ataque y su gesto?
17. Haz lo mismo con **ShroomShaman** (no está en tu biblioteca web: el cuerpo se lee de Unity). ¿Se ve bien montado?
    Pulsa ▶ Probar en un gesto: ¿suena su sonido?
18. Cierra Unity y pulsa Guardar en Unity en la web: debe decir "Unity no responde", sin perder nada.

## F · Sonido, línea de tiempo y gestos de transición

**Antes: cierra y vuelve a abrir Unity una vez** (el puerto del puente se quedó atascado en mis pruebas).

19. ShroomShaman ▸ **🌐 Editar…** ▸ abre un gesto. Bajo el lienzo hay una pista **🔊** con su sonido. Haz clic en
    ella: a la izquierda sale el panel **"🔊 Salida de audio"** (clip, volumen, tono, momento).
20. Arrastra el 🔊 a otro instante y pulsa ▶ Probar: ¿suena ahí? Cambia el clip en el desplegable (hay un filtro
    por nombre) y el tono: ¿suena distinto?
21. Línea de tiempo: **+ / −** (o Ctrl+rueda) para el zoom; **← / →** mueven el cursor un frame; elige un efecto y
    **Ctrl+← / →** lo mueve; **Ctrl+D** lo duplica; cursor a mitad de un paso + **Ctrl+B** lo divide.
22. Quita la ◆ suelta de un gesto que no use ningún ataque: ya no debe quedar el rombo transparente.
23. En un gesto corto marca **🔁 Gesto de transición** (derecha, en su tarjeta) ▸ Guardar en Unity ▸ juega: ¿lo
    hace a veces entre ataque y ataque? (Sólo sale si el gesto entero cabe en el `attackCooldown` del enemigo;
    la probabilidad está en "🔁 Entre ataques".)
24. Guarda en Unity y juega: ¿el sonido suena en el instante nuevo?

Dime sólo lo que falle o resulte incómodo.
