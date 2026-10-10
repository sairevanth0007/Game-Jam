using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ShadowRenderer : MonoBehaviour
{
    private ShadowGenerator generator;
    private LineRenderer line;


    void Awake()
    {
        generator =
        GetComponentInParent<ShadowGenerator>();

        line =
        GetComponent<LineRenderer>();
    }


    void LateUpdate()
    {
        line.useWorldSpace = false;
        if(generator == null || generator.shadowPoints == null || generator.shadowPoints.Length == 0)
            return;


        line.positionCount =
            generator.shadowPoints.Length + 1;


        for(int i=0;i<generator.shadowPoints.Length;i++)
        {
            line.SetPosition(
                i,
                generator.shadowPoints[i]
            );
        }


        line.SetPosition(
            generator.shadowPoints.Length,
            generator.shadowPoints[0]
        );
    }
}
