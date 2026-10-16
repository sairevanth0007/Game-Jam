import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lvgen2 import L, LV

ONLY = set(sys.argv[2:])


def want(n):
    return not ONLY or n in ONLY


def hdr(name, intro, fog, mood, drone):
    return {'name': name, 'intro': intro, 'fog': fog, 'mood': mood, 'drone': drone}


# ======================================================================= 01 First Light
def first_light():
    l = L(26, hdr('First Light', 'Umbra wakes in the dark. Only shadows are safe.', 0.03, 'blue', 1))
    l.import_level('01_first_light.txt')
    # --- The Hollow: drop into a cave, thorn rhythm, ledges, dash.
    l.flat(4)
    l.tut('The hollow runs deep. Drop down.', dx=0)
    l.down(l.g - 3, 6)
    l.cp(-3)
    l.flat(4)
    l.tut('Thorns in rhythm: jump, land, jump.', dx=0)
    for _ in range(3):
        l.gap(2, after=3)
    l.flat(3)
    l.up(2, 5)
    l.gap(4, after=4)
    l.orb(2, 4)
    l.down(2, 6)
    l.cp(-3)
    l.flat(3)
    l.tut('Climb the hanging ledges.', dx=0)
    l.ledges(3, rise=2, run=4, gapw=2)
    l.flat(6)
    l.orb(3, 4)
    l.cp(-4)
    l.tut('A long fall. {Dash} across before it.', dx=-2)
    l.gap(6, after=4)
    l.down(2, 6)
    l.up(2, 4)
    l.gap(3, after=3)
    l.up(2, 4)
    l.flat(6)
    l.finish()
    l.write('01_first_light.txt')


# ======================================================================= 02 Echo Path
def echo_path():
    l = L(26, hdr('Echo Path', 'Sound can become solid. Sing across the void.', 0.035, 'violet', 2))
    l.import_level('02_echo_path.txt')
    l.flat(4)
    l.down(l.g - 3, 6) if l.g > 3 else l.flat(6)
    l.cp(-3)
    l.flat(4)
    l.tut('Wider voids need two songs. Sing again from your own bridge.', dx=-2)
    l.echo_gap(11, after=6)
    l.orb(-3, 4)
    l.cp(-3)
    # echo then hop up onto a higher shore
    l.tut('Sing, then leap from the end of your bridge.', dx=0)
    l.flat(3)
    l.bot(l.x - 1.0, 'echo')
    l.bot(l.x + 5.4, 'jump', t=0.45)
    l.flat(8, 'thorns')
    l.g += 2
    l.flat(6)
    # high ledge -> sing out and drop to a lower shore
    l.flat(2)
    l.bot(l.x - 1.0, 'echo')
    l.flat(6, 'thorns')
    l.g -= 2
    l.flat(6)
    l.cp(-3)
    # the long crossing: three bridges
    l.tut('The great void. Keep singing.', dx=0)
    l.flat(4)
    l.echo_gap(16, after=6)
    l.orb(-8, 5)
    l.flat(4)
    l.finish()
    l.write('02_echo_path.txt')


