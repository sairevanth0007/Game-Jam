# Decisions log

Format: time (Sun 11 Oct 2026) — decision — why.

- 00:35 — Branch `lumenwave-25d` from updated `main`. Other branches left untouched; stash@{0} kept.
- 00:35 — Existing files on `main` (Cainos pack, `Assets/Scripts/Light|Shadow`, 2D renderer, sample scenes) are **kept**, not deleted. LumenWave lives in `Assets/LumenWave/` with its own asmdefs, so it is isolated from them. Build settings only list LumenWave scenes.
- 00:36 — Windows MCP tools were not exposed in this session even after reconnect. Visual verification uses built-player screenshot mode (`scripts/screenshot.ps1`) and batchmode; will retry MCP later. Asset downloads via Brave not possible without MCP → hero and assets are procedural (CC0 downloads only if a direct, licence-clear URL works from the CLI).
- 00:40 — New URP pipeline asset with a **Universal Renderer (Forward+)** in `Assets/LumenWave/Settings`, assigned to Graphics + all quality levels. The old 2D renderer asset is left in place, unused.
- 00:40 — Input actions are **built in code** (`LumenInput`) rather than an `.inputactions` asset: diff-friendly, no editor import step. Rebinding overrides persist as `Map/Action|index|path` lines in PlayerPrefs because code-built actions get new GUIDs every run (the Input System JSON override format keys on GUIDs — caught by an EditMode test).
- 00:40 — Keys: Move A/D (+arrows, stick, d-pad), Jump Space/A, Dash LeftShift/B, Echo J/LMB/X, Shield K(hold)/RMB/LT, Pulse L/MMB/Y, Pause Esc/Start.
- 00:42 — Levels are ASCII text files in `Resources/Levels` + entity lines; scenes contain only a `SceneBootstrap`, all content is built at runtime. Reason: fully reproducible, no hand-written scene YAML, procedural meshes don't need asset serialisation, easy for workers to iterate in parallel.
- 00:42 — Entity factories are discovered by attribute (`[LevelEntity("type")]`) so each subsystem registers its own entities without editing shared files.
- 00:43 — Light rules as pure logic: White (R∧G∧B part) is neutral; overlapping Red+Green becomes Yellow (beat-gated damage); remaining Red burns, Green heals + refills, Blue lowers gravity. Darkness (total < 0.12) refills resonance.
- 00:44 — Music in D, just intonation (1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8). Echo = fifth, Shield = tonic, Pulse = major third. 100 BPM, beat on the DSP clock.
- 00:50 — `Start-Process -Wait` hangs on Unity's lingering child processes; scripts now wait on the Unity process only (with timeout).
- 01:00 — Parallel workers (forks) with strict folder ownership: visuals, player, light, sound, enemies, levels+bot, UI. The orchestrator commits; workers never touch git state.
- 01:20 — The Unity lock also waits for any Unity.exe running on this project (CIM command-line check). A test run started without the lock once, after a lock dir was removed by hand.
- 01:25 — Third enemy: **Echo Moth**. It is harmless until Umbra uses a power nearby, then it hunts and drains resonance, so players have to choose when to make noise. The Shield and Pulse stop it.
- 01:40 — Prism splits per channel around its angle (blue bends most). White right at the prism is neutral, so the level 6 puzzle has to separate the blue ray with a mirror.
- 01:50 — Visuals: a per-level `mood:` header (blue/violet/red/green/gold/dawn) sets the sky, fog and grading.
- 01:55 — Moved the tutorial message banner from the upper sky to the lower middle so it never hides lamps or sentinels.
- 01:55 — Windows MCP is still not exposed. "Play the first minute" verification is done with a built-player `-lw-autoplay` mode: the bot plays at 1× and takes screenshots every 5 s.
- 02:45 — Commit the release zip (about 35 MB) only for verified release candidates (RC1 02:45, RC2 03:39, RC3) to keep repo growth small.
- 03:05 — Assist mode instead of an easier default difficulty: the bot proves solvability, and assist covers the gap between a bot and human timing.
- 03:45 — Added the Flare enemy because the brief says the shield blocks *projectiles* and no enemy had any.
- 04:30 — Deadline met early. Remaining time goes to small, verified polish only; no risky refactors.

