using RedMagic.Audio;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Final de una sección. Cuando el jugador entra en el trigger, pide a
    /// <see cref="RunManager"/> que avance a la siguiente sección (o al jefe, o al mundo
    /// siguiente: eso lo decide el RunManager, aquí no se sabe ni hace falta).
    ///
    /// Se pone en cada escena de sección, en un collider marcado como <c>Is Trigger</c>.
    /// La escena del jefe normalmente no lleva SectionExit: el avance lo dispara la muerte del
    /// jefe llamando a <see cref="RunManager.AdvanceSection"/> desde su evento <c>Died</c>.
    ///
    /// <b>También es la salida del hub.</b> Con un <see cref="world"/> asignado y sin run en
    /// curso, la misma puerta empieza la run de ese mundo en vez de avanzar. Salir del hub y
    /// pasar de sección son el mismo gesto para quien juega — cruzar una puerta — así que son el
    /// mismo componente; el RunManager es quien sabe si eso es empezar o continuar.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class SectionExit : MonoBehaviour
    {
        [Header("Salida del hub")]
        [Tooltip("Sólo para la puerta del hub: mundo con el que empieza la run. Vacío = salida " +
                 "normal de sección. Es una referencia al asset, no un índice ni un nombre, para " +
                 "que reordenar la lista de mundos del RunManager no pueda desemparejarla.")]
        [SerializeField] private WorldDefinition world;

        [Tooltip("Sólo para la puerta del hub: no deja empezar la run sin un arma equipada — el " +
                 "cofre es la única forma de conseguirla, así que esto es lo que hace que el " +
                 "arma inicial sea obligatoria de verdad. Desactívalo para probar el resto del " +
                 "hub sin tener que pasar por el cofre cada vez.")]
        [SerializeField] private bool requireWeaponToStartRun = true;

        [Header("Detección")]
        [Tooltip("Etiqueta del objeto que puede activar la salida.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Si está activo, la salida sólo funciona una vez (evita disparos dobles si el " +
                 "jugador entra y sale del trigger durante la transición).")]
        [SerializeField] private bool oneShot = true;

        [Tooltip("La salida no se abre hasta que no queda ningún enemigo vivo en la sección " +
                 "(lo decide SectionClearTracker). Desactívalo para una sección de tránsito.")]
        [SerializeField] private bool requireEnemiesDead = true;

        private bool _used;

        /// <summary>True si esta salida es la puerta del hub: empieza una run en vez de avanzar.</summary>
        public bool StartsRun => world != null;

        /// <summary>Mundo con el que empieza la run, o null si es una salida de sección normal.</summary>
        public WorldDefinition World => world;

        /// <summary>True si la salida está esperando a que el jugador limpie la sección.</summary>
        public bool BlockedByEnemies =>
            requireEnemiesDead && SectionClearTracker.Instance != null &&
            !SectionClearTracker.Instance.IsCleared;

        /// <summary>
        /// True si esta es la puerta del hub y el jugador todavía no tiene arma equipada. El
        /// arma sólo se consigue del cofre (<c>Hub.ChestLootContainer</c>), así que esto es lo
        /// que impide salir sin haberlo abierto.
        /// </summary>
        public bool BlockedByMissingWeapon =>
            StartsRun && requireWeaponToStartRun &&
            (WeaponLoadout.Instance == null || WeaponLoadout.Instance.Inventory.Weapon == null);

        private void Reset()
        {
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.isTrigger = true;
        }

        private void OnEnable() => _used = false;

        private void OnTriggerEnter2D(Collider2D other) => TryUse(other, entering: true);

        // También en Stay: si el jugador ya estaba dentro del trigger cuando cayó el último
        // enemigo, Enter no vuelve a dispararse y se quedaría encerrado en la sección.
        private void OnTriggerStay2D(Collider2D other) => TryUse(other, entering: false);

        private void TryUse(Collider2D other, bool entering)
        {
            if (_used && oneShot) return;
            if (!other.CompareTag(playerTag)) return;
            if (BlockedByEnemies || BlockedByMissingWeapon)
            {
                // Sólo al llegar: en Stay sonaría cada frame mientras se empuja la puerta.
                if (entering) SystemSounds.PlayAt(s => s.doorLocked, transform.position);
                return;
            }

            var manager = RunManager.Instance;

            if (manager == null)
            {
                Debug.LogWarning("[SectionExit] No hay RunManager activo: la salida no hace nada. " +
                                 "¿Se está jugando esta sección suelta desde el editor?", this);
                return;
            }

            // Dos triggers solapados o el Stay del frame siguiente no deben disparar dos veces
            // mientras la primera carga aún está en marcha.
            if (manager.IsTransitioning) return;

            if (!manager.RunInProgress)
            {
                // Sin run en curso, la única salida que tiene sentido es la del hub. Una salida de
                // sección normal aquí sería una escena jugada suelta desde el editor.
                if (!StartsRun)
                {
                    Debug.LogWarning("[SectionExit] No hay run en curso y esta salida no tiene " +
                                     "mundo asignado: no hay nada a lo que avanzar.", this);
                    return;
                }

                // _used sólo se marca si la run arranca de verdad: si el mundo está bloqueado o
                // mal configurado, la puerta tiene que seguir funcionando al volver a pasar.
                if (manager.StartRun(world)) _used = true;
                return;
            }

            _used = true;
            manager.AdvanceSection();
        }

        private void OnDrawGizmos()
        {
            var collider = GetComponent<Collider2D>();
            if (collider == null) return;

            // Azul la puerta del hub (empieza run), ámbar la salida de sección (avanza).
            Gizmos.color = StartsRun
                ? new Color(0.4f, 0.8f, 1f, 0.35f)
                : new Color(1f, 0.85f, 0.2f, 0.35f);

            var bounds = collider.bounds;
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
