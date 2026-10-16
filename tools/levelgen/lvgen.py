"""Authoring helper for LumenWave levels: writes ASCII level files (the files are the source of truth)."""
import os, sys

OUT = sys.argv[1]


class L:
    def __init__(self, name, w, h, **header):
        self.name, self.w, self.h = name, w, h
        self.g = [[' '] * w for _ in range(h)]  # g[y][x], y=0 bottom
        self.header = dict(name=name, **header)
        self.ents = []

    def fill(self, x0, x1, y0, y1, ch='#'):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if 0 <= x < self.w and 0 <= y < self.h:
                    self.g[y][x] = ch

    def ground(self, x0, x1, top):
        """Solid from y=0 up to top-1 (walkable surface at y=top)."""
        self.fill(x0, x1, 0, top - 1)

    def ledge(self, x0, x1, y):
        """Thin ledge in row y (surface at y+1)."""
        self.fill(x0, x1, y, y, '=')

    def put(self, x, y, ch):
        self.g[y][x] = ch

    def e(self, line):
        self.ents.append(line)

    def tut(self, x, y, text, r=2.5):
        self.e(f"tutorial {x} {y} text={text.replace(' ', '_')} radius={r}")

    def bot(self, x, y, do, **kw):
        extra = ''.join(f" {k}={v}" for k, v in kw.items())
        self.e(f"bot {x} {y} do={do}{extra}")

    def save(self, fname):
        lines = []
        for k, v in self.header.items():
            lines.append(f"{k}: {v}")
        lines.append('---')
        for y in range(self.h - 1, -1, -1):
            lines.append(''.join(self.g[y]).rstrip())
        lines.append('---')
        lines += self.ents
        with open(os.path.join(OUT, fname + '.txt'), 'w', newline='\n') as f:
            f.write('\n'.join(lines) + '\n')
        return fname


def walls(l, top):
    l.fill(0, 1, 0, l.h - 1)
    l.fill(l.w - 2, l.w - 1, 0, l.h - 1)


names = []

# ------------------------------------------------------------------ 1. First Light: move / jump / dash
l = L("First Light", 112, 16, intro="Umbra wakes in the dark. Only shadows are safe.", fog="0.03", drone="1")
walls(l, 3)
l.ground(0, 20, 3)
l.ground(14, 20, 4)             # first step
l.ground(24, 40, 3)
l.ground(30, 40, 5)             # 2-high wall
l.ground(47, 69, 5)
l.ground(70, 111, 3)
l.ground(84, 89, 4); l.ground(88, 93, 5); l.ground(92, 97, 6)  # stairs
l.ground(98, 111, 6)
l.put(4, 3, 'P')
l.tut(5.5, 3, "{Move} to move", 3)
l.tut(11.5, 3, "Press {Jump} to jump", 2.5)
l.bot(12.6, 3, "jump")
l.bot(19.4, 4, "jump")
l.tut(26.5, 3, "Hold {Jump} longer to jump higher", 2.5)
l.bot(28.4, 3, "jump", t=0.5)
l.put(36, 5, 'C')
l.tut(38.5, 5, "Jump, then {Dash} in mid-air to cross wide gaps", 2.5)
l.bot(40.4, 5, "jumpdash", t=0.36)
# thorns
for x in (57, 58, 59):
    l.put(x, 5, '^')
l.tut(53.5, 5, "Shadow thorns kill. Jump over them.", 2.5)
l.bot(55.3, 5, "jump")
l.e("orb 66.5 8.2")
l.bot(65.6, 5, "jump", t=0.5)
l.put(74, 3, 'C')
# second thorns patch on the lower ground + dash practice over a pit
l.fill(77, 81, 0, 2, ' ')       # small pit
l.bot(76.4, 3, "jumpdash", t=0.36)
l.bot(83.4, 3, "jump"); l.bot(87.4, 4, "jump"); l.bot(91.4, 5, "jump")
l.e("orb 95.5 10")
l.put(106, 6, 'E')
l.e("lamp 36.5 9 color=white radius=6 intensity=0.6")
l.e("lamp 106.5 11 color=white radius=7 intensity=0.8")
names.append(l.save("01_first_light"))

