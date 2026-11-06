"""Original synthesised SFX for Shadow Steps (D-070). Pure Python, 44.1 kHz 16-bit mono WAV, one file per Sfx enum value.
Usage: python3 -I Tools/Sfx/generate_sfx.py Assets/_ShadowSteps/Audio/Sfx   (then Shadow Steps ▸ Audio ▸ Assign generated clips)."""
import math, random, wave, struct, os, sys
SR = 44100
OUT = sys.argv[1]

def env(t, dur, a=0.004, d=None, sustain=0.0, r=None, curve=2.0):
    """Attack/decay/sustain/release envelope in seconds; decay to `sustain` then release to 0 at dur."""
    d = d if d is not None else dur * 0.5; r = r if r is not None else dur * 0.3
    if t < a: return t / a
    if t < a + d: k = (t - a) / d; return 1 - (1 - sustain) * (1 - (1 - k) ** curve)
    if t < dur - r: return sustain
    k = (dur - t) / r; return max(0.0, sustain * k ** 1.5) if sustain > 0 else max(0.0, (1 - (1 - k)) * (k ** 1.5))

def tone(dur, freq, amp=1.0, wave_='sine', env_=None, phase=0.0):
    n = int(dur * SR); out = [0.0] * n; ph = phase
    for i in range(n):
        t = i / SR; f = freq(t) if callable(freq) else freq
        ph += 2 * math.pi * f / SR
        if wave_ == 'sine': s = math.sin(ph)
        elif wave_ == 'tri': s = 2 / math.pi * math.asin(math.sin(ph))
        elif wave_ == 'saw': s = 2 * ((ph / (2 * math.pi)) % 1.0) - 1
        else: s = 1.0 if math.sin(ph) > 0 else -1.0
        e = env_(t) if env_ else env(t, dur)
        out[i] = s * amp * e
    return out

def noise(dur, amp=1.0, lp=4000.0, hp=0.0, env_=None, seed=1):
    """Filtered white noise: one-pole low-pass at lp Hz, then one-pole high-pass at hp Hz."""
    rng = random.Random(seed); n = int(dur * SR); out = [0.0] * n
    a_lp = 0.0; y = 0.0; a_hp = math.exp(-2 * math.pi * max(1.0, hp) / SR); yh = 0.0; xh = 0.0
    for i in range(n):
        t = i / SR; x = rng.uniform(-1, 1)
        cutoff = lp(t) if callable(lp) else lp; a_lp = math.exp(-2 * math.pi * cutoff / SR)
        y = (1 - a_lp) * x + a_lp * y
        if hp > 0: yh = a_hp * (yh + y - xh); xh = y; v = yh
        else: v = y
        e = env_(t) if env_ else env(t, dur)
        out[i] = v * amp * e
    return out

def mix(*layers, offset=()):
    n = max(len(l) + int((offset[i] if i < len(offset) else 0) * SR) for i, l in enumerate(layers)); out = [0.0] * n
    for i, l in enumerate(layers):
        o = int((offset[i] if i < len(offset) else 0) * SR)
        for j, v in enumerate(l): out[o + j] += v
    return out

def finish(data, peak=0.7):
    m = max(1e-6, max(abs(v) for v in data)); g = peak / m
    n = len(data); fade = int(0.003 * SR)
    for i in range(n):
        f = min(1.0, i / fade, (n - 1 - i) / fade)
        data[i] = max(-1.0, min(1.0, data[i] * g * f))
    return data

def save(name, data):
    path = os.path.join(OUT, 'sfx_%s.wav' % name)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b''.join(struct.pack('<h', int(v * 32767)) for v in data))
    print('%-10s %.2fs' % (name, len(data) / SR))

E = env
# ---- board-piece beats
save('land', finish(mix(
    tone(0.09, lambda t: 170 - 90 * t, 1.0, env_=lambda t: E(t, 0.09, 0.002, 0.05, 0, 0.03)),           # soft thump
    noise(0.025, 0.5, lp=3500, env_=lambda t: E(t, 0.025, 0.001, 0.01, 0, 0.012), seed=3))))           # contact click
save('leap', finish(mix(
    noise(0.20, 0.8, lp=lambda t: 700 + 2600 * t, hp=250, env_=lambda t: E(t, 0.20, 0.03, 0.08, 0.5, 0.08), seed=5),  # rising air
    tone(0.16, lambda t: 240 + 420 * t * t, 0.35, 'tri', env_=lambda t: E(t, 0.16, 0.01, 0.10, 0.2, 0.05)))))        # spring
save('impact', finish(mix(
    tone(0.20, lambda t: 95 - 55 * min(1, t * 6), 1.0, env_=lambda t: E(t, 0.20, 0.002, 0.12, 0, 0.06)),   # sub slam
    noise(0.06, 0.9, lp=2500, env_=lambda t: E(t, 0.06, 0.001, 0.03, 0, 0.025), seed=7),                   # crack
    tone(0.12, lambda t: 300 - 200 * t, 0.3, 'tri', env_=lambda t: E(t, 0.12, 0.002, 0.05, 0, 0.05)))))   # body
save('whoosh', finish(mix(
    noise(0.34, 1.0, lp=lambda t: 1800 - 1300 * t, hp=180, env_=lambda t: E(t, 0.34, 0.06, 0.12, 0.45, 0.14), seed=11))))
save('thud', finish(mix(
    tone(0.14, lambda t: 120 - 50 * t, 1.0, env_=lambda t: E(t, 0.14, 0.002, 0.08, 0, 0.05)),
    noise(0.03, 0.6, lp=1800, env_=lambda t: E(t, 0.03, 0.001, 0.012, 0, 0.015), seed=13),
    tone(0.08, lambda t: 110 - 30 * t, 0.45, env_=lambda t: E(t, 0.08, 0.002, 0.04, 0, 0.03)), offset=(0, 0, 0.11))))   # small second bounce
