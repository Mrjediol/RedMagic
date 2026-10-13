using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// PRUEBA DE BAKING (todo en <c>Assets/Test/Baking</c>, se borra con su menú). Va en la variante de la puerta que usa el
    /// shader "Fx Door Energy Flipbook": <see cref="LevelDoor"/> crea el material de la energía desde el shader y no sabe
    /// de texturas, así que esto le pone el flipbook en cuanto existe. LevelDoor no se ha tocado.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]   // después de LevelDoor (-40), que es quien crea el material
    public class DoorFlipbookBinder : MonoBehaviour
    {
        private static readonly int FlipId = Shader.PropertyToID("_Flip");

        [SerializeField] private Texture flipbook;

        public void SetFlipbook(Texture texture) => flipbook = texture;

        private MeshRenderer _energy;

        private void OnEnable() => Bind();

        private void Update() => Bind();

        /// <summary>Se llama también desde fuera tras cambiar el tamaño de la puerta (se vuelve a crear la malla).</summary>
        public void Bind()
        {
            if (flipbook == null) return;
            if (_energy == null)
            {
                var t = transform.Find(LevelDoor.EnergyName);
                if (t == null || !t.TryGetComponent(out _energy)) return;
            }

            var material = _energy.sharedMaterial;
            if (material != null && material.GetTexture(FlipId) != flipbook) material.SetTexture(FlipId, flipbook);
        }
    }
}
