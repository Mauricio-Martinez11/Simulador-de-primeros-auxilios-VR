# Bienvenida y familiarización — 2 noviembre 2026

La escena Main presenta MenuWelcome antes de iniciar ejercicios. Se puede escuchar el recorrido o saltarlo para volver al menú principal (Tutorial, Salir y demás opciones). Solo al entrar en Tutorial se elige VR/MR. No se selecciona modalidad automáticamente.

Solo el menú Main incorpora un panel izquierdo con subtítulos por palabras y barra de audio, y un panel derecho con repetir, anterior, siguiente y saltar. Los paneles permanecen fijos junto al central y se recentran juntos. Se amplió la superficie de interacción de Meta para alcanzar los botones laterales.

La familiarización mantiene el avance automático por acción y audio terminado; los botones permiten navegar explícitamente. El tutorial evaluado no incorpora estos controles.

La voz de bienvenida está en Resources/RCP/Narration/MenuWelcome.wav. Los seis audios del inicio incluyen archivos Timing.json con posiciones de palabras de System.Speech.SpeakProgress. Se usa la frecuencia nativa de Sabina (16 kHz) para que AudioPosition coincida con la duración del WAV; el texto consulta el tiempo real del narrador. Tools/GenerateCprIntroTiming.ps1 regenera audio y tiempos juntos; GenerateCprNarration.ps1 deriva estos diálogos a ese generador.

La mano del hombro se aproxima a la superficie mediante una inclinación calculada sobre la palma, conservando la forma de dedos y mano. No se desplaza la referencia de compresiones.

Verificación: CprMenuIntroChecks.Run comprueba bienvenida, límites de subtítulos, tiempos dentro del audio, superficie de rayos, anterior/siguiente, repetir y saltar; además ejecuta regresiones de estación y observación. Previsualizaciones en RCP-V2-Bienvenida.png y RCP-V2-Mano-hombro.png. No se generó APK; pendiente validación de lectura, láser y comodidad en Quest.

## Recentrado de los paneles del inicio (2 noviembre 2026)

Se reemplazó la espera fija de dos fotogramas por una recolocación única después de recibir los anclajes actualizados de OVRCameraRig. Se agrupan notificaciones Meta y OpenXR (trackingOriginUpdated), incluida la recuperación del foco del visor. Se espera al menos 0,35 segundos y una estabilidad breve de la pose; el límite de 1,5 segundos evita bloquearse si el usuario sigue moviéndose.

La introducción consulta la cámara activa al recentrar y mueve el Canvas completo, incluidos sus dos paneles laterales. El menú principal también se coloca delante al volver a activarse después de estar oculto. No hay seguimiento continuo de la cámara.

CprRecenterChecks añade pruebas para cambio tardío de origen, cuatro orientaciones de mirada, conservación de los laterales y reaparición del menú oculto. Log: Logs/V2-recenter-settled.log. El gesto físico de Meta debe comprobarse en Quest con la próxima compilación; no se generó APK.

El selector de modalidad y los diálogos de preparación ahora son hijos de la estación: permanecen encima del modelo y se mueven con él al recentrar. No se recentran por separado respecto del visor. La bienvenida se regrabó para explicar el retorno al menú principal.

## Ajustes posteriores: voz, observación y hombro

Welcome ahora presenta directamente la elección de modalidad sin repetir saludo. Se regeneraron MenuWelcome, Welcome y Mixed con la adaptación fonética de «mixta» a /ks/ (texto visible sin cambios y tiempos de subtítulos remapeados). Se retiraron «caso simulado» de Step03/Confirm03, «entorno simulado» de Confirm01 y referencias innecesarias al simulador de Step02/Step05; se regeneraron sus WAV en Resources y las copias de Audio/RCP existentes.

Los marcadores del paso 1 están a 1,15 m hacia los laterales del participante, a 85 grados de cada lado y 1,5 cm sobre la referencia del suelo del rig. Radio 22 cm y línea más gruesa. Vista comprobada en RCP-V2-Observacion-suelo.png, sin intersección con paciente ni paneles en la estación actual.

