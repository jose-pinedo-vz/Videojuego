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

## Funcionalidad que falta (código)

- Soltar el arma al morir / reaparecer sin arma.
- Si el host se desconecta, los clientes se quedan en la partida: volver al menú con `OnClientDisconnectCallback`.
- Quitar el nombre de `GameManager.nombresJugadores` cuando un jugador se desconecta.
- Botón "Jugar otra vez" en `PanelFin` (el host llama de nuevo a `GameManager.iniciarPartida`).
- Sonidos de disparo / pasos.

## Repositorio

- `proyecto2, refactorisacion/` es un build compilado (≈9 MB que cambian en cada build). Conviene sacarlo del repo
  (`git rm -r --cached`) y agregarlo al `.gitignore`; los builds se comparten por fuera (Drive, Releases de GitHub).
- `Assets/FishNetworking.4.7.2R.unitypackage` no se usa (el proyecto usa Netcode for GameObjects).
