using UnityEngine;
using UnityEngine.Pool;

namespace RedMagic.Fx
{
    /// <summary>
    /// La curación de UNA baja repartida entre sus motas rojas (<see cref="LifeMotes.Drain"/>).
    /// Cada mota que llega cura su parte; la primera que de verdad sube la vida enseña las motas
    /// verdes. Una mota que se pierde (el jugador desaparece, carga de escena) no cura. Vuelve a
    /// <see cref="GenericPool{T}"/> cuando ya no queda ninguna mota en vuelo.
    /// </summary>
    internal sealed class DrainPacket
    {
        private Combat.Health _target;
        private float _share;
        private int _inFlight;
        private int _healMotes;
        private GameObject _sortingReference;
        private bool _greenShown;

        public static DrainPacket Get(Combat.Health target, float healAmount, int motes, int healMotes,
                                      GameObject sortingReference)
        {
            var packet = GenericPool<DrainPacket>.Get();
            packet._target = target;
            packet._share = motes > 0 ? healAmount / motes : healAmount;
            packet._inFlight = Mathf.Max(1, motes);
            packet._healMotes = healMotes;
            packet._sortingReference = sortingReference;
            packet._greenShown = false;
            return packet;
        }

        /// <summary>Una mota ha llegado al jugador: cura su parte ahora.</summary>
        public void Arrive()
        {
            if (_target != null && !_target.IsDead)
            {
                float before = _target.CurrentHealth;
                _target.Heal(_share);

                if (!_greenShown && _target.CurrentHealth > before)
                {
                    _greenShown = true;
                    LifeMotes.Heal(_target.transform, _healMotes, _sortingReference);
                }
            }

            Done();
        }

        /// <summary>Una mota se ha perdido sin llegar: no cura.</summary>
        public void Lost() => Done();

        private void Done()
        {
            if (--_inFlight > 0) return;

            _target = null;
            _sortingReference = null;
            GenericPool<DrainPacket>.Release(this);
        }
    }
}
