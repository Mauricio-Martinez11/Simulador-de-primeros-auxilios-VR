# Tutorial continuo con demostraciones ilustradas

La escena `2_Tutorial_RCP` presenta cuatro pantallas fijas: progreso, demostración del paso, ritmo y recorrido virtual. A la derecha queda el ajuste de altura. Las tarjetas usan esquinas redondeadas, una fila destacada para el paso actual, números grandes y mensajes breves. El paciente y todas las referencias siguen agrupados en `RCP_Estacion_Fija`.

## Aprender sin botones de avance

No hay botones de comenzar, confirmar, elegir respuesta o siguiente en el recorrido. Las acciones observadas producen una confirmación breve y el siguiente paso. No se declara completada una acción por haberse reproducido su animación.

| Paso | Demostración | Condición de avance |
| --- | --- | --- |
| Preparación | Vista del paciente y orientación | Permanecer cuatro segundos al costado, dentro de la zona de práctica. |
| Seguridad | Personaje que observa ambos lados | Orientar el visor durante 0,65 s hacia cada zona iluminada. Es una aproximación de observación, no detección de peligros reales. |
| Respuesta | Contacto suave en el hombro | Una mano medida cerca del hombro durante 0,7 s. La pregunta se enseña; no se reconoce voz. |
| Respiración | Observación de cara y pecho, seguida de solicitud de ayuda; ilustración sin textos | Cinco segundos acumulados orientado al pecho o límite de diez segundos desde el inicio. El panel derecho indica que el caso simulado no responde y no respira normalmente. Pausa y pérdida de foco detienen el reloj. |
| Ayuda | Personaje con teléfono | Cerrar cualquiera de las manos o hacer pinza sobre el teléfono holográfico; mantenerlo un segundo junto al oído. La mano visible adopta el agarre del teléfono. No hay llamada real ni reconocimiento de palabras. |
| Manos | Derecha primero; izquierda encima | Entrada inmediata en la zona y orientación válidas, sin carga de sujeción. La unión se conserva ante oclusiones; una separación o alejamiento medidos y sostenidos la liberan. |
| Compresiones | Manos superpuestas, bajada y retorno | Completar el bloque configurable de ciclos medidos; inicialmente 30. |
| Revisión | Resultados por criterio | Repetir o salir con los controles opcionales. |

El paso de ayuda procede del perfil proporcionado por el autor: sección 3.14.4, cuadro 3.50, estado `CallForHelp` («Solicitar ayuda y DEA dentro del caso simulado»). El teléfono es la nueva forma de representar esa confirmación sin botón. No se añade marcación de un número, conversación con una central ni aplicación interactiva de un DEA.

