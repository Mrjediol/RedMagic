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

        public static string Sing(string dir)
        {
            ArsenalSetup.Run();
            var w = Weapon("sing"); var fan = Weapon("fan");
            Vector2 m = new Vector2(0.5f, 1.05f);
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, 0); Setup(fan, 0);
                var c = Ctrl(w); c.OnPress(Aim(m, new Vector2(5.2f, 1.7f)));
                st.Run(0.3f); st.Shot(dir + "/sing_viaje.png");
                st.Run(0.32f); st.Shot(dir + "/sing_apertura.png");
                var f = Ctrl(fan);
                st.Run(0.4f); f.OnPress(Aim(m, new Vector2(5.2f, 1.6f)));
                st.Run(0.5f); st.Shot(dir + "/sing_captura.png");
                st.Run(1.1f); st.Shot(dir + "/sing_colapso.png");
                st.Run(0.15f); st.Shot(dir + "/sing_colapso_b.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, SingularityKit.Fauces, true, true, true); ArsenalBuild.ElementOverride = ArsenalBuild.ElementMode.Fire;
                var c = Ctrl(w); c.OnPress(Aim(m, new Vector2(5.2f, 1.2f)));
                st.Run(1.2f); st.Shot(dir + "/sing_fauces.png");
                st.Run(1.25f); st.Shot(dir + "/sing_fauces_fuego_colapso.png");
                ArsenalBuild.ElementOverride = ArsenalBuild.ElementMode.Auto;
            }
            return "ok sing";
        }

        public static string Ray(string dir)
        {
            ArsenalSetup.Run();
            var w = Weapon("ray"); Vector2 m = new Vector2(0.5f, 1.05f);
            var aim = Aim(m, new Vector2(6f, 1.05f));
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, 0);
                var c = Ctrl(w); c.OnPress(aim);
                st.Run(0.5f, _ => c.OnHold(aim, ArsenalPreviewStage.Dt)); st.Shot(dir + "/ray_base.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, RayKit.Sobrecarga, true, true, true);
                var c = Ctrl(w); c.OnPress(aim);
                st.Run(1.65f, _ => c.OnHold(aim, ArsenalPreviewStage.Dt)); st.Shot(dir + "/ray_sobrecarga_franja.png");
                c.OnRelease(aim); st.Run(0.06f); st.Shot(dir + "/ray_sobrecarga_descarga.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); st.Dummy(new Vector2(7.6f, 39 * P)); Setup(w, RayKit.Prisma, true, false, true);
                var c = Ctrl(w); c.OnPress(aim);
                st.Run(0.6f, _ => c.OnHold(aim, ArsenalPreviewStage.Dt)); st.Shot(dir + "/ray_prisma_corona.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, RayKit.Faro, true, true, false);
                var c = Ctrl(w); c.OnPress(aim);
                st.Run(0.3f, _ => c.OnHold(aim, ArsenalPreviewStage.Dt)); c.OnRelease(aim);
                st.Run(0.8f, _ => c.Tick(ArsenalPreviewStage.Dt, false)); st.Shot(dir + "/ray_faro.png");
            }
            return "ok ray";
        }

        public static string Moon(string dir)
        {
            ArsenalSetup.Run();
            var w = Weapon("moon"); Vector2 m = new Vector2(0.5f, 1.05f);
            var aim = Aim(m, new Vector2(6f, 1.2f));
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, 0);
                var c = Ctrl(w); c.OnPress(aim);
                st.Run(0.3f); st.Shot(dir + "/moon_ida.png");
                st.Run(0.55f); st.Shot(dir + "/moon_vuelta.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, MoonKit.Sierra, false, true, true);
                var c = Ctrl(w); c.OnPress(aim);
                st.Run(0.9f); st.Shot(dir + "/moon_sierra.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(1.5f, 1.2f), 2.5f))
            {
                Setup(w, MoonKit.Satelite, true, true, false);
                var c = Ctrl(w); c.Tick(0f, true);
                st.Run(2.1f, _ => c.Tick(ArsenalPreviewStage.Dt, true)); st.Shot(dir + "/moon_satelite.png");
            }
            using (var st = new ArsenalPreviewStage(new Vector2(3.8f, 2.4f)))
            {
                Group(st); Setup(w, MoonKit.Malabar);
                var c = Ctrl(w); c.OnPress(aim);
                st.Run(1.03f, _ => c.Tick(ArsenalPreviewStage.Dt, true)); st.Shot(dir + "/moon_malabar_ventana.png");
            }
            return "ok moon";
        }
    }
}
