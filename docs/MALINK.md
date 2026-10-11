# MALink: drive a grandMA3 rig from LumenWave

MALink sends **OSC over UDP** to a grandMA3 console (or onPC) whenever something happens in the game.
Each event sends one command string, `Go+ Exec N`, to the OSC address `/<prefix>/cmd`.
MALink is **off by default** and safe to run offline: UDP is fire-and-forget and every send is caught.

## Event → executor map (defaults)

| Game event | Command | Suggested cue on the rig |
|---|---|---|
| Red light damage | `Go+ Exec 201` | red flash / strobe hit |
| Yellow (beat) damage | `Go+ Exec 202` | amber chase step |
| Green heal | `Go+ Exec 203` | soft green wash |
| Echo Path | `Go+ Exec 211` | beam fan sweep in facing direction |
| Resonance Shield | `Go+ Exec 212` | cyan ring / dome |
| Pulse | `Go+ Exec 213` | white ripple outward |
| Dash | `Go+ Exec 214` | quick blinder tick |
| Checkpoint | `Go+ Exec 221` | warm glow |
| Death | `Go+ Exec 222` | blackout + slow fade in |
| Level complete | `Go+ Exec 223` | rainbow bump |
| Win | `Go+ Exec 224` | full stage, all colours in harmony |
| Finale | `Go+ Exec 225` | slow build to white |

Repeated events are throttled (`throttle`, default 0.25 s per command).

## Configure the game
Use any one of these:
1. **Command line**: `LumenWave.exe -malink 192.168.1.50:8000:gma3` (ip:port:prefix). This also enables MALink.
2. **malink.json** next to `LumenWave.exe` (or in `%USERPROFILE%\AppData\LocalLow\LumenWave Team\LumenWave\`):
   ```json
   { "enabled": true, "ip": "192.168.1.50", "port": 8000, "prefix": "gma3", "showOverlay": true, "throttle": 0.25,
     "execRedDamage": 201, "execGreenHeal": 203, "execEcho": 211, "execShield": 212, "execPulse": 213,
     "execCheckpoint": 221, "execWin": 224 }
   ```
3. **In game**: press **F9** to open the debug overlay. It has enable/disable, a test button and *Save config*, and lists the last messages sent.

## Set up the grandMA3 (with the MA staff)
1. **Network**: put the console/onPC and the game PC on the same subnet, e.g. 192.168.1.x. Note the console's IP.
2. **Menu → In & Out → OSC**: add a line.
   - *Mode*: **Receive** (and *Receive Command* = Yes, so incoming `/cmd` strings run as commands).
   - *Port*: 8000 (must match the game).
   - *Prefix*: `gma3` (must match the game's `prefix`).
   - Enable the line and turn on **Enable Input** for OSC.
3. **Executors**: store cues/sequences on executors 201–225 to match the table, or change the numbers in `malink.json` to match your show.
4. **Test**: start the game with `-malink <console-ip>:8000:gma3`, press F9 and click *Test*. The console's command line should show `Go+ Exec 224`.
5. **Troubleshooting**: check the Windows firewall (outbound UDP is normally allowed), the IP, port and prefix. Run Wireshark with filter `udp.port == 8000`. The overlay shows the last socket error.

## Verified
- EditMode tests (`Tests/EditMode/MALinkTests.cs`) check the OSC encoding byte by byte (4-byte padding, big-endian int32) and the decode round-trip. They also start a **real UDP listener** on localhost and assert it receives `/gma3/cmd ,s "Go+ Exec 42"`, and confirm that sending to an unreachable host never throws.

## Lightkeeper mode: OSC lamp control (input)
Lightkeeper mode (title screen → LIGHTKEEPER) can take its four lamps from OSC, e.g. TouchOSC, a grandMA3 or any OSC app. It is off by default.
- **Enable**: `LumenWave.exe -lk-osc 9000` (UDP port to listen on), or set the PlayerPrefs int `lw.lk.oscport` to a value above 0.
- **Messages** (N = 1..4). Plain messages and simple `#bundle`s are accepted:
  | Address | Argument | Effect |
  |---|---|---|
  | `/lamp/N/x` | float | lamp position on the rail (world metres, clamped to the arena) |
  | `/lamp/N/angle` | float (deg) | beam aim. 0 = straight down, positive tilts toward the door (+x), range −75…+75 |
  | `/lamp/N/on` | int or float (0/1) | on/off. The same rules as the keyboard apply: max 2 lit, energy needed |
  | `/lamp/N/color` | int 0..4 or string | red, green, blue, yellow, white |
  | `/lamp/N/select` | none | selects the lamp for the keyboard |
- Packets are received on a background thread and applied on the main thread (`Lightkeeper/LightkeeperOsc.cs`, decoder `Osc.TryDecode`).
- **Mirror out**: when MALink is enabled (`-malink ip:port:prefix`), every lamp change is also sent to the MALink ip:port with the same addresses (≤10 Hz per lamp). On/off additionally sends `Go+ Exec 23N` / `Off Exec 23N`, so executors 231–234 can follow the lamps on a real rig.
- Verified by the PlayMode test `LightkeeperTests.OscMessage_MovesLamp`, which sends real UDP packets to the listener.