El contacto del hombro se calcula sobre la zona central de la palma, no la muñeca. Tras inclinar la mano, se vuelve a consultar la superficie real de piel/ropa para apoyar la palma con 2 mm de margen. Ver RCP-V2-Mano-hombro-lateral.png. La malla conserva la forma original.

Validación Unity: CprObservationFeedbackChecks.RunLatest, log Logs/V2-direct-narration-floor.log, salida 0. Regresiones de menú, tiempos de subtítulos, recentrado, observación y práctica aprobadas. Pronunciación y contacto deben confirmarse perceptualmente en Quest. No se generó APK.

## Paso 2: contacto y movimiento suave

CprShoulderContact conserva la mano al tocar (radio 9 cm), libera al retirarla (14 cm), tolera 0,25 s de pérdida de seguimiento y permite recolocar cualquiera de las dos manos. Durante la voz se reconoce contacto, sin completar el ejercicio. Mantener la mano quieta no completa el paso: se requiere ida y vuelta de al menos 1,5 cm, retorno a 8 mm y 0,5 s de interacción. Saltos de más de 5 cm por muestra no completan el gesto. La animación del tronco y ropa está acotada a 8 mm y no alimenta la medición; la mano visual acompaña el desplazamiento. Al retirar la mano recupera su representación seguida.

Se regeneró Step02.wav con una indicación directa de apoyo y movimiento suave, sin coletillas de simulación. Referencia de la indicación: https://www.redcross.org.uk/first-aid/learn-first-aid/unresponsive-and-not-breathing

La mano de guía usa contacto central de palma sobre superficie real y corrección continua de despeje para dedos. Prueba geométrica en la escena actual: mínimo despeje 2 mm y contacto de palma 2 mm. Ver Logs/V2-shoulder-contact-verified.log y RCP-V2-Mano-hombro-lateral.png. No equivale a comprobación de seguimiento físico en Quest.

CprShoulderChecks.Run aprobado: contacto estático no avanza; retirada libera; derecha aceptada; salto brusco no amplifica; movimiento durante voz no avanza; ida/vuelta suave completa; pausa limpia gesto. Incluye regresiones de práctica, menú, observación y recentrado. Logs/V2-shoulder-interaction.log. No APK generado.


## Modelo movido en el editor
BuildLayout ahora recalcula las referencias del pecho y del hombro aunque la estación esté serializada. Reposiciona la estación conservando la pose mundial elegida para el paciente. Las pantallas, referencias de compresión y objetos auxiliares mantienen su distribución relativa. El ajuste se ejecuta al iniciar; no modifica la escena guardada ni mueve el paciente a su ubicación anterior. CprMovedPatientChecks verifica conservación de pose, traslación conjunta de referencias y ausencia de deriva al repetir. Log Logs/V2-moved-patient.log: PASS y salida 0. Sin APK.

## Distribución guardada del paciente y las pantallas

Corrección posterior: NO se reposiciona la raíz de la estación para recalcular referencias. Se conservan las posiciones del paciente y las pantallas elegidas en la escena, actualizando solo pecho y hombro. Inicio y recentrado usan el centro de la estación como referencia del visor, no el pecho desplazado. Se corrigieron en 2_Tutorial_RCP.unity las dos referencias y las tres guías de pecho existentes, sin cambiar los transforms del modelo ni de los paneles. Esta decisión sustituye la descripción anterior de reposicionar la estación al pecho.

CprMovedPatientChecks comprueba conservación de posiciones de paciente y paneles, referencias tras mover paciente e idempotencia. Prueba sobre la escena guardada: Logs/V2-saved-scene-layout.log. No APK.

## Conteo continuo y avisos visuales de compresiones

