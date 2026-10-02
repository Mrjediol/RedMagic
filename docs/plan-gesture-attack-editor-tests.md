# Editor de gestos — pruebas pendientes (Paso 1)

Lo que Claude comprobó: los módulos cargan sin errores de consola, la barra de tiempo se dibuja, y el
export de ShroomShaman por `make-enemy.mjs` sale idéntico al de antes. Lo que sigue sólo se puede juzgar a mano.

Servir la web (`npx serve Web/RedMagicWeb`) ▸ 🧬 Enemy Studio.

## En la web

1. **Abrir un enemigo guardado antes de este cambio** (Biblioteca ▸ Editar en Enemy Studio).
   - Sus gestos siguen ahí, ahora con 2 pasos (Carga / Golpe) o 1 (los que no tenían golpe).
   - Sus efectos siguen en el mismo momento y sitio. → Si alguno cambió de momento, dime cuál.
2. **Abrir un gesto**: aparece la línea de tiempo bajo el lienzo.
   - ▶ Probar (o Espacio): el cuerpo hace el gesto igual que antes.
   - Arrastrar la regla de arriba: el cuerpo se para en ese instante.
   - ¿Se lee bien a tu tamaño de pantalla? ¿Quita demasiado alto al lienzo?
3. **Pasos**: `＋ Paso`, clic en un bloque, `✋ Posar este paso`, mover piezas, salir.
   - Arrastrar el borde derecho de un bloque cambia su duración.
   - Arrastrar un bloque sobre otro cambia el orden (también ◀ ▶).
   - "Dura el aviso del ataque": el bloque rayado se estira al cambiar "Aviso".
4. **Sueltas (◆)**: `◆ Suelta`, arrastrarla por la línea.
   - En el lienzo aparece una diana roja: arrastrarla = de dónde sale el ataque.
   - "Sale de": probar una pieza que no sea la boca.
   - Al reproducir, un anillo rojo salta en cada suelta. ¿Cae donde y cuando esperas?
5. **Efectos (✨)**: añadir uno desde el desplegable, y otro arrastrándolo desde el paso 3 (Efectos) a la línea.
   - Moverlo por la línea; "En bucle" + "Se apaga al acabar".
   - "Mira": acción / ancla / derecho. "Capa": detrás del cuerpo.
   - Arrastrar su cruz en el lienzo.
6. **Sprites de este gesto**: los desplegables "se ve desde / hasta" ahora nombran los pasos.
7. Ctrl+Z / Ctrl+Y tras varias de las anteriores.
8. **⬆ Traer de un rig-config.json**: en Unity, Tools ▸ Web ▸ Rig ▸ exportar un enemigo por partes; traer ese
   JSON sobre el mismo enemigo abierto en la web. Sus gestos deben aparecer con sus pasos y sueltas.

## En Unity

9. Exportar el .zip de un enemigo con un gesto de 3+ pasos y 2 sueltas ▸ Tools ▸ Web ▸ Rig ▸ Importar rig.
   - Sin avisos nuevos en el informe de importación.
   - En el Inspector, el `RigGesture` tiene los mismos pasos / sueltas / efectos.
   - En juego: ¿el gesto se ve como en la vista previa de la web? (tiempos, orden, de dónde sale el disparo)
10. Quitar un efecto de un gesto en la web, reexportar, reimportar: el efecto desaparece en Unity.

## Qué decirme

- Paso en el que falla + qué viste.
- Cualquier cosa de la línea de tiempo que resulte incómoda (tamaño, arrastres, qué falta a la vista).
