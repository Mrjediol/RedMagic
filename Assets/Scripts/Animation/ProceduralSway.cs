using UnityEngine;

public class ProceduralSway : MonoBehaviour
{
    [SerializeField] Vector2 moveAmplitude = new Vector2(0f, 0.05f);
    [SerializeField] float rotateAmplitude = 3f;   // grados
    [SerializeField] float speed = 1.5f;
    [SerializeField] float phase = 0f;             // desfasa cada parte

    Vector3 startPos;
    Quaternion startRot;

    void OnEnable()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
    }

    void Update()
    {
        float s = Mathf.Sin(Time.time * speed + phase);
        transform.localPosition = startPos + (Vector3)(moveAmplitude * s);
        transform.localRotation = startRot * Quaternion.Euler(0f, 0f, rotateAmplitude * s);
    }
}