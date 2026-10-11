using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// Los ajustes de la nieve de un bioma en un solo sitio del Inspector. Escribe sobre los sistemas de partículas
    /// hijos ("Nieve plano", su sub-emisor "Copo posado" y "Nieve fondo"), que siguen siendo partículas nativas de
    /// Unity: barato, sin script por copo. Los topes de partículas acotan el coste por mucho que se suba la cantidad.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("RedMagic/Ajustes de nieve")]
    public class SnowSettings : MonoBehaviour
    {
        [Header("Plano del juego (choca y se posa)")]
        [Tooltip("Copos por segundo.")]
        [Range(0f, 150f)] public float amount = 60f;
        [Tooltip("Tamaño mínimo y máximo, en unidades (1 unidad = 40 px del boceto).")]
        public Vector2 size = new Vector2(0.2f, 0.55f);
        [Range(0f, 1f)] public float opacity = 0.9f;
        [Tooltip("Velocidad de caída mínima y máxima (unidades/s).")]
        public Vector2 fallSpeed = new Vector2(0.9f, 2f);
        [Tooltip("Segundos que se queda un copo posado antes de desvanecerse (mín, máx).")]
        public Vector2 landedTime = new Vector2(2f, 3.5f);
        [Tooltip("Tope de copos cayendo a la vez (coste).")]
        [Range(50, 1500)] public int maxFalling = 1000;
        [Tooltip("Tope de copos posados a la vez (coste).")]
        [Range(20, 800)] public int maxLanded = 400;

        [Header("Fondo (detrás del terreno, no choca)")]
        [Range(0f, 150f)] public float backgroundAmount = 40f;
        public Vector2 backgroundSize = new Vector2(0.09f, 0.24f);
        [Range(0f, 1f)] public float backgroundOpacity = 0.45f;
        [Range(50, 1500)] public int maxBackground = 700;

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        [ContextMenu("Aplicar")]
        public void Apply()
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var emission = ps.emission;
                switch (ps.name)
                {
                    case "Nieve plano":
                        emission.rateOverTime = amount;
                        main.startSize = new ParticleSystem.MinMaxCurve(size.x, Mathf.Max(size.x, size.y));
                        main.startColor = new Color(1f, 1f, 1f, opacity);
                        main.maxParticles = maxFalling;
                        SetFall(ps, fallSpeed);
                        break;
                    case "Copo posado":
                        main.startLifetime = new ParticleSystem.MinMaxCurve(landedTime.x, Mathf.Max(landedTime.x, landedTime.y));
                        main.maxParticles = maxLanded;
                        break;
                    case "Nieve fondo":
                        emission.rateOverTime = backgroundAmount;
                        main.startSize = new ParticleSystem.MinMaxCurve(backgroundSize.x, Mathf.Max(backgroundSize.x, backgroundSize.y));
                        main.startColor = new Color(1f, 1f, 1f, backgroundOpacity);
                        main.maxParticles = maxBackground;
                        SetFall(ps, fallSpeed * 0.7f);
                        break;
                }
            }
        }

        private static void SetFall(ParticleSystem ps, Vector2 speed)
        {
            var vel = ps.velocityOverLifetime;
            vel.y = new ParticleSystem.MinMaxCurve(-Mathf.Max(speed.x, speed.y), -speed.x);
            // Que viva lo justo para cruzar la pantalla (22 unidades desde el emisor) al ir lento.
            var main = ps.main;
            float slow = Mathf.Max(0.2f, speed.x);
            main.startLifetime = new ParticleSystem.MinMaxCurve(22f / Mathf.Max(slow, speed.y), 22f / slow + 1f);
        }
    }
}
