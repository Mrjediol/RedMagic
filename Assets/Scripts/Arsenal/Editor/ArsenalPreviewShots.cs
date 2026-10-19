using System.Text;
using RedMagic.Items;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Arsenal.Editor
{
    /// <summary>
    /// Renders de comprobación de cada arma (sin Play): <c>unity command eval --code "return RedMagic.Arsenal.Editor.ArsenalPreviewShots.Fan(@\"carpeta\");"</c>.
    /// Cada escenario monta su <see cref="ArsenalPreviewStage"/>, dispara con el controlador real y guarda 1-3 instantes.
    /// </summary>
    public static class ArsenalPreviewShots
    {
        private const float P = ArsenalDraw.PX;

        public static WeaponDefinition Weapon(string id) =>
            AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{ArsenalSetup.WeaponsFolder}/Weapon_Arsenal_{id}.asset");

        public static ArsenalAim Aim(Vector2 from, Vector2 to) =>
            new ArsenalAim { origin = from, dir = (to - from).normalized, target = to };

        public static ArsenalController Ctrl(WeaponDefinition w)
        {
            var c = ArsenalRuntime.For(w).CreateController();
            c.Bind(w, null);
            return c;
        }

        /// <summary>Pone evolución y mejoras en la copia de Play (la del menú DEV) de <paramref name="w"/>.</summary>
        public static ArsenalKit Setup(WeaponDefinition w, int evo, bool u1 = false, bool u2 = false, bool u3 = false)
        {
            var k = ArsenalRuntime.Editable(w);
            k.evolution = evo; k.upgrade1 = u1; k.upgrade2 = u2; k.upgrade3 = u3;
            return k;
        }

        private static void Group(ArsenalPreviewStage st)
        {
            st.Dummy(new Vector2(4.6f, 39 * P));
            st.Dummy(new Vector2(6.3f, 39 * P));
            st.Dummy(new Vector2(5.4f, 3.1f), true);
        }

        public static string Fan(string dir)
        {
            var w = Weapon("fan"); var log = new StringBuilder();
            Vector2 m = new Vector2(0.5f, 1.05f);
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, 0);
                var c = Ctrl(w); c.OnPress(Aim(m, new Vector2(5f, 1f)));
                st.Run(0.06f); st.Shot(dir + "/fan_base_vuelo.png");
                st.Run(0.12f); st.Shot(dir + "/fan_base_impacto.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, FanKit.Erizo, true, true, true);
                var c = Ctrl(w); c.OnPress(Aim(m, new Vector2(5f, 0.6f)));
                st.Run(0.6f); st.Shot(dir + "/fan_erizo_clavadas.png");
                st.Run(2.05f); st.Shot(dir + "/fan_erizo_estallido.png"); st.Run(0.2f); st.Shot(dir + "/fan_erizo_retorno.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, FanKit.Lanza, true, false, true);
                var c = Ctrl(w); c.OnPress(Aim(m, new Vector2(6f, 1f)));
                st.Run(0.16f, t => c.Tick(ArsenalPreviewStage.Dt, true)); st.Shot(dir + "/fan_lanza.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, FanKit.Caleidoscopio, true, true, false);
                var c = Ctrl(w); c.OnPress(Aim(m, new Vector2(5f, -0.2f)));
                st.Run(0.22f); st.Shot(dir + "/fan_caleidoscopio.png");
            }
            log.Append("ok fan");
            return log.ToString();
        }
    }
}
