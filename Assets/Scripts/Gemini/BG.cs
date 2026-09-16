using System.Collections.Generic;
using UnityEngine;

public class MaskToColliders : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Asigna aquí la imagen PNG de la máscara en blanco y negro")]
    public Sprite maskSprite;

    [Tooltip("Usa un valor pequeño (ej: 0.05) para simplificar y suavizar los bordes")]
    public float tolerance = 0.05f;

    [Tooltip("¿Las plataformas permiten saltar desde abajo hacia arriba?")]
    public bool isOneWay = true;

    void Start()
    {
        GenerateColliders();
    }

    [ContextMenu("Generar Colisionadores Ahora")]
    public void GenerateColliders()
    {
        if (maskSprite == null)
        {
            Debug.LogError("Por favor asigna un Sprite de máscara en el inspector.");
            return;
        }

        // 1. Limpiar colisionadores previos
        PolygonCollider2D[] oldColliders = GetComponents<PolygonCollider2D>();
        foreach (var col in oldColliders)
        {
            DestroyImmediate(col);
        }

        // 2. Extraer los contornos de la máscara
        List<Vector2[]> paths = new List<Vector2[]>();
        int count = maskSprite.GetPhysicsShapeCount(); // Corregido a int

        for (int i = 0; i < count; i++)
        {
            List<Vector2> path = new List<Vector2>();
            maskSprite.GetPhysicsShape(i, path);

            // Simplificar el contorno para evitar exceso de vértices
            if (tolerance > 0)
            {
                LineUtility.Simplify(path, tolerance, path);
            }

            paths.Add(path.ToArray());
        }

        // 3. Crear los PolygonCollider2D para cada plataforma
        for (int i = 0; i < paths.Count; i++)
        {
            PolygonCollider2D polyCollider = gameObject.AddComponent<PolygonCollider2D>();
            polyCollider.pathCount = 1;
            polyCollider.SetPath(0, paths[i]);

            if (isOneWay)
            {
                polyCollider.usedByEffector = true;
            }
        }

        // 4. Agregar PlatformEffector2D si es One-Way y no existe
        if (isOneWay && GetComponent<PlatformEffector2D>() == null)
        {
            PlatformEffector2D effector = gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
        }

        Debug.Log($"¡Listo! Se generaron {paths.Count} colisionadores automáticamente.");
    }
}