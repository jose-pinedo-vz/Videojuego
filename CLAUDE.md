# CLAUDE.md — Contexto del proyecto

Juego multijugador 3D (vista lateral / 2.5D) en Unity. Proyecto escolar en equipo.

## Entorno

- **Motor:** Unity **6000.5.9f1** (ver `ProyectoUnity/ProjectSettings/ProjectVersion.txt`), desarrollo en Arch Linux.
- **Red:** Netcode for GameObjects 2.x (`com.unity.netcode.gameobjects`) + Unity Relay + Authentication (anónima).
  - `Docs/arquitectura.md` menciona FishNet: **no se usa**, el código es 100% Netcode for GameObjects.
- **Input:** Input System nuevo (`Keyboard.current`). `activeInputHandler: 2` (ambos).
- **Render:** URP.
- **Repo:** GitHub `jose-pinedo-vz/Videojuego`. Rama de trabajo: `multijugador`. `main` está muy atrasada.
- Proyecto Unity en `ProyectoUnity/`. `proyecto2, refactorisacion/` es un **build** compilado (no editar a mano).

Compilar/validar sin abrir el editor:

```sh
~/Unity/Hub/Editor/6000.5.9f1/Editor/Unity -batchmode -nographics -quit \
  -projectPath ProyectoUnity -logFile -
```

## Estructura

- Escena principal: `Assets/Scenes/Juego.unity` (`Prueba animacion.unity` es de pruebas).
- Prefab del jugador: `Assets/cuerpo.prefab` (tag `Player`, hijo `Mano` donde se pega el arma).
  Componentes: NetworkObject, NetworkTransform (autoridad del dueño), CharacterController,
  CapsuleCollider, `Red`, `Personaje`, `CamaraSeguir`, `JugadorMovimiento` (**desactivado**), Animator.
- Scripts en `Assets/script/`:

| Archivo | Clase | Rol |
|---|---|---|
| `net.cs` | `NetWork` | Relay: crear sala (StartHost) y unirse (StartClient). |
| `main.cs` | `inicio` | UI/menús, botones, paneles, inicio y fin de partida en pantalla. |
| `GameManager.cs` | `GameManager` | Lista de nombres, spawns, temporizador (servidor). |
| `Red.cs` | `Red` | Nombre del jugador en red, registro y teleport al spawn, aviso de arma. |
| `Personaje.cs` | `Personaje` | Movimiento, salto, deslizar, agacharse, muerte/respawn, Animator. |
| `Arma.cs` | `Arma` | Recoger (C), disparar (V) por raycast, trazo (LineRenderer). |
| `CamaraSeguir.cs` | `CamaraSeguir` | Cámara sigue al jugador local en X/Y. |
| `movimientoJugador.cs` | `JugadorMovimiento` | Movimiento viejo (desactivado en el prefab). |

Controles: A/D mover, Espacio saltar, Shift deslizar (en movimiento) / agacharse (quieto), C recoger arma, V disparar.

## Animator (`Assets/PersonajeAnimator.controller`)

Estados: `parado`, `carrera`, `salto vertical`, `salto corriendo`, `slide corriendo`.

Parámetros **tal como están hoy en el controller** (respetar mayúsculas):

| Parámetro | Tipo | Nota |
|---|---|---|
| `movimiento` | Float | 0 quieto, 1 moviéndose (transiciones usan > 0.1 / < 0.1). |
| `EstaEnSuelo` | Bool | En el documento original aparece como `estaEnSuelo`. |
| `Saltar` | Bool | En el documento original aparece como `saltar` (Trigger/Bool). |
| `estaDeslizando` | Bool | Con `e` minúscula. |
| `estaAgachado` | Bool | |

`Personaje.cs` busca los parámetros sin distinguir mayúsculas, así que funciona con ambas versiones.

## Reglas estrictas

1. **Nunca editar el YAML de `Assets/Scenes/*.unity`** a mano (conflictos de merge / corrupción). Los cambios de escena se hacen en el editor y se describen en texto.
2. Mantener nombres exactos de variables y parámetros del Animator.
3. Netcode: todo lo que se sincroniza va en `NetworkBehaviour` con `NetworkVariable` o `[Rpc(SendTo.X)]`. El servidor es la autoridad de puntaje y tiempo; el dueño es autoridad de su posición (NetworkTransform owner).
4. Explicar los cambios en C# de forma directa y concisa; no modificar lógica que ya funciona.
5. Comentarios y nombres en español, como el código existente.

## Pendientes que requieren el editor de Unity

Ver `Docs/pendientes.md`.
