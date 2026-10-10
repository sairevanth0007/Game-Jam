# DEVLOG

## Sun 11 Oct 2026

**00:30 · M0 setup.** Created branch `lumenwave-25d` from `main`. Added a URP pipeline with a Universal Renderer (Forward+) next to the existing 2D one, and added physics layers. Input actions are built in code. Wrote the batchmode scripts (compile/test/build/screenshot) with a lock so parallel workers never open Unity twice.
- *Problem:* the first batch run seemed to take more than 10 minutes. *Cause:* `Start-Process -Wait` also waits for Unity's child processes (licensing client), which never exit. *Fix:* wait on the Unity PID only. A compile now takes about 15 s.
- *Problem:* rebinding overrides didn't survive a restart (EditMode test failed). *Cause:* code-built actions get new GUIDs every run. *Fix:* store overrides as `Map/Action|index|path`.

**01:00 · Architecture.** Levels are ASCII text plus entity lines. Scenes contain only a `SceneBootstrap`. Entity factories are discovered by attribute, so seven workers (visuals, player, light, sound, enemies, levels, UI) can build in parallel without touching each other's files.

**01:10 · MALink.** Wrote a hand-made OSC encoder and grandMA3 `Go+ Exec N` mapping, plus an F9 overlay. Verified it with a real UDP listener in an EditMode test.