# ------------------------------------------------------------------ 2. Echo Path
l = L("Echo Path", 120, 16, intro="Sound can become solid. Sing across the void.", fog="0.035", drone="2")
walls(l, 3)
l.ground(0, 15, 3)
l.ground(26, 39, 3)
l.ground(54, 70, 3)
l.ground(81, 92, 5)
l.ground(93, 119, 5)
l.put(4, 3, 'P')
l.tut(9.5, 3, "Press {Echo} at the edge: your song becomes a bridge", 3)
l.bot(15.0, 3, "echo")
l.bot(21.6, 3, "jump")
l.put(30, 3, 'C')
l.tut(35.5, 3, "Bridges fade. Sing again from a bridge to go further", 3)
l.bot(39.2, 3, "echo")
l.bot(45.8, 3, "echo")
l.e("orb 47.5 7.2")
l.bot(52.6, 3, "jump")
l.put(58, 3, 'C')
l.tut(64.5, 3, "Resonance refills in darkness. Watch the ring.", 3)
l.bot(70.2, 3, "echo")
l.bot(76.4, 3, "jump", t=0.5)
l.put(88, 5, 'C')
# final: gap with thorn floor and a ledge-high exit
l.fill(97, 107, 0, 4, ' ')
l.ground(97, 107, 1)
for x in range(97, 108):
    l.put(x, 1, '^')
l.bot(96.2, 5, "echo")
l.bot(102.9, 5, "jump")
l.e("orb 110.5 9")
l.put(114, 5, 'E')
l.e("lamp 114.5 10 color=white radius=6 intensity=0.6")
names.append(l.save("02_echo_path"))

# ------------------------------------------------------------------ 3. Red Light
l = L("Red Light", 116, 16, intro="Red light burns. Your song can shield you.", fog="0.04", drone="3")
walls(l, 3)
l.ground(0, 115, 3)
l.fill(2, 113, 13, 15)   # ceiling
l.put(4, 3, 'P')
l.tut(9.5, 3, "Red light burns", 3)
l.e("beam 16.5 12.9 color=red dir=-90 range=11 intensity=0.6 width=0.8")
l.tut(22.5, 3, "Hold {Shield} to block light with a standing wave", 3)
l.bot(25.4, 3, "shield", t=1.4)
l.e("beam 28.5 12.9 color=red dir=-90 range=11 intensity=10 width=1.0")
l.put(34, 3, 'C')
l.e("lamp 34.5 7 color=green radius=4 intensity=1.2")
l.tut(36.5, 3, "Green light heals and refills resonance", 2.5)
l.bot(35.0, 3, "wait", t=1.6)
# horizontal low beam: jump over
l.e("beam 47.0 3.45 color=red dir=0 range=3.5 intensity=2 width=0.4")
l.tut(43.5, 3, "...or simply jump over low beams", 2.5)
l.bot(45.5, 3, "jump", t=0.5)
l.put(66, 3, 'C')
# sweeping beam + double beams to shield through
l.e("beam 76.5 12.9 color=red dir=-90 range=11 intensity=5 width=0.8 sweep=35 speed=0.8")
l.bot(71.0, 3, "shield", t=2.4)
l.e("beam 86.5 12.9 color=red dir=-90 range=11 intensity=5 width=0.9")
l.e("beam 90.5 12.9 color=red dir=-90 range=11 intensity=5 width=0.9")
l.bot(83.6, 3, "shield", t=1.9)
l.e("lamp 97.5 7 color=green radius=4 intensity=1.2")
l.e("orb 99.5 9.5")
l.bot(98.6, 3, "jump", t=0.5)
l.put(110, 3, 'E')
names.append(l.save("03_red_light"))

# ------------------------------------------------------------------ 4. Blue Ascent
l = L("Blue Ascent", 104, 26, intro="Blue light makes you light as a song.", fog="0.035", drone="4")
walls(l, 3)
l.ground(0, 103, 3)
l.put(4, 3, 'P')
l.tut(9.5, 3, "Blue light lowers gravity. Jump inside it.", 3)
l.e("beam 17.5 3.1 color=blue dir=90 range=12 intensity=2.5 width=2.2")
l.ground(19, 30, 9)      # 6-high wall
l.bot(16.4, 3, "jump", t=1.2)
l.put(26, 9, 'C')
# red hurt then heal
l.e("beam 33.5 22 color=red dir=-90 range=15 intensity=0.7 width=0.8")
l.ground(31, 44, 9)
l.e("lamp 40.5 12 color=green radius=4 intensity=1.3")
l.tut(38.5, 9, "Rest in green light to heal", 3)
l.bot(40.0, 9, "wait", t=1.2)
# zigzag climb with a big blue lamp
l.e("lamp 44.5 12.5 color=blue radius=7 intensity=3")
l.e("lamp 56.5 17.5 color=blue radius=7 intensity=3")
l.ground(45, 56, 14)
l.ground(57, 74, 19)
l.tut(42.5, 9, "Climb inside the blue glow", 2.5)
l.bot(43.4, 9, "jump", t=1.0)
l.bot(55.4, 14, "jump", t=1.0)
l.e("orb 50.5 20")
l.put(64, 19, 'C')
# long blue glide over a pit
l.ground(75, 103, 3)
l.fill(75, 84, 0, 2, ' ')
for x in range(75, 85):
    l.put(x, 0, '^')
