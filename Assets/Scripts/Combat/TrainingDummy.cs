using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// Maniquí de pruebas de daño. Va sobre un <see cref="Health"/> con vida enorme e i-frames a 0:
    /// se cura por completo en cada golpe recibido, así que nunca muere y cada perdigón, tick de
    /// haz o pellizco cuenta.
    ///
    /// Dibuja encima suyo (IMGUI, misma convención que <c>FpsOverlay</c>) la lectura de la tanda de
    /// disparos en curso: daño del último golpe, DPS, número de golpes, media y máximo. La tanda se
    /// reinicia sola tras <see cref="idleResetSeconds"/> sin recibir daño, así que cada prueba
    /// empieza limpia; también se puede reiniciar a mano con <c>R</c>.
    ///
    /// Los números flotantes de daño los pone <c>DamagePopups</c> por su cuenta (engancha a todos
    /// los Health), este componente sólo lleva las estadísticas.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class TrainingDummy : MonoBehaviour
    {
        [Tooltip("Segundos sin recibir daño tras los que la tanda se reinicia en el siguiente golpe.")]
        [Min(0.5f)]
        [SerializeField] private float idleResetSeconds = 2.5f;

        [Tooltip("Altura, en unidades de mundo sobre el origen, del panel de lectura.")]
        [SerializeField] private float readoutHeight = 1.9f;

        [Tooltip("Permite reiniciar la tanda con la tecla R.")]
        [SerializeField] private bool resetKey = true;

        private Health _health;

        private float _lastHit;
        private float _total;
        private int _hits;
        private float _maxHit;
        private float _windowStart;
        private float _lastHitTime;

        private GUIStyle _style;
        private Texture2D _bg;
        private Camera _cam;

        private bool HasData => _hits > 0;

        /// <summary>DPS de la tanda: daño total dividido por el tiempo entre el primer y el último golpe.</summary>
        private float Dps
        {
            get
            {
                if (_hits < 2) return 0f;
                float span = Mathf.Max(_lastHitTime - _windowStart, 0.0001f);
                return _total / span;
            }
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            _bg.Apply();
        }

        private void OnEnable() => _health.Damaged += OnDamaged;

        private void OnDisable() => _health.Damaged -= OnDamaged;

        private void OnDestroy()
        {
            if (_bg != null) Destroy(_bg);
        }

        private void Update()
        {
            if (resetKey && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
                ResetRun();
        }

        private void OnDamaged(float amount)
        {
            if (amount <= 0f) return;

            float now = Time.time;
            if (_hits == 0 || now - _lastHitTime > idleResetSeconds)
                ResetRun();

            if (_hits == 0) _windowStart = now;

            _lastHit = amount;
            _total += amount;
            _hits++;
            _maxHit = Mathf.Max(_maxHit, amount);
            _lastHitTime = now;

            // Vida infinita: se rellena hasta el tope en cada golpe. ResetHealth (no Heal) porque
            // funciona incluso si un golpe enorme lo dejó a 0 en el mismo frame.
            _health.ResetHealth();
        }

        private void ResetRun()
        {
            _total = 0f;
            _hits = 0;
            _maxHit = 0f;
            _lastHit = 0f;
        }

        private void OnGUI()
        {
            if (_cam == null) _cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (_cam == null) return;

            Vector3 screen = _cam.WorldToScreenPoint(transform.position + Vector3.up * readoutHeight);
            if (screen.z <= 0f) return;   // detrás de la cámara

            int fontSize = Mathf.Max(11, Mathf.RoundToInt(Screen.height * 0.019f));
            _style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold,
                richText = false,
            };
            _style.fontSize = fontSize;
            _style.normal.textColor = Color.white;

            string body = HasData
                ? $"DUMMY\n" +
                  $"Last hit: {_lastHit:0.#}\n" +
                  $"DPS: {(_hits < 2 ? "—" : Dps.ToString("0.#"))}\n" +
                  $"Hits: {_hits}   avg {_total / Mathf.Max(1, _hits):0.#}   max {_maxHit:0.#}"
                : "DUMMY\nShoot me.";

            var lines = body.Split('\n');
            float width = 0f;
            foreach (var line in lines) width = Mathf.Max(width, _style.CalcSize(new GUIContent(line)).x);
            float pad = fontSize * 0.45f;
            float height = fontSize * 1.35f * lines.Length + pad * 2f;

            // Centrado sobre el maniquí; screen.y viene de abajo, GUI cuenta desde arriba.
            float x = screen.x - width * 0.5f - pad;
            float y = (Screen.height - screen.y) - height;

            var box = new Rect(x, y, width + pad * 2f, height);
            GUI.DrawTexture(box, _bg);
            GUI.Label(new Rect(box.x + pad, box.y + pad, box.width, box.height), body, _style);
        }
    }
}