## Visual overhaul (branch `lumenwave-visual`, from `lumenwave-25d` at RC4)
- 07:51 — New branch `lumenwave-visual` from `lumenwave-25d` (a242ee2). RC4 on `lumenwave-25d` stays untouched as the safe fallback. The visual release zip is `Release/LumenWave-Windows-visual.zip`, so the RC4 zip is never overwritten.
- 07:52 — Windows MCP still fails to connect (connection timeout), so assets come from the command line (curl) instead. No browser and no logins.
- 07:53 — Hero source: Kenney's CC0 animated characters only have idle/run/jump and chibi proportions. Quaternius' Universal Animation Library is only on itch.io, which sits behind a Cloudflare bot check, and we don't bypass bot checks. Picked **Quaternius "Animated Human" (CC0, OpenGameArt direct link)**: realistic proportions, Humanoid-compatible rig (avatar valid, all major bones mapped), Idle/Run/Walk/Jump/Punch/Death clips. Fall, land, dash, cast, shield, hurt and sing are built on top procedurally (Animator + LateUpdate overlays).
- 07:55 — Environment art: Kenney Nature, Graveyard and Castle kits (CC0 FBX, untextured, so we can apply our own silhouette/rock materials), ambientCG 1K rock/moss/ice/brick textures, two Poly Haven pure night skies. Only the files we use were imported.
- 07:58 — Third-party files live under `ThirdParty/<Pack>/Resources/Art/<Pack>/`, so runtime code loads them with `Resources.Load("Art/<Pack>/<name>")`. That fits the existing "everything is built at runtime from code" architecture, with no scene or prefab wiring.
- 08:00 — Build and screenshot scripts take `-Name`, so the three parallel workers (hero, environment, UI/VFX) each build into `Builds/<name>/` and never overwrite a player that is still running.
- 08:00 — Before screenshots: same 10 framings as the after shots (one per level) plus the title, from the RC4 build → `docs/images/before_after/before/`.

### Visual overhaul — hero
- 08:10 — The skinned hero (`UmbraHero`) sits behind the unchanged `UmbraRig` API. If `Resources/Hero/UmbraModel` is missing or invalid (no avatar, controller or head bone), `UmbraRig` builds the old primitive rig automatically. `UmbraRig.UseSkinnedHero = false` forces the primitive rig.
- 08:12 — The Animator states are picked from code with `CrossFadeInFixedTime`, so the controller has no transition graph to maintain. Jump, Fall and Land all play the Jump clip through a time parameter (`AirTime`) mapped from vertical velocity: rising runs 0.13→0.34, falling runs 0.38→0.56 with a slow wobble, landing runs 0.70→0.95. Air poses always match the physics, including in blue light.
- 08:12 — Import fixes per clip, in ArtImport (hero part):
  - Idle and Punch use body orientation, because they were authored about 45° off forward.
  - Jump's vertical travel is not baked into the pose, because the controller does the rise.
  - Death's forward slide is not baked, so the body stays inside the collider.
- 08:15 — `HeroSetup.Ensure()` (one line in `ProjectSetup.Run`) builds the controller and prefab only when they are missing or `Version` changed. That keeps GUIDs stable across `compile.ps1 -Setup` runs.
- 08:20 — Rendered height is `ctl.height × 1.06`, measured from the bind-pose mesh bounds, with feet on the collider bottom. Girth is ×1.14 on the model's X/Z for a bulkier silhouette. The model faces ±X with a 22° turn toward the camera and turns through the camera-facing view.
- 08:25 — Look:
  - Same Umbra shader, with new properties (rim strength, key direction, ambient, fog amount, dissolve). Old names and defaults are kept, so the primitive rig looks the same as before.
  - The hero uses a light-facing Schlick rim on the mesh plus the existing stencil outline hull. The outline is dropped while dissolving.
  - The hero gets only 25% of the scene fog, so it stays inky.
  - Eyes are camera-facing Glow quads that follow the head bone and blink.