# ======================================================================= 03 Echo Canyon (new)
def echo_canyon():
    l = L(26, hdr('Echo Canyon', 'A canyon of frozen sound. Some paths only answer a Pulse.', 0.035, 'violet', 3))
    l.start(8)
    l.tut('Combine them: sing a bridge, then {Dash} off its end.', dx=1)
    l.flat(6)
    # echo + jumpdash: bridge 7 then 5 more
    l.bot(l.x - 1.0, 'echo')
    l.bot(l.x + 5.4, 'jumpdash', t=0.22)
    l.flat(11, 'thorns')
    l.flat(6)
    l.cp(-3)
    l.tut('{Pulse} sends a wave. It reveals paths of frozen sound.', dx=0)
    l.flat(4)
    l.bot(l.x - 0.8, 'pulse')
    l.bot(l.x - 0.7, 'wait', t=0.4)
    for i in range(8):
        l.set(l.x + i, l.g - 1, 'h')
        l.column(l.x + i, 0, 'thorns')
    l.x += 8
    l.flat(6)
    l.orb(2, 4)
    l.cp(-3)
    # hidden stairs up
    l.tut('The wave travels outward. Watch the ghosts appear.', dx=0)
    l.flat(4)
    l.bot(l.x - 1.5, 'pulse')
    for step in range(3):
        l.bot(l.x - 0.4, 'jump', t=0.45)
        l.column(l.x, 0, 'thorns')
        l.x += 1
        l.g += 2
        if step < 2:
            l.bot(l.x + 1.0, 'pulse')
        for i in range(5):
            l.set(l.x + i, l.g - 1, 'h')
            l.column(l.x + i, 0, 'thorns')
        l.x += 5
    l.bot(l.x - 0.6, 'jump', t=0.45)
    l.flat(2, 'thorns')
    l.flat(6)
    l.cp(-3)
    l.orb(3, 4)
    # canyon descent: bridges from high cliffs
    l.tut('Sing from the cliff edge and walk the air.', dx=0)
    l.flat(2)
    l.echo_gap(6, after=3)
    l.down(3, 3)
    l.echo_gap(10, after=5)
    l.g -= 3 if l.g >= 6 else 0
    l.flat(6)
    l.cp(-3)
    # relay: bridge, hop to a pillar, bridge again, dash to the far shore
    l.tut('Relay: bridge, pillar, bridge.', dx=0)
    l.flat(3)
    l.echo_gap(6, after=3)
    l.up(2, 3)
    l.bot(l.x - 1.0, 'echo')
    l.flat(6, 'thorns')
    l.g -= 2
    l.flat(3)
    l.gap(6, after=5)
    l.orb(-2, 5)
    l.cp(-3)
    # finale of the canyon: pulse + echo + dash
    l.tut('Everything at once: Pulse, sing, dash.', dx=0)
    l.flat(3)
    l.bot(l.x - 1.0, 'pulse')
    l.bot(l.x - 0.9, 'wait', t=0.4)
    for i in range(5):
        l.set(l.x + i, l.g - 1, 'h')
        l.column(l.x + i, 0, 'thorns')
    l.x += 5
    l.bot(l.x - 1.0, 'echo')
    l.bot(l.x + 5.4, 'jumpdash', t=0.22)
    l.flat(11, 'thorns')
    l.flat(6)
    l.orb(-3, 5)
    l.finish()
    l.write('03_echo_canyon.txt')


# ======================================================================= 04 Red Light
def red_light():
    l = L(22, hdr('Red Light', 'Red light burns. Your song can shield you.', 0.04, 'red', 4))
    l.import_level('03_red_light.txt')
    l.ceil = 13
    l.flat(4)
    l.cp(-3)
    l.tut('Sweeping light. Shield as it passes.', dx=0)
    l.flat(4)
    l.ent('beam', l.x + 3.5, 12.9, color='red', dir=-90, range=11, intensity=5, width=0.9, sweep=40, speed=0.9)
    l.bot(l.x - 1.5, 'shield', t=2.2)
    l.flat(8)
    l.ent('lamp', l.x + 1.5, 9, color='green', radius=4, intensity=1.2)
    l.bot(l.x + 1.0, 'wait', t=1.8)
    l.flat(5)
    l.tut('The burning hall: soft red light. Keep moving, heal after.', dx=0)
    l.flat(2)
    for i in range(4):
        l.ent('beam', l.x + 2.5 + i * 3, 12.9, color='red', dir=-90, range=11, intensity=0.5, width=1.2)
    l.flat(14)
    l.ent('lamp', l.x + 2.5, 9, color='green', radius=4, intensity=1.3)
    l.bot(l.x + 2.0, 'wait', t=2.5)
    l.flat(6)
    l.cp(-3)
    l.tut('Climb under fire. Shield on the steps.', dx=0)
    l.flat(3)
    l.ent('beam', l.x + 1.5, 12.9, color='red', dir=-90, range=11, intensity=6, width=0.9)
    l.bot(l.x - 2.0, 'shield', t=1.2)
    l.up(2, 4)
    l.ent('beam', l.x + 1.5, 12.9, color='red', dir=-90, range=11, intensity=6, width=0.9)
    l.bot(l.x - 2.0, 'shield', t=1.2)
    l.up(2, 5)
    l.orb(2, 3)
    l.down(2, 4)
    l.down(2, 6)
    l.ent('beam', l.x + 2.0, 3.45, color='red', dir=180, range=3, intensity=2, width=0.4)
    l.bot(l.x - 2.2, 'jump', t=0.5)
    l.flat(4)
    l.flat(4)
    l.cp(-3)
    l.ent('lamp', l.x - 1.5, 9, color='green', radius=4, intensity=1.2)
    l.bot(l.x - 2.0, 'wait', t=2.5)
    l.tut('Three wardens of red. One long breath of song.', dx=0)
    l.flat(3)
    for i in range(3):
        l.ent('beam', l.x + 1.5 + i * 2.5, 12.9, color='red', dir=-90, range=11, intensity=6, width=0.9, sweep=15, speed=1.2)
    l.bot(l.x - 1.6, 'shield', t=2.2)
    l.flat(9)
    l.flat(4)
    l.finish()
    l.write('04_red_light.txt')