El detector exigía 160 ms seguidos dentro de 8 mm de reposo para armarse, condición que puede no cumplirse al empezar inmediatamente una secuencia continua. Se reduce a 40 ms de muestras medidas; se conservan rechazo de saltos, calibración, zona, ida/vuelta y bloqueo durante audio. CprContinuousStartChecks verifica arranque sin pausa a 110, 150 y 190/min (18, 25, 30 ciclos en 10 segundos; rápidos clasificados) y regresiones de oclusión, recolocación y puntuación.

CprLiveFeedback añade un panel lateral fijo de incidencias recientes con hora, ciclo, recorrido/retorno/ritmo y desfase respecto del pulso. Muestra además el estado que impide contar. La pérdida de seguimiento se identifica sin penalización ni destello de error. El aviso periférico rojo dura 0,65 s, alfa máximo 0,10, separación mínima 1,5 s; no usa audio ni mueve las pantallas. Las correcciones habladas automáticas durante compresiones se eliminan, manteniendo instrucción inicial y pitido.

Log Logs/V2-continuous-start.log: arranque continuo, regrip, fiabilidad y registro visual aprobados. Son pruebas sintéticas en Unity; no se reprodujo todavía el caso físico concreto del usuario en Quest. Comprobar conteo, texto del estado y visibilidad/confort del aviso en el dispositivo. Sin APK.

## Mano editable y recuperación de compresiones (ajuste pendiente del usuario)

Se dejó visible y seleccionable AJUSTAR_HOLOGRAMA_HOMBRO en la escena 2_Tutorial_RCP. El usuario puede mover/girar este objeto y guardar la escena; CprShoulderGuideOverride aplica su pose y malla al holograma funcional y oculta solo la copia de edición durante Play. No borrar ni ocultar la copia de edición hasta que el usuario termine de colocarla. Al terminar se debe conservar la referencia funcional.

El detector recupera una referencia desplazada después de observar una excursión ascendente y un cambio de dirección descendente; no recalibra por mantener una mano quieta abajo. Pruebas con reposo desplazado ±2,5 cm pasan, además de arranque continuo a 110/150/190 cpm y las regresiones de oclusión/recolocación. La evaluación del pulso usa el comienzo de bajada observado (tras 3 mm) y no el instante posterior de activación a 18 mm.

Se reemplazaron las tiras de UI por un shader de aviso periférico sobre la cámara: centro despejado, bordes rojos graduales, ZTest Always y estéreo; duración 0,65 s y separación mínima 1,5 s. Historial a la izquierda del panel de pasos, sin voces correctivas durante compresiones. Título PRÁCTICA RCP · PASOS; eliminados A TU RITMO de los encabezados.

Logs/V2-final-compression-fixes.log y Logs/V2-editable-visual-check.log: salida 0. La segunda prueba verifica la mano visible, el respeto de su pose manual y renderiza Documentation/RCP-V2-Aviso-rojo.png. Pendiente prueba física en Quest para confirmar la causa concreta del conteo intermitente. No APK.

## Colocación final elegida y dos movimientos del hombro

Se conserva Transform (posición, giro y escala) de AJUSTAR_HOLOGRAMA_HOMBRO colocado por el usuario. La copia de edición queda oculta, manteniendo componente y referencia funcional. Se elimina Zona de reposo únicamente del paso Response; pecho/respiración conservan su indicador.

Al terminar la explicación, mantener contacto inicia dos movimientos suaves de 0,7 s cada uno. Al terminar el segundo, la animación vuelve exactamente al reposo y se completa el paso; no exige un retorno manual a una coordenada concreta. Retirar la mano libera y permite recolocar. Se regeneró Step02 para corresponder con esta interacción.

La animación desplaza el paciente completo (piel y ropa) y la guía con el mismo offset de hasta 8 mm. Usa origen fijo para evitar deriva acumulativa. Prueba de la posición actual elegida: no se requirió corrección de malla (0 m), poses coherentes a lo largo de 140 muestras, círculo oculto. Logs/V2-final-shoulder-placement.log. Lógica y regresiones: Logs/V2-shoulder-two-moves.log. Ambas salidas 0. Sin APK; pendiente prueba perceptual en Quest.
