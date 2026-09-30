# Simulador de primeros auxilios VR

Proyecto de Unity para una práctica guiada de primeros auxilios en realidad virtual con Meta Quest. La implementación actual se centra en la secuencia de RCP, la colocación de las manos y la interacción con un paciente virtual.

## Funciones implementadas

- Inicio desde una escena de arranque, menú principal y tutorial de RCP, con un rig XR persistente.
- Paneles de instrucciones con botones seleccionables mediante el láser de los mandos o de las manos, además de interacción cercana.
- Colocación secuencial de la mano izquierda y después la derecha sobre el pecho, con guías holográficas, tolerancia a oclusiones y liberación al separar las manos.
- Agarre con una o dos manos, arrastre y elevación del paciente, con guías que alternan entre brazos y tobillos.
- Destino holográfico y ajuste de posición y orientación al soltar al paciente cerca del destino.
- Ragdoll articulado con colisiones propias y límites en cintura, hombros, codos y muñecas; postura relajada de los dedos.
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
4. Desde el menú principal, inicia el tutorial. Los controles y ajustes se explican en [la documentación de RCP](Documentation/RCP-Tutorial.md).

Para compilar manualmente para Quest, selecciona Android en Build Settings y conserva las escenas habilitadas en este orden: `Boobstrap`, `1_MainMenu` y `2_Tutorial_RCP`. Los APK se generan localmente y no se incluyen en el repositorio.

## Estructura del repositorio

| Carpeta | Contenido |
| --- | --- |
| `Assets` | Escenas, scripts, modelos, materiales, prefabs y sus archivos `.meta`. |
| `Packages` | Dependencias y versiones de los paquetes. |
| `ProjectSettings` | Configuración del proyecto, física, XR y compilación. |
| `Documentation` | Guía de uso, capturas y registros de validación. |

`Library`, `Temp`, `Logs`, `UserSettings` y las compilaciones se excluyen mediante `.gitignore`. Unity reconstruye sus cachés al abrir el proyecto. Conserva los archivos `.meta`: contienen los identificadores que enlazan las escenas con sus recursos.

## Validación registrada

El [registro de presentación y Quest](Documentation/RCP-validacion-quest-presentacion.txt) conserva **235 comprobaciones correctas del editor**: 33 de presentación, 30 de RCP, 104 de ragdoll y agarre, y 68 de postura de brazos. También consta la compilación correcta de los scripts con Android seleccionado.

La comprobación de esas correcciones dentro de unas Quest físicas sigue pendiente. El tutorial guía la secuencia y la colocación; no mide fuerza, profundidad ni ritmo de compresión. El traslado es una interacción del escenario y no valida una técnica clínica de movilización.