# ======================================================================= 05 Blue Ascent
def blue_ascent():
    l = L(30, hdr('Blue Ascent', 'Blue light makes you light as a song.', 0.035, 'green', 5))
    l.import_level('04_blue_ascent.txt')
    # Exit area of the original is at g=7 (ground) – continue.
    l.flat(4)
    l.cp(-2)
    l.down(l.g - 3, 8) if l.g > 3 else l.flat(8)
    l.tut('A tower. Let the blue column carry you.', dx=0)
    l.flat(3)
    l.ent('beam', l.x - 0.5, 3.1, color='blue', dir=90, range=14, intensity=2.5, width=2.2)
    l.bot(l.x - 1.6, 'jump', t=1.2)
    l.g += 7
    l.flat(6)
    l.ent('lamp', l.x - 3, l.g + 4, color='green', radius=4, intensity=1.3)
    l.bot(l.x - 3.5, 'wait', t=1.0)
    l.cp(-3)
    l.orb(-1, 6)
    l.tut('Glide over the chasm inside the blue glow.', dx=-1)
    l.ent('lamp', l.x + 4.5, l.g + 5, color='blue', radius=8, intensity=2.5)
    l.gap(7, after=6)
    l.down(3, 5)
    l.tut('Blue steps. Each glow lifts you higher.', dx=0)
    for i in range(3):
        l.ent('lamp', l.x + 1, l.g + 3, color='blue', radius=6, intensity=3)
        l.bot(l.x - 1.2, 'jump', t=1.0)
        l.g += 4
        l.flat(5)
    l.ent('lamp', l.x - 2, l.g + 4, color='green', radius=4, intensity=1.3)
    l.bot(l.x - 2.5, 'wait', t=1.0)
    l.orb(0, 5)
    l.cp(-3)
    l.flat(4)
    l.down(4, 5)
    l.down(4, 5)
    l.gap(4, after=4)
    l.down(l.g - 3, 6)
    l.flat(4)
    l.finish()
    l.write('05_blue_ascent.txt')


# ======================================================================= 06 Crystal Choir (new)
def crystal_choir():
    l = L(24, hdr('Crystal Choir', 'Crystals hold the light. A Pulse can break their song.', 0.04, 'blue', 6))
    l.start(8)
    l.tut('Light crystals block the way. {Pulse} shatters them.', dx=1)
    l.flat(6)
    l.ent('crystal', l.x + 0.5, l.g, h=3)
    l.bot(l.x - 2.5, 'pulse')
    l.bot(l.x - 2.4, 'wait', t=0.5)
    l.flat(6)
    l.cp(-3)
    # crystal roof: crystals shield from red until broken
    l.tut('Crystals also block light. Break only what you must.', dx=0)
    l.flat(4)
    l.ent('beam', l.x + 3.5, 12.9, color='red', dir=-90, range=11, intensity=6, width=0.9)
    l.ent('crystal', l.x + 3.5, 7, h=1)
    l.flat(8)
    l.ent('crystal', l.x + 0.5, l.g, h=3)
    l.bot(l.x - 2.5, 'pulse')
    l.bot(l.x - 2.4, 'wait', t=0.5)
    l.flat(6)
    l.cp(-3)
    l.tut('Echo moths hunt any song. Sing, then shield.', dx=0)
    l.flat(4)
    l.ent('moth', l.x + 6.5, l.g + 5)
    l.ent('lamp', l.x + 1.5, l.g + 6, color='green', radius=4, intensity=1.2)
    l.bot(l.x + 0.5, 'wait', t=1.0)
    l.flat(4)
    l.ent('crystal', l.x + 0.5, l.g, h=3)
    l.bot(l.x - 2.5, 'pulse')
    l.bot(l.x - 2.4, 'shield', t=3.0)
    l.flat(10)
    l.orb(-4, 5)
    l.cp(-3)
    # hidden platforms + crystals over a pit
    l.tut('Pulse once: reveal the path and break the crystal.', dx=0)
    l.flat(3)
    l.bot(l.x - 1.0, 'pulse')
    l.bot(l.x - 0.9, 'wait', t=0.4)
    for i in range(6):
        l.set(l.x + i, l.g - 1, 'h')
        l.column(l.x + i, 0, 'thorns')
    l.x += 6
    l.ent('crystal', l.x + 0.5, l.g, h=3)
    l.flat(6)
    l.cp(-2)
    l.tut('A choir of crystals. Climb.', dx=0)
    l.flat(3)
    l.up(2, 4)
    l.ent('crystal', l.x + 0.5, l.g, h=2)
    l.bot(l.x - 2.5, 'pulse')
    l.bot(l.x - 2.4, 'wait', t=0.5)
    l.flat(3)
    l.up(2, 4)
    l.orb(1, 4)
    l.ent('lamp', l.x - 2, l.g + 4, color='green', radius=4, intensity=1.2)
    l.bot(l.x - 2.5, 'wait', t=1.2)
    l.echo_gap(10, after=5)
    l.down(2, 4)
    l.down(2, 6)
    l.cp(-3)
    # The held breath: a crystal holds back a red beam; the Pulse that reveals your path frees it.
    l.tut('A crystal holds back the red. Your Pulse will free it: shield as you cross.', dx=0)
    l.flat(4)
    l.ent('beam', l.x + 8.5, l.g + 10, color='red', dir=-90, range=12, intensity=8, width=1.0)
    l.ent('crystal', l.x + 8.5, l.g + 4, h=1)
    l.bot(l.x + 2.0, 'pulse')
    l.bot(l.x + 2.1, 'shield', t=2.2)
    l.flat(3)
    for i in range(5):
        l.set(l.x + i, l.g - 1, 'h')
        l.column(l.x + i, 0, 'thorns')
    l.x += 5
    l.flat(6)
    l.ent('lamp', l.x - 3, l.g + 5, color='green', radius=4, intensity=1.3)
    l.bot(l.x - 3.5, 'wait', t=1.5)
    l.cp(-3)
    l.orb(-1, 5)
    l.tut('Last song: the choir sings with you.', dx=0)
    l.flat(3)
    l.bot(l.x - 1.0, 'pulse')
    l.bot(l.x + 5.0, 'pulse')
    for i in range(3):
        l.ent('crystal', l.x + 3.5 + i * 4, l.g, h=2)
    l.flat(14)
    l.finish()
    l.write('06_crystal_choir.txt')


