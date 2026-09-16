using System;
using System.Collections.Generic;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Run
{
    /// <summary>
    /// Escala del jugador por escena. Existe porque el fondo de cada nivel sale de IA generado por
    /// separado: la proporción entre el personaje y el arte varía de una lámina a otra aunque el
    /// jugador no cambie, así que hace falta un ajuste manual, por escena, que se pruebe y se deje
    /// fijado.
    ///
    /// Vive en <c>Assets/Resources/PlayerScaleConfig.asset</c> y lo carga <see cref="RunManager"/>
    /// con <c>Resources.Load</c>, igual que <c>CurrencyConfig</c> o <c>ShopConfig</c> — no hay que
    /// arrastrarlo a ninguna escena. Para ajustar una escena se edita este asset en el Inspector:
    /// el editor a medida (<c>PlayerScaleConfigEditor</c>) puede rellenar la lista sola con
    /// <b>Tools ▸ RedMagic ▸ Jugador ▸ Sincronizar escalas con Build Settings</b> (o el botón del
    /// propio Inspector), y el número de cada fila se toca a mano tras probar esa escena.
    ///
    /// La referencia a la escena es un <see cref="SceneReference"/>, no un string, por la misma
    /// razón que <c>RunManager.hubScene</c>: renombrar o mover la escena no puede desemparejar la
    /// fila silenciosamente.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerScaleConfig", menuName = "RedMagic/Player Scale Config")]
    public class PlayerScaleConfig : ScriptableObject
    {
        /// <summary>Escala con la que está modelado el personaje. Se usa si una escena no está en la lista.</summary>
        public const float DefaultScale = 1f;

        [Serializable]
        public class Entry
        {
            public SceneReference scene = new SceneReference();

            [Min(0.01f)]
            public float scale = DefaultScale;
        }

        [Tooltip("Una fila por escena. 'Sincronizar con Build Settings' rellena las que falten a " +
                 "escala 1.0 sin tocar las que ya estén ajustadas.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>Sólo lectura: el editor a medida es quien añade/quita filas.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>
        /// Escala configurada para <paramref name="scene"/>, o <see cref="DefaultScale"/> si la
        /// escena no aparece en la lista — lo que pasa siempre que se crea una escena nueva y
        /// todavía no se ha probado, así que se avisa en consola en vez de fallar en silencio.
        /// </summary>
        public float ScaleFor(Scene scene)
        {
            if (TryGetEntry(scene, out var entry)) return entry.scale;

            Debug.LogWarning($"[PlayerScaleConfig] La escena '{scene.name}' no está en la lista: " +
                              $"se usa la escala por defecto ({DefaultScale}). Añádela y ajústala " +
                              "en Assets/Resources/PlayerScaleConfig.asset " +
                              "('Sincronizar con Build Settings' la mete sola).");
            return DefaultScale;
        }

        private bool TryGetEntry(Scene scene, out Entry found)
        {
            string path = scene.path;

            foreach (var entry in entries)
            {
                if (entry?.scene == null || !entry.scene.IsAssigned) continue;
                if (entry.scene.Path == path)
                {
                    found = entry;
                    return true;
                }
            }

            found = null;
            return false;
        }
    }
}
