using System;
using System.Collections.Generic;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Árbol de un arma (como <c>archetypes</c> en la web): nombre y descripción de cada evolución y de sus 3 mejoras.
    /// Índice 0 de <see cref="evolutions"/> = sin evolución (el arma base). Lo leen el Inspector (etiquetas de las casillas)
    /// y el menú DEV. Textos para el editor/DEV; cuando lleguen a una pantalla del jugador pasarán por localización.
    /// </summary>
    public sealed class ArsenalTree
    {
        public sealed class Evo
        {
            public string name, desc;
            public string[] ups = new string[3], upDescs = new string[3];
        }

        public readonly List<Evo> evolutions = new List<Evo>();

        public ArsenalTree(string baseDesc)
        {
            evolutions.Add(new Evo { name = "Base", desc = baseDesc });
        }

        public ArsenalTree Add(string name, string desc, params string[] upNamesAndDescs)
        {
            var e = new Evo { name = name, desc = desc };
            for (int i = 0; i < 3 && i * 2 + 1 < upNamesAndDescs.Length; i++) { e.ups[i] = upNamesAndDescs[i * 2]; e.upDescs[i] = upNamesAndDescs[i * 2 + 1]; }
            evolutions.Add(e);
            return this;
        }
    }

    /// <summary>
    /// Un arma del arsenal: <b>qué</b> arma es (el tipo), su <b>evolución</b> (0 = base), sus 3 <b>mejoras</b> (las de la
    /// evolución elegida) y todos sus ajustes. Va en <c>WeaponDefinition ▸ Arsenal</c>. No guarda estado de juego: el estado
    /// vive en el <see cref="ArsenalController"/> que crea.
    /// </summary>
    [Serializable]
    public abstract class ArsenalKit
    {
        [Tooltip("Evolución (nivel 2): 0 = arma base. Los nombres salen en el desplegable.")]
        [ArsenalEvolution] public int evolution;

        [Tooltip("Mejora 1 de la evolución elegida (nivel 3-5). Sin evolución no hace nada.")]
        [ArsenalUpgrade(0)] public bool upgrade1;
        [Tooltip("Mejora 2 de la evolución elegida.")]
        [ArsenalUpgrade(1)] public bool upgrade2;
        [Tooltip("Mejora 3 de la evolución elegida.")]
        [ArsenalUpgrade(2)] public bool upgrade3;

        /// <summary>Evoluciones y mejoras (datos fijos del tipo).</summary>
        public abstract ArsenalTree Tree { get; }

        /// <summary>Id corto del arma, el de la web (fan, sing, ray, moon, seed, star): nombre del asset y clave de idioma.</summary>
        public abstract string WeaponId { get; }

        /// <summary>Crea el controlador de juego de esta arma para un hueco.</summary>
        public abstract ArsenalController CreateController();

        public bool Evo(int e) => evolution == e;

        /// <summary>¿Está activa la mejora <paramref name="i"/> (0..2) de la evolución <paramref name="evo"/>?</summary>
        public bool Up(int evo, int i) => evolution == evo && evo > 0 && (i == 0 ? upgrade1 : i == 1 ? upgrade2 : upgrade3);

        public bool GetUpgrade(int i) => i == 0 ? upgrade1 : i == 1 ? upgrade2 : upgrade3;

        public void SetUpgrade(int i, bool on)
        {
            if (i == 0) upgrade1 = on; else if (i == 1) upgrade2 = on; else upgrade3 = on;
        }

        /// <summary>Copia independiente (el menú DEV edita una copia, nunca el asset).</summary>
        public ArsenalKit Clone()
        {
            var c = (ArsenalKit)Activator.CreateInstance(GetType());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), c);
            return c;
        }
    }

    /// <summary>Dibujo en el Inspector: desplegable con los nombres de las evoluciones del kit.</summary>
    public sealed class ArsenalEvolutionAttribute : PropertyAttribute { }

    /// <summary>Dibujo en el Inspector: la casilla lleva el nombre de la mejora <see cref="index"/> de la evolución elegida.</summary>
    public sealed class ArsenalUpgradeAttribute : PropertyAttribute
    {
        public readonly int index;
        public ArsenalUpgradeAttribute(int index) { this.index = index; }
    }

    /// <summary>
    /// Kits efectivos en Play: el del asset, o la copia que haya tocado el menú DEV (como <c>CometRuntime</c>). Nunca se
    /// escribe en el asset.
    /// </summary>
    public static class ArsenalRuntime
    {
        private static readonly Dictionary<WeaponDefinition, ArsenalKit> Overrides = new Dictionary<WeaponDefinition, ArsenalKit>();

        /// <summary>Sube cada vez que el menú DEV cambia algo: los controladores lo usan para rehacer lo que dependa del árbol.</summary>
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Overrides.Clear(); Version = 0; }

        public static ArsenalKit For(WeaponDefinition weapon)
        {
            if (weapon == null) return null;
            return Overrides.TryGetValue(weapon, out var k) ? k : weapon.ArsenalKitAsset;
        }

        /// <summary>La copia editable (se crea la primera vez a partir del asset).</summary>
        public static ArsenalKit Editable(WeaponDefinition weapon)
        {
            if (weapon == null || weapon.ArsenalKitAsset == null) return null;
            if (!Overrides.TryGetValue(weapon, out var k)) { k = weapon.ArsenalKitAsset.Clone(); Overrides[weapon] = k; }
            return k;
        }

        public static void Touch() => Version++;

        public static void Clear() { Overrides.Clear(); Version++; }
    }

    /// <summary>Hacia dónde dispara el jugador: boca (mundo), dirección y punto apuntado.</summary>
    public struct ArsenalAim
    {
        public Vector2 origin;
        public Vector2 dir;
        public Vector2 target;
        public float Ang => Mathf.Atan2(dir.y, dir.x);
    }

    /// <summary>
    /// El estado de juego de un arma del arsenal equipada (lo que devuelve <c>create(slot)</c> en la web): pulsar, mantener,
    /// soltar y su recarga. Lo crea y alimenta <see cref="WeaponUser"/>; la recarga del hueco inactivo sigue corriendo
    /// (<see cref="Tick"/> con current = false).
    /// </summary>
    public abstract class ArsenalController
    {
        public WeaponDefinition Weapon { get; private set; }
        /// <summary>Quien dispara (el jugador).</summary>
        public WeaponUser User { get; private set; }

        /// <summary>El kit efectivo ahora (el del menú DEV si lo hay).</summary>
        public ArsenalKit KitBase => ArsenalRuntime.For(Weapon);

        public void Bind(WeaponDefinition weapon, WeaponUser user) { Weapon = weapon; User = user; }

        public abstract void OnPress(ArsenalAim aim);
        public virtual void OnHold(ArsenalAim aim, float dt) { }
        public virtual void OnRelease(ArsenalAim aim) { }

        /// <summary>Cada frame. <paramref name="current"/> = es el arma activa.</summary>
        public virtual void Tick(float dt, bool current) { }

        /// <summary>0..1 para el HUD (1 = lista).</summary>
        public virtual float Ready => 1f;

        /// <summary>Quita <paramref name="seconds"/> a la recarga (items de Reset, Retorno del Abanico…).</summary>
        public virtual void Refund(float seconds) { }

        /// <summary>Se desequipa o se cambia el arma del hueco: quitar lo que viva solo (satélites, faros).</summary>
        public virtual void Dispose() { }

        /// <summary>Las otras armas del arsenal equipadas (para efectos que tocan la de reserva).</summary>
        public IEnumerable<ArsenalController> Others() => User != null ? User.OtherArsenal(this) : Array.Empty<ArsenalController>();
    }

    /// <summary>Atajo tipado: <c>K</c> = el kit efectivo de su tipo.</summary>
    public abstract class ArsenalController<TKit> : ArsenalController where TKit : ArsenalKit
    {
        protected TKit K => KitBase as TKit;
    }
}
