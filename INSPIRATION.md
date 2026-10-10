# LumenWave: Inspiration & Science

*A shadow learns to sing. Every light in LumenWave follows real optics, and every sound follows real acoustics.*

## Games that inspired us

| Game | What we took from it |
|---|---|
| **Limbo** (Playdead, 2010) | A black silhouette hero in a hostile, foggy world. Readability comes from contrast, not detail. |
| **Inside** (Playdead, 2016) | Side camera with subtle depth, layered parallax, quiet environmental storytelling. |
| **Shadow Fight** (Nekki) | A purely black humanoid whose rim of light carries all the expressiveness. |
| **Ori and the Blind Forest** (Moon Studios) | Glowing particles, bloom, tight platforming (coyote time, jump buffering, dash). |
| **Journey** (thatgamecompany) | A trailing scarf as character and emotion; the hero "sings" to change the world. |
| **Celeste** (Maddy Makes Games) | Game feel: variable jump height, apex hang, an air dash, generous input windows. |
| **Child of Light** (Ubisoft) | Light as both a weapon and a guide. |
| **Sound Shapes** (Queasy Games) | Levels that are also music. Hazards and platforms move to the beat. |

## The science, and where it shows up

### 1. Inverse-square law: `I = I₀ / d²`
A point source spreads its energy over a sphere of area 4πd², so intensity falls with the square of distance.
**In game:** red burns harder close to a lamp, and green heals more at the centre of a pool. `LightMath.InverseSquare` (d is clamped to a minimum so it never divides by zero).

### 2. Beer–Lambert law: `I = I₀ · e^(−σd)`
Light passing through fog, dust or smoke is absorbed and scattered exponentially with distance.
**In game:** beams fade along their length in the `Beam` shader, and the fog density σ also reduces gameplay intensity (`LightMath.BeerLambert`, `LightMath.Attenuate`).

### 3. Additive (RGB) colour mixing
Light adds; paint subtracts. Red + Green = Yellow, and Red + Green + Blue = White.
**In game:** where a red beam crosses a green beam, the overlap behaves like **yellow**: it is harmless off the beat and dangerous on it. White light (all three channels) is neutral (`LightRules.Evaluate`).

### 4. Newton's prism (dispersion)
Glass has a refractive index that depends on wavelength, so short wavelengths (blue) bend more than long ones (red). This is how Newton split white light into a spectrum (1666).
**In game:** a white beam entering a prism leaves as separate red, green and blue beams, with blue deviated most (`LightMath.PrismDeviation`). Puzzles use this to pull the blue component out of harmless white light.

### 5. Law of reflection: `r = d − 2(d·n)n`
The angle of incidence equals the angle of reflection.
**In game:** mirrors bounce beams. A Pulse rotates a mirror so the light lands somewhere new (`LightMath.Reflect`).

### 6. Fresnel effect (Schlick's approximation): `F = F₀ + (1 − F₀)(1 − cos θ)⁵`
Surfaces reflect more light at grazing angles.
**In game:** Umbra's thin glowing rim. The silhouette is pure black, and the rim brightens where the surface turns away from the camera (`Umbra` shader, `LightMath.Schlick`).

### 7. Standing waves & Chladni figures
Two waves travelling in opposite directions make a standing wave, with nodes that never move. On a vibrating plate, sand collects on the nodal lines (Ernst Chladni, 1787). For a square plate the pattern is roughly `cos(nπx)cos(mπy) − cos(mπx)cos(nπy) = 0`.
**In game:** the Resonance Shield's surface shows animated Chladni nodal patterns that step through modes (n, m).

### 8. Harmonic series & just intonation
A vibrating string sounds at f, 2f, 3f, 4f… Just intonation builds a scale from these simple ratios: 1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8.
**In game:** every note is in D, built from these ratios (`Harmony`). Echo Path plays the fifth (3/2), the Shield holds the tonic, and Pulse rings the major third (5/4). In the finale Umbra sings the harmonic series 1:2:3:4:5:6.

### 9. Finite wave speed: `λ = v / f`
Waves take time to travel. Wavelength is speed divided by frequency.
**In game:** Pulse is an expanding ring that reaches distant objects later (`d / v` seconds). The Echo Path bridge's ripple wavelength comes from its note's frequency (`Harmony.Wavelength`).

### 10. Verlet integration: `x(t+Δt) = 2x(t) − x(t−Δt) + a·Δt²`
A stable, simple way to simulate ropes and cloth using positions only, plus distance constraints.
**In game:** Umbra's scarf is a Verlet chain that trails behind them.

### 11. The beat
Yellow lights and the music share one 100 BPM clock, locked to the audio DSP clock (`BeatClock`), so the visuals and the sound never drift apart.

*(Screenshots: see `Screenshots/`.)*
