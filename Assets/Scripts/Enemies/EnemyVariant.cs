using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// Una versión de cualquier enemigo sin hacerle prefab nuevo: más fuerte / más débil, más grande / más pequeño, de
    /// otro color (shader <c>RedMagic/Sprite Lit FX</c>) y, si se quiere, con algo colgado (el escudo
    /// <c>_Comportamientos/Shield.prefab</c>…). Sirve para dos cosas: las <b>especiales</b> (naranja, morada, dorada:
    /// las elige el modo Supervivencia según la dificultad de la oleada) y los <b>colores de zona</b> provisionales
    /// (rojo volcán, verde bosque) hasta que existan enemigos propios de esas zonas.
    /// Se aplica en tiempo de juego con <see cref="Apply"/>, justo después de crear el enemigo.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Enemigos/Variante de enemigo", fileName = "Variant_")]
    public class EnemyVariant : ScriptableObject
    {
        [Tooltip("Dificultad que representa (1 = 100 %, 1,4 = 140 %). En Supervivencia sólo sale cuando la dificultad " +
                 "de la oleada llega a este valor.")]
        [Min(0f)] public float difficulty = 1.4f;

        [Header("Fuerza")]
        [Min(0.01f)] public float healthMultiplier = 1f;
        [Min(0f)] public float damageMultiplier = 1f;
        [Tooltip("Escala del enemigo entero (colliders incluidos).")]
        [Min(0.1f)] public float sizeMultiplier = 1f;

        [Header("Color (shader Sprite Lit FX)")]
        [Tooltip("Pinta el enemigo. Apagado = sólo cambia la fuerza (y el hijo).")]
        public bool recolor = true;
        [Tooltip("Giro de tono del shader: resta al tono. Desde el azul hielo: -0,18 morado, +0,27 verde, +0,47 dorado, " +
                 "-0,4 rojo, -0,48 naranja.")]
        [Range(-0.5f, 0.5f)] public float hue;
        [Range(0f, 2f)] public float saturation = 1f;
        [Range(0f, 2f)] public float brightness = 1f;
        [Tooltip("Borde interior de color (alfa 0 = sin borde). Lo que más ayuda a reconocer una especial.")]
        [ColorUsage(true, true)] public Color rimColor = new Color(0f, 0f, 0f, 0f);
        [Range(1f, 200f)] public float rimWidth = 40f;
        [Tooltip("Brillo propio en las zonas claras (alfa 0 = sin brillo).")]
        [ColorUsage(true, true)] public Color glowColor = new Color(0f, 0f, 0f, 0f);
        [Tooltip("Destello diagonal que recorre el cuerpo (alfa 0 = sin destello).")]
        [ColorUsage(true, true)] public Color sheenColor = new Color(0f, 0f, 0f, 0f);

        [Header("Extra")]
        [Tooltip("Prefab que se le cuelga como hijo al crearlo (p. ej. Prefabs/Enemies/_Comportamientos/Shield.prefab).")]
        public GameObject attachPrefab;

        // ------------------------------------------------------------------ aplicar

        /// <summary>
        /// Escala vida (cura a tope), daño y tamaño de <paramref name="enemy"/>. Acumula: llamarlo dos veces multiplica dos
        /// veces (oleada × variante).
        /// </summary>
        public static void ApplyStats(GameObject enemy, float healthMultiplier, float damageMultiplier, float sizeMultiplier = 1f)
        {
            if (enemy == null) return;
            var health = enemy.GetComponent<Health>();
            if (health != null && healthMultiplier != 1f) health.SetMaxHealth(health.MaxHealth * healthMultiplier, true);
            if (damageMultiplier != 1f && enemy.TryGetComponent(out IOutgoingDamageScale scale))
                scale.OutgoingDamageScale *= damageMultiplier;
            if (sizeMultiplier != 1f) enemy.transform.localScale *= sizeMultiplier;
        }

        /// <summary>Fuerza, hijo y (si <paramref name="withLook"/>) color de esta variante sobre <paramref name="enemy"/>.</summary>
        public void Apply(GameObject enemy, Shader litFx, bool withLook = true)
        {
            if (enemy == null) return;
            ApplyStats(enemy, healthMultiplier, damageMultiplier, sizeMultiplier);
            if (attachPrefab != null) Instantiate(attachPrefab, enemy.transform, false);
            if (withLook) ApplyLook(enemy, litFx);
        }

        /// <summary>Sólo el color, sin tocar la fuerza (colores de zona).</summary>
        public void ApplyLook(GameObject enemy, Shader litFx)
        {
            if (!recolor || enemy == null || litFx == null) return;
            foreach (var sr in enemy.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var source = sr.sharedMaterial;
                if (!Recolorable(source, litFx)) continue;
                sr.sharedMaterial = MaterialFor(source, sr.sprite != null ? sr.sprite.texture : null, litFx);
            }
        }

        // Sólo las partes con el material normal de sprite (o ya con el FX): los aditivos y efectos se quedan como están.
        private static bool Recolorable(Material m, Shader litFx) =>
            m != null && m.shader != null && (m.shader == litFx || m.shader.name.Contains("Sprite-Lit-Default"));

        // Un material por (material de origen, variante, textura): los enemigos iguales siguen compartiendo material, y
        // nunca comparten uno piezas de texturas distintas — con este shader se dibujaban con la textura de otra pieza.
        private static readonly Dictionary<(int, int, int), Material> Cache = new Dictionary<(int, int, int), Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (var m in Cache.Values) if (m != null) Destroy(m);
            Cache.Clear();
        }

        private Material MaterialFor(Material source, Texture texture, Shader litFx)
        {
            var key = (source.GetInstanceID(), GetInstanceID(), texture != null ? texture.GetInstanceID() : 0);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            // Del FX se copia todo (sombra de contacto, degradado…); del normal sólo las texturas que comparten.
            Material m;
            if (source.shader == litFx) m = new Material(source);
            else
            {
                m = new Material(litFx);
                if (source.HasProperty("_MaskTex")) m.SetTexture("_MaskTex", source.GetTexture("_MaskTex"));
                if (source.HasProperty("_NormalMap")) m.SetTexture("_NormalMap", source.GetTexture("_NormalMap"));
            }
            m.name = $"{source.name} · {name}";
            m.hideFlags = HideFlags.DontSave;
            if (texture != null) m.SetTexture("_MainTex", texture);
            m.SetFloat("_Hue", hue);
            m.SetFloat("_Saturation", saturation);
            m.SetFloat("_Brightness", brightness);
            if (rimColor.a > 0f) { m.SetColor("_RimColor", rimColor); m.SetFloat("_RimWidth", rimWidth); }
            if (glowColor.a > 0f) m.SetColor("_GlowColor", glowColor);
            if (sheenColor.a > 0f) m.SetColor("_SheenColor", sheenColor);
            Cache[key] = m;
            return m;
        }
    }
}