Para el contenido de RCP de personas sin formación sanitaria se conserva respuesta y respiración, sin exigir búsqueda del pulso. Referencia: [AHA, Adult Basic Life Support](https://cpr.heart.org/en/resuscitation-science/cpr-and-ecc-guidelines/adult-basic-life-support). Esta versión sigue practicando compresiones; no incorpora ventilaciones.

## Animaciones y controles

Los seis clips utilizan ilustraciones con sombreado suave: paciente con camiseta verde, ayudante azul, proporciones naturales y detalle en las manos. Los gestos grandes se muestran mediante poses sucesivas nítidas; la compresión usa movimiento continuo de la ilustración mediante una deformación local suave, conservando fijo el resto del paciente. Son MP4 locales de seis segundos, 1280 × 720, 24 fps, H.264 baseline, sin sonido incorporado. El bucle de compresiones muestra 11 ciclos en seis segundos (110/min). Se repiten mientras permanece el paso. Las ocho locuciones en español son independientes. No hay streaming ni descarga durante la práctica.

`CprLessonVideo` utiliza un único reproductor y una textura de destino. Se pausa al perder foco o pausar la sesión. Cada clip tiene una imagen de respaldo si el decodificador no está disponible. Los vídeos son demostraciones: no alimentan el detector ni suman puntos.

Los seis atlas originales y los prompts están en `ArtSource/RCP`; se crearon con la herramienta integrada `image_gen`. `Tools/GenerateCprLessons.py` permite reconstruir los vídeos a partir de esos archivos con Pillow y ffmpeg, sin servicios externos. Los recursos finales están en `Assets/Videos/RCP`. `CprLessonVideoTools` configura imágenes 2D y asigna los vídeos al preparar la escena. Los atlas de trabajo y el codificador no forman parte de los recursos de la aplicación.

El botón lateral «Opciones de práctica» permite pausar, repetir la voz, silenciar, terminar o volver al menú. Estos controles están ocultos inicialmente y no son necesarios para superar un paso. La altura sigue siendo opcional. Las guías de manos y su tolerancia a oclusiones se conservan. Los controles opcionales aceptan el láser de manos/mandos y contacto cercano.

## Métricas y alcance

Al iniciar cada bajada, la respuesta de pulsación es verde si coincide con la ventana del pitido, o roja si se adelanta, se atrasa o repite demasiado pronto. La referencia fija es 110/min; no se mueve según la profundidad. La aguja de recorrido permanece visible: si no hay señal fiable conserva en gris la última lectura, sin inventar ciclos.

El ritmo muestra un valor únicamente cuando existen tres ciclos continuos. Su pulso visual y sonoro es una guía, no una lectura cardíaca. El recorrido representa desplazamiento virtual de manos; no mide fuerza ni profundidad torácica física. La medición sigue separada de las animaciones.

La preparación aporta hasta 20 puntos por cinco etapas observadas. Los criterios de ejecución conservan colocación 20 (menos dos puntos por alejamiento o separación observados), recorrido 25, ritmo 25 y retorno 10, multiplicados por la fracción del bloque completada. Se restan dos puntos por pausa prolongada con señal válida, con máximo diez. Los fallos técnicos de seguimiento y las pausas manuales no restan puntos. Sin datos suficientes no se publica calificación.

La validación automática verifica secuencia sin clics, acciones, recuperación del seguimiento, métricas, reproducción de cada clip, controles opcionales, altura y texto. La prueba de oclusiones, comodidad, alcance del teléfono, legibilidad y rendimiento con manos reales en Quest sigue pendiente. No se genera APK como parte de estos cambios.


## Panel de respuesta y respiración

`CprBreathingPanel` se crea al inicializar la estación y queda anclado arriba a la derecha de la cámara del visor. Solo aparece durante el paso de respiración; se oculta al pausar, perder el foco, finalizar el paso o desactivar la estación. No recibe rayos ni bloquea botones.

Se muestran el estado del paciente del caso y el tiempo de observación. El trazo es una ilustración programada: dos latidos con sonido, seguidos de línea plana y silencio. No se obtiene un ECG, frecuencia cardíaca ni saturación de sensores clínicos. Para alumnos se mantiene comprobar respuesta y respiración; no se exige palpación del pulso ni auscultación. Las locuciones de los pasos 3 y 4 se actualizaron. La ilustración y el vídeo de respiración no contienen textos añadidos; los mensajes pertenecen a la interfaz.

Comprobaciones del 20/10/2026: 15 casos de lógica de respiración, 104 casos de lógica de estación y 523 comprobaciones de escena, interfaz y ciclo de vida. Además se conservan 917 comprobaciones de teléfono, continuidad de medición y respuesta temporal. Son pruebas controladas de Unity Editor, no una revisión con alumnos ni una validación clínica.


### Fundamento del monitor del caso

El monitor y sus dos sonidos ilustran la evolución narrativa del paciente virtual. La cantidad de latidos es una elección audiovisual del prototipo, no una recomendación clínica. Su reloj comparte el tiempo del paso; al pausar se detiene y al reanudar no repite los latidos ya emitidos. Desaparece al salir de la comprobación de respiración. No concede puntos ni determina el avance.

El recorrido para alumnos mantiene valoración de respuesta y respiración. La AHA 2025, parte 7, apartado 6.1, basa el reconocimiento por personas no sanitarias en falta de respuesta y respiración ausente o anormal. No se enseña el monitor como medición ni como criterio aislado para iniciar RCP.

Fuente: [AHA 2025 — Adult Basic Life Support, apartado 6.1](https://cpr.heart.org/en/resuscitation-science/cpr-and-ecc-guidelines/adult-basic-life-support).
