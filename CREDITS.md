# Credits

## LumenWave
Design, code, shaders, procedural audio and levels: the LumenWave team (built with Unity 6, URP).

## Third-party assets
Audio is fully synthesised in code. Shaders, VFX and many meshes are procedural; the third-party art below is CC0 (public domain).

| Asset | Author | Licence | URL | Used for |
|---|---|---|---|---|
| Unity Legacy Runtime font (LegacyRuntime.ttf) | Unity Technologies | Unity built-in | — | UI text |
| Animated Human (low poly, rigged; Idle/Run/Walk/Jump/Punch/Death clips) | Quaternius | CC0 1.0 | https://opengameart.org/content/animated-human-low-poly | Umbra's body and animations (`ThirdParty/QuaterniusHuman`) |
| Nature Kit (trees, rocks, stones, grass, plants, cliffs, statues, stumps) | Kenney (kenney.nl) | CC0 1.0 | https://kenney.nl/assets/nature-kit | Playfield dressing, mid/background layers (`ThirdParty/KenneyNature`) |
| Graveyard Kit (crooked pines, iron fences, pillars, crypts, walls, lamp posts) | Kenney (kenney.nl) | CC0 1.0 | https://kenney.nl/assets/graveyard-kit | Ruins, foreground and background silhouettes (`ThirdParty/KenneyGraveyard`) |
| Castle Kit (towers, walls, gates, bridges) | Kenney (kenney.nl) | CC0 1.0 | https://kenney.nl/assets/castle-kit | Fortress and ruin layers (`ThirdParty/KenneyCastle`) |
| Kenney Fonts (Kenney Future, Kenney Future Narrow) | Kenney (kenney.nl) | CC0 1.0 | https://kenney.nl/assets/kenney-fonts | Logo, headings, HUD (`ThirdParty/KenneyFonts`) |
| GGBotNet Fonts CC0 (OSerif Regular, Jupiteroid Regular/Bold/Light) | GGBotNet | CC0 1.0 | https://opengameart.org/content/ggbotnet-fonts-cc0-all-in-1 | Logo/headings (OSerif), HUD and body text (Jupiteroid) (`ThirdParty/GGBotNetFonts`) |
| Rock035, Rock030, Rock051 (rock), Moss002 (moss), Ground037 (ground), Ice002 (ice), PavingStones131 (cobbles), Bricks066 (stone blocks), 1K JPG colour + normal maps | ambientCG (Lennart Demes) | CC0 1.0 | https://ambientcg.com/view?id=Rock035 (same pattern for each id) | Platform and ruin materials (`ThirdParty/AmbientCG`) |
| Kloppenheim 02 (Pure Sky), Kloppenheim 07 (Pure Sky), 2K HDR | Greg Zaal, Jarod Guest (Poly Haven) | CC0 1.0 | https://polyhaven.com/a/kloppenheim_02_puresky , https://polyhaven.com/a/kloppenheim_07_puresky | Night sky with stars / clouds (`ThirdParty/PolyHaven`) |

All third-party files were downloaded from the command line (no login), inspected in a temp folder outside the project, and only the files used were imported under `Assets/LumenWave/ThirdParty/<Pack>/` with the original licence file.

Note: the `Assets/Cainos` pack and `Assets/Scripts/Light|Shadow` were already on `main` from teammates' earlier prototypes; LumenWave does not use them.
