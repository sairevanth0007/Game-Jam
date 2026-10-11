"""LumenWave level generator (third wave).
Cursor-based DSL: terrain ops advance a cursor x with a current ground height g and place bot markers
at the right spots. Existing, bot-verified levels are imported as the opening section.
Writes Assets/LumenWave/Resources/Levels/*.txt and index.txt.
"""
import os, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'
LV = os.path.join(ROOT, 'Assets', 'LumenWave', 'Resources', 'Levels')
ORIG = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'orig')


def fmt(v):
    if isinstance(v, float):
        s = ('%.2f' % v).rstrip('0').rstrip('.')
        return s
    return str(v)


class L:
    def __init__(self, H, header):
        self.H = H
        self.header = header
        self.cells = {}
        self.ents = []
        self.x = 0
        self.g = 3
        self.ceil = None   # y of ceiling bottom (ceiling fills ceil..H-1)
        self.left_wall()

    # ---------- primitives
    def set(self, x, y, ch):
        if 0 <= y < self.H:
            self.cells[(x, y)] = ch

    def rect(self, x0, y0, x1, y1, ch='#'):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                self.set(x, y, ch)

    def column(self, x, g, kind='solid'):
        if kind == 'solid':
            self.rect(x, 0, x, g - 1)
        elif kind == 'thorns':
            self.set(x, 0, '^')
        elif kind == 'void':
            pass
        if self.ceil is not None:
            self.rect(x, self.ceil, x, self.H - 1)

    def ent(self, typ, x, y, **kw):
        self.ents.append((typ, x, y, kw))

    def bot(self, x, do, y=None, **kw):
        kw['do'] = do
        self.ent('bot', x, self.g if y is None else y, **kw)

    def tut(self, text, dx=1.5, radius=3, y=None):
        self.ent('tutorial', self.x + dx, self.g if y is None else y, text=text.replace(' ', '_'), radius=radius)

    def left_wall(self):
        self.rect(0, 0, 1, self.H - 1)
        self.x = 2

    # ---------- terrain ops
    def flat(self, w, kind='solid'):
        for i in range(w):
            self.column(self.x + i, self.g, kind)
        self.x += w

    def start(self, w=6):
        self.flat(w)
        self.set(4, self.g, 'P')

    def cp(self, dx=0):
        self.set(self.x + dx, self.g, 'C')

    def orb(self, dx, dy):
        self.ent('orb', self.x + dx + 0.5, self.g + dy)

    def up(self, dy, w, hold=0.45, before=1.3):
        self.bot(self.x - before, 'jump', t=hold)
        self.g += dy
        self.flat(w)

    def down(self, dy, w):
        self.g -= dy
        self.flat(w)

    def gap(self, w, after=6, kind='thorns', before=0.6):
        if w <= 4:
            self.bot(self.x - before, 'jump', t=0.45)
        else:
            self.bot(self.x - 0.4, 'jumpdash', t=0.22)
        self.flat(w, kind)
        self.flat(after)

    def echo_gap(self, w, after=6, kind='thorns'):
        """Gap crossed with Echo Path bridges (one per 6 tiles)."""
        self.bot(self.x - 1.0, 'echo')
        if w > 6:
            self.bot(self.x + 5.0, 'echo')
        if w > 11:
            self.bot(self.x + 11.0, 'echo')
        self.flat(w, kind)
        self.flat(after)

    def ledges(self, n, rise=2, run=4, gapw=2, ch='='):
        """Floating ledges climbing up over a pit."""
        base = self.g
        for i in range(n):
            # gap before ledge
            self.bot(self.x - 0.6, 'jump', t=0.45)
            self.flat(gapw, 'thorns')
            self.g += rise
            for k in range(run):
                self.set(self.x + k, self.g - 1, ch)
                self.column(self.x + k, 0, 'thorns')
            self.x += run
        return base

    def wall_block(self, w, top):
        """Solid block to y=top (used to build towers)."""
        for i in range(w):
            self.rect(self.x + i, 0, self.x + i, top - 1)
            if self.ceil is not None:
                self.rect(self.x + i, self.ceil, self.x + i, self.H - 1)
        self.x += w

    def import_level(self, fname, cut_right=2, keep_entities=True, drop=('exit',)):
        text = open(os.path.join(ORIG, fname), encoding='utf-8').read().replace('\r', '')
        sec = 0
        grid = []
        ents = []
        for line in text.split('\n'):
            if line.strip().startswith('//'):
                continue
            if line.strip() == '---':
                sec += 1
                continue
            if sec == 1:
                grid.append(line.rstrip())
            elif sec == 2 and line.strip():
                ents.append(line.strip())
        while grid and not grid[0]:
            grid.pop(0)
        while grid and not grid[-1]:
            grid.pop()
        h = len(grid)
        w = max(len(r) for r in grid)
        width = w - cut_right
        has_ceiling = all(c == '#' for c in grid[0][:w])
        ox = self.x - 2   # imported block includes its own left wall at cols 0-1
        for row_i, row in enumerate(grid):
            y = h - 1 - row_i
            for cx in range(min(len(row), width)):
                ch = row[cx]
                if ch == 'E':
                    ch = ' '
                if ch != ' ':
                    self.set(ox + cx, y, ch)
        if has_ceiling:
            for cx in range(width):
                self.rect(ox + cx, h, ox + cx, self.H - 1)
        for e in ents:
            p = e.split()
            if p[0] in drop:
                continue
            kw = {}
            for a in p[3:]:
                k, _, v = a.partition('=')
                kw[k] = v
            self.ent(p[0], float(p[1]) + ox, float(p[2]), **kw)
        self.x = ox + width
        # ground height at the last column
        col = width - 1
        g = 0
        for y in range(h):
            r = grid[h - 1 - y]
            if col < len(r) and r[col] in '#':
                g = y + 1
            else:
                break
        self.g = g
        return has_ceiling, h

    def finish(self, w=8, exit_dx=4):
        self.flat(w)
        self.set(self.x - w + exit_dx, self.g, 'E')
        # right wall
        self.rect(self.x, 0, self.x + 1, self.H - 1)
        self.x += 2

    # ---------- output
    def write(self, fname):
        W = self.x
        rows = []
        for y in range(self.H - 1, -1, -1):
            rows.append(''.join(self.cells.get((x, y), ' ') for x in range(W)).rstrip())
        out = []
        for k, v in self.header.items():
            out.append('%s: %s' % (k, v))
        out.append('---')
        out.extend(rows)
        out.append('---')
        for typ, x, y, kw in self.ents:
            args = ' '.join('%s=%s' % (k, fmt(v)) for k, v in kw.items())
            out.append(('%s %s %s %s' % (typ, fmt(float(x)), fmt(float(y)), args)).rstrip())
        open(os.path.join(LV, fname), 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
        print('%-24s %4d x %2d  ents=%d' % (fname, W, self.H, len(self.ents)))