l.e("lamp 80.5 14 color=blue radius=8 intensity=2.5")
l.ground(85, 103, 6)
l.tut(70.5, 19, "Leap and glide. {Dash} if you fall short", 3)
l.bot(73.6, 19, "jump", t=0.8)
l.put(98, 6, 'E')
names.append(l.save("04_blue_ascent"))

# ------------------------------------------------------------------ 5. Beat Lasers
l = L("Beat Lasers", 110, 16, intro="Yellow light fires on the beat. Move between the pulses.", fog="0.04", drone="5")
walls(l, 3)
l.ground(0, 109, 3)
l.fill(2, 107, 13, 15)
l.put(4, 3, 'P')
l.tut(9.5, 3, "Yellow lasers pulse on the beat (100 BPM). Cross after a flash.", 3)
l.e("beam 18.5 12.9 color=yellow dir=-90 range=11 intensity=1.5 width=0.6 beat=1")
l.bot(17.0, 3, "waitbeat")
l.put(24, 3, 'C')
# rhythm run: lasers 4 apart = one beat at full speed
for i, x in enumerate((32.5, 36.5, 40.5, 44.5)):
    l.e(f"beam {x} 12.9 color=yellow dir=-90 range=11 intensity=1.5 width=0.6 beat=1")
l.tut(27.5, 3, "Run on the rhythm", 3)
l.bot(30.9, 3, "waitbeat")
l.bot(34.9, 3, "waitbeat")
l.bot(38.9, 3, "waitbeat")
l.bot(42.9, 3, "waitbeat")
l.put(50, 3, 'C')
# jump + laser over a pit
l.fill(56, 59, 0, 2, ' ')
l.e("beam 58.0 12.9 color=yellow dir=-90 range=11 intensity=1.5 width=0.6 beat=1")
l.bot(54.4, 3, "waitbeat")
l.bot(55.0, 3, "jump")
l.e("orb 58 8.5")
# mixing preview: red + green overlap = yellow
l.put(64, 3, 'C')
l.tut(67.5, 3, "Red + green light mix into yellow: safe between beats", 3.5)
l.e("beam 74.5 12.9 color=red dir=-90 range=11 intensity=1.2 width=1.2")
l.e("beam 74.5 3.1 color=green dir=90 range=11 intensity=1.2 width=1.2")
l.bot(72.6, 3, "waitbeat")
# horizontal beat laser at head height under a low ceiling + two vertical
l.e("beam 84.5 12.9 color=yellow dir=-90 range=11 intensity=1.5 width=0.6 beat=1")
l.e("beam 88.5 12.9 color=yellow dir=-90 range=11 intensity=1.5 width=0.6 beat=1")
l.bot(82.9, 3, "waitbeat")
l.bot(86.9, 3, "waitbeat")
l.put(104, 3, 'E')
names.append(l.save("05_beat_lasers"))

# ------------------------------------------------------------------ 6. Prism & Mirror
l = L("Prism and Mirror", 120, 20, intro="Light obeys laws. Bend it, split it, mix it.", fog="0.035", drone="6")
walls(l, 3)
l.ground(0, 119, 3)
l.fill(2, 117, 17, 19)
l.put(4, 3, 'P')
# Puzzle 1: a lethal red beam reflects off a mirror across the path. Pulse rotates the mirror upward.
l.tut(8.5, 3, "Mirrors reflect: angle in = angle out. {Pulse} turns them.", 3)
l.e("beam 14.5 16.9 color=red dir=-90 range=24 intensity=25 width=0.7")
l.e("mirror 14.5 9 angle=-67.5 rotatable=1")
l.bot(10.0, 3, "pulse")
l.bot(10.1, 3, "wait", t=0.9)
l.put(28, 3, 'C')
# Puzzle 2: prism splits white light; only the blue part lifts you up the shaft.
l.tut(31.5, 3, "White light is harmless. A prism splits it into red, green and blue (Newton)", 3.5)
l.e("beam 26.5 8 color=white dir=0 range=40 intensity=16 width=1.6")
l.e("prism 33.5 8 angle=0")
l.e("mirror 42.0 10.76 angle=144")
l.ground(43, 54, 9)
l.tut(37.5, 3, "Blue bends the most. A mirror drops it here: ride it up", 3)
l.bot(41.6, 3, "jump", t=1.6)
l.e("orb 41.5 15.5")
l.put(50, 9, 'C')
# Puzzle 3: crystal blocks a green beam; Pulse shatters it to make the red+green overlap (yellow window)
l.ground(55, 119, 3)
l.e("beam 66.5 16.9 color=red dir=-90 range=14 intensity=8 width=1.4")
l.e("beam 58.5 4.5 color=green dir=0 range=14 intensity=6 width=1.4")
l.e("crystal 61.5 3 h=3")
l.tut(57.5, 3, "Shatter the crystal with {Pulse}: red + green = yellow", 3.5)
l.bot(56.0, 3, "pulse")
l.bot(56.1, 3, "wait", t=0.6)
l.bot(64.6, 3, "waitbeat")
l.put(72, 3, 'C')
# Puzzle 4: hidden platforms over a thorn pit revealed by Pulse
l.fill(78, 93, 0, 2, ' ')
for x in range(78, 94):
    l.put(x, 0, '^')
