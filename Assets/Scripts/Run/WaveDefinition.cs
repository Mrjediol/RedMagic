using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Una oleada de <see cref="WaveManager"/>: la lista de enemigos que salen juntos. La oleada
    /// acaba cuando todos han aparecido y todos han muerto.
    ///
    /// Es una clase serializada y no un ScriptableObject a propósito: cada entrada apunta a un
    /// punto de spawn <b>de la escena</b>, y un asset no puede guardar referencias a objetos de
    /// escena.
    /// </summary>
    [Serializable]
    public class WaveDefinition
    {
        [Tooltip("Segundos de espera antes de que empiece esta oleada (tras cargar la escena en " +
                 "la primera, tras morir el último enemigo de la anterior en las demás).")]
        [Min(0f)]
        public float startDelay = 0.5f;

        public List<EnemySpawn> enemies = new();
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
