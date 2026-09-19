using System.Collections;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Suplente de movimiento para los proyectiles construidos desde la biblioteca web
    /// (<c>FxPrefabBuilder</c>). Hace dos cosas, y sólo dos:
    ///
    /// <list type="number">
    /// <item><b>Lleva el modo de movimiento</b> que el <c>FxConfig</c> pidió
    /// (<see cref="Mode"/> ← <c>fx-config.schema.json</c>'s <c>movement</c>). Es el dato, no el
    /// comportamiento: cuando exista <c>BoomerangMovement</c>, <c>BounceMovement</c>,
    /// <c>SplitOnImpact</c>… cada uno leerá este campo (o sustituirá directamente a este
    /// componente) y el cambio será <b>quitar y poner un componente</b>, no reexportar configs ni
    /// tocar el importador. Por eso el enum existe entero desde hoy aunque sólo uno esté hecho.</item>
    ///
    /// <item><b>Vuela recto y se apaga</b> cuando <i>nadie</i> lo ha lanzado — es decir, cuando el
    /// prefab está suelto en una escena y se le da a Play. Eso es exactamente la comprobación para
    /// la que existe: ver que el proyectil que salió de la pestaña web se construyó bien, se mueve
    /// y se despawnea, sin montar un enemigo que lo dispare.</item>
    /// </list>
    ///
    /// <b>Qué NO hace, y por qué es importante:</b> en cuanto algo lo lanza de verdad
    /// (<c>ProjectileFactory.Spawn</c> → <see cref="Projectile.Launch"/>), este componente se
    /// aparta y no vuelve a tocar nada. Los números que vuelan en partida siguen saliendo del
    /// <c>ProjectileSpec</c> del que dispara (<c>EnemyStats ▸ Tuning ▸ projectile</c>, o el del
    /// ataque del jefe) porque la factoría reescribe el prefab en cada disparo — la convención
    /// documentada del proyecto. <see cref="speed"/>/<see cref="lifetime"/> de aquí son sólo los
    /// valores de la vista previa suelta.
    ///
    /// <b>Cómo distingue "lanzado" de "suelto":</b> espera un frame y mira si el
    /// <see cref="Rigidbody2D"/> lleva velocidad. <see cref="Projectile.Launch"/> la escribe de
    /// forma síncrona justo después de <c>PrefabPool.Spawn</c>, así que en el momento en que esta
    /// corrutina despierta ya está puesta si alguien disparó. No hay ninguna bandera pública
    /// "lanzado" en <see cref="Projectile"/> y no se ha añadido una: este suplente es temporal y no
    /// merece ampliar la API de un componente que usa medio juego.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProjectileMovementPlaceholder : MonoBehaviour
    {
        /// <summary>
        /// Mismo vocabulario, mismos nombres y mismo orden que
        /// <c>fx-config.schema.json#/definitions/movementMode</c>. Se escribe/lee por NOMBRE, nunca
        /// por ordinal, así que reordenar esta lista no repunta ningún config existente.
        /// </summary>
        public enum MovementMode
        {
            Straight,
            Homing,
            Boomerang,
            Bounce,
            SplitOnImpact,
        }

        [Tooltip("Cómo debería volar. Hoy sólo Straight tiene lógica; el resto se guarda tal cual " +
                 "para que el script definitivo de ese modo sea un reemplazo directo.")]
        [SerializeField] private MovementMode mode = MovementMode.Straight;

        [Tooltip("Velocidad de la vista previa suelta. NO es el tuning de partida: eso sale del " +
                 "ProjectileSpec de quien dispara.")]
        [Min(0.01f)]
        [SerializeField] private float speed = 12f;

        [Tooltip("Segundos antes de apagarse en la vista previa suelta.")]
        [Min(0.05f)]
        [SerializeField] private float lifetime = 2.5f;

        [Tooltip("Hacia dónde vuela cuando nadie lo lanza. +X por convención del proyecto.")]
        [SerializeField] private Vector2 previewDirection = Vector2.right;

        /// <summary>El modo que este proyectil declara. Lo que leerá el mover definitivo.</summary>
        public MovementMode Mode => mode;

        public float PreviewSpeed => speed;
        public float PreviewLifetime => lifetime;

        private Projectile _projectile;
        private Rigidbody2D _body;

        /// <summary>
        /// Si esta instancia apagó el <see cref="Projectile"/> para conducirla ella. Se recuerda
        /// para volver a encenderlo en <c>OnDisable</c>: la instancia vuelve al pool y la próxima
        /// vez puede tocarle un disparo de verdad, que con el componente apagado no se movería ni
        /// golpearía nada.
        /// </summary>
        private bool _suspendedProjectile;

        private void Awake()
        {
            _projectile = GetComponent<Projectile>();
            _body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            _suspendedProjectile = false;
            StartCoroutine(DriveIfNobodyLaunched());
        }

        private void OnDisable()
        {
            if (!_suspendedProjectile || _projectile == null) return;
            _projectile.enabled = true;
            _suspendedProjectile = false;
        }

        private IEnumerator DriveIfNobodyLaunched()
        {
            // Un frame de cortesía: quien spawnea llama a Launch en el mismo frame, justo después
            // de activar el objeto, así que aquí ya se sabe cuál de los dos casos es.
            yield return null;

            if (WasLaunched()) yield break;

            // Nadie lo disparó: vista previa suelta. Se apaga el Projectile para que su propio
            // temporizador de vida no compita con el de aquí (los dos llaman a Despawn y ganaría
            // el más corto, que es justo el tipo de resultado que no se puede leer de un vistazo).
            if (_projectile != null)
            {
                _projectile.enabled = false;
                _suspendedProjectile = true;
            }

            Vector2 direction = previewDirection.sqrMagnitude < 0.0001f
                ? Vector2.right
                : previewDirection.normalized;

            // Recto, sea cual sea el modo. Un modo todavía sin script no se finge: se avisa una vez
            // y se vuela recto, en vez de dejar creer que el boomerang ya vuelve.
            if (mode != MovementMode.Straight)
            {
                Debug.Log($"[ProjectileMovementPlaceholder] '{name}' pide el modo '{mode}', que " +
                          "todavía no tiene script propio; vuela recto.", this);
            }

            if (_body != null) _body.linearVelocity = direction * speed;

            float remaining = lifetime;
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                // Sin Rigidbody2D (un prefab montado a mano sin cuerpo) se mueve el transform.
                if (_body == null) transform.position += (Vector3)(direction * (speed * Time.deltaTime));
                yield return null;
            }

            Despawn();
        }

        /// <summary>
        /// Velocidad ya escrita = alguien llamó a <see cref="Projectile.Launch"/> este frame.
        /// Sin cuerpo no hay forma de saberlo, así que se asume que no (la vista previa es el caso
        /// para el que existe este componente).
        /// </summary>
        private bool WasLaunched() => _body != null && _body.linearVelocity.sqrMagnitude > 0.0001f;

        /// <summary>
        /// Se apaga por la misma vía que el resto del proyecto. Con <see cref="Projectile"/>
        /// delante se usa su <see cref="Projectile.Despawn"/>, que ya sabe si esta instancia vino
        /// de <c>Pool&lt;Projectile&gt;</c>, de <c>PrefabPool</c> o de ningún pool (una arrastrada a
        /// mano en la escena, que simplemente se desactiva). Sin él, se intenta el pool de prefabs
        /// y, si tampoco vino de ahí, se desactiva: nunca Destroy, porque un proyectil es
        /// exactamente lo que el proyecto poolea siempre.
        /// </summary>
        private void Despawn()
        {
            if (_projectile != null)
            {
                // Despawn() es un método normal: se ejecuta aunque el componente esté apagado (sólo
                // los mensajes de Unity, Update/OnTriggerEnter2D, dependen de 'enabled').
                _projectile.Despawn();
                return;
            }

            if (_body != null) _body.linearVelocity = Vector2.zero;
            Core.PrefabPool.Despawn(gameObject);
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
