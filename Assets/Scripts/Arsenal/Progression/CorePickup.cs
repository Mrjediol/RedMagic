using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Lo que suelta un <see cref="CoreCarrier"/>: un <b>núcleo</b> (<see cref="WeaponRunProgress.GrantCore"/>: subir de nivel
    /// un arma) o, si tus armas ya no pueden subir más, <b>fragmentos de alma</b> (moneda de las mejoras permanentes). Flota
    /// donde murió, y al acercarse el jugador (<see cref="WeaponProgressionConfig.coreMagnetRadius"/>) vuela hacia él. No
    /// caduca. Sale pocas veces por partida: se crea y se destruye, sin pool.
    /// </summary>
    public class CorePickup : MonoBehaviour
    {
        private const float CollectRadius = 0.7f;

        private Transform _player;
        private Vector3 _home;
        private float _speed, _t;
        private bool _homing;
        private int _souls;   // 0 = núcleo

        public static CorePickup Spawn(Vector2 at)
        {
            var p = Create(at, WeaponProgressionConfig.Instance.coreSprite);
            ArsenalProgressUi.Toast("progress.core_dropped");
            ArsenalSounds.Play("star_mark", at);
            return p;
        }

        public static CorePickup SpawnSouls(Vector2 at, int amount)
        {
            var visual = CurrencyManager.Instance != null ? CurrencyManager.Instance.Config?.VisualFor(Currency.SoulFragment) : null;
            var p = Create(at, visual != null ? visual.icon : WeaponProgressionConfig.Instance.coreSprite);
            p._souls = Mathf.Max(1, amount);
            ArsenalProgressUi.Toast("progress.souls_dropped");
            ArsenalSounds.Play("star_mark", at);
            return p;
        }

        private static CorePickup Create(Vector2 at, Sprite sprite)
        {
            var cfg = WeaponProgressionConfig.Instance;
            var go = new GameObject("CorePickup");
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Characters";
            sr.sortingOrder = 60;
            if (sprite != null)
            {
                float h = sprite.bounds.size.y;
                if (h > 0f) go.transform.localScale = Vector3.one * (cfg.coreSize / h);
            }
            var p = go.AddComponent<CorePickup>();
            p._home = at;
            return p;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 2f) * 12f);

            if (_player == null)
            {
                var user = WeaponUser.Current;
                _player = user != null ? user.transform : GameObject.FindWithTag(Teams.PlayerTag)?.transform;
            }
            Vector3 target = _player != null ? (Vector3)PlayerHit.BodyCenter(_player) : _home;
            if (_player != null && !_homing && Vector2.Distance(transform.position, target) <= WeaponProgressionConfig.Instance.coreMagnetRadius)
                _homing = true;

            if (!_homing)
            {
                transform.position = _home + Vector3.up * (Mathf.Sin(_t * 2.5f) * 0.15f);
                return;
            }

            _speed += 30f * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, target, (4f + _speed) * Time.deltaTime);
            if (Vector2.Distance(transform.position, target) > CollectRadius) return;

            if (_souls > 0)
            {
                CurrencyManager.Instance?.Add(Currency.SoulFragment, _souls);
                ArsenalProgressUi.ToastFormat("progress.souls_collected", _souls);
            }
            else
            {
                WeaponRunProgress.GrantCore();
                ArsenalProgressUi.Toast("progress.core_collected");
            }
            ArsenalSounds.Play("sing_capture", transform.position);
            Destroy(gameObject);
        }
    }
}
