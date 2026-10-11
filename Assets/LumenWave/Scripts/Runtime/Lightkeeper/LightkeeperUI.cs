using LumenWave.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LumenWave
{
    /// <summary>Lightkeeper HUD (timer, bot deaths, energy, lamps, key help) and the result screen. Own canvas.</summary>
    public class LightkeeperUI : MonoBehaviour
    {
        LightkeeperController _c;
        TMP_Text _timer, _deaths, _energyLabel, _flash, _resultTitle, _resultSub;
        Image _energyFill;
        readonly TMP_Text[] _lampText = new TMP_Text[4];
        readonly Image[] _lampBg = new Image[4];
        GameObject _result;
        Button _retry;

        public static LightkeeperUI Build(LightkeeperController c)
        {
            var canvas = UIKit.CreateCanvas("LightkeeperUI", 30, c.transform);
            var ui = canvas.gameObject.AddComponent<LightkeeperUI>();
            ui._c = c;
            ui.Create(canvas.transform);
            c.Finished += ui.ShowResult;
            return ui;
        }

        void Create(Transform root)
        {
            // Mode tag + timer + deaths (top centre).
            var tag = UIKit.Label(root, UIKit.Spaced("LIGHTKEEPER"), 26, TextAnchor.MiddleCenter, UIStyle.Gold, "Mode");
            tag.font = UIKit.Bold;
            UIKit.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(600, 36));
            _timer = UIKit.Label(root, "1:30", 96, TextAnchor.MiddleCenter, UIStyle.Text, "Timer");
            _timer.font = UIKit.Bold;
            UIKit.Shadowed(_timer);
            UIKit.Place(_timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -105), new Vector2(500, 110));
            _deaths = UIKit.Label(root, "", 32, TextAnchor.MiddleCenter, UIStyle.Text, "Deaths");
            UIKit.Place(_deaths.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -205), new Vector2(700, 40));

            // Energy bar (bottom left).
            _energyLabel = UIKit.Label(root, "ENERGY", 24, TextAnchor.MiddleLeft, UIStyle.TextDim, "EnergyLabel");
            UIKit.Place(_energyLabel.rectTransform, new Vector2(0f, 1f), new Vector2(60, -265), new Vector2(520, 32), new Vector2(0f, 0.5f));
            var bg = UIKit.Image(root, "EnergyBg", new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(60, -300), new Vector2(520, 26), new Vector2(0f, 0.5f));
            _energyFill = UIKit.Image(bg.rectTransform, "EnergyFill", UIStyle.Gold);
            var fr = _energyFill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(1f, 1f); fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = new Vector2(3, 3); fr.offsetMax = new Vector2(-3, -3);

            // Lamp cards (bottom centre).
            for (int i = 0; i < 4; i++)
            {
                var card = UIKit.Image(root, "Lamp" + (i + 1), new Color(0.02f, 0.035f, 0.07f, 0.75f), UIKit.Soft);
                UIKit.Place(card.rectTransform, new Vector2(1f, 0.5f), new Vector2(-50, 170 - i * 96), new Vector2(280, 84), new Vector2(1f, 0.5f));
                _lampBg[i] = card;
                var t = UIKit.Label(card.rectTransform, "", 24, TextAnchor.MiddleCenter, UIStyle.Text, "Text");
                UIKit.Stretch(t.rectTransform, 6);
                _lampText[i] = t;
            }
            var help = UIKit.Label(root, "<b>1-4</b> select lamp\n<b>A/D</b> or drag: move\n<b>Q/E</b> or wheel: aim\n<b>W</b> on/off   <b>R</b> colour\nmax 2 lit, energy drains", 22, TextAnchor.UpperRight, UIStyle.TextDim, "Help");
            UIKit.Place(help.rectTransform, new Vector2(1f, 0.5f), new Vector2(-50, -230), new Vector2(420, 170), new Vector2(1f, 1f));

            _flash = UIKit.Label(root, "", 44, TextAnchor.MiddleCenter, UIStyle.Danger, "Flash");
            _flash.font = UIKit.Bold;
            UIKit.Shadowed(_flash);
            UIKit.Place(_flash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1200, 60));

            // Result screen.
            var dim = UIKit.Image(root, "ResultDim", new Color(0f, 0f, 0f, 0.82f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UIKit.Panel(dim.rectTransform, "ResultPanel", new Vector2(1100, 470));
            _resultTitle = UIKit.Heading(panel, "", 76, TextAnchor.MiddleCenter, UIStyle.Gold, "ResultTitle", 2);
            UIKit.Place(_resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1050, 110));
            _resultSub = UIKit.Label(panel, "", 30, TextAnchor.MiddleCenter, UIStyle.TextDim, "ResultSub");
            UIKit.Place(_resultSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -205), new Vector2(1000, 50));
            var buttons = UIKit.Rect(panel, "Buttons");
            UIKit.Place(buttons, new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(440, 150), new Vector2(0.5f, 0f));
            UIKit.VList(buttons, 12);
            _retry = UIKit.Button(buttons, "Retry", () => ScreenFader.FadeThen(_c.Retry), 62);
            UIKit.Button(buttons, "Title", () => ScreenFader.FadeThen(_c.ToTitle), 62);
            _result = dim.gameObject;
            _result.SetActive(false);
        }

        void Update()
        {
            if (_c == null) return;
            HideUmbraLegend();
            float t = Mathf.Ceil(_c.TimeLeft);
            _timer.text = $"{(int)t / 60}:{(int)t % 60:00}";
            _timer.color = _c.TimeLeft < 10f ? UIStyle.Gold : UIStyle.Text;
            _deaths.text = $"UMBRA DEATHS  <color=#ff5a4a>{_c.BotDeaths}</color> / {LightkeeperMode.DeathsToWin}";
            float e = _c.Energy / Mathf.Max(1f, _c.maxEnergy);
            _energyFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.001f, e), 1f);
            _energyFill.color = e < 0.25f ? UIStyle.Danger : UIStyle.Gold;
            _energyLabel.text = $"ENERGY  {Mathf.RoundToInt(_c.Energy)}   LIT {_c.LitCount}/{_c.maxLit}";
            for (int i = 0; i < 4; i++)
            {
                bool has = i < _c.Lamps.Count;
                _lampBg[i].gameObject.SetActive(has);
                if (!has) continue;
                var l = _c.Lamps[i];
                var col = LightMath.DisplayColor(l.beam.color);
                string hex = ColorUtility.ToHtmlStringRGB(col);
                bool sel = i == _c.Selected;
                _lampText[i].text = $"<b>{i + 1}</b>  <color=#{hex}>{l.beam.color.ToString().ToUpperInvariant()}</color>  {(l.On ? "ON" : "<color=#7f8aa0>off</color>")}\n<size=18>AIM {Mathf.RoundToInt(l.angle + 90f)}</size>";
                _lampBg[i].color = sel ? new Color(0.45f, 0.95f, 1f, 0.28f) : new Color(0.02f, 0.035f, 0.07f, 0.75f);
            }
            _flash.text = Time.unscaledTime < _c.FlashUntil ? _c.Flash : "";
        }

        Transform _hudLegend;
        /// <summary>The normal HUD shows Umbra's controls, which the human doesn't use here: hide that strip.</summary>
        void HideUmbraLegend()
        {
            if (_hudLegend == null)
            {
                var hud = FindFirstObjectByType<Hud>();
                if (hud == null) return;
                foreach (var t in hud.GetComponentsInChildren<Transform>(true)) if (t.name == "Legend") { _hudLegend = t; break; }
                if (_hudLegend == null) return;
            }
            if (_hudLegend.gameObject.activeSelf) _hudLegend.gameObject.SetActive(false);
        }

        void ShowResult(bool lightkeeperWins)
        {
            _result.SetActive(true);
            _resultTitle.text = lightkeeperWins ? "THE LIGHTKEEPER WINS" : "UMBRA ESCAPES";
            _resultSub.text = lightkeeperWins
                ? (_c.BotDeaths >= LightkeeperMode.DeathsToWin ? $"Umbra burned {_c.BotDeaths} times." : "Umbra never reached the door.")
                : $"Umbra reached the door with {Mathf.CeilToInt(_c.TimeLeft)} s left ({_c.BotDeaths} deaths).";
            if (_retry != null) UIKit.Select(_retry.gameObject);
        }
    }
}