save('key', finish(mix(
    tone(0.30, 1760, 0.7, env_=lambda t: E(t, 0.30, 0.001, 0.18, 0, 0.1, 1.5)),
    tone(0.30, 2637, 0.35, env_=lambda t: E(t, 0.30, 0.001, 0.12, 0, 0.1, 1.5)),
    tone(0.24, 2349, 0.5, env_=lambda t: E(t, 0.24, 0.001, 0.14, 0, 0.08, 1.5)), offset=(0, 0, 0.09))))                 # second chime
save('dooropen', finish(mix(
    noise(0.04, 0.7, lp=3000, env_=lambda t: E(t, 0.04, 0.001, 0.015, 0, 0.02), seed=17),                                  # latch
    tone(0.42, lambda t: 150 + 90 * t + 18 * math.sin(t * 55), 0.6, 'saw', env_=lambda t: E(t, 0.42, 0.03, 0.2, 0.5, 0.15)),  # creak
    tone(0.42, lambda t: 300 + 180 * t + 36 * math.sin(t * 55), 0.2, 'tri', env_=lambda t: E(t, 0.42, 0.03, 0.2, 0.5, 0.15)), offset=(0, 0.03, 0.03))))
save('smoke', finish(mix(
    noise(0.55, 1.0, lp=lambda t: 1400 - 900 * t, env_=lambda t: E(t, 0.55, 0.02, 0.2, 0.35, 0.3), seed=19),
    tone(0.3, lambda t: 220 - 120 * t, 0.25, env_=lambda t: E(t, 0.3, 0.01, 0.15, 0, 0.1)))))
save('emp', finish(mix(
    tone(0.28, lambda t: 2400 * (1 - t) ** 2 + 180, 0.8, 'square', env_=lambda t: E(t, 0.28, 0.002, 0.15, 0.2, 0.08)),
    noise(0.12, 0.5, lp=6000, hp=1500, env_=lambda t: E(t, 0.12, 0.001, 0.06, 0, 0.05), seed=23),
    tone(0.22, 60, 0.4, env_=lambda t: E(t, 0.22, 0.01, 0.1, 0.3, 0.1)), offset=(0, 0, 0.05))))
# ---- the original set, redone
save('footstep', finish(mix(tone(0.07, lambda t: 190 - 70 * t, 1.0, env_=lambda t: E(t, 0.07, 0.002, 0.04, 0, 0.025)), noise(0.02, 0.4, lp=3000, env_=lambda t: E(t, 0.02, 0.001, 0.008, 0, 0.01), seed=29))))
save('tick', finish(mix(tone(0.04, 1500, 0.6, env_=lambda t: E(t, 0.04, 0.001, 0.02, 0, 0.015)), noise(0.015, 0.5, lp=5000, env_=lambda t: E(t, 0.015, 0.001, 0.006, 0, 0.008), seed=31))))
save('rotate', finish(mix(*[noise(0.03, 0.8, lp=4500, hp=800, env_=lambda t: E(t, 0.03, 0.001, 0.012, 0, 0.015), seed=37 + i) for i in range(3)] + [tone(0.14, lambda t: 420 + 120 * t, 0.25, 'tri', env_=lambda t: E(t, 0.14, 0.01, 0.08, 0.2, 0.05))], offset=(0, 0.045, 0.09, 0))))   # ratchet
save('gadget', finish(mix(tone(0.18, lambda t: 500 + 400 * math.sin(t * 20), 0.7, 'tri', env_=lambda t: E(t, 0.18, 0.005, 0.1, 0.2, 0.06)))))
save('alert', finish(mix(*[tone(0.14, 880 if i % 2 == 0 else 660, 0.8, 'square', env_=lambda t: E(t, 0.14, 0.005, 0.08, 0.6, 0.04)) for i in range(3)], offset=(0, 0.16, 0.32))))
save('unlock', finish(mix(noise(0.03, 0.6, lp=3500, env_=lambda t: E(t, 0.03, 0.001, 0.012, 0, 0.015), seed=41), tone(0.3, 1319, 0.6, env_=lambda t: E(t, 0.3, 0.002, 0.18, 0, 0.1, 1.5)), tone(0.3, 1976, 0.3, env_=lambda t: E(t, 0.3, 0.002, 0.12, 0, 0.1, 1.5)), offset=(0, 0.04, 0.04))))
save('clear', finish(mix(*[mix(tone(0.5, f, 0.6, env_=lambda t: E(t, 0.5, 0.004, 0.3, 0, 0.15, 1.5)), tone(0.5, f * 2, 0.15, env_=lambda t: E(t, 0.5, 0.004, 0.2, 0, 0.15, 1.5))) for f in (523.25, 659.25, 783.99, 1046.5)], offset=(0, 0.11, 0.22, 0.33))))
save('tap', finish(mix(tone(0.06, lambda t: 1000 - 300 * t, 0.7, env_=lambda t: E(t, 0.06, 0.001, 0.03, 0, 0.02)), noise(0.012, 0.3, lp=6000, env_=lambda t: E(t, 0.012, 0.001, 0.005, 0, 0.006), seed=43))))
save('error', finish(mix(*[tone(0.09, 150, 0.8, 'square', env_=lambda t: E(t, 0.09, 0.003, 0.05, 0.5, 0.03)) for _ in range(2)], offset=(0, 0.12))))
