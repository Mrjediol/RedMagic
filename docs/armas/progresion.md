# Progresión de armas y builds en la run (propuesta en prueba, 2026-10-19)

Pedido del usuario (2026-10-19): las mejoras de un arma no pueden ser items generales (un item «Lluvia» no hace nada en el
Abanico). Quiere **tres vías** de mejora, un árbol de arma **profundo** (varias formas avanzadas de la misma arma, no «3 flechas
→ 10 flechas que rebotan») y la curva de un roguelite: **difícil de base**, fácil con una run con algo de suerte, y rota con un
combo muy bueno. Se experimenta primero con **Abanico** y **Golpe estelar**.

## Las tres vías

| Vía | Qué es | Dónde se consigue (propuesta) | Regla de diseño |
|---|---|---|---|
| **1 · Árbol del arma** | Evolución (forma avanzada) + mejoras de esa evolución | Forja con Skulls (`WeaponLevelManager`, niveles) | Profundidad de UNA arma; cada evolución cambia cómo se juega |
| **2 · Sinergias generales** | Reglas del mundo: reacciones elementales, captura, marcas | No se compran: salen al combinar | Funcionan con cualquier pareja de armas |
| **3 · Items generales** | Pasivos con rareza: elemento, Duplicador, gatillos… | Tienda (oro) / cofres | **Sólo usan eventos que tienen todas las armas** (golpear, golpe fuerte, matar, cambiar de arma, dash, recargar) o un parámetro que todas definen (nº de proyectiles, elemento) → nunca un item muerto |

## 1 · Árbol del arma

- **Nivel 1**: el arma base.
- **Nivel 2 — Evolución**: 1 de 3 formas avanzadas. En la run se ofrecen **2 al azar** (no siempre puedes ir a tu favorita).
- **Niveles 3, 4, 5 — Mejoras**: cada vez se ofrecen **2 al azar** de las 3 mejoras de tu evolución que aún no tienes (la
  última subida puede no darte a elegir). Máximo = evolución + sus 3 mejoras.
- Las armas aún no rediseñadas (Singularidad, Rayo, Media luna, Semilla) tienen de momento una lista plana de mejoras provisionales.

### Abanico

| Evolución | Idea | Mejoras |
|---|---|---|
| **Erizo** | Las esquirlas se clavan (enemigos y suelo) y **estallan al cambiar de arma** (o solas al rato): premia alternar armas | Retorno (vuelven atravesando y recargan el arma de reserva) · Metralla (cada estallido suelta esquirlas pequeñas) · Hemorragia (cada clavada hace daño continuo) |
| **Lanza** | Ráfaga en **línea**, rápida y lejana, que perfora: el abanico de lejos | Perforación (atraviesa todo, +daño por enemigo atravesado) · Aceleración (manteniendo, cada ráfaga sale antes) · Cristal mayor (la última de cada ráfaga es una lanza grande, golpe fuerte) |
| **Caleidoscopio** | Rebotes (suelo, plataformas, enemigos) que **ganan daño** con cada rebote | Buscador (todo rebote busca al enemigo más cercano) · Prisma (al rebotar en un enemigo se divide en dos) · Eterno (+3 rebotes y no caducan mientras rebotan) |

### Golpe estelar

| Evolución | Idea | Mejoras |
|---|---|---|
| **Bombardeo** | Lanzas más pequeñas en **fila** desde el punto marcado hacia delante | Salva (+2 lanzas) · Final (la última es enorme) · Rodante (la fila sigue al ratón mientras cae) |
| **Cazador** | Tus otras armas **marcan**; las lanzas caen solas sobre los marcados | Acumulación (marcas apilables hasta 3: más daño por marca) · Ejecución (remata a los que quedan con poca vida y marca al siguiente) · Vigilancia (cada 4 s cae una lanza sola sobre un marcado, aunque lleves la otra arma) |
| **Tormenta** | Lluvia de agujas sobre una zona | Diluvio (más agujas, dura el doble y sigue al ratón) · Agujas pesadas (golpes fuertes: rompen congelados) · Ojo de la tormenta (atrae hacia el centro y acaba con una lanza) |

## 3 · Items generales (rareza: común · raro · legendario)

| Item | Rareza | Efecto (en todas las armas) |
|---|---|---|
| Esencia de hielo | raro (hueco Elemento) | Los golpes congelan; cada arma lo expresa a su manera (tabla abajo) |
| Esencia de fuego | raro (hueco Elemento) | Los golpes queman; ídem |
| **Duplicador** | legendario | ×2 proyectiles: 2 lanzas, el doble de esquirlas, 2 semillas, 2 hojas, 2 haces; **2 singularidades que se funden en una mayor al llegar** |
| Gatillo de cambio | raro | Tras cambiar de arma, el siguiente ataque (1,5 s) hace +60 % y es golpe fuerte |
| Constelación | raro | Cada 8 golpes cae una lanza pequeña sobre el último enemigo golpeado |
| Corazón de vacío | raro | Los golpes fuertes atraen un instante a los enemigos cercanos |
| Cristal afilado | común | +20 % de daño |
| Engranaje | común | −15 % de recarga |
| Sangre de cristal | común | Cada enemigo eliminado cura 3 |

### Cómo expresa cada arma el elemento

| Arma | Hielo | Fuego |
|---|---|---|
| Abanico | congela por esquirla; congelado + golpe fuerte = añicos | quema por esquirla |
| Singularidad | el colapso congela a todos los atrapados (bloque) | el colapso quema y abre llamas |
| Rayo | congela por tick; al soltar, añicos | quema por tick |
| Media luna | la ida congela, **la vuelta rompe** (golpe fuerte) | estela de llamas colgada |
| Semilla | nova de escarcha (picos, congela todo) | charco de fuego donde estalla |
| Golpe estelar | el impacto congela la columna y alrededor | cráter ardiendo |

## Curva de dificultad (para probar)

Panel «Dificultad» de la jugable: vida de los enemigos ×1-×4 y que disparen. Objetivo a comprobar jugando: arma base sola =
cuesta; evolución + 1 mejora + 2 comunes = cómodo; evolución completa + Duplicador + elemento con reacciones = arrasa.

## Abierto

- ¿Skulls para el árbol y oro para items (lo que ya hay), o una sola moneda?
- ¿El elemento es de toda la build (propuesta) o por arma?
- ¿Cuántas evoluciones por arma en el juego final (3 parece el mínimo para que haya variedad entre runs)?
