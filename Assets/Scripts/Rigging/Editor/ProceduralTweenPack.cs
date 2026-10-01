using System;
using System.Text;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Los tres cuerpos por partes de referencia (dron, torreta, Coloso de Hielo) con el motor de
    /// <b>DOTween</b> (<see cref="RigDriverTween"/>), para compararlos lado a lado con los de código:
    /// <c>RobotTween</c>, <c>TurretTween</c>, <c>BossIce2Tween</c>. Son <b>copias completas</b> del
    /// prefab original (mismos datos de <see cref="ProceduralRig"/>, mismo comportamiento, misma
    /// definición de jefe) a las que sólo se les cambia el motor y se les afinan sus curvas.
    ///
    /// Idempotente: la copia se crea una vez; después sólo se asegura el motor y lo afinado en el
    /// Inspector se conserva. Son para comparar — cuando se elija motor, se cambia el componente en
    /// el prefab de verdad y estas copias se borran (un enemigo = un prefab).
    /// </summary>
    public static class ProceduralTweenPack
    {
        public const string Suffix = "Tween";

        [MenuItem("Tools/RedMagic/Procedural/Packs/Copias con DOTween (Robot · Turret · BossIce2)")]
        private static void RunMenu()
        {
            string log = Run();
            Debug.Log(log);
            EditorUtility.DisplayDialog("Copias con DOTween", log, "OK");
        }

        public static string Run()
        {
            var log = new StringBuilder();

            // Dron: ligero y nervioso — coz seca, vuelta elástica, piezas que rebotan al asentarse.
            Build("Robot", log, d =>
            {
                d.recoilEase = Ease.OutBack;
                d.settleEase = Ease.OutElastic;
                d.chargeSquash = 0.06f;
                d.releasePunch = 0.12f;
                d.chargeShake = 1.8f;
                d.hurtVibrato = 8;
                d.hurtElasticity = 0.8f;
            });

            // Torreta: mecánica — se despliega con muelle y el disparo le da un estirón corto.
            Build("Turret", log, d =>
            {
                d.wakeEase = Ease.OutElastic;
                d.sleepEase = Ease.InBack;
                d.recoilEase = Ease.OutBack;
                d.settleEase = Ease.OutBounce;
                d.chargeSquash = 0.04f;
                d.releasePunch = 0.08f;
                d.hurtVibrato = 6;
                d.hurtElasticity = 0.5f;
            });

            // Coloso: pesado — el latigazo se pasa poco, la vuelta es limpia (sin rebote de muelle) y
            // las piezas se solapan más: el torso arranca, los puños llegan después.
            Build("BossIce2", log, d =>
            {
                d.gestureWindupEase = Ease.InOutCubic;
                d.gestureStrikeEase = Ease.OutBack;
                d.strikeOvershoot = 1.1f;
                d.gestureRecoverEase = Ease.OutQuart;
                d.poseStagger = 0.06f;
                d.chargeSquash = 0f;
                d.releasePunch = 0f;
                d.hurtVibrato = 5;
                d.hurtElasticity = 0.45f;
            });

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static void Build(string name, StringBuilder log, Action<RigDriverTween> tune)
        {
            string source = $"{ProceduralEnemyPacks.Folder}/{name}.prefab";
            string copy = $"{ProceduralEnemyPacks.Folder}/{name}{Suffix}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
            {
                log.AppendLine($"x {name}: no existe '{source}'.");
                return;
            }

            bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(copy) == null;
            if (isNew && !AssetDatabase.CopyAsset(source, copy))
            {
                log.AppendLine($"x {name}: no se pudo copiar a '{copy}'.");
                return;
            }

            ProceduralRigTool.EnsureDriver(copy, typeof(RigDriverTween), log);

            if (!isNew)
            {
                log.AppendLine($"= {copy} ya existía: motor asegurado, curvas conservadas.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(copy);
            try
            {
                tune(root.GetComponent<RigDriverTween>());
                PrefabUtility.SaveAsPrefabAsset(root, copy);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            log.AppendLine($"+ {copy} (copia de {name} con RigDriverTween).");
        }
    }
}
