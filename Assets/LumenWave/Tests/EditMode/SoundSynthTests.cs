using NUnit.Framework;
using UnityEngine;

namespace LumenWave.Tests
{
    public class SoundSynthTests
    {
        [Test] public void Sine_HasRequestedFrequency_ByZeroCrossings()
        {
            float f = Harmony.EchoNote;   // ~440 Hz
            var data = Synth.Render(f, 1f, Synth.Sine, new Envelope(0f, 0f, 1f, 0f), 0.5f);
            // A sine crosses zero twice per cycle.
            float measured = Synth.ZeroCrossings(data, 0, Synth.SampleRate) / 2f;
            Assert.AreEqual(f, measured, 2f);
        }

        [Test] public void Loop_IsSeamless_AndInTune()
        {
            float f = Harmony.Root;
            var data = Synth.RenderLoop(f, 2f, Synth.Sine);
            // Wrap point continuity: last sample is one step before the first.
            Assert.Less(Mathf.Abs(data[data.Length - 1] - data[0]), 0.05f);
            float measured = Synth.ZeroCrossings(data) / 2f / 2f;
            Assert.AreEqual(f, measured, 1f);
        }

        [Test] public void Envelope_AttackDecaySustainRelease()
        {
            var e = new Envelope(0.1f, 0.2f, 0.5f, 0.4f);
            Assert.AreEqual(0f, e.Evaluate(0f, 1f), 1e-4f);
            Assert.AreEqual(0.5f, e.Evaluate(0.05f, 1f), 1e-4f);   // halfway up the attack
            Assert.AreEqual(1f, e.Evaluate(0.1f, 1f), 1e-4f);      // peak
            Assert.AreEqual(0.75f, e.Evaluate(0.2f, 1f), 1e-4f);   // halfway down the decay
            Assert.AreEqual(0.5f, e.Evaluate(0.8f, 1f), 1e-4f);    // sustain
            Assert.AreEqual(0.25f, e.Evaluate(1.2f, 1f), 1e-4f);   // halfway through release
            Assert.AreEqual(0f, e.Evaluate(1.5f, 1f), 1e-4f);      // silent after release
        }

        [Test] public void Render_IsBoundedAndSilentAtEnd()
        {
            var data = Synth.Render(Harmony.Root, 0.2f, Synth.Voice, Envelope.Note, 0.5f, 0.2f);
            foreach (var s in data) Assert.LessOrEqual(Mathf.Abs(s), 1f);
            Assert.Less(Mathf.Abs(data[data.Length - 1]), 0.01f);
        }

        [Test] public void EchoWavelength_UsesSpeedOfSound()
        {
            // λ = v / f : 343 m/s at ~440 Hz → ~0.78 m.
            float lambda = Harmony.Wavelength(Harmony.EchoNote, EchoBridge.SpeedOfSound);
            Assert.AreEqual(343f / Harmony.EchoNote, lambda, 1e-4f);
            Assert.That(lambda, Is.InRange(0.7f, 0.85f));
        }

        [Test] public void Resonance_SpendDrainAdd()
        {
            var go = new GameObject("res");
            var r = go.AddComponent<Resonance>();
            r.max = 100; r.current = 30;
            Assert.IsTrue(r.TrySpend(25));
            Assert.AreEqual(5f, r.current, 1e-4f);
            Assert.IsFalse(r.TrySpend(25));
            Assert.AreEqual(5f, r.current, 1e-4f);
            Assert.IsFalse(r.Drain(10));
            Assert.AreEqual(0f, r.current, 1e-4f);
            r.Add(500);
            Assert.AreEqual(100f, r.current, 1e-4f);
            Object.DestroyImmediate(go);
        }
    }
}
