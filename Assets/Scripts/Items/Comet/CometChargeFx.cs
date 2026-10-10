using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;
using static RedMagic.Items.CometDraw;

namespace RedMagic.Items
{
    /// <summary>
    /// La carga del cometa en la boca, dibujada por código y <b>pooled</b>: un orbe pequeño que crece
    /// recibiendo partículas; al llegar a plena carga, el aviso de «listo» (onda corta y el orbe se hincha) y
    /// después el pulso en bucle (el halo respira y sale una onda suave cada medio segundo) hasta soltar. Con
    /// upgrade llegan muchas más partículas, de colores intensos. Suena la campanilla de «listo» y el zumbido
    /// de mantener (<see cref="CometSettings.readySound"/> / <see cref="CometSettings.holdLoopSound"/>).
    /// La crea <see cref="WeaponUser"/> al empezar a cargar un arma Comet. Traducción de <c>chargeReady()</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CometChargeFx : MonoBehaviour, IPooled
    {
        private static Pool<CometChargeFx> _pool;

        private FxCanvas _back, _front;
        private System.Func<Vector2> _muzzle;
        private CometSettings _settings;
        private CometPalette _pal = CometPalette.Normal;
        private float _px = 0.016f, _orb = 1f;
        private bool _pow;

        private float _tt, _fraction, _readyAt = -1f, _releaseAt = -1f, _cancelAt = -1f;
        private SoundLoop _loop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _pool = null;

        /// <summary>Empieza a cargar en la boca que devuelve <paramref name="muzzle"/> (se consulta cada frame).</summary>
        public static CometChargeFx Begin(CometSettings settings, System.Func<Vector2> muzzle)
        {
            _pool ??= new Pool<CometChargeFx>(() => new GameObject("Comet Charge Fx").AddComponent<CometChargeFx>(), prewarm: 1);
            var fx = _pool.Get();
            fx.Init(settings, muzzle);
            return fx;
        }

        private void Init(CometSettings settings, System.Func<Vector2> muzzle)
        {
            _settings = settings;
            _muzzle = muzzle;
            _px = settings != null ? settings.pixel : 0.016f;
            _pow = settings != null && settings.upgraded;
            _orb = _pow ? 1.35f : 1f;
            _pal = settings != null && settings.behaviour == CometBehaviour.Homing ? CometPalette.Amber : CometPalette.Normal;
            _tt = 0f;
            _fraction = 0f;
            _readyAt = _releaseAt = _cancelAt = -1f;
            if (_back == null)
            {
                var mat = settings != null && settings.fxMaterial != null ? settings.fxMaterial : new Material(Shader.Find("RedMagic/Fx Vertex Color"));
                _back = new FxCanvas(transform, "__CometChargeBack", mat, "Characters", 2);
                _front = new FxCanvas(transform, "__CometChargeFront", mat, "Characters", 40);
            }
        }

        /// <summary>Carga 0..1 de este frame. Al llegar a 1 pasa a «listo».</summary>
        public void SetFraction(float fraction)
        {
            if (_releaseAt >= 0f || _cancelAt >= 0f) return;
            _fraction = Mathf.Clamp01(fraction);
            if (_fraction >= 1f && _readyAt < 0f)
            {
                _readyAt = _tt;
                var audio = AudioManager.Instance;
                if (audio != null && _settings != null)
                {
                    audio.Play(_settings.readySound, transform.position);
                    _loop = audio.StartLoop(_settings.holdLoopSound, this, transform);
                }
            }
        }

        /// <summary>Sale el disparo: el orbe se encoge y se apaga en un instante (el fogonazo lo dibuja el cometa).</summary>
        public void Release()
        {
            if (_releaseAt < 0f) _releaseAt = _tt;
            StopLoop();
        }

        /// <summary>Se soltó sin llegar al mínimo (o se interrumpió): se desvanece.</summary>
        public void Cancel()
        {
            if (_cancelAt < 0f && _releaseAt < 0f) _cancelAt = _tt;
            StopLoop();
        }

        private void StopLoop()
        {
            if (_loop.IsValid) AudioManager.Instance?.StopLoop(_loop);
            _loop = default;
        }

        void IPooled.OnReturnedToPool()
        {
            StopLoop();
            _muzzle = null;
            _back?.Clear(); _back?.Apply();
            _front?.Clear(); _front?.Apply();
        }

        private void OnDestroy()
        {
            _back?.Destroy();
            _front?.Destroy();
        }

        private void LateUpdate()
        {
            _tt += Time.deltaTime;
            if (_muzzle != null) transform.position = _muzzle();

            float end = _releaseAt >= 0f ? _releaseAt + 0.08f : _cancelAt >= 0f ? _cancelAt + 0.15f : -1f;
            if (end >= 0f && _tt >= end)
            {
                _pool?.Release(this);
                return;
            }
            Draw();
        }

        private void Draw()
        {
            _back.Clear();
            _front.Clear();

            Vector2 c = transform.position;
            float px = _px, prep = _fraction;
            bool ready = _readyAt >= 0f;
            float tr = ready ? _tt - _readyAt : 0f;
            float rl = _releaseAt >= 0f ? Seg(_tt, _releaseAt, _releaseAt + 0.08f) : 0f;
            float cancel = _cancelAt >= 0f ? Seg(_tt, _cancelAt, _cancelAt + 0.15f) : 0f;
            float al = (1f - rl) * (1f - cancel);
            float pop = ready ? 1f - Seg(tr, 0f, 0.18f) : 0f;
            float mul = (ready ? 1f + 0.1f * Mathf.Sin(_tt * 14f) : 1f) * (1f + 0.55f * pop) * (1f - rl * 0.5f) * _orb;

            // partículas que entran (siguen entrando, más suaves, al estar listo)
            float e2 = _tt * 1.6f;
            var R = new FxRng(5);
            for (int i = 0; i < 8; i++)
            {
                float a = R.Next() * Mathf.PI * 2f, d = (1f - Mathf.Repeat(e2 * 2f + R.Next(), 1f)) * 36f;
                _front.Disc(c + Dir(a) * (d * px), 1.6f * px, Fade(i % 2 == 1 ? _pal.light : _pal.white, prep * (ready ? 0.65f : 1f) * al));
            }
            if (_pow)
            {
                // upgrade: muchas más partículas, de colores intensos, desde todas las direcciones por igual
                for (int i = 0; i < 30; i++)
                {
                    var Ri = new FxRng(i * 41 + 7);
                    float a = i * 2.39996f + 0.4f, cyc = Mathf.Repeat(e2 * 2f + Ri.Next() + i * 0.37f, 1f), d = 6f + 56f * (1f - cyc);
                    var canvas = Mathf.Sin(a) > 0f ? _back : _front;
                    canvas.Disc(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * (d * px), (1.8f + (i % 3) * 0.6f) * px,
                                Fade(_pal.vivid[i % _pal.vivid.Length], Mathf.Min(1f, prep * 1.5f) * al * (ready ? 0.8f : 1f)));
                }
            }

            Glow(_front, c, (8f + 26f * prep) * mul * (ready ? 1.25f : 1f) * px, _pal.glowPurple, 0.5f * prep * al);
            if (ready) Glow(_front, c, 24f * mul * px, _pal.glowCyan, 0.35f * al);
            _front.Disc(c, (2f + 7f * prep) * mul * px, Fade(_pal.cyan, al));
            _front.Disc(c, (1f + 4f * prep) * mul * (ready ? 1.15f : 1f) * px, Fade(_pal.white, al));

            if (ready && rl <= 0f && cancel <= 0f)
            {
                // aviso: onda corta
                float u = tr / 0.3f;
                if (u < 1f)
                {
                    float r = Mathf.Lerp(10f, 46f, EaseOut(u)) * px, w = Mathf.Lerp(4f, 1f, u) * px;
                    RingHalf(_back, c, 0f, r, 0.32f, 0.08f, 3, w, _pal.cyan, 1f - u, false);
                    RingHalf(_front, c, 0f, r, 0.32f, 0.08f, 3, w, _pal.white, 1f - u, true);
                }
                // pulso: una onda suave cada medio segundo
                float p = Mathf.Repeat(_tt * 2f, 1f), pr = Mathf.Lerp(12f, 36f, EaseOut(p)) * px;
                RingHalf(_back, c, 0f, pr, 0.32f, 0.06f, 7, 1.6f * px, _pal.light, (1f - p) * 0.55f, false);
                RingHalf(_front, c, 0f, pr, 0.32f, 0.06f, 7, 1.6f * px, _pal.light, (1f - p) * 0.55f, true);
            }

            _back.Apply();
            _front.Apply();
        }
    }
}