for x in (81, 82, 83, 86, 87, 88, 91, 92, 93):
    l.put(x, 2, 'h')
l.tut(75.5, 3, "{Pulse} reveals paths of frozen sound", 3)
l.bot(76.0, 3, "pulse")
l.bot(76.1, 3, "wait", t=0.5)
l.bot(77.6, 3, "jump")
l.bot(82.0, 3, "pulse")
l.bot(83.6, 3, "jump")
l.bot(86.8, 3, "pulse")
l.bot(88.6, 3, "jump")
l.e("orb 100.5 7")
l.put(112, 3, 'E')
names.append(l.save("06_prism_mirror"))

# ------------------------------------------------------------------ 7. Sentinels
l = L("Sentinels", 128, 28, intro="The lamp-wardens sweep the dark. Stun them with a Pulse.", fog="0.045", drone="7")
walls(l, 3)
l.ground(0, 60, 3)
l.put(4, 3, 'P')
l.tut(9.5, 3, "Sentinels sweep red light. {Pulse} stuns them.", 3)
l.e("sentinel 20.5 9 range=9 sweep=50 speed=0.8")
l.bot(13.5, 3, "pulse")
l.bot(14.5, 3, "shield", t=2.6)
l.put(28, 3, 'C')
l.tut(31.5, 3, "Wisps chase light. Your {Shield} repels them.", 3)
l.e("wisp 44.5 6")
l.bot(36.0, 3, "shield", t=3.0)
l.e("sentinel 50.5 10 range=10 sweep=60 speed=1")
l.bot(44.5, 3, "pulse")
l.bot(45.0, 3, "shield", t=1.8)
l.put(56, 3, 'C')
# final climb
l.ground(61, 66, 5); l.ground(67, 72, 7); l.ground(73, 78, 9)
l.ledge(80, 84, 11); l.ground(86, 92, 14)
l.bot(59.4, 3, "jump"); l.bot(65.4, 5, "jump"); l.bot(71.4, 7, "jump")
l.bot(77.6, 9, "jump", t=0.5); l.bot(83.2, 12, "jump", t=0.5)
l.e("sentinel 96.5 22 range=12 sweep=40 speed=0.7")
l.put(90, 14, 'C')
l.ground(93, 127, 14)
l.e("wisp 104.5 18")
l.bot(92.0, 14, "pulse")
l.bot(92.6, 14, "shield", t=3.0)
l.e("orb 99.5 18")
l.put(118, 14, 'E')
names.append(l.save("07_sentinels"))

# ------------------------------------------------------------------ 8. Harmony (finale)
l = L("Harmony", 64, 18, intro="Sing, Umbra. Let every light become music.", fog="0.03", drone="8")
walls(l, 3)
l.ground(0, 63, 3)
l.ground(26, 63, 4)
l.put(4, 3, 'P')
for i, (x, c) in enumerate(((14.5, 'red'), (22.5, 'green'), (30.5, 'blue'), (38.5, 'yellow'))):
    l.e(f"lamp {x} 10 color={c} radius=5 intensity=0.5")
l.tut(10.5, 3, "The lights are listening...", 3)
l.bot(24.4, 3, "jump")
l.e("ending 44.5 4 time=7")
l.bot(44.6, 4, "wait", t=9)
l.e("orb 50.5 7")
l.put(56, 4, 'E')
names.append(l.save("08_harmony"))

with open(os.path.join(OUT, 'index.txt'), 'w', newline='\n') as f:
    f.write("// Level order (resource names in Resources/Levels)\n" + '\n'.join(names) + '\n')
print('\n'.join(names))
