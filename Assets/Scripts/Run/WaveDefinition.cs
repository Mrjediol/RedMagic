using System;
using System.Collections.Generic;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>Cuándo empieza una oleada.</summary>
    public enum WaveStart
    {
        [InspectorName("Al empezar el nivel")] OnLoad,
        [InspectorName("Al entrar en su sala")] OnRoomEnter,
        [InspectorName("La lanza otra oleada")] Launched,
    }

    /// <summary>
    /// Una oleada de <see cref="WaveManager"/>: la lista de enemigos que salen juntos, cuándo empieza y qué pasa al
    /// acabar (todos han aparecido y todos han muerto). Varias pueden estar en marcha a la vez (p. ej. los enemigos que ya
    /// esperan en la sala siguiente).
    ///
    /// Es una clase serializada y no un ScriptableObject a propósito: cada entrada apunta a un
    /// punto de spawn <b>de la escena</b>, y un asset no puede guardar referencias a objetos de
    /// escena.
    /// </summary>
    [Serializable]
    public class WaveDefinition
    {
        [Tooltip("Al empezar el nivel / al entrar el jugador en su sala / cuando otra oleada la lance al terminar.")]
        public WaveStart start = WaveStart.Launched;

        [Tooltip("Sala de la oleada. Al entrar el jugador se cierran sus puertas (y si la oleada empieza 'al entrar " +
                 "en su sala', sale). 'Terminar sala' abre esas puertas.")]
        public LevelRoom room;

        [Tooltip("Segundos de espera desde que se lanza hasta que empieza a salir.")]
        [Min(0f)]
        public float startDelay = 0.5f;

        public List<EnemySpawn> enemies = new();

        [Tooltip("Al terminar: lanzar otra oleada.")]
        public bool launchWave;

        [Tooltip("Qué oleada lanza (desde 0). -1 = la siguiente en la lista.")]
        public int launchIndex = -1;

        [Tooltip("Al terminar: abrir las puertas de su sala.")]
        public bool finishRoom;

        [Tooltip("Al terminar la sala: item que cae en su centro. Vacío = ninguno.")]
        public ItemDefinition dropItem;

        [Tooltip("Al terminar: el nivel queda superado y se puede salir (aunque queden salas opcionales).")]
        public bool finishLevel;
    }

    /// <summary>Un enemigo de una oleada: qué prefab, dónde y cuánto después de empezar.</summary>
    [Serializable]
    public class EnemySpawn
    {
        [Tooltip("Prefab del enemigo, tal cual (Enemy_*.prefab). Tiene que llevar un Health.")]
        public GameObject enemyPrefab;

        [Tooltip("Uno de los puntos de spawn del WaveManager.")]
        public Transform spawnPoint;

        [Tooltip("Segundos desde el inicio de la oleada hasta que aparece este enemigo.")]
        [Min(0f)]
        public float spawnDelay;
    }
}
