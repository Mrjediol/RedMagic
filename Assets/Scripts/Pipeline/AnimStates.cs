using System;

namespace RedMagic.Pipeline
{
    /// <summary>
    /// <b>La regla única de qué animaciones NO pueden repetirse</b>: muerte e impacto (y sus
    /// variantes: explosión, fin, despawn). Una animación terminal se reproduce una vez y se queda
    /// clavada en el último dibujo hasta que el objeto se destruye o vuelve al pool — nunca vuelve a
    /// empezar ni pasa a otro estado.
    ///
    /// Existe porque el bucle llegaba por datos, no por código: el exportador web marca
    /// <c>loop: true</c> por defecto en cada animación, y los importadores lo copiaban tal cual al
    /// clip (<c>loopTime</c>) o al estado del <c>SpriteStateMachine</c>. Con el cadáver visible ~1 s
    /// (<c>Corpse</c>) o el proyectil esperando a que acabe su "Impact", el bucle enseñaba el primer
    /// dibujo otra vez justo al final. La aplican:
    /// <list type="bullet">
    /// <item>los importadores al escribir el clip/estado (<c>AnimClipBuilder</c>, <c>EnemyImporter</c>,
    /// <c>FxPrefabBuilder</c>), vía <see cref="Loops"/>;</item>
    /// <item>el runtime, por si un asset viejo aún trae el bucle (<see cref="SpriteStateMachine"/>,
    /// <c>EnemyAnimation</c>, <c>VfxOneShot</c>);</item>
    /// <item>la auditoría <c>Pipeline ▸ 8/9 · Animaciones terminales</c>, que repara los assets.</item>
    /// </list>
    /// </summary>
    public static class AnimStates
    {
        // Contienen: "FireImpact", "Orbe_Impacto", "Enemy_Death", "Explosion"…
        private static readonly string[] Containing = { "death", "impact", "explo", "muerte", "despawn" };

        // Exactos: como subcadena darían falsos positivos ("Soldier", "Extend", "Bend").
        private static readonly string[] Exact = { "die", "dead", "end", "destroy" };

        /// <summary>true si el estado/clip <paramref name="name"/> es terminal (muerte, impacto…).</summary>
        public static bool IsTerminal(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            // Un clip se llama a menudo "<Personaje>_<Estado>": se mira el último tramo para los exactos.
            string lower = name.Trim().ToLowerInvariant();
            int cut = lower.LastIndexOfAny(new[] { '_', '/', ' ', '.' });
            string tail = cut >= 0 ? lower.Substring(cut + 1) : lower;

            foreach (var token in Containing)
                if (lower.Contains(token, StringComparison.Ordinal)) return true;

            foreach (var token in Exact)
                if (tail == token) return true;

            return false;
        }

        /// <summary>El bucle efectivo: lo pedido, salvo que el estado sea terminal (nunca repite).</summary>
        public static bool Loops(string name, bool requested) => requested && !IsTerminal(name);
    }
}
