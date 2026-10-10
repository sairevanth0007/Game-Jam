using UnityEngine;
using TMPro;

namespace LumenWave.Demo
{
    public sealed class DemoHud : MonoBehaviour
    {
        public DemoSession session;
        public ChordInput input;
        public TMP_Text status, notes, message;
        public UnityEngine.UI.Image healthFill;
        float nextRefresh;
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
            notes.text = $"{((input.Held & 5) != 0 ? "<color=#8FE9EE>C</color>" : "C")}   {((input.Held & 10) != 0 ? "<color=#8FE9EE>E</color>" : "E")}   {((input.Held & 16) != 0 ? "<color=#FFDCA0>G</color>" : "G")}     |     BLUE {blue:0.0}s    RED {red:0.0}s";
            message.text = session.Message;
        }
    }
}