- 08:30 — Procedural layers:
  - Shield, singing and head look use humanoid IK. Hand goals are in world space relative to the feet, so they don't depend on bone axes.
  - Lean, hurt flinch and breathing are spine and chest rotations about world Z in LateUpdate.
  - Foot IK raycasts Ground/Bridge only when standing still (idle/land), so running strides are never altered.
- 08:32 — Dash ghosts bake a snapshot of the skinned mesh each time (`BakeMesh`). The ghost owns the mesh and destroys it.
- 08:32 — Death runs a noise dissolve from the top down with rising GlowParticle motes. Respawn reverses it in 0.5 s.
- 08:35 — Debug mode `-lw-heroposes <dir>` (Player/HeroPoseDirector.cs) captures every hero state in the real build and logs Umbra's screen rectangle for cropping. We used it to tune the poses.

### Visual overhaul — UI/VFX
- 09:10 — Typography: Kenney Future (CC0) for the logo, headings, numbers, buttons and HUD labels. Its R/K/M glyphs read as A/H/m at small sizes, so sentences (tutorial banner, credits, end line) and every key-cap stay in the built-in body font (bold). Kenney Future Narrow was tried and dropped: same ambiguity at HUD sizes.
- 09:10 — UI stays code-built uGUI with procedural sprites (gradients, diamond, sine icon, ring) plus two small helpers: `UIGradient` (vertex-colour gradient and light sweep) and `UIWaveLine` (an animated waveform mesh; it skips rebuilds while invisible). No textures or prefabs to import.
- 09:10 — HUD: thick rounded, masked bars with a red damage trail, segment dividers, a leading-edge glow and icons. The resonance bar has cost ticks (shield start, pulse, echo; assist-scaled), and power chips light up when a power is affordable, flash on use and turn red on failure. Key hints are key-cap boxes rebuilt only when the bindings change.
- 09:10 — Title: logo and menu on the left over a soft left-hand gradient, so the environment worker's live backdrop stays visible on the right. Button labels, names and order are unchanged, so FlowTestDirector and the UI tests didn't need changes.
- 09:10 — Effects all go through `ImpactFx` (Enemies/ImpactFx.cs): five pooled world-space particle systems (sparks, motes, flash, ring, mesh glass shards) driven by `Emit(EmitParams)`. No allocations per hit, capped particle counts, and its own xorshift RNG so cosmetic randomness never consumes `UnityEngine.Random` (keeps bot runs deterministic). Flare bolt bursts used to create a ParticleSystem and a material per bolt; they now use the pool.
- 09:10 — Shield hit ripples: four `_HitN` slots in the shader (object-space direction and age). Inputs are absorbed flare bolts, wisps or moths pushed off the dome, and beams ending on its collider (throttled). Pulse camera shake goes through `CameraRig.Shake`, so the Screen shake setting still applies. No gameplay values or timings were changed.
- 09:10 — The flow test sets `InputSystem.settings.backgroundBehavior = IgnoreFocus` (flow-test mode only). Its injected key events were dropped whenever another window had focus, which made the run fail for reasons unrelated to the UI.
- 09:25 — Fonts: Kenney Future's R and K read as A and H ("AED LIGHT"), and every Kenney font looks pixel/gamey next to the Limbo/Ori mood. Switched to GGBotNet's CC0 fonts from OpenGameArt: **OSerif** (elegant serif caps) for logo and headings, **Jupiteroid** (clean sans) for HUD and body text. Kenney fonts stay imported but unused.
- 09:15 — Hero polish round 2:
  - Rim reads like Limbo / Shadow Fight: the outline hull is strong and wider on the key-light side (`_KeyPower` 5, `_WidthUnlit` 0.45), with only a faint minimum line on the far side. The key direction follows Umbra's upper-back edge and swings smoothly when Umbra turns. The body fill is darker (mesh rim 0.22, ambient halved).
  - Idle guard stance (all IK): the body drops 4.5% of height with breathing, the front/back stride narrows to 50% with feet planted, and fists come up in front of the chest with elbows down.
  - Dash ghosts: new additive rim-only `LumenWave/UmbraGhost` shader, cyan, fading over 0.25 s. `BakeMesh` must use `useScale=false` for this model, because the 69× armature scale is already in the bones; `useScale=true` produced 3 cm ghosts.
  - The scarf tip trail is brighter.