# ======================================================================= 07 Beat Lasers
def beat_lasers():
    l = L(22, hdr('Beat Lasers', 'Yellow light fires on the beat. Move between the pulses.', 0.04, 'gold', 7))
    l.import_level('05_beat_lasers.txt')
    l.ceil = 13
    l.flat(4)
    l.cp(-3)
    l.tut('Faster now: two lasers, one beat apart.', dx=0)
    l.flat(4)
    for i in range(3):
        l.ent('beam', l.x + 2.5, 12.9, color='yellow', dir=-90, range=11, intensity=1.5, width=0.6, beat=1)
        l.bot(l.x + 0.9, 'waitbeat')
        l.flat(4)
    l.flat(3)
    l.tut('Jump the low laser between beats.', dx=0)
    l.flat(3)
    l.ent('beam', l.x + 2.0, 3.45, color='yellow', dir=180, range=3, intensity=1.5, width=0.4, beat=1)
    l.bot(l.x - 2.2, 'jump', t=0.5)
    l.flat(6)
    l.cp(-3)
    l.tut('Steps of light: climb on the rhythm.', dx=0)
    l.flat(3)
    for i in range(3):
        l.up(2, 7)
        l.ent('beam', l.x - 2.5, 12.9, color='yellow', dir=-90, range=11, intensity=1.5, width=0.6, beat=1)
        l.bot(l.x - 4.1, 'waitbeat')
    l.orb(1, 4)
    l.flat(2)
    l.down(2, 4)
    l.down(2, 4)
    l.down(2, 5)
    l.cp(-3)
    l.tut('The last corridor. Feel the beat.', dx=0)
    l.flat(4)
    for i in range(4):
        l.ent('beam', l.x + 2.5, 12.9, color='yellow', dir=-90, range=11, intensity=1.5, width=0.6, beat=1)
        l.bot(l.x + 0.9, 'waitbeat')
        l.flat(4)
    l.flat(6)
    l.finish()
    l.write('07_beat_lasers.txt')


