#!/usr/bin/env python3
"""Genera los sonidos del juego (estilo caricatura) como .wav en Assets/Resources/Audio.

Uso:  python3 Docs/generar_sonidos.py ProyectoUnity/Assets/Resources/Audio
Todo se sintetiza con numpy, no hay archivos externos. Para cambiar un sonido se edita aqui y se vuelve a correr.
Los ambientes (amb_*) son bucles perfectos de 10 s.
"""
import os
import sys
import wave

import numpy as np

SR = 22050
rng = np.random.default_rng(7)
SALIDA = sys.argv[1] if len(sys.argv) > 1 else "."
os.makedirs(SALIDA, exist_ok=True)


def guardar(nombre, x, pico=0.9):
    x = np.asarray(x, dtype=np.float64)
    m = np.max(np.abs(x))
    if m > 0:
        x = x / m * pico
    corte = int(SR * 0.004)  # 4 ms de fundido final para evitar el "click" al cortar
    if len(x) > corte * 2:
        x[-corte:] *= np.linspace(1, 0, corte)
    with wave.open(os.path.join(SALIDA, nombre + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())


def tiempo(d):
    return np.arange(int(SR * d)) / SR


def barrido(d, f0, f1):
    """seno cuya frecuencia va de f0 a f1 (exponencial)"""
    t = tiempo(d)
    f = f0 * (f1 / f0) ** (t / d)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def ruido(d):
    return rng.standard_normal(int(SR * d))


def banda(x, lo, hi):
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    X[(f < lo) | (f > hi)] = 0
    return np.fft.irfft(X, len(x))


def sobre(d, ataque, caida):
    t = tiempo(d)
    return np.minimum(t / max(ataque, 1e-4), 1.0) * np.exp(-t / caida)


def poner(total, partes):
    """suma partes [(offset_segundos, senal)] en un buffer de `total` segundos"""
    out = np.zeros(int(SR * total))
    for off, s in partes:
        i = int(SR * off)
        n = min(len(s), len(out) - i)
        if n > 0:
            out[i:i + n] += s[:n]
    return out


def tono(d, f, forma="seno"):
    t = tiempo(d)
    if forma == "triangulo":
        return 2 * np.abs(2 * ((t * f) % 1) - 1) - 1
    return np.sin(2 * np.pi * f * t)


# ---------------------------------------------------------------- disparos
def disparo_bereta():
    d = 0.28
    pum = barrido(d, 700, 140) * sobre(d, 0.002, 0.07)
    chasquido = banda(ruido(d), 900, 6000) * sobre(d, 0.001, 0.025)
    return pum * 0.9 + chasquido * 0.8


def disparo_ak47():
    d = 0.3
    golpe = barrido(d, 420, 110) * sobre(d, 0.002, 0.09)
    grave = barrido(d, 150, 60) * sobre(d, 0.002, 0.12)
    chasquido = banda(ruido(d), 600, 5000) * sobre(d, 0.001, 0.035)
    return golpe * 0.8 + grave * 0.7 + chasquido * 0.8


def disparo_mac10():
    d = 0.16
    pip = barrido(d, 1100, 380) * sobre(d, 0.001, 0.04)
    chasquido = banda(ruido(d), 1500, 7000) * sobre(d, 0.001, 0.018)
    return pip * 0.9 + chasquido * 0.7


def disparo_barret():
    d = 1.1
    boom = barrido(d, 110, 38) * sobre(d, 0.003, 0.28)
    latigo = barrido(d, 1500, 260) * sobre(d, 0.001, 0.05)
    cola = banda(ruido(d), 80, 1200) * sobre(d, 0.01, 0.3)
    eco = poner(d, [(0.0, boom), (0.22, boom * 0.35)])
    return eco * 0.9 + latigo * 0.7 + cola * 0.6


def disparo_raygun():
    d = 0.55
    t = tiempo(d)
    vib = 1 + 0.06 * np.sin(2 * np.pi * 28 * t)
    f = 1600 * (180 / 1600) ** (t / d) * vib
    onda = np.sin(2 * np.pi * np.cumsum(f) / SR)
    armonico = np.sin(2 * np.pi * np.cumsum(f * 2.01) / SR) * 0.4
    brillo = banda(ruido(d), 3000, 9000) * sobre(d, 0.002, 0.08) * 0.3
    return (onda + armonico) * sobre(d, 0.003, 0.2) + brillo


# ---------------------------------------------------------------- armas: otros
def recarga():
    clic1 = banda(ruido(0.05), 1500, 6000) * sobre(0.05, 0.001, 0.012) + barrido(0.05, 900, 500) * sobre(0.05, 0.001, 0.015)
    desliza = banda(ruido(0.22), 800, 3500) * np.sin(np.linspace(0, np.pi, int(SR * 0.22)))
    clic2 = (banda(ruido(0.07), 800, 5000) * sobre(0.07, 0.001, 0.02) + barrido(0.07, 500, 220) * sobre(0.07, 0.001, 0.03)) * 1.2
    return poner(0.75, [(0.0, clic1), (0.18, desliza * 0.5), (0.55, clic2)])


def sin_municion():
    return banda(ruido(0.08), 1200, 5000) * sobre(0.08, 0.001, 0.015) + barrido(0.08, 700, 400) * sobre(0.08, 0.001, 0.02) * 0.6


def recoger():
    a = barrido(0.14, 500, 900) * sobre(0.14, 0.003, 0.08)
    b = barrido(0.22, 800, 1300) * sobre(0.22, 0.003, 0.1)
    return poner(0.4, [(0.0, a), (0.09, b)])


def romper_caja():
    golpe = barrido(0.3, 200, 70) * sobre(0.3, 0.002, 0.07)
    astillas = banda(ruido(0.5), 400, 4500) * sobre(0.5, 0.001, 0.06)
    pedazos = poner(0.55, [(0.0, astillas), (0.09, astillas * 0.5), (0.17, astillas * 0.3)])
    pop = barrido(0.15, 400, 900) * sobre(0.15, 0.002, 0.05)
    return poner(0.6, [(0.0, golpe * 1.1), (0.0, pedazos * 0.8), (0.05, pop * 0.5)])


def impacto():
    bonk = barrido(0.2, 340, 110) * sobre(0.2, 0.002, 0.05)
    chispa = banda(ruido(0.2), 1000, 6000) * sobre(0.2, 0.001, 0.025)
    return bonk * 0.9 + chispa * 0.6


# ---------------------------------------------------------------- personaje
def deslizar():
    d = 0.7
    n = ruido(d)
    f = banda(n, 500, 4000)
    # el filtro "se abre" y se cierra: swoosh
    t = tiempo(d)
    swell = np.sin(np.pi * np.clip(t / d, 0, 1)) ** 1.5
    return f * swell


def salto():
    d = 0.28
    t = tiempo(d)
    vib = 1 + 0.03 * np.sin(2 * np.pi * 18 * t)
    f = 260 * (720 / 260) ** (t / d) * vib
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * sobre(d, 0.004, 0.12)


def aterrizar():
    return barrido(0.16, 130, 55) * sobre(0.16, 0.002, 0.05) + banda(ruido(0.16), 100, 900) * sobre(0.16, 0.001, 0.03) * 0.5


def paso(n):
    d = 0.09
    base = banda(ruido(d), 300 + 120 * n, 2200 + 300 * n) * sobre(d, 0.001, 0.018)
    toc = barrido(d, 220 + 40 * n, 140) * sobre(d, 0.001, 0.02)
    return base * 0.6 + toc * 0.7


# ---------------------------------------------------------------- interfaz
def ui_hover():
    return tono(0.05, 1500) * sobre(0.05, 0.002, 0.012)


def ui_click():
    return barrido(0.12, 600, 1100) * sobre(0.12, 0.002, 0.04)


def panel():
    d = 0.28
    t = tiempo(d)
    return banda(ruido(d), 600, 3500) * np.sin(np.pi * np.clip(t / d, 0, 1)) * 0.6 + barrido(d, 400, 800) * sobre(d, 0.01, 0.1) * 0.2


def inicio_ronda():
    whoosh = banda(ruido(0.7), 300, 3000) * np.sin(np.pi * np.clip(tiempo(0.7) / 0.7, 0, 1)) ** 2
    n1 = tono(0.4, 523.25, "triangulo") * sobre(0.4, 0.005, 0.18)
    n2 = tono(0.6, 659.25, "triangulo") * sobre(0.6, 0.005, 0.25)
    n3 = tono(0.9, 783.99, "triangulo") * sobre(0.9, 0.005, 0.35)
    return poner(1.6, [(0.0, whoosh * 0.7), (0.35, n1 * 0.6), (0.5, n2 * 0.6), (0.65, n3 * 0.7)])


def tick():
    return tono(0.07, 1200) * sobre(0.07, 0.001, 0.02) + banda(ruido(0.07), 2000, 6000) * sobre(0.07, 0.001, 0.01) * 0.3


def fin_partida():
    notas = [523.25, 659.25, 783.99, 1046.5]
    partes = []
    for i, f in enumerate(notas):
        partes.append((i * 0.14, tono(0.7, f, "triangulo") * sobre(0.7, 0.004, 0.3 if i == 3 else 0.14)))
    return poner(1.5, partes)


# ---------------------------------------------------------------- ambiente: sonidos sueltos
def gota():
    a = barrido(0.14, 2200, 1100) * sobre(0.14, 0.001, 0.035)
    b = barrido(0.14, 2000, 1000) * sobre(0.14, 0.001, 0.03) * 0.35
    return poner(0.45, [(0.0, a), (0.18, b)])


def clank():
    d = 0.7
    s = sum(a * np.sin(2 * np.pi * f * tiempo(d)) for f, a in [(820, 1), (1330, 0.7), (2110, 0.5), (3200, 0.25)])
    return s * sobre(d, 0.001, 0.18) + banda(ruido(d), 1500, 7000) * sobre(d, 0.001, 0.01) * 0.4


def pajaro1():
    partes = []
    for i, (f0, f1) in enumerate([(3000, 4300), (3400, 4800), (3000, 4000)]):
        d = 0.11
        p = barrido(d, f0, f1) * sobre(d, 0.006, 0.07)
        partes.append((i * 0.15, p))
    return poner(0.6, partes)


def pajaro2():
    d = 0.6
    t = tiempo(d)
    f = 3600 + 500 * np.sin(2 * np.pi * 22 * t)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.sin(np.pi * np.clip(t / d, 0, 1)) ** 0.7 * 0.6


def reloj():
    tic = tick()
    toc = barrido(0.07, 800, 700) * sobre(0.07, 0.001, 0.02) + banda(ruido(0.07), 1500, 5000) * sobre(0.07, 0.001, 0.01) * 0.3
    return poner(1.0, [(0.0, tic), (0.5, toc * 0.8)])


def crujido():
    d = 0.7
    t = tiempo(d)
    f = 140 + 90 * np.sin(np.pi * t / d)
    saw = 2 * ((np.cumsum(f) / SR) % 1) - 1
    return saw * banda(ruido(d), 50, 400) * np.sin(np.pi * np.clip(t / d, 0, 1)) * 0.5 + banda(ruido(d), 400, 1500) * 0.12 * np.sin(np.pi * np.clip(t / d, 0, 1))


def nevera():
    clic = banda(ruido(0.12), 200, 2500) * sobre(0.12, 0.001, 0.03)
    return poner(0.5, [(0.0, clic), (0.0, barrido(0.5, 70, 55) * sobre(0.5, 0.05, 0.25) * 0.5)])


# ---------------------------------------------------------------- ambiente: bucles de 10 s perfectos
D_AMB = 10.0
N_AMB = int(SR * D_AMB)
T_AMB = np.arange(N_AMB) / SR


def ruido_periodico(forma):
    """ruido con el espectro dado; al ser periodico en N_AMB el bucle no tiene corte"""
    f = np.fft.rfftfreq(N_AMB, 1 / SR)
    fase = rng.uniform(0, 2 * np.pi, len(f))
    X = forma(f) * np.exp(1j * fase)
    X[0] = 0
    x = np.fft.irfft(X, N_AMB)
    return x / np.max(np.abs(x))


def modular(k, prof):
    """modulacion lenta con k ciclos enteros en 10 s (se repite igual al reiniciar)"""
    return 1 + prof * np.sin(2 * np.pi * k * T_AMB / D_AMB)


def amb_menu():
    pad = sum(a * np.sin(2 * np.pi * f * T_AMB) for f, a in [(110, 1), (164.8, 0.6), (220, 0.5), (329.6, 0.25)])
    pad *= modular(2, 0.25)
    aire = ruido_periodico(lambda f: 1 / (1 + (f / 300) ** 2)) * 0.15
    return pad + aire


def amb_cochera():
    zumbido = sum(a * np.sin(2 * np.pi * f * T_AMB) for f, a in [(60, 1), (120, 0.5), (180, 0.3), (300, 0.12)])
    zumbido *= modular(7, 0.12)
    aire = ruido_periodico(lambda f: 1 / (1 + (f / 250) ** 2)) * 0.5
    return zumbido * 0.5 + aire


def amb_cocina():
    nevera_ = (np.sin(2 * np.pi * 50 * T_AMB) + 0.5 * np.sin(2 * np.pi * 100 * T_AMB)) * modular(3, 0.3)
    aire = ruido_periodico(lambda f: 1 / (1 + (f / 500) ** 2)) * 0.4
    brillo = ruido_periodico(lambda f: np.where((f > 3000) & (f < 7000), 1.0, 0.0)) * 0.03
    return nevera_ * 0.5 + aire + brillo


def amb_patio():
    viento = ruido_periodico(lambda f: 1 / (1 + (f / 450) ** 2)) * modular(1, 0.5) * modular(3, 0.25)
    # grillos: trinos de tres pulsos cada 0.5 s (20 repeticiones en 10 s)
    ciclo = (T_AMB % 0.5) / 0.5
    pulsos = ((ciclo < 0.12) | ((ciclo > 0.18) & (ciclo < 0.30)) | ((ciclo > 0.36) & (ciclo < 0.48))).astype(float)
    grillos = np.sin(2 * np.pi * 4200 * T_AMB) * pulsos * 0.08
    return viento * 0.9 + grillos


def amb_sala():
    aire = ruido_periodico(lambda f: 1 / (1 + (f / 700) ** 2)) * 0.35 * modular(2, 0.2)
    zumbido = np.sin(2 * np.pi * 55 * T_AMB) * 0.15 + np.sin(2 * np.pi * 110 * T_AMB) * 0.06
    return aire + zumbido


CLIPS = {
    "disparo_bereta": disparo_bereta, "disparo_ak47": disparo_ak47, "disparo_mac10": disparo_mac10,
    "disparo_barret": disparo_barret, "disparo_raygun": disparo_raygun,
    "recarga": recarga, "sin_municion": sin_municion, "recoger": recoger, "romper_caja": romper_caja, "impacto": impacto,
    "deslizar": deslizar, "salto": salto, "aterrizar": aterrizar,
    "paso1": lambda: paso(0), "paso2": lambda: paso(1),
    "ui_hover": ui_hover, "ui_click": ui_click, "panel": panel, "inicio_ronda": inicio_ronda, "tick": tick, "fin_partida": fin_partida,
    "gota": gota, "clank": clank, "pajaro1": pajaro1, "pajaro2": pajaro2, "reloj": reloj, "crujido": crujido, "nevera": nevera,
}
BUCLES = {"amb_menu": amb_menu, "amb_cochera": amb_cochera, "amb_cocina": amb_cocina, "amb_patio": amb_patio, "amb_sala": amb_sala}


# ---------------------------------------------------------------- musica (bucles de varios compases)
def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12.0)


