// a2b-singularidad-variantes.js — 2b · Singularidad: ronda de aspectos refinados (2026-10-19). Mismo comportamiento que la base
// (viaja, atrae, colapsa, estalla); cambia sólo cómo se ve. Los dibujos están en fx-sing.js (compartidos con la jugable).
'use strict';
let orbOver = false;                                        // interruptor de la cabecera: todo el vórtice por encima de los enemigos
(function () {
  const TR = 0.32, XS = 462, YS = HY - 10;
  function build(P) {
    const look = P.look, pal = PAL.void, bp = PAL.set, s = 1, dur = P.dur || 0.9;
    const TA = TR + 0.5, TC = TA + dur, TB = TC + 0.14, total = TB + 1.15;
    const orbX = t => t < TA ? lerp(O.x + 10, XS, easeOut(seg(t, TR, TA))) : XS;
    const orbY = t => t < TA ? YS - 26 * bump(seg(t, TR, TA)) : YS + Math.sin(t * 5) * 2;
    const pullU = t => t < TC ? easeIn(seg(t, TA, TC)) * 0.9 + 0.1 * seg(t, TA, TC) : (1 - easeOut(seg(t, TB, TB + 0.3)));
    const mkE = ex => {
      const E = { x: ex, hits: [] }, dx = XS - ex;
      E.pull = t => [dx * 0.42 * pullU(t) + (t > TA && t < TB ? Math.sin(t * 61 + ex) * 1.5 : 0), 0];
      E.suck = t => (t > TA && t < TB ? { x: orbX(t), y: orbY(t), k: Math.min(1, pullU(t) * 1.3) } : null);
      E.hits.push({ t: TB, k: Math.sign(-dx) * 28, fl: 0.28, shake: 6, sq: 0.16, d: 0.6 });
      return E;
    };
    const enemies = [mkE(400), mkE(532)];
    const sfx = [{ t: 0.02, f: k => SFXLIB.castShort(k) }, { t: TR, f: SND.sing_launch.fn }, ...(look === 'rift' ? [{ t: TA, f: SND.sing_tear.fn }] : []), { t: TC, f: SND.sing_collapse.fn }, { t: TB, f: SND.sing_burst.fn }];
    function draw(ctx, t, layer) {
      castFx(ctx, t, layer, 0.04, TR, pal, 1.1);
      if (t > TR && t < TB) singLook(look, ctx, t, layer, orbX(t), orbY(t), { s, pal, bp, form: easeOut(seg(t, TR, TA)), collapse: seg(t, TC, TB), pullT: t > TA && t < TC ? t - TA : -1, liveT: t - TR, phase: t < TA ? 'travel' : 'live', openT: t - TA,
        trail: t < TA ? [0.025, 0.05, 0.075, 0.1, 0.125].filter(d => t - d > TR).map(d => ({ x: orbX(t - d), y: orbY(t - d) })) : [] }, orbOver);
      if (t >= TB) singBurst(look, ctx, t, layer, TB, XS, YS, s, bp, {});
    }
    return { total, draw, enemies, playerPose: t => poseCast(t, 0.04, TR, { lean: 0.1, kick: 0.16 }), shakes: [{ t: TB, a: 9, d: 0.4 }], frames: [{ t: TB, d: 0.06 }], sfx };
  }
  addAbility({
    id: 'singv', num: '2b', name: 'Singularidad — variantes visuales', short: 'Singularidad (aspecto)',
    pitch: 'Ronda de aspectos para la Singularidad: la primera versión eran anillas con huecos. Mismo comportamiento (viaja, atrae, colapsa, estalla), cinco formas de verse, todas con superficies continuas. El interruptor «Vórtice por encima de los enemigos» de la cabecera compara las dos formas de ordenar las capas. Los atrapados se estiran hacia el centro.',
    fit: 'Elige uno (o mezcla piezas: p. ej. el disco del Horizonte con la rejilla del Abismo) y se adopta en la jugable y en Unity. En la jugable: Ajustes de la Singularidad ▸ «Aspecto».',
    clips: [
      { tab: 'wip', title: '2b · 1 · Horizonte de sucesos', tag: 'Variante', text: 'Disco de acreción liso: blanco caliente por dentro que se enfría a violeta, con textura de seda que gira (más rápido cerca del centro) y el lado que se acerca más brillante. Sobre la sombra, el anillo de luz de la parte trasera del disco curvada por la gravedad (estilo Interstellar). Filamentos que caen en espiral. Estallido: onda continua y destello alargado.', P: { look: 'horizon' } },
      { tab: 'wip', title: '2b · 2 · Remolino', tag: 'Variante', text: 'Cuatro brazos en espiral de tinta violeta (oscuro → violeta → blanco en el filo) que giran hacia un ojo negro, en perspectiva (pasan por detrás y por delante del ojo); gotas arrastradas por los brazos. Más orgánico y legible de lejos.', P: { look: 'whirl' } },
      { tab: 'wip', title: '2b · 3 · Grieta', tag: 'Elegida', text: 'Elegida por el usuario. Viaja un orbe pequeño (con la grieta anunciada en vertical) y, al llegar, se abre en el sitio: línea de luz que crece y se rasga con destello y chispas. Dura más (atrae 1,8 s). Un desgarro vertical en el espacio: dentro, vacío con estrellas y nebulosa; los bordes arden (morado, cian, blanco) y saltan chispas eléctricas; escombros orbitan y se cuelan por la grieta. Al colapsar se cierra de golpe en una línea de luz y escupe los escombros.', P: { look: 'rift', dur: 1.8 } },
      { tab: 'wip', title: '2b · 4 · Abismo (rejilla)', tag: 'Variante', text: 'La rejilla del espacio-tiempo se hunde hacia un punto negro: se lee «gravedad» al instante, sin anillos. Filamentos que caen. Al estallar, la onda recorre la rejilla hacia fuera.', P: { look: 'grid' } },
      { tab: 'wip', title: '2b · 5 · Lente', tag: 'Variante', text: 'Una esfera que refracta lo que hay detrás (fondo y enemigos), aumentado y retorcido en remolino hacia un centro oscuro; borde de cristal con aberración cromática y un brillo. Es el efecto real de un agujero negro sobre lo que lo rodea.', P: { look: 'lens' } },
      { tab: 'wip', title: '2b · 0 · Anillas (versión anterior)', tag: 'Referencia', text: 'La primera versión, para comparar.', P: { look: 'rings' } },
    ],
    build,
  });
})();
