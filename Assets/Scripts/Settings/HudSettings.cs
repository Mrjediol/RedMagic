using System;
using System.Globalization;
using UnityEngine;

namespace RedMagic.Settings
{
    /// <summary>Piezas del HUD que el jugador puede recolocar (Opciones ▸ Interfaz ▸ Colocar).</summary>
    public enum HudElement
    {
        Health,
        Cooldowns,
        Currency,
        Controls,
        EnemyFocus,
        EnemyList,
    }

    /// <summary>Botones táctiles de los controles por gestos que el jugador puede recolocar (Opciones ▸ Interfaz).</summary>
    public enum TouchElement
    {
        Jump,
        Attack,
        Dash,
        Parry,
        Interact,
    }

    /// <summary>Cómo se enseña la vida de los enemigos con los que se pelea (Opciones ▸ Interfaz).</summary>
    public enum EnemyHealthMode
    {
        /// <summary>No se enseña.</summary>
        Off = 0,

        /// <summary>Una barra pequeña encima de cada uno.</summary>
        Overhead = 1,

        /// <summary>Una barra grande, la del último golpeado (o el más cercano), como la de un jefe.</summary>
        Focus = 2,

        /// <summary>Una lista de barras, una por enemigo, ordenadas por cercanía (de fábrica, a la derecha).</summary>
        List = 3,
    }

    /// <summary>
    /// Dónde va una pieza del HUD, independiente de la resolución: <see cref="Pivot"/> es el punto de
    /// la pieza que se ancla (0 = borde izquierdo/superior, 0,5 = centro, 1 = derecho/inferior) y
    /// <see cref="Position"/> dónde cae ese punto, en fracción de pantalla. Una pieza pegada a la
    /// derecha se ancla por su borde derecho, así que si crece (más dígitos de oro) crece hacia dentro.
    /// </summary>
    [Serializable]
    public struct HudAnchor
    {
        public Vector2 Pivot;
        public Vector2 Position;

        public HudAnchor(Vector2 pivot, Vector2 position)
        {
            Pivot = pivot;
            Position = position;
        }
    }

    /// <summary>
    /// Preferencias del HUD que el jugador cambia en Opciones ▸ Interfaz. Se guardan en
    /// <c>PlayerPrefs</c> y avisan con <see cref="Changed"/> para que el HUD se repinte en el acto.
    /// </summary>
    public static class HudSettings
    {
        private const string Prefix = "settings.hud.";

        /// <summary>Se dispara al cambiar cualquier preferencia del HUD.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;

        /// <summary>Enseñar la chuleta de controles (sólo con teclado y ratón). Por defecto sí.</summary>
        public static bool ShowControls
        {
            get => GetBool("showControls", true);
            set => SetBool("showControls", value);
        }

        /// <summary>
        /// Controles táctiles por gestos (arrastrar para andar, tocar para disparar) en vez de los
        /// botones clásicos. Sólo afecta al modo táctil. Por defecto no.
        /// </summary>
        public static bool GestureControls
        {
            get => GetBool("gestureControls", false);
            set => SetBool("gestureControls", value);
        }

        /// <summary>Bordes de la pantalla en rojo al recibir un golpe.</summary>
        public static bool HurtEffect
        {
            get => GetBool("hurtEffect", true);
            set => SetBool("hurtEffect", value);
        }

        /// <summary>0..1, cuánto se tiñen los bordes al recibir un golpe.</summary>
        public static float HurtIntensity
        {
            get => GetFloat("hurtIntensity", 0.6f);
            set => SetFloat("hurtIntensity", value);
        }

        /// <summary>Bordes de la pantalla en verde al curarse.</summary>
        public static bool HealEffect
        {
            get => GetBool("healEffect", true);
            set => SetBool("healEffect", value);
        }

        /// <summary>0..1, cuánto se tiñen los bordes al curarse.</summary>
        public static float HealIntensity
        {
            get => GetFloat("healIntensity", 0.5f);
            set => SetFloat("healIntensity", value);
        }

        /// <summary>Latido rojo sutil en los bordes con poca vida.</summary>
        public static bool LowHealthEffect
        {
            get => GetBool("lowHealthEffect", true);
            set => SetBool("lowHealthEffect", value);
        }

        /// <summary>0..1, intensidad del aviso de poca vida.</summary>
        public static float LowHealthIntensity
        {
            get => GetFloat("lowHealthIntensity", 0.5f);
            set => SetFloat("lowHealthIntensity", value);
        }

        // ---------------------------------------------------------------- enemigos