# ======================================================================= 08 Prism and Mirror
def prism_mirror():
    l = L(24, hdr('Prism and Mirror', 'Light obeys laws. Bend it, split it, mix it.', 0.035, 'violet', 8))
    l.import_level('06_prism_mirror.txt')
    l.ceil = 21
    l.flat(4)
    l.cp(-3)
    l.tut('Two mirrors. {Pulse} turns both: angle in = angle out.', dx=0)
    l.flat(3)
    # red beam deflected by rotatable mirror over the path (same pattern as the start, twice)
    l.ent('beam', l.x + 3.5, 20.9, color='red', dir=-90, range=24, intensity=25, width=0.7)
    l.ent('mirror', l.x + 3.5, 9, angle=-67.5, rotatable=1)
    l.ent('beam', l.x + 9.5, 20.9, color='red', dir=-90, range=24, intensity=25, width=0.7)
    l.ent('mirror', l.x + 9.5, 9, angle=-67.5, rotatable=1)
    l.bot(l.x - 1.0, 'pulse')
    l.bot(l.x - 0.9, 'wait', t=0.9)
    l.flat(14)
    l.orb(-6, 6)
    l.ent('lamp', l.x - 2.5, 9, color='green', radius=4, intensity=1.2)
    l.cp(-3)
    l.tut('Pulse reveals; the wave also turns mirrors.', dx=0)
    l.flat(3)
    l.bot(l.x - 1.0, 'pulse')
    l.bot(l.x - 0.9, 'wait', t=0.4)
    for i in range(7):
        l.set(l.x + i, l.g - 1, 'h')
        l.column(l.x + i, 0, 'thorns')
    l.x += 7
    l.flat(6)
    l.finish()
    l.write('08_prism_mirror.txt')


# ======================================================================= 09 Sentinels
def sentinels():
    l = L(30, hdr('Sentinels', 'The lamp-wardens sweep the dark. Stun them with a Pulse.', 0.045, 'red', 9))
    l.import_level('07_sentinels.txt')
    l.flat(4)
    l.cp(-3)
    l.tut('Two wardens and a wisp. Pulse, then shield.', dx=0)
    l.flat(4)
    l.ent('sentinel', l.x + 6.5, l.g + 6, range=9, sweep=50, speed=0.9)
    l.ent('wisp', l.x + 14.5, l.g + 3)
    l.bot(l.x - 0.5, 'pulse')
    l.bot(l.x + 0.5, 'shield', t=3.0)
    l.flat(18)
    l.ent('lamp', l.x - 2.5, l.g + 6, color='green', radius=4, intensity=1.2)
    l.bot(l.x - 3.0, 'wait', t=1.5)
    l.cp(-3)
    l.tut('The final climb. Stun the warden first.', dx=0)
    l.flat(3)
    l.ent('sentinel', l.x + 5.5, l.g + 6, range=10, sweep=40, speed=0.7)
    l.bot(l.x - 1.0, 'pulse')
    l.up(2, 4)
    l.up(2, 4)
    l.bot(l.x - 2.5, 'shield', t=2.2)
    l.up(2, 4)
    l.orb(1, 4)
    l.up(2, 5)
    l.flat(4)
    l.finish()
    l.write('09_sentinels.txt')


# ======================================================================= 10 Harmony
def harmony():
    l = L(22, hdr('Harmony', 'Sing, Umbra. Let every light become music.', 0.03, 'dawn', 10))
    l.start(8)
    l.tut('The lights are listening...', dx=2)
    cols = ['red', 'green', 'blue', 'yellow', 'white', 'red', 'green', 'blue']
    for i, c in enumerate(cols):
        l.ent('lamp', l.x + 4.5 + i * 6, 10 + (i % 2), color=c, radius=5, intensity=0.4)
    l.flat(14)
    l.up(1, 10)
    l.up(1, 10)
    l.orb(4, 5)
    l.flat(10)
    l.tut('Every light you feared was a note waiting for a song.', dx=0)
    l.flat(6)
    l.ent('ending', l.x + 2.5, l.g + 1, time=11)
    l.bot(l.x + 2.6, 'wait', t=13)
    l.flat(10)
    l.finish(w=8)
    l.write('10_harmony.txt')


order = [
    ('01_first_light', first_light), ('02_echo_path', echo_path), ('03_echo_canyon', echo_canyon),
    ('04_red_light', red_light), ('05_blue_ascent', blue_ascent), ('06_crystal_choir', crystal_choir),
    ('07_beat_lasers', beat_lasers), ('08_prism_mirror', prism_mirror), ('09_sentinels', sentinels),
    ('10_harmony', harmony),
]
for name, fn in order:
    if want(name[:2]):
        fn()
open(os.path.join(LV, 'index.txt'), 'w', encoding='utf-8', newline='\n').write(
    '// Level order (resource names in Resources/Levels). Generated by lvgen2/levels.py\n' + '\n'.join(n for n, _ in order) + '\n')
