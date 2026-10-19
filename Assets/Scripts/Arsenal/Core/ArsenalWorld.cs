using System;
using System.Collections.Generic;
using RedMagic.Fx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Algo vivo del arsenal: proyectil, zona, haz o efecto. Como las entidades de <c>G.ents</c> en la web:
    /// <see cref="Tick"/> devuelve false cuando se acaba y <see cref="Draw"/> se llama una vez por capa.
    /// Objetos C# corrientes (sin GameObject): se dibujan todos en los lienzos compartidos de <see cref="ArsenalWorld"/>.
    /// </summary>
    public abstract class ArsenalEntity
    {
        /// <summary>Avanza <paramref name="dt"/> segundos (ya escalados por el tiempo del juego). false = se quita.</summary>
        public abstract bool Tick(float dt);
        public abstract void Draw(L layer);
        /// <summary>Se llama al quitarla (para devolverla a su pool o cortar un sonido en bucle).</summary>
        public virtual void OnRemoved() { }

        /// <summary>Está en el mundo (se actualiza y se dibuja). Lo pone el mundo al añadirla y lo quita al sacarla o al vaciarse.</summary>
        public bool InWorld { get; internal set; }
    }

    /// <summary>
    /// Mundo de las armas del arsenal: lista de entidades que se actualizan cada frame y cuatro lienzos <see cref="FxCanvas"/>
    /// donde se dibujan (detrás / delante, cada uno con su lienzo aditivo de brillos debajo). Se crea solo la primera vez que
    /// algo lo usa y vive entre escenas, pero se vacía al cargar una (nada se cuela de una sección a otra).
    /// Orden de dibujo: brillos de detrás 1, detrás 2 (el player está en 10), brillos de delante 39, delante 40.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ArsenalWorld : MonoBehaviour
    {
        private static ArsenalWorld _instance;
        private static bool _quitting;

        private readonly List<ArsenalEntity> _entities = new List<ArsenalEntity>(256);
        private readonly List<ArsenalEntity> _snapshot = new List<ArsenalEntity>(256);
        private FxCanvas _back, _front, _backGlow, _frontGlow;

        /// <summary>Renders del editor: borde superior de la vista (sin Play, Camera.main no es la del banco de pruebas).</summary>
        public static Func<float> ViewTopOverride;

        /// <summary>Altura del borde superior de la cámara (las lanzas del Golpe estelar caen desde ahí).</summary>
        public static float ViewTop(float fallback)
        {
            if (ViewTopOverride != null) return ViewTopOverride();
            var cam = Camera.main;
            return cam != null && cam.orthographic ? cam.transform.position.y + cam.orthographicSize : fallback;
        }

        /// <summary>Segundos de juego desde que existe el mundo (el <c>G.t</c> de la web). Respeta pausa y hit-stop.</summary>
        public static float T { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _quitting = false;
            T = 0f;
        }

        public static ArsenalWorld Instance
        {
            get
            {
                if (_instance == null && !_quitting && Application.isPlaying)
                {
                    var go = new GameObject("[ArsenalWorld]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<ArsenalWorld>();
                }
                return _instance;
            }
        }

        public static IReadOnlyList<ArsenalEntity> Entities => _instance != null ? _instance._entities : (IReadOnlyList<ArsenalEntity>)Array.Empty<ArsenalEntity>();

        /// <summary>Añade una entidad (empieza a actualizarse el frame siguiente si se añade durante una actualización).</summary>
        public static T Add<T>(T entity) where T : ArsenalEntity
        {
            var w = Instance;
            if (w != null && entity != null) { w._entities.Add(entity); entity.InWorld = true; }
            return entity;
        }

        /// <summary>Efecto de sólo dibujo que dura <paramref name="life"/> s; <paramref name="draw"/> recibe la capa y el instante en que nació.</summary>
        public static void AddFx(float life, Action<L, float> draw) => Add(FxEntity.Get(life, draw));

        private void Awake() => Init();

        private bool _inited;

        private void Init()
        {
            if (_inited) return;
            _inited = true;
            var assets = ArsenalAssets.Load();
            Material solid = assets != null ? assets.solidMaterial : null, glow = assets != null ? assets.glowMaterial : null;
            if (solid == null) Debug.LogWarning("[Arsenal] Falta Resources/Arsenal/ArsenalAssets (Tools ▸ RedMagic ▸ Arsenal ▸ Preparar): los efectos no se verán.");
            _backGlow = new FxCanvas(transform, "Arsenal_BackGlow", glow != null ? glow : solid, "Characters", 1);
            _back = new FxCanvas(transform, "Arsenal_Back", solid, "Characters", 2);
            _frontGlow = new FxCanvas(transform, "Arsenal_FrontGlow", glow != null ? glow : solid, "Characters", 39);
            _front = new FxCanvas(transform, "Arsenal_Front", solid, "Characters", 40);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Clear();
            _back?.Destroy(); _front?.Destroy(); _backGlow?.Destroy(); _frontGlow?.Destroy();
            if (_instance == this) _instance = null;
        }

        private void OnApplicationQuit() => _quitting = true;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Clear();
        }

        /// <summary>Quita todas las entidades (cambio de escena, reinicio de la escena de prueba).</summary>
        public void Clear()
        {
            foreach (var e in _entities) { e.InWorld = false; e.OnRemoved(); }
            _entities.Clear();
        }

        private void Update() => Step(Time.deltaTime);

        private void LateUpdate() => Render();

        /// <summary>
        /// Mundo para los renders del editor (sin Play): las entidades se avanzan y se dibujan a mano con <see cref="Step"/> y
        /// <see cref="Render"/>. Quitarlo con <see cref="Object.DestroyImmediate"/> del GameObject al terminar.
        /// </summary>
        public static ArsenalWorld CreatePreview()
        {
            if (_instance != null) Destroy0(_instance.gameObject);
            var go = new GameObject("[ArsenalWorld preview]") { hideFlags = HideFlags.HideAndDontSave };
            _instance = go.AddComponent<ArsenalWorld>();
            _instance.Init();
            T = 0f;
            return _instance;
        }

        /// <summary>Capa de los cuatro lienzos (los renders del editor los aíslan en una capa propia).</summary>
        public void SetCanvasLayer(int layer)
        {
            _back.GameObject.layer = layer; _front.GameObject.layer = layer; _backGlow.GameObject.layer = layer; _frontGlow.GameObject.layer = layer;
        }

        private static void Destroy0(UnityEngine.Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        /// <summary>Avanza <paramref name="dt"/> segundos: lo que se crea durante la actualización se actualiza el frame siguiente.</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;
            T += dt;

            // se recorre una copia: lo que se crea durante la actualización entra en la lista y se actualiza el frame siguiente
            _snapshot.Clear();
            _snapshot.AddRange(_entities);
            _dead.Clear();
            foreach (var e in _snapshot)
            {
                bool alive;
                try { alive = e.Tick(dt); }
                catch (Exception ex) { Debug.LogException(ex); alive = false; }
                if (!alive) _dead.Add(e);
            }
            if (_dead.Count == 0) return;
            // primero se quitan y DESPUÉS se devuelven a su pool: si no, una entidad reciclada en este mismo frame
            // se quitaría de la lista junto con la vieja (es el mismo objeto)
            _entities.RemoveAll(IsDead);
            foreach (var e in _dead) { e.InWorld = false; e.OnRemoved(); }
            _dead.Clear();
        }

        private readonly HashSet<ArsenalEntity> _dead = new HashSet<ArsenalEntity>();
        private bool IsDead(ArsenalEntity e) => _dead.Contains(e);

        /// <summary>Vuelve a dibujar todas las entidades en los lienzos.</summary>
        public void Render()
        {
            _back.Clear(); _front.Clear(); _backGlow.Clear(); _frontGlow.Clear();
            ArsenalDraw.Bind(_back, _front, _backGlow, _frontGlow);
            foreach (var e in _entities)
            {
                try { e.Draw(L.Back); e.Draw(L.Front); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            _back.Apply(); _front.Apply(); _backGlow.Apply(); _frontGlow.Apply();
        }

        /// <summary>Efecto de sólo dibujo (el <c>addFx</c> de la web), reutilizado desde un pool.</summary>
        private sealed class FxEntity : ArsenalEntity
        {
            private static readonly Stack<FxEntity> Pool = new Stack<FxEntity>(64);
            private float _life, _t0;
            private Action<L, float> _draw;

            public static FxEntity Get(float life, Action<L, float> draw)
            {
                var e = Pool.Count > 0 ? Pool.Pop() : new FxEntity();
                e._life = life; e._t0 = T; e._draw = draw;
                return e;
            }

            public override bool Tick(float dt) => T - _t0 < _life;
            public override void Draw(L layer) => _draw?.Invoke(layer, _t0);
            public override void OnRemoved() { _draw = null; Pool.Push(this); }
        }
    }
}
