# Pendientes

## Hacer en el editor de Unity (no se pueden hacer desde código)

1. **Rig del gato a Humanoid** — `Assets/Personaje gato.fbx` → Inspector → Rig → *Animation Type* = **Humanoid** → Apply.
   Hoy está en *Generic* y las animaciones de `Assets/Animations/` son *Humanoid*, así que no se van a reproducir.
2. **Animator con Avatar** — en `cuerpo.prefab`, mover el componente **Animator** al hijo `Personaje gato`
   (o dejarlo en `cuerpo` y asignarle el *Avatar* del gato). Controller: `PersonajeAnimator`.
   `Personaje.cs` busca el Animator con `GetComponentInChildren`, funciona en cualquiera de los dos lugares.
3. **Orientación del modelo** — el código gira `cuerpo` a Y=0 cuando camina a +X y a Y=180 cuando va a −X
   (el arma dispara hacia `transform.right`). Si el gato queda mirando a la cámara, rotar solo el hijo
   `Personaje gato` 90° en Y dentro del prefab.
4. **Armas con nombre único** en la escena (ej. `Barret`, `Pistola1`). Los demás jugadores encuentran el arma recogida por su nombre.
   Cada arma necesita un **LineRenderer** (desactivado) para que se vea el trazo del disparo.
5. **Marcador final** — dentro de `PanelFin` crear un *Text - TextMeshPro* y arrastrarlo al campo
   `txt_resultados` del objeto que tiene el script `inicio`.
6. **Botón Salir** — confirmar que `btn_salir` está asignado en `inicio` (ahora sí cierra el juego).
7. (Opcional) Unificar nombres de parámetros del Animator con el documento: `EstaEnSuelo` → `estaEnSuelo`,
   `Saltar` → `saltar`. El código acepta ambas formas.

## Hacer en el editor — spawns y armas (cambios del 2026-10-27)

8. **6 spawns de jugador adicionales** — dentro de `Mapa3D` crear 6 GameObjects vacíos (`spawn3` a `spawn8`)
   en posiciones separadas del mapa. Arrastrarlos al arreglo `puntosDeSpawn` del componente `GameManager`
   (junto a los ya existentes `spawn1` y `spawn2`, total **8**). El código ya elige uno al azar.

9. **GestorArmas en la escena** — agregar el script `GestorArmas` al objeto `GameManager ` (el que ya existe).
   - En `todasLasArmas`: arrastrar TODOS los objetos de arma que estén en la escena (`barret`, `ak47`, y los que se agreguen).
   - En `maxArmasActivas`: dejar en 4 (o cambiar según se quiera).

10. **Puntos de spawn de armas** — crear 6–8 GameObjects vacíos en `Mapa3D` (ej. `spawnArma1` … `spawnArma8`)
    en posiciones distintas a los spawns de jugadores. Arrastrarlos al arreglo `puntosSpawnArmas` del GestorArmas.

11. **Importar armas restantes** — `mac10.fbx`, `bereta.fbx`, `raygun.fbx` están en `Assets/Modelos/` pero no
    tienen prefab ni componente `Arma`. Para cada una:
    a. Importar el .fbx a la escena.
    b. Agregarle BoxCollider (isTrigger = true), LineRenderer (desactivado) y el script `Arma`.
    c. Configurar `posicionEnMano` y `rotacionEnMano` a ojo.
    d. Arrastrar al array `todasLasArmas` del GestorArmas.

## Funcionalidad que falta (código)

- ~~Soltar el arma al morir / reaparecer sin arma.~~ **✓ Implementado (2026-10-27)**
- Si el host se desconecta, los clientes se quedan en la partida: volver al menú con `OnClientDisconnectCallback`.
- Quitar el nombre de `GameManager.nombresJugadores` cuando un jugador se desconecta.
- Botón "Jugar otra vez" en `PanelFin` (el host llama de nuevo a `GameManager.iniciarPartida`).
- Sonidos de disparo / pasos.

## Repositorio

- `proyecto2, refactorisacion/` es un build compilado (≈9 MB que cambian en cada build). Conviene sacarlo del repo
  (`git rm -r --cached`) y agregarlo al `.gitignore`; los builds se comparten por fuera (Drive, Releases de GitHub).
- `Assets/FishNetworking.4.7.2R.unitypackage` no se usa (el proyecto usa Netcode for GameObjects).
