#!/usr/bin/env python3
"""RedMagic - sonidos genericos de enemigo (placeholder por defecto).
Neutros a proposito: sirven para cualquier enemigo hasta que tenga los suyos."""
import numpy as np, wave, os
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'sfx_gen.py')).read().split('# ---------- 1. UI hover')[0])
OUT = "/home/claude/sfx_enemy"; os.makedirs(OUT, exist_ok=True)

def save(name, x, peak=0.85):
    x = declick(np.asarray(x,float).copy()); m=np.max(np.abs(x))
    if m>0: x=x/m*peak
    with wave.open(os.path.join(OUT,name),"wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x,-1,1)*32767).astype(np.int16).tobytes())
    print(f"  {name:28s} {len(x)/SR*1000:6.0f} ms")

def enemy_move(seed=0, pm=1.0):
    r=np.random.default_rng(900+seed); d=0.13; n=len(t(d)); x=np.linspace(0,1,n)
    nz=r.uniform(-1,1,n)
    body=svf(nz, lerp(700,240,x)*pm, q=1.2, mode="bp")*1.3          # paso pesado
    thump=osc(lerp(110,52,x)*pm, n, "sine")*0.75*np.exp(-x*16)      # peso/masa
    drag=svf(nz, 2600*pm, q=1.5, mode="bp")*0.22*np.exp(-x*9)       # arrastre de garra
    y=(body+thump+drag)*env_ad(n,0.003,d,curve=2.4)
    return svf(y,4200,q=0.7,mode="lp")*0.55

def enemy_hurt():
    d=0.22; n=len(t(d)); x=np.linspace(0,1,n)
    pitch=lerp(330,150,x**0.7)                                      # gruñido que cae
    growl=(osc(pitch,n,"saw")*0.6+osc(pitch*1.008,n,"square",duty=0.4)*0.4)
    growl*= 1+0.25*np.sin(2*np.pi*28*np.linspace(0,d,n))            # vibrato = organico
    breath=svf(noise(n),lerp(2400,900,x),q=1.1,mode="bp")*0.35
    y=(growl+breath)*env_ad(n,0.006,d,curve=1.9)
    return svf(y,4800,q=0.8,mode="lp")

def enemy_attack():
    d=0.21; n=len(t(d)); x=np.linspace(0,1,n)
    swipe=svf(noise(n), lerp(600,3400,np.sin(x*np.pi*0.8)**0.6), q=2.2, mode="bp")*1.5
    accent=osc(lerp(420,120,x**0.5),n,"square",duty=0.35)*0.5*np.exp(-x*9)
    y=(swipe+accent)*env_ad(n,0.005,d,curve=1.5)
    return svf(y,6500,q=0.7,mode="lp")

def enemy_die():
    d=0.62; n=len(t(d)); x=np.linspace(0,1,n)
    pitch=lerp(300,70,x**0.55)                                      # colapso descendente
    growl=(osc(pitch,n,"saw")*0.55+osc(pitch*0.5,n,"tri")*0.45)
    growl*= 1+0.18*np.sin(2*np.pi*19*np.linspace(0,d,n))
    rasp=svf(noise(n),lerp(1800,300,x),q=1.0,mode="bp")*0.45*np.exp(-x*3.2)
    thud=osc(lerp(90,40,x),n,"sine")*0.5*np.exp(-(x-0.55)**2*90)    # cuerpo cayendo
    y=(growl+rasp+thud)*env_ad(n,0.01,d,curve=1.25)
    return svf(y,4000,q=0.7,mode="lp")

print("Generando SFX enemigo generico ->",OUT)
save("enemy_move_01.wav", enemy_move(0,1.0))
save("enemy_move_02.wav", enemy_move(1,0.93))
save("enemy_hurt.wav",    enemy_hurt())
save("enemy_attack.wav",  enemy_attack())
save("enemy_die.wav",     enemy_die())