- 09:40 — Fonts replaced (supersedes the Kenney typography note above): GGBotNet CC0 OSerif for the logo, headings, level names and end title, and Jupiteroid (Regular/Bold/Light) for everything else. Numbers (timer, level numbers, end-screen stats) use Jupiteroid too, because OSerif's ornamented 0 reads poorly in digits. Key caps use Jupiteroid Regular, since Bold looks cramped at key-cap sizes.

### Visual overhaul — environment
- 09:50 — Camera: FOV 30, distance 21, 3° downward pitch, so Umbra is about 15–16 % of the screen height at 1600x900. Look-ahead goes up from 2.6 to 3.4, so more of the path ahead is visible. Bounds clamping accounts for the pitch. Gaussian DOF only blurs beyond camera distance + 24, so the far scenery and sky go soft while the playfield stays sharp.
- 09:50 — Platforms keep their BoxColliders untouched; only the cube renderer is hidden. `PlatformVisual` replaces it with a bevelled, slightly lumpy rock body plus a themed cap on every exposed top run (moss, grass, snow, paving or dust). The cap has a flat walkable top, an irregular overhang, hanging roots or icicles, and a soft moonlit lip. Exposed ceilings get short, thin strands so they never read as hazards. Shader: `LumenWave/Rock` is triplanar ambientCG colour + normal. Each texture is normalised by its own last mip, so tints are absolute albedos. The rock darkens with depth below the walkable surface (baked into uv1) and gets a stylised moon fill, so the texture and bevels read on camera-facing faces.
- 09:50 — Depth layers per theme (`Theme`, chosen by the new `theme:` header line):
  - Background: Kenney models flattened into parts with our own `LumenWave/Prop` material (near-black albedo, moon-tone rim, per-layer atmosphere amount plus low mist), arranged as near, mid, far and mountain layers. The layers follow a smoothed floor profile of the level and are sized so the skyline tops out around 55–75 % of the screen, keeping the moon and sky open.
  - Mist sheets sit between the layers.
  - Foreground: `SoftSilhouette` grass, roots, vines, branches and chains with pre-blurred alpha. They follow the camera vertically and stay in the top and bottom screen bands, so they never sit on the player path.
  - Near-layer props are vertical shapes only (no flat-topped walls or boxes), so nothing behind the playfield looks walkable.
- 09:50 — Sky: the Poly Haven night panorama is sampled by view direction on the existing Sky quad, magnified, mood-tinted and clamped (the HDR sun/moon must not bloom). The horizon uses the same luminous fog colour the scenery fades into. The moon disc gets a corona and 7 additive god-ray shafts at 62 % of its distance, so the far silhouettes cut them into beams. Lamps get a soft additive cone and a halo (`LightShaft`).
- 09:50 — Linear fog starts 5 units behind the playfield, so the play plane is never fogged. Grading is per mood (contrast, saturation, exposure). Bloom threshold is 0.95. Red moods use saturation 0 and a dark crimson-brown fog, so red beams stay the most saturated red on screen (ACES kept). Split-toning shadows were reduced, because they turned rock faces neon blue.
- 09:50 — Themes and moods per level:
  - 01 forest (blue) · 02/03 canyon (violet) · 04 ruins (red) · 05 ice (new `ice` mood, snow motes, ice shards) · 06 garden (green, fireflies, crystals) · 07 clockwork (gold; procedural gears tick one tooth per `BeatClock.OnBeat`) · 08 prism (new `prism` mood, iridescent crystals) · 09 fortress (red, castle towers, embers) · 10 dawn.
  - Only the `mood:`/`theme:` header lines changed (05 green→ice, 06 blue→green, 08 violet→prism).
- 09:50 — Performance: the Kenney meshes import non-readable, so static batching isn't possible. They rely on the SRP batcher (few shared materials, no shadows, no probes). Particles are capped (at most 320) and follow the camera. Autoplay on L1, L4 and L7 stays at 60 fps (p95 16.7 ms).
- 09:50 — The ambientCG `_Color` textures had imported as Sprites without mipmaps. I fixed their .meta files (Default type, mipmaps on) and added the rule to `ArtImport` for future imports.
