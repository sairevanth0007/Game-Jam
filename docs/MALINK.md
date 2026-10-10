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
