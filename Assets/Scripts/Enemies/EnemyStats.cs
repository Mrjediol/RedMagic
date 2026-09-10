using RedMagic.Combat;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// El <b>único componente que hay que tocar</b> para afinar un enemigo.
    ///
    /// Lleva el bloque <see cref="EnemyTuning"/> entero y se encarga de repartirlo: lo que es suyo
    /// lo leen en vivo el cerebro, la animación y los ataques; lo que pertenece a componentes
    /// compartidos del proyecto (<see cref="Health"/>, <see cref="Knockback"/>) se les escribe al
    /// arrancar, y también al editar en el inspector, para que lo que se ve aquí sea la verdad.
    ///
    /// Ese reparto es el motivo de que exista: <c>Health</c> y <c>Knockback</c> los usan también el
    /// jugador y los jefes, así que no pueden depender de nada de enemigos. En vez de hacerles
    /// preguntar, esto se los empuja.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class EnemyStats : MonoBehaviour
    {
        [SerializeField] private EnemyTuning tuning = new EnemyTuning();

        /// <summary>Los valores en vivo. Todos los scripts del enemigo leen de aquí.</summary>
        public EnemyTuning Tuning => tuning;

#if UNITY_EDITOR
        /// <summary>Lo usa el pipeline para sembrar el bloque desde la ficha del enemigo.</summary>
        public void EditorSetTuning(EnemyTuning value)
        {
            tuning = value ?? new EnemyTuning();
            Apply();
        }
#endif

        private void Awake() => Apply();

        /// <summary>
        /// Vuelca lo que es de otros componentes. Se llama al arrancar y desde el inspector: así
        /// cambiar la vida aquí se ve al momento en el <c>Health</c> en vez de en la siguiente
        /// partida.
        /// </summary>
        public void Apply()
        {
            var health = GetComponent<Health>();
            if (health != null)
            {
                // healToFull sólo fuera de juego: en pleno combate subir el máximo no debe curar.
                health.SetMaxHealth(tuning.maxHealth, !Application.isPlaying);
                health.SetInvulnerabilityDuration(tuning.invulnerabilityDuration);
                health.SetSfx(tuning.hurtSfxId, tuning.deathSfxId);
            }

            var knockback = GetComponent<Knockback>();
            if (knockback != null)
            {
                knockback.Configure(tuning.knockbackHorizontal, tuning.knockbackVertical,
                                    tuning.knockbackDuration, tuning.knockbackResistance);
            }

            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.gravityScale = tuning.Flies ? 0f : tuning.gravityScale;

            // Para un volador las plataformas no existen: atraviesa el terreno. Se hace con el
            // excludeLayers del propio collider — ni capas nuevas ni tocar la matriz de colisiones
            // del proyecto — y sólo con la capa de terreno, así que sigue chocando con el jugador y
            // el daño por contacto no cambia. En uno de suelo se quita, por si cambia de arquetipo.
            GroundMotion.PhaseThroughTerrain(gameObject, tuning.obstacleLayers, tuning.Airborne);
        }

#if UNITY_EDITOR
        /// <summary>Hueco mínimo entre 'a tiro' y 'detectado' para que quepa un paso de acercarse.</summary>
        private const float ApproachBandMargin = 2f;

        private void OnValidate()
        {
            // Coherencias que si no se escapan sólo se notan jugando.

            // La detección tiene que dejar una banda por encima del rango de ataque, si no el
            // enemigo aparece ya "a tiro" y nunca camina: la fase de acercarse queda vacía.
            if (tuning.Moves)
            {
                float minDetection = tuning.attackRange + ApproachBandMargin;
                if (tuning.detectionRange < minDetection) tuning.detectionRange = minDetection;
            }

            if (tuning.personalSpace > tuning.attackRange)
                tuning.personalSpace = tuning.attackRange;

            if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += ApplyIfAlive;
        }

        private void ApplyIfAlive()
        {
            if (this == null) return;
            Apply();
        }
#endif

        /// <summary>
        /// Rangos a la vista al seleccionar el enemigo: son el 90% de cómo se siente.
        ///
        /// Se dibuja <b>sólo lo que el arquetipo usa de verdad</b>, con las mismas reglas que sigue
        /// el cerebro. Un círculo de más aquí no es cosmético: se lee como una capacidad que el
        /// enemigo no tiene, y manda a buscar el fallo donde no está.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;

            // Detección: sólo los que persiguen. Un Static no detecta, sólo tiene a tiro o no.
            if (tuning.Moves)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
                Gizmos.DrawWireSphere(origin, tuning.detectionRange);
            }

            // Ataque: lo tienen todos.
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(origin, tuning.attackRange);

            // Espacio propio: sólo los que huyen (se mueven y disparan).
            if (tuning.Retreats)
            {
                Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
                Gizmos.DrawWireSphere(origin, tuning.personalSpace);
            }
        }
    }
}