        /// <summary>Cómo se ve la vida de los enemigos. De fábrica, la lista lateral.</summary>
        public static EnemyHealthMode EnemyHealth
        {
            get
            {
                int value = PlayerPrefs.GetInt(Prefix + "enemyHealthMode", (int)EnemyHealthMode.List);
                return Enum.IsDefined(typeof(EnemyHealthMode), value) ? (EnemyHealthMode)value : EnemyHealthMode.List;
            }
            set
            {
                if (value == EnemyHealth) return;
                PlayerPrefs.SetInt(Prefix + "enemyHealthMode", (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Escribir la vida exacta ("35 / 80") en las barras de enemigo, o sólo la barra.</summary>
        public static bool EnemyHealthNumbers
        {
            get => GetBool("enemyHealthNumbers", true);
            set => SetBool("enemyHealthNumbers", value);
        }

        /// <summary>
        /// Pruebas: ver los cooldowns de los enemigos sin el item que lo concede
        /// (<c>Gameplay.EnemyCooldownReveal</c>). Apagado de fábrica.
        /// </summary>
        public static bool EnemyCooldownsDebug
        {
            get => GetBool("enemyCooldownsDebug", false);
            set => SetBool("enemyCooldownsDebug", value);
        }

        // ---------------------------------------------------------------- posiciones

        /// <summary>Posición guardada de <paramref name="element"/>, o false si usa la de fábrica.</summary>
        public static bool TryGetAnchor(HudElement element, out HudAnchor anchor) => TryGetAnchor(AnchorKey(element), out anchor);

        public static void SetAnchor(HudElement element, HudAnchor anchor) => SetAnchor(AnchorKey(element), anchor);

        /// <summary>Vuelve todas las piezas a su sitio de fábrica.</summary>
        public static void ResetAnchors()
        {
            foreach (HudElement element in Enum.GetValues(typeof(HudElement)))
                PlayerPrefs.DeleteKey(AnchorKey(element));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Posición guardada de un botón táctil de los gestos, o false si usa la de fábrica (la del USS).</summary>
        public static bool TryGetTouchAnchor(TouchElement element, out HudAnchor anchor) => TryGetAnchor(TouchKey(element), out anchor);

        public static void SetTouchAnchor(TouchElement element, HudAnchor anchor) => SetAnchor(TouchKey(element), anchor);

        /// <summary>Vuelve todos los botones táctiles a su sitio de fábrica.</summary>
        public static void ResetTouchAnchors()
        {
            foreach (TouchElement element in Enum.GetValues(typeof(TouchElement)))
                PlayerPrefs.DeleteKey(TouchKey(element));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Claves literales (no "$touch.{elemento}") para que se puedan buscar.
        private static string TouchKey(TouchElement element) => element switch
        {
            TouchElement.Jump => Prefix + "touch.jump",
            TouchElement.Attack => Prefix + "touch.attack",
            TouchElement.Dash => Prefix + "touch.dash",
            TouchElement.Parry => Prefix + "touch.parry",
            _ => Prefix + "touch.interact",
        };

        private static bool TryGetAnchor(string key, out HudAnchor anchor)
        {
            anchor = default;
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw)) return false;

            var parts = raw.Split(';');
            if (parts.Length != 4) return false;

            var values = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return false;

            anchor = new HudAnchor(new Vector2(values[0], values[1]), new Vector2(values[2], values[3]));
            return true;
        }

        private static void SetAnchor(string key, HudAnchor anchor)
        {
            string raw = string.Join(";",
                anchor.Pivot.x.ToString(CultureInfo.InvariantCulture), anchor.Pivot.y.ToString(CultureInfo.InvariantCulture),
                anchor.Position.x.ToString(CultureInfo.InvariantCulture), anchor.Position.y.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(key, raw);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static string AnchorKey(HudElement element) => element switch
        {
            HudElement.Health => Prefix + "pos.health",
            HudElement.Cooldowns => Prefix + "pos.cooldowns",
            HudElement.Currency => Prefix + "pos.currency",
            HudElement.Controls => Prefix + "pos.controls",
            HudElement.EnemyFocus => Prefix + "pos.enemyFocus",
            _ => Prefix + "pos.enemyList",
        };

        // ---------------------------------------------------------------- PlayerPrefs

        private static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;

        private static void SetBool(string key, bool value)
        {
            if (value == GetBool(key, !value)) return;
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static float GetFloat(string key, float fallback) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, fallback));

        private static void SetFloat(string key, float value)
        {
            value = Mathf.Clamp01(value);
            if (PlayerPrefs.HasKey(Prefix + key) && Mathf.Approximately(PlayerPrefs.GetFloat(Prefix + key), value)) return;
            PlayerPrefs.SetFloat(Prefix + key, value);   // sin Save: un slider lo llama cada frame; se guarda al salir
            Changed?.Invoke();
        }
    }
}
