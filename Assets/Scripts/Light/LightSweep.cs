using UnityEngine;
using LumenWave.Demo;

public class LightSweep : MonoBehaviour
{
    [SerializeField] float sweepAngle = 30f;   // degrees to each side
    [SerializeField] float sweepTime = 4f;     // seconds for one trip across
    [SerializeField] float pauseTime = 1.5f;   // wait at each end
    [SerializeField, Range(0f, 1f)] float startOffset = 0f;

    float baseAngle;
    float clock;
    DemoLightField field;

    void Awake()
    {
        baseAngle = transform.localEulerAngles.z;
        field = GetComponent<DemoLightField>();
        clock = startOffset * 2f * (sweepTime + pauseTime);
    }

    void Update()
    {
        if (field != null && field.Remaining > 0) return; // glowing, so hold still

        clock += Time.deltaTime;
        float cycle = 2f * (sweepTime + pauseTime);
        float c = clock % cycle;
        float k;

        if (c < sweepTime)
            k = Mathf.Lerp(-1f, 1f, Mathf.SmoothStep(0f, 1f, c / sweepTime));
        else if (c < sweepTime + pauseTime)
            k = 1f;
        else if (c < 2f * sweepTime + pauseTime)
            k = Mathf.Lerp(1f, -1f, Mathf.SmoothStep(0f, 1f, (c - sweepTime - pauseTime) / sweepTime));
        else
            k = -1f;

        transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle + k * sweepAngle);
    }
}