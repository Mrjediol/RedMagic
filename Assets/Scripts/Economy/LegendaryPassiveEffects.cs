using System;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Qué pasivas legendarias tiene el jugador y a qué nivel, en un sitio estático que cualquier
    /// sistema puede leer — el jugador cambia de instancia entre hub y run, así que no puede vivir
    /// en un componente suyo.
    ///
    /// <see cref="Recompute"/> lo llama <see cref="LegendaryPassiveManager"/> cada vez que algo
    /// cambia (desbloqueo, mejora, al arrancar). <see cref="Level"/> da 0 (no la tiene), 1 o 2. El
    /// nivel 2 incluye lo del nivel 1 salvo que el texto diga "en vez de". Los números están en
    /// <see cref="LegendaryPassiveTuning"/>.
    ///
    /// Dónde se aplica cada una:
    ///  - Codex Aurum, Páginas del Eco, Anales del Vacío, Manuscrito Eterno:
    ///    <see cref="LegendaryPassiveRunner"/> (eventos de la run).
    ///  - Grimorio del Umbral, El Tomo Roto: <c>Hub.ChestLootContainer</c>.
    ///  - El Libro Sin Nombre: <c>Gameplay.LegendaryPassivePlayerHook</c> (en Player.prefab).
    ///  - Volumen Carmesí: <see cref="PlayerDamageMultiplier"/>, leído por <c>Items.PlayerHit.Deal</c>.
    ///  - Tomo del Destino: TODO (ver <see cref="Recompute"/>).
    /// </summary>
    public static class LegendaryPassiveEffects
    {
        private static readonly int KindCount = Enum.GetValues(typeof(LegendaryPassiveEffectKind)).Length;
        private static int[] _levels = new int[KindCount];

        /// <summary>Multiplicador de todo el daño que hace el jugador (Volumen Carmesí). 1 = sin bono.</summary>
        public static float PlayerDamageMultiplier { get; private set; } = 1f;

        /// <summary>Se dispara al terminar cada <see cref="Recompute"/>, con los valores ya actualizados.</summary>
        public static event Action Changed;

        /// <summary>Nivel que tiene el jugador de esa mecánica: 0 = no desbloqueada, 1 o 2.</summary>
        public static int Level(LegendaryPassiveEffectKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < _levels.Length ? _levels[i] : 0;
        }

        public static bool Has(LegendaryPassiveEffectKind kind) => Level(kind) > 0;

        /// <summary>El asset de la pasiva de esa mecánica (para nombre/icono en avisos), o null.</summary>
        public static LegendaryPassive Find(LegendaryPassiveEffectKind kind)
        {
            foreach (var passive in LegendaryPassiveLibrary.BySlot)
                if (passive != null && passive.effectKind == kind) return passive;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _levels = new int[KindCount];
            PlayerDamageMultiplier = 1f;
            Changed = null;
        }

        /// <summary>
        /// Recalcula todo desde cero a partir de lo desbloqueado/mejorado en
        /// <see cref="LegendaryPassiveManager"/>. Barato: 9 pasivas como mucho.
        /// </summary>
        public static void Recompute()
        {
            Array.Clear(_levels, 0, _levels.Length);

            var manager = LegendaryPassiveManager.Instance;
            if (manager != null)
            {
                foreach (var passive in LegendaryPassiveLibrary.BySlot)
                {
                    if (passive == null || !manager.IsUnlocked(passive)) continue;

                    int i = (int)passive.effectKind;
                    if (i < 0 || i >= _levels.Length) continue;
                    _levels[i] = Mathf.Max(_levels[i], Mathf.Max(1, manager.GetLevel(passive)));
                }
            }

            var tuning = LegendaryPassiveTuning.Current;
            PlayerDamageMultiplier = Level(LegendaryPassiveEffectKind.DamageBonus) switch
            {
                >= 2 => 1f + tuning.damageBonusLevel2,
                1 => 1f + tuning.damageBonusLevel1,
                _ => 1f,
            };

            // TODO ItemChoiceOptions (Tomo del Destino): al recoger un objeto ofrecer 3 opciones en
            //      vez de 1; nivel 2 = un reroll gratis de esas 3. Pendiente del rework de tienda.
            // TODO PermanentRerolls nivel 2 (Páginas del Eco): cada reroll muestra 2 opciones.
            //      Pendiente del rework de rerolls; el +2/+4 ya lo aplica LegendaryPassiveRunner.

            Changed?.Invoke();
        }
    }
}
