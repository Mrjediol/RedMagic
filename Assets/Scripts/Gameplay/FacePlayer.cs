using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Gira el sprite hacia el jugador según a qué lado esté, sea cual sea lo que esté haciendo el
    /// resto del personaje (patrullar, perseguir, quedarse quieto telegrafiando un ataque…).
    ///
    /// Antes de esto, cada sitio que necesitaba "mirar hacia algo" lo resolvía a mano asumiendo que
    /// el sprite base mira a la derecha (<c>EnemyController</c> en persecución/patrulla,
    /// <c>BossController.faceTarget</c>). Esa suposición no vale para todo el arte importado —
    /// el cuervo, por ejemplo, mira a la izquierda de base — y el síntoma es un bicho persiguiendo
    /// de espaldas. Este componente hace la cuenta una sola vez, con la orientación base como dato
    /// del inspector en vez de una suposición fija en el código.
    ///
    /// <b>Por qué gana siempre, aunque algo más también escriba <c>flipX</c> el mismo frame</b>:
    /// corre en <see cref="LateUpdate"/>, que en Unity se ejecuta después de todos los
    /// <c>FixedUpdate</c>/<c>Update</c> del frame — así que aunque <c>EnemyController</c> (que fija
    /// su propio <c>flipX</c> según la dirección de movimiento, en <c>FixedUpdate</c>) o
    /// <c>BossController</c> (en su propio <c>Update</c>) sigan escribiendo el suyo, este
    /// componente tiene la última palabra sin que haga falta tocar ninguno de los dos. Basta con
    /// añadirlo donde haga falta "mirar al jugador de verdad" en vez de "mirar hacia donde me
    /// muevo".
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class FacePlayer : MonoBehaviour
    {
        [Tooltip("Marca esto si el sprite, tal cual está dibujado, mira hacia la DERECHA. Por " +
                 "defecto (apagado) se asume que mira hacia la izquierda, que es lo habitual en " +
                 "el arte de este proyecto.")]
        [SerializeField] private bool spriteFacesRight;

        [Tooltip("Objetivo a mirar. Vacío = se busca por etiqueta.")]
        [SerializeField] private Transform target;

        [Tooltip("Etiqueta con la que buscar el objetivo si no hay uno asignado a mano.")]
        [SerializeField] private string targetTag = "Player";

        private SpriteRenderer _renderer;
        private float _retargetTimer;

        private const float RetargetInterval = 0.5f;

        private void Awake() => _renderer = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            var current = ResolveTarget();
            if (current == null || _renderer == null) return;

            bool playerToTheRight = current.position.x >= transform.position.x;

            // Con base a la izquierda, mirar a la derecha significa voltear (flipX = true).
            // Con base a la derecha, es justo al revés.
            _renderer.flipX = spriteFacesRight ? !playerToTheRight : playerToTheRight;
        }

        /// <summary>
        /// Igual que <c>EnemyController.ResolveTarget</c>: se reintenta cada pocos segundos en vez
        /// de cada frame, porque el jugador de la run es un objeto persistente que puede no existir
        /// todavía cuando esto arranca (un esbirro invocado nada más empezar la sección, por
        /// ejemplo).
        /// </summary>
        private Transform ResolveTarget()
        {
            if (target != null) return target;

            _retargetTimer -= Time.unscaledDeltaTime;
            if (_retargetTimer > 0f) return null;

            _retargetTimer = RetargetInterval;

            if (string.IsNullOrEmpty(targetTag)) return null;

            var found = GameObject.FindGameObjectWithTag(targetTag);
            target = found != null ? found.transform : null;
            return target;
        }
    }
}
