# Simulador de primeros auxilios VR

Proyecto de Unity para una práctica guiada de primeros auxilios en realidad virtual con Meta Quest. El tutorial actual utiliza una estación fija con un paciente preparado, instrucciones por pasos, compresiones virtuales y resultados del intento.

## Funciones implementadas

- Inicio desde una escena de arranque, menú principal y tutorial de RCP, con un rig XR persistente.
- Cuatro pantallas fijas: progreso, demostración animada, ritmo y recorrido virtual; control lateral de altura.
- Seis demostraciones ilustradas locales y avance continuo por acciones, sin botones de siguiente ni confirmación. Los controles opcionales quedan en un panel desplegable.
- Instrucciones de voz en español, repetición, silencio y pulso de ritmo; botones seleccionables con el láser de las manos o mandos y mediante contacto cercano.
- Colocación secuencial de la mano izquierda y después la derecha sobre el pecho, con guías holográficas, tolerancia a oclusiones y liberación al separar las manos.
- Detección de ciclos de compresión a partir del movimiento medido, con referencias de ritmo, recorrido virtual y retorno; recuperación ante pérdidas de seguimiento.
- Puntuación educativa, correcciones, resultados por criterio, pausa, cierre parcial y repetición sin datos heredados.
- Paciente estático con dedos relajados. El ragdoll, el arrastre y el destino holográfico quedan conservados como modo experimental desactivado.
- Malla preparada para ocultar la piel cubierta por la ropa conservando materiales válidos en Quest.
- Herramientas de configuración, regeneración de recursos y comprobación desde el editor.

## Requisitos

- **Unity 2022.3.62f2**, preferiblemente instalado desde Unity Hub.
- Módulo **Android Build Support**, con Android SDK & NDK Tools y OpenJDK, para compilar para Quest.
- **Git y Git LFS** para descargar el modelo del paciente.
- Internet en la primera apertura para restaurar los paquetes. Las versiones están registradas en `Packages/manifest.json` y `Packages/packages-lock.json`, incluidos Meta XR SDK 201.0.0 y XR Interaction Toolkit 2.6.5.

## Descargar y abrir

```sh
git lfs install
git clone https://github.com/Mauricio-Martinez11/Simulador-de-primeros-auxilios-VR.git
cd Simulador-de-primeros-auxilios-VR
git lfs pull
```

1. Añade la carpeta descargada a Unity Hub y ábrela con la versión indicada.
2. Espera a que Unity restaure los paquetes e importe los recursos.
3. Abre `Assets/Scenes/Boobstrap.unity` y entra en Play Mode con tu entorno XR configurado.
4. Desde el menú principal, inicia el tutorial. Los pasos, animaciones y controles se explican en [la guía del tutorial continuo](Documentation/RCP-Tutorial-Animado.md).

Para compilar manualmente para Quest, selecciona Android en Build Settings y conserva las escenas habilitadas en este orden: `Boobstrap`, `1_MainMenu` y `2_Tutorial_RCP`. Los APK se generan localmente y no se incluyen en el repositorio.

## Estructura del repositorio

| Carpeta | Contenido |
| --- | --- |
| `Assets` | Escenas, scripts, modelos, materiales, prefabs y sus archivos `.meta`. |
| `Packages` | Dependencias y versiones de los paquetes. |
| `ProjectSettings` | Configuración del proyecto, física, XR y compilación. |
| `Documentation` | Guía de uso, capturas y registros de validación. |
| `ArtSource/RCP` | Atlas ilustrados originales y prompts de creación. No se incluyen en la aplicación. |
| `Tools` | Ensamblado reproducible de los vídeos a partir de sus fotogramas. |

`Library`, `Temp`, `Logs`, `UserSettings` y las compilaciones se excluyen mediante `.gitignore`. Unity reconstruye sus cachés al abrir el proyecto. Conserva los archivos `.meta`: contienen los identificadores que enlazan las escenas con sus recursos.

## Validación registrada

El tutorial continuo pasó **96 comprobaciones de lógica y 449 de escena, controles ISDK, vídeo y ciclo de vida**; el [registro de validación](Documentation/RCP-validacion-tutorial-animado.txt) incluye el alcance y las limitaciones. El menú **Primeros auxilios → RCP → Verificar estación y compresiones** permite repetir las comprobaciones de lógica. La guía documenta los parámetros utilizados. [Vista de las pantallas](Documentation/RCP-tutorial-animado.png).

El [registro anterior](Documentation/RCP-validacion-quest-presentacion.txt) corresponde a las 235 comprobaciones de la versión con traslado, no a la validación de la nueva estación. La prueba con manos reales dentro de Quest sigue pendiente. Los nuevos indicadores estiman movimiento y frecuencia virtuales; no miden fuerza, presión ni profundidad física, ni certifican habilidades clínicas.
