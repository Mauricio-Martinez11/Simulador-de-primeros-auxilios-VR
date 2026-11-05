# Estación fija de práctica guiada de RCP

Actualización del 20/10/2026. Escena `Assets/Scenes/2_Tutorial_RCP.unity`; entrada desde **Tutorial** en `1_MainMenu`. El paciente empieza tumbado. El traslado experimental permanece desactivado con `stationaryPractice`. La guía completa está en [Tutorial continuo con demostraciones](RCP-Tutorial-Animado.md).

## Recorrido actual

1. Situarse al costado del paciente durante cuatro segundos.
2. Orientar el visor hacia las dos zonas del entorno durante 0,65 s en cada una.
3. Tocar suavemente el hombro durante 0,7 s y preguntar si responde. Se comprueba la proximidad de la mano, no el contenido de la voz.
4. Comprobar respiración orientándose hacia el pecho: cinco segundos acumulados o un máximo de diez desde el inicio. El panel derecho muestra el estado simulado y acompaña a la vista. Pausa y pérdida de foco suspenden el reloj.
5. Tomar el teléfono holográfico con cualquiera de las manos y mantenerlo un segundo junto al oído para representar la solicitud de ayuda y DEA.
6. Acercar primero la derecha al pecho, en la zona y orientación del holograma. Colocar la izquierda encima. El agarre es inmediato; no hay carga para sujetar las manos.
7. Realizar el bloque configurable de 30 ciclos virtuales. Bajar con el pitido y el tramo verde; regresar al reposo antes de repetir.
8. Revisar **Ver puntos** y **Ver incidencias**, repetir o salir.

El bloque de 30 ciclos define el ejercicio de interfaz, no cuándo detener una RCP real. La práctica no incluye ventilaciones ni aplicación interactiva de un DEA. El recorrido para alumnos mantiene respuesta y respiración, sin palpación del pulso ni auscultación.

## Interfaz, voz y altura

Las pantallas de progreso, demostración, ritmo y recorrido permanecen fijas respecto de la estación. El control de altura comparte esa raíz. Solo el panel de estado simulado del paso de respiración acompaña la cámara, arriba a su derecha; se oculta al salir del paso, pausar o perder foco.

Las ilustraciones y su vídeo no llevan rótulos superpuestos. Las explicaciones se presentan como texto de interfaz y locuciones WAV locales en español. **Opciones de práctica** permite pausar, repetir voz, silenciar, terminar o volver al menú.

El control de altura ofrece botones de 5 cm, deslizador, **A mi altura** y **Restablecer**, entre 0 y 1,2 m. Durante compresiones requiere pausa y posterior recolocación de manos para renovar el reposo. No se combinan muestras de referencias diferentes.

## Medición y seguimiento

`CprTrackedHands` aporta muestras de manos o mandos. `CprFlow` conserva la unión visual de la postura durante oclusiones; únicamente una separación o alejamiento fiables sostenidos liberan las manos. Las guías holográficas no alimentan al detector.

`CprCompressionDetector` estima recorrido virtual desde el reposo medido. La referencia de 5–6 cm virtuales no equivale a profundidad torácica física. Cada ciclo requiere bajada y retorno. El tórax y las manos visibles tienen un desplazamiento limitado de contacto para no atravesar visualmente el pecho.

La medición admite cambios compatibles de mano fuente y cortes muy breves. Si falta señal fiable, conserva la última lectura en gris y pausa el registro sin crear ciclos. Las pérdidas técnicas no descuentan puntos. Mirar fuera de la zona de manos puede hacer que las cámaras del visor pierdan el seguimiento; conservar la unión no recupera información que el dispositivo no observa.

`CprPacing` comparte el reloj con el pitido: referencia inicial 110/min. La pulsación se clasifica al iniciar la bajada. Verde significa **A tiempo**; rojo identifica adelanto, retraso o repetición demasiado rápida. El ritmo calculado es una métrica distinta, obtenida de los ciclos observados.

## Puntuación y registros

| Criterio | Máximo | Base |
| --- | ---: | --- |
| Preparación | 20 | Cinco etapas antes del bloque. |
| Colocación | 20 | Menos 2 por alejamiento o separación observados. |
| Recorrido | 25 | Proporción de ciclos dentro de la referencia virtual. |
| Ritmo | 25 | Proporción de ventanas evaluables dentro de 100–120/min. |
| Retorno | 10 | Proporción de ciclos con retorno completo. |

Los criterios de ejecución se multiplican por la fracción de ciclos completada. Las pausas prolongadas con seguimiento válido restan 2 puntos, hasta 10; las pérdidas técnicas y pausas manuales no generan esa penalización. Sin suficientes ciclos y ventanas temporales no se publica nota.

`CprAttemptReport` conserva el desglose, incidencias y respuesta temporal en JSON y TXT dentro de `Application.persistentDataPath/RegistrosRCP`. El informe representa un intento del prototipo, no certificación clínica.

## Pruebas y límites

`CprBreathingChecks` verifica inicio, pausa, pérdida de foco y límite temporal del paso. `CprStationPlayChecks` comprueba panel derecho, visibilidad, movimiento, textos y controles. `CprInteractionFeedbackChecks` cubre teléfono con ambos lados, continuidad del recorrido y respuesta al pitido.

La ejecución del 20/10/2026 pasó 15 comprobaciones de respiración, 104 de lógica de estación, 523 de escena/interfaz y 917 de interacción. Las capturas de estas pruebas proceden de entradas controladas en el editor. Falta probar esta actualización en Quest 3S para valorar seguimiento real, comodidad, rendimiento y legibilidad. No se generó un APK en esta intervención.


### Fundamento del monitor del caso

El monitor y sus dos sonidos ilustran la evolución narrativa del paciente virtual. La cantidad de latidos es una elección audiovisual del prototipo, no una recomendación clínica. Su reloj comparte el tiempo del paso; al pausar se detiene y al reanudar no repite los latidos ya emitidos. Desaparece al salir de la comprobación de respiración. No concede puntos ni determina el avance.

El recorrido para alumnos mantiene valoración de respuesta y respiración. La AHA 2025, parte 7, apartado 6.1, basa el reconocimiento por personas no sanitarias en falta de respuesta y respiración ausente o anormal. No se enseña el monitor como medición ni como criterio aislado para iniciar RCP.

Fuente: [AHA 2025 — Adult Basic Life Support, apartado 6.1](https://cpr.heart.org/en/resuscitation-science/cpr-and-ecc-guidelines/adult-basic-life-support).
