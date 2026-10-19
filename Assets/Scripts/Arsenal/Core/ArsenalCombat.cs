using System;
using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Fx;
using RedMagic.Items;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Un golpe de las armas del arsenal (el <c>o</c> de <c>damage(e, o)</c> en la web). Daño, de dónde viene (para el
    /// empujón), empujón (multiplicador sobre el <see cref="Knockback"/> de la víctima), si es <b>fuerte</b> (rompe
    /// congelados), elemento y cuánto congela / quema.
    /// </summary>
    public struct ArsenalHit
    {
        public float dmg;
        /// <summary>Origen del golpe; el empujón sale alejándose de aquí. Sin fijar = desde el cuerpo del jugador.</summary>
        public Vector2? from;
        /// <summary>Multiplicador del empujón propio de la víctima (0 = no empuja).</summary>
        public float knock;
        /// <summary>Velocidad hacia arriba (u/s) además del empujón (la Semilla lanza por los aires).</summary>
        public float launch;
        public bool heavy;
        public ArsenalElement elem;
        public float freeze;
        public float burn;
        /// <summary>Daño continuo (quemadura, desgarro…): sin reacciones de golpe fuerte ni marcas.</summary>
        public bool dot;
        public bool noReact;
        public HitKind kind;
        public float hitstop;
        public float shake;
        public Action<Body> onKill;
    }

    /// <summary>
    /// Reglas comunes de daño de las armas del arsenal: pasa por <see cref="PlayerHit.Deal"/> (items, sinergias, avisos al
    /// golpear y al matar) y además aplica los estados de <see cref="ElementStatus"/> y las <b>reacciones</b>:
    /// congelado + golpe fuerte = <b>añicos</b>; congelado + fuego = <b>deshielo</b> (×2, quita el hielo); quemado + hielo =
    /// <b>vapor</b> (explosión pequeña que aturde).
    /// </summary>
    public static class ArsenalCombat
    {
        private static readonly List<Body> Scratch = new List<Body>(32);

        /// <summary>Golpea a <paramref name="b"/>. Devuelve el daño hecho (0 si no entró).</summary>
        public static float Hit(Body b, ArsenalHit o)
        {
            if (b == null || b.Dead) return 0f;
            var rules = ArsenalAssets.Load();
            var st = ElementStatus.Of(b.health);
            float dmg = o.dmg;
            bool react = ArsenalBuild.Reactions && !o.noReact && st != null, frozen = st != null && st.IsFrozen;
            string label = null; bool shatter = false, thaw = false;

            if (react && frozen && o.elem == ArsenalElement.Fire) { dmg *= 2f; st.Thaw(); thaw = true; label = "arsenal.react.thaw"; SteamFx(b.c); }
            else if (react && frozen && o.heavy) { shatter = true; label = "arsenal.react.shatter"; }
            else if (react && st.IsBurning && o.elem == ArsenalElement.Ice) { st.Extinguish(); label = "arsenal.react.vapor"; Vapor(b, rules); }

            if (st != null && o.elem == ArsenalElement.Ice && o.freeze > 0f && !shatter)
                st.AddFreeze(o.freeze, rules != null ? rules.frozenTime : 2.5f);
            if (st != null && o.elem == ArsenalElement.Fire && o.burn > 0f && !thaw) st.Burn(o.burn);

            Vector2 from = o.from ?? PlayerCenter();
            float knock = st != null && st.IsFrozen ? 0f : o.knock;
            bool landed = dmg <= 0f || PlayerHit.Deal(b.health, dmg, from, knock, o.dot ? HitKind.Burst : o.kind, out _);
            if (!landed) return 0f;

            if (o.launch > 0f && knock > 0f && b.knockback != null && !b.Dead)
            {
                float dir = Mathf.Sign(b.c.x - from.x); if (dir == 0f) dir = 1f;
                b.knockback.ApplyVelocity(new Vector2(dir * Mathf.Max(2f, o.launch * 0.35f), o.launch), 0.35f);
            }
            if (shatter) Shatter(b, rules);
            if (label != null) Label(b.c + Vector2.up * (b.ry + 0.8f), label, label == "arsenal.react.shatter" ? new Color(0.8f, 0.96f, 1f) : label == "arsenal.react.vapor" ? new Color(0.88f, 0.91f, 1f) : new Color(1f, 0.81f, 0.48f));
            if (o.hitstop > 0f && Application.isPlaying) HitStop.Play(o.hitstop);
            if (o.shake > 0f) ArsenalFeel.Shake(o.shake);
            if (b.Dead) o.onKill?.Invoke(b);
            return Mathf.Max(dmg, 0.0001f);
        }

        /// <summary>Daño en área (el <c>damageArea</c> de la web): cae con la distancia salvo <paramref name="falloff"/> = false. Devuelve los golpeados.</summary>
        public static List<Body> Area(Vector2 p, float r, ArsenalHit o, bool falloff = true, List<Body> into = null)
        {
            into ??= new List<Body>(8);
            into.Clear();
            Scratch.Clear();
            Scratch.AddRange(ArsenalTargets.Alive());
            foreach (var e in Scratch)
            {
                float d = Vector2.Distance(e.c, p) - Mathf.Max(e.rx, e.ry) * 0.6f;
                if (d > r) continue;
                float k = falloff ? 1f - 0.5f * Mathf.Clamp01(d / r) : 1f;
                var h = o; h.dmg = o.dmg * k; h.knock = o.knock * k; h.launch = o.launch * k; h.from = p;
                Hit(e, h);
                into.Add(e);
            }
            return into;
        }

        /// <summary>Atrae (o arrastra) a <paramref name="b"/> hacia <paramref name="to"/> a <paramref name="speed"/> u/s este frame.
        /// Usa el empujón de la víctima, que la IA ya respeta; congelado va a la mitad. <paramref name="lift"/> = también en vertical.</summary>
        public static void Pull(Body b, Vector2 to, float speed, bool lift = false)
        {
            if (b == null || b.Dead || b.knockback == null) return;
            var st = ElementStatus.Peek(b.health);
            if (st != null && st.IsFrozen) speed *= 0.5f;
            Vector2 d = to - b.c; float dist = d.magnitude;
            if (dist < 0.35f) { b.knockback.ApplyVelocity(Vector2.zero, 0.1f); return; }
            Vector2 v = d / dist * speed;
            if (!lift && !b.flies && b.rb != null) v.y = Mathf.Min(0f, b.rb.linearVelocity.y);
            b.knockback.ApplyVelocity(v, 0.1f);
        }

        /// <summary>Aviso flotante localizado (reacciones, «¡JUSTO!»…).</summary>
        public static void Label(Vector2 p, string key, Color color, params object[] args)
        {
            if (!Application.isPlaying) return;
            string text = args != null && args.Length > 0 ? Loc.Get(key, args) : Loc.Get(key);
            DamagePopups.ShowLabel(p, text, color);
        }

        public static Vector2 PlayerCenter()
        {
            var u = WeaponUser.Current;
            return u != null ? PlayerHit.BodyCenter(u) : Vector2.zero;
        }

        // ------------------------------------------------------------------ reacciones

        private static void Shatter(Body b, ArsenalAssets rules)
        {
            var st = ElementStatus.Peek(b.health);
            st?.Thaw();
            float dmg = rules != null ? rules.shatterDamage : 45f, r = rules != null ? rules.shatterRadius : 2f, splash = rules != null ? rules.shatterSplash : 15f;
            bool ice6 = PlayerHit.SynergyActive(BuildTag.Ice, 6);
            if (!b.Dead) PlayerHit.Deal(b.health, dmg * (ice6 ? 2f : 1f), b.c, 0f, HitKind.Burst);
            Scratch.Clear(); Scratch.AddRange(ArsenalTargets.Alive());
            foreach (var o in Scratch)
                if (o != b && Vector2.Distance(o.c, b.c) < r)
                    Hit(o, new ArsenalHit { dmg = splash, from = b.c, knock = 0.6f, elem = ArsenalElement.Ice, freeze = 0.25f, noReact = true, kind = HitKind.Burst });
            ArsenalReactFx.Shatter(b.c, b.rx, b.ry);
            if (Application.isPlaying) HitStop.Play(0.06f);
            ArsenalFeel.Shake(0.18f);
        }

        private static void Vapor(Body b, ArsenalAssets rules)
        {
            float dmg = rules != null ? rules.vaporDamage : 25f, r = rules != null ? rules.vaporRadius : 1.75f, stun = rules != null ? rules.vaporStun : 0.6f;
            Scratch.Clear(); Scratch.AddRange(ArsenalTargets.Alive());
            foreach (var o in Scratch)
            {
                if (Vector2.Distance(o.c, b.c) >= r) continue;
                PlayerHit.Deal(o.health, dmg, b.c, 0f, HitKind.Burst);
                ElementStatus.Of(o.health)?.Stun(stun);
            }
            SteamFx(b.c);
        }

        private static void SteamFx(Vector2 p) => ArsenalReactFx.Steam(p);
    }
}
