#!/usr/bin/env python3
"""RedMagic SFX generator - sintesis procedural, sin dependencias externas salvo numpy."""
import numpy as np, wave, os, struct

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sfx")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1337)

# ---------- helpers ----------
def t(dur): return np.arange(int(SR*dur))/SR

def lerp(a,b,x): return a+(b-a)*x

def env_ad(n, atk, dec, curve=2.0):
    """attack lineal + decay exponencial. atk/dec en segundos."""
    e = np.zeros(n)
    na = max(1,int(SR*atk)); na = min(na,n)
    e[:na] = np.linspace(0,1,na)
    nd = n-na
    if nd>0:
        e[na:] = (1-np.linspace(0,1,nd))**curve
    return e

def osc(freq, n, wave_type="square", duty=0.5, phase0=0.0):
    """freq puede ser escalar o array (envolvente de pitch)."""
    f = np.full(n, freq, float) if np.isscalar(freq) else freq
    ph = np.cumsum(f)/SR + phase0
    fr = ph % 1.0
    if wave_type=="square": return np.where(fr<duty, 1.0, -1.0)
    if wave_type=="saw":    return 2*fr-1
    if wave_type=="tri":    return 4*np.abs(fr-0.5)-1
    return np.sin(2*np.pi*ph)

def noise(n): return rng.uniform(-1,1,n)

def svf(x, cutoff, q=1.0, mode="lp"):
    """State-variable filter, cutoff escalar o array (barridos)."""
    n = len(x)
    fc = np.full(n, cutoff, float) if np.isscalar(cutoff) else cutoff
    fc = np.clip(fc, 20, SR*0.45)
    g = np.tan(np.pi*fc/SR); k = 1.0/max(q,0.05)
    lp=np.zeros(n); hp=np.zeros(n); bp=np.zeros(n)
    ic1=ic2=0.0
    for i in range(n):
        a1 = 1.0/(1.0+g[i]*(g[i]+k))
        a2 = g[i]*a1
        v3 = x[i]-ic2
        v1 = a1*ic1 + a2*v3
        v2 = ic2 + g[i]*v1
        ic1 = 2*v1-ic1; ic2 = 2*v2-ic2
        lp[i]=v2; bp[i]=v1; hp[i]=x[i]-k*v1-v2
    return {"lp":lp,"bp":bp,"hp":hp}[mode]

def bitcrush(x, bits=8, downsample=1):
    q = 2**(bits-1)
    y = np.round(x*q)/q
    if downsample>1:
        y = np.repeat(y[::downsample], downsample)[:len(x)]
    return y

def declick(x, ms=3.0):
    n = min(int(SR*ms/1000), len(x)//2)
    if n>1:
        x[:n]  *= np.linspace(0,1,n)
        x[-n:] *= np.linspace(1,0,n)
    return x

def save(name, x, peak=0.85):
    x = declick(np.asarray(x, float).copy())
    m = np.max(np.abs(x))
    if m>0: x = x/m*peak
    data = (np.clip(x,-1,1)*32767).astype(np.int16)
    p = os.path.join(OUT, name)
    with wave.open(p,"wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(f"  {name:28s} {len(x)/SR*1000:6.0f} ms")

# ---------- 1. UI hover ----------
def ui_hover():
    d = 0.055; n = len(t(d))
    pitch = lerp(1180, 1560, np.linspace(0,1,n)**0.5)   # micro-subida
    body  = osc(pitch, n, "square", duty=0.5)*0.7 + osc(pitch*2.01, n, "sine")*0.3
    click = noise(n)*np.exp(-np.linspace(0,60,n))*0.15
    y = (body+click)*env_ad(n, 0.002, d-0.002, curve=2.6)
    y = svf(y, 6500, q=0.7, mode="lp")
    return bitcrush(y, bits=9)*0.55   # discreto, se repite mucho

# ---------- 2. Jump ----------
def jump():
    d = 0.20; n = len(t(d))
    x = np.linspace(0,1,n)
    pitch = lerp(300, 880, x**0.45)                      # sweep ascendente
    body = osc(pitch, n, "square", duty=lerp(0.5,0.28,x))
    sub  = osc(pitch*0.5, n, "tri")*0.35
    y = (body+sub)*env_ad(n, 0.004, d, curve=1.7)
    y = svf(y, lerp(2200, 5200, x), q=0.9, mode="lp")
    return bitcrush(y, bits=8)

# ---------- 3. Dash ----------
def dash():
    d = 0.26; n = len(t(d))
    x = np.linspace(0,1,n)
    # whoosh: ruido con bandpass que barre hacia arriba y cae
    nz = noise(n)
    sweep = lerp(400, 3800, np.sin(x*np.pi*0.85)**0.7)
    air = svf(nz, sweep, q=2.4, mode="bp")*1.6
    # cuerpo tonal descendente = sensacion de impulso
    pitch = lerp(620, 150, x**0.6)
    tone = osc(pitch, n, "saw")*0.45*np.exp(-x*7)
    y = (air+tone)*env_ad(n, 0.008, d, curve=1.35)
    y = svf(y, 7000, q=0.7, mode="lp")
    return y

# ---------- 4. Buy item ----------
def buy():
    d = 0.42; n = len(t(d))
    y = np.zeros(n)
    # arpegio ascendente corto (coin/confirm): C6 - E6 - G6
    notes = [(1046.5, 0.00, 0.075), (1318.5, 0.055, 0.075), (1568.0, 0.11, 0.31)]
    for f, start, ln in notes:
        ns = int(SR*ln); i0 = int(SR*start)
        if i0+ns > n: ns = n-i0
        seg = (osc(f, ns, "square", duty=0.45)*0.75 +
               osc(f*2, ns, "sine")*0.25)
        seg *= env_ad(ns, 0.003, ln, curve=2.2)
        y[i0:i0+ns] += seg*0.8
    # shimmer metalico encima de la ultima nota
    i0 = int(SR*0.11); ns = n-i0
    sh = (osc(3136, ns, "sine")+osc(4700, ns, "sine")*0.6)*0.18
    y[i0:] += sh*env_ad(ns, 0.004, ns/SR, curve=3.0)
    y = svf(y, 9000, q=0.7, mode="lp")
    return bitcrush(y, bits=9)

# ---------- 5. Footstep ----------
def footstep(seed_shift=0, pitch_mul=1.0):
    r = np.random.default_rng(4242+seed_shift)
    d = 0.095; n = len(t(d))
    x = np.linspace(0,1,n)
    nz = r.uniform(-1,1,n)
    # impacto: ruido filtrado en banda media-baja
    body = svf(nz, lerp(1100, 380, x)*pitch_mul, q=1.3, mode="bp")*1.4
    # thump sub para peso
    thump = osc(lerp(150,70,x)*pitch_mul, n, "sine")*0.5*np.exp(-x*22)
    # scuff: chispa aguda al inicio (suela/grava)
    scuff = svf(nz, 4200*pitch_mul, q=1.8, mode="bp")*0.3*np.exp(-x*45)
    y = (body+thump+scuff)*env_ad(n, 0.002, d, curve=2.8)
    y = svf(y, 5200, q=0.7, mode="lp")
    return y*0.6   # suena en loop, debe quedar por debajo de la mezcla

# ---------- build ----------
print("Generando SFX ->", OUT)
save("ui_hover.wav",   ui_hover())
save("player_jump.wav", jump())
save("player_dash.wav", dash())
save("shop_buy.wav",    buy())
for i,(s,pm) in enumerate([(0,1.0),(1,1.06),(2,0.94)], start=1):
    save(f"player_footstep_{i:02d}.wav", footstep(s,pm))
print("OK")
