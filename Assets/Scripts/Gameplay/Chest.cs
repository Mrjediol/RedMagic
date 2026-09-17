using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Migrado desde Assets/Cainos/Pixel Art Platformer - Village Props/Script/Chest.cs
    /// (limpieza de paquetes de terceros, VendorCleanupMigration) — mismo comportamiento; se
    /// quitaron los atributos de Cainos.LucidEditor (decorativos, sólo Inspector) porque ese
    /// paquete se eliminó del proyecto junto con el resto de Cainos/.
    /// </summary>
    public class Chest : MonoBehaviour
    {
        public Animator animator;

        public bool IsOpened
        {
            get => isOpened;
            set
            {
                isOpened = value;
                animator.SetBool("IsOpened", isOpened);
            }
        }
        private bool isOpened;

        public void Open() => IsOpened = true;
        public void Close() => IsOpened = false;
    }
}
