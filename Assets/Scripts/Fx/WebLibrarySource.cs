using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Marca un prefab como "construido a partir de una entrada de la biblioteca web" y recuerda
    /// de CUÁL. Inerte en ejecución — no tiene Update ni toca nada; existe sólo para que el
    /// importador pueda responder "¿ya construí esto?" sin adivinar por el nombre del archivo.
    ///
    /// <b>Por qué un id y no la ruta.</b> La convención del proyecto es no acoplar assets por
    /// cadenas de nombre (ver la memoria <c>avoid-name-string-coupling</c> y
    /// <see cref="RedMagic.SceneReference"/>): si el prefab se renombra o se mueve a mano, una
    /// búsqueda por ruta lo daría por inexistente y construiría un DUPLICADO en la ruta canónica,
    /// dejando dos prefabs del mismo proyectil y una referencia apuntando al que nadie editó. El
    /// id de la biblioteca web no cambia nunca, así que buscar por él encuentra el prefab de
    /// verdad esté donde esté.
    ///
    /// <b>Por qué es un componente de runtime y no un ScriptableObject aparte.</b> Viaja CON el
    /// prefab: copiarlo, moverlo o meterlo en un paquete se lleva el marcador consigo, igual que
    /// <c>Core.PooledInstance</c> o <see cref="FxPlaceholderStyle"/> ya hacen para sus propios
    /// fines. Un índice externo se desincronizaría en cuanto alguien arrastrara el prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WebLibrarySource : MonoBehaviour
    {
        [Tooltip("Id (uuid) de la entrada en la biblioteca web de la que salió este prefab.")]
        [SerializeField] private string libraryId;

        [Tooltip("Tipo de entrada: 'projectile' o 'fx'. Mismo vocabulario que entry-kinds.js.")]
        [SerializeField] private string kind;

        [Tooltip("Carpeta de Assets/ con el manifest.json + PNG de los que se construyó. " +
                 "Informativa: sirve para volver a construirlo si se cambia el arte.")]
        [SerializeField] private string sourceFolder;

        public string LibraryId => libraryId;
        public string Kind => kind;
        public string SourceFolder => sourceFolder;

#if UNITY_EDITOR
        /// <summary>Lo rellena <c>FxPrefabBuilder</c> al construir. Sólo editor.</summary>
        public void EditorSet(string id, string entryKind, string folder)
        {
            libraryId = id;
            kind = entryKind;
            sourceFolder = folder;
        }
#endif
    }
}
