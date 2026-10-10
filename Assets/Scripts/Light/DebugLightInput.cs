using UnityEngine;
using UnityEngine.InputSystem;

public class DebugLightInput : MonoBehaviour
{
    public LightController lightController;

    void Update()
    {
        if (Keyboard.current.jKey.isPressed)
        {
            lightController.SetResonance(1f);
        }
        else
        {
            lightController.SetResonance(0f);
        }
    }
}