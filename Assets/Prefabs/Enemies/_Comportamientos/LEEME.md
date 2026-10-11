# Comportamientos extra para enemigos

Piezas sueltas que se añaden a cualquier enemigo sin tocar su código. La carpeta empieza por `_` para que la
Biblioteca Jere y los importadores no la confundan con un enemigo.

| Qué | Cómo se usa |
|---|---|
| `Shield.prefab` (`HitShield`) | Arrastrar como **hijo** del enemigo. Absorbe los primeros golpes y se rompe. |
| `HeatGlow` (script, no prefab) | *Add Component* en el objeto con el **sprite del arma** del enemigo (cañón, lanza…). Al disparar se pone al rojo y se enfría hasta que puede volver a disparar. |