def suavizar(x, k=3):
    return np.convolve(x, np.ones(k) / k, mode="same")


def nota_pluck(n, d, vol=1.0):
    t = tiempo(d)
    f = midi(n)
    onda = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * 2 * f * t) + 0.15 * np.sin(2 * np.pi * 3 * f * t)
    return onda * sobre(d, 0.004, 0.18) * vol


def nota_bajo(n, d, vol=1.0):
    t = tiempo(d)
    f = midi(n)
    onda = 2 * np.abs(2 * ((t * f) % 1) - 1) - 1  # triangulo
    onda = onda + 0.3 * np.sin(2 * np.pi * 2 * f * t)
    return onda * np.minimum(t / 0.005, 1) * np.exp(-t / (d * 0.9)) * vol


def nota_lead(n, d, vol=1.0):
    t = tiempo(d)
    f = midi(n) * (1 + 0.006 * np.sin(2 * np.pi * 5.5 * t) * np.minimum(t / 0.15, 1))
    fase = (np.cumsum(f) / SR) % 1
    onda = np.where(fase < 0.28, 1.0, -1.0)  # cuadrada con ancho de pulso, sonido de videojuego
    return suavizar(onda, 3) * np.minimum(t / 0.006, 1) * np.exp(-t / (d * 0.8)) * vol * 0.5


