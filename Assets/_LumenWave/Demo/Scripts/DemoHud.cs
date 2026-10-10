using UnityEngine;
using TMPro;

namespace LumenWave.Demo
{
    public sealed class DemoHud : MonoBehaviour
    {
        public DemoSession session;
        public ChordInput input;
        public TMP_Text status, notes, message, controls;
        public UnityEngine.UI.Image healthFill;
        float nextRefresh;
        void OnEnable() { if (input!=null) input.BindingsChanged+=RefreshControls; }
        void OnDisable() { if (input!=null) input.BindingsChanged-=RefreshControls; }
        void Start() { RefreshControls(); }
        void RefreshControls() { if (controls!=null && input!=null) controls.text=input.ControlsHint; }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .05f;
            healthFill.fillAmount = session.Health / 100f;
            status.text = $"SHADOW  {Mathf.CeilToInt(session.Health):000}     FALLS  {session.Deaths:00}";
            float blue = 0, red = 0;
            foreach (var light in session.Lights)
            {
                if (light.Kind == LightColorType.Blue) blue = Mathf.Max(blue, light.Remaining);
                if (light.Kind == LightColorType.Red) red = Mathf.Max(red, light.Remaining);
            }
            notes.text = $"{((input.Held & ChordInput.C) != 0 ? "<color=#8FE9EE>C</color>" : "C")}   {((input.Held & ChordInput.E) != 0 ? "<color=#8FE9EE>E</color>" : "E")}   {((input.Held & ChordInput.G) != 0 ? "<color=#FFDCA0>G</color>" : "G")}     |     BLUE {blue:0.0}s    RED {red:0.0}s";
            message.text = session.Message;
        }
    }
}