def nota_acorde(notas, d, vol=1.0):
    return sum(nota_pluck(n, d, 1.0) for n in notas) * vol / max(len(notas), 1)


def bombo(vol=1.0):
    return barrido(0.22, 150, 42) * sobre(0.22, 0.002, 0.09) * vol


def caja(vol=1.0):
    return (banda(ruido(0.18), 1200, 7000) * sobre(0.18, 0.001, 0.05) * 0.8 + barrido(0.18, 240, 150) * sobre(0.18, 0.001, 0.05) * 0.5) * vol


def hat(vol=1.0, abierto=False):
    d = 0.16 if abierto else 0.05
    return banda(ruido(d), 6000, 10500) * sobre(d, 0.001, 0.06 if abierto else 0.015) * vol


ACORDES = {"maj": [0, 4, 7], "min": [0, 3, 7], "dom7": [0, 4, 7, 10], "min7": [0, 3, 7, 10], "maj7": [0, 4, 7, 11]}
PENTA = {"maj": [0, 2, 4, 7, 9], "min": [0, 3, 5, 7, 10]}


def componer(bpm, progresion, modo, estilo, compases_por_acorde=1, repeticiones=2, nota_raiz=60, semilla=1, swing=0.0):
    """Arma una cancion. progresion = [(semitonos sobre nota_raiz, calidad)], cada acorde dura `compases_por_acorde` compases."""
    r = np.random.default_rng(semilla)
    spb = 60.0 / bpm
    paso = spb / 2.0  # corchea
    compases = len(progresion) * compases_por_acorde * repeticiones
    total = compases * 4 * spb
    cola = 1.0
    pista = np.zeros(int(SR * (total + cola)))

    def poner_(t, senal, vol=1.0):
        i = int(SR * t)
        n = min(len(senal), len(pista) - i)
        if n > 0:
            pista[i:i + n] += senal[:n] * vol

    escala = PENTA[modo]
    motivo = None
    for c in range(compases):
        idx = (c // compases_por_acorde) % len(progresion)
        raiz, calidad = progresion[idx]
        acorde = [nota_raiz + raiz + i for i in ACORDES[calidad]]
        t0 = c * 4 * spb

        for b in range(8):  # 8 corcheas por compas
            t = t0 + b * paso + (paso * swing if b % 2 else 0.0)
            tiempo_fuerte = (b % 2 == 0)

            # bajo
            if estilo in ("bote", "funk", "circo"):
                if estilo == "bote":
                    n = acorde[0] - 24 + (7 if b in (3, 7) else 0)
                    poner_(t, nota_bajo(n, paso * 1.8), 0.9)
                elif estilo == "circo":
                    n = acorde[0] - 24 + (7 if b % 4 == 2 else 0)
                    if b % 2 == 0:
                        poner_(t, nota_bajo(n, paso * 1.9), 0.9)
                else:  # funk
                    if b in (0, 3, 4, 6):
                        poner_(t, nota_bajo(acorde[0] - 24 + (12 if b == 6 else 0), paso * 1.2), 0.95)
            else:  # calmo
                if b in (0, 4):
                    poner_(t, nota_bajo(acorde[0] - 24, paso * 3.5), 0.8)

            # acordes
            if estilo == "calmo":
                poner_(t, nota_pluck(acorde[b % len(acorde)] + 12, paso * 2.5), 0.45)
            elif estilo == "circo":
                if b % 2 == 1:
                    poner_(t, nota_acorde(acorde, paso * 1.2), 0.55)
            elif b in (1, 3, 5, 7) if estilo == "bote" else b in (2, 5, 7):
                poner_(t, nota_acorde(acorde, paso * 1.0), 0.5)

            # melodia: patron que se repite cada 2 compases con pequenas variaciones
            if c % 2 == 0 and b == 0:
                motivo = []
                pos = r.integers(2, 4)
                for k in range(16):
                    if r.random() < (0.35 if estilo == "calmo" else 0.22):
                        motivo.append(None)
                    else:
                        pos = int(np.clip(pos + r.integers(-1, 2), 0, 7))
                        motivo.append(pos)
            if motivo:
                m = motivo[(c % 2) * 8 + b]
                if m is not None:
                    grado = escala[m % len(escala)] + 12 * (m // len(escala))
                    n = nota_raiz + 12 + grado + (raiz if tiempo_fuerte else raiz)
                    # en los tiempos fuertes se corrige hacia una nota del acorde
                    if b in (0, 4):
                        n = min((a + 12 * k for a in acorde for k in (1, 2)), key=lambda x: abs(x - n))
                    poner_(t, nota_lead(n, paso * (1.8 if estilo != "calmo" else 3.2)), 0.5 if estilo != "calmo" else 0.35)

            # bateria
            if estilo in ("bote", "funk"):
                if b in (0, 4) or (estilo == "funk" and b == 6):
                    poner_(t, bombo(), 0.9)
                if b in (2, 6):
                    poner_(t, caja(), 0.55)
                poner_(t, hat(1.0, abierto=(b % 4 == 3)), 0.28 if b % 2 else 0.2)
            elif estilo == "circo":
                if b in (0, 4):
                    poner_(t, bombo(), 0.7)
                if b in (2, 6):
                    poner_(t, caja(), 0.35)
            else:
                if b in (0, 4):
                    poner_(t, bombo(), 0.35)
                if b % 2 == 1:
                    poner_(t, hat(1.0), 0.12)

    # la cola de las ultimas notas se pega al principio: el bucle no se corta
    n = int(SR * total)
    pista[:len(pista) - n] += pista[n:]
    pista = pista[:n]
    return pista


def musica_menu():
    return componer(92, [(0, "maj7"), (9, "min7"), (5, "maj7"), (7, "dom7")], "maj", "calmo", 1, 4, 60, 11)


def musica_juego1():  # cochera: energica
    return componer(134, [(9, "min"), (5, "maj"), (0, "maj"), (7, "maj")], "min", "bote", 1, 4, 57, 21)


def musica_juego2():  # cocina: circo / ragtime
    return componer(124, [(0, "maj"), (5, "maj"), (0, "maj"), (7, "dom7")], "maj", "circo", 1, 4, 60, 31, swing=0.28)


def musica_juego3():  # patio: funk soleado
    return componer(112, [(4, "min7"), (9, "dom7"), (2, "maj7"), (4, "min7")], "min", "funk", 1, 4, 52, 41)


def musica_juego4():  # sala: saltarina
    return componer(118, [(2, "min"), (7, "maj"), (0, "maj"), (9, "dom7")], "maj", "bote", 1, 4, 62, 51)


MUSICA = {"musica_menu": musica_menu, "musica_juego1": musica_juego1, "musica_juego2": musica_juego2,
          "musica_juego3": musica_juego3, "musica_juego4": musica_juego4}

for nombre, fn in CLIPS.items():
    guardar(nombre, fn())
for nombre, fn in BUCLES.items():
    guardar(nombre, fn(), pico=0.5)
for nombre, fn in MUSICA.items():
    guardar(nombre, fn(), pico=0.8)
print("listo:", len(CLIPS) + len(BUCLES) + len(MUSICA), "sonidos en", SALIDA)
