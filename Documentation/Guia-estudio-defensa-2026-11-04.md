# Guía de estudio y defensa del simulador RCP

Preparada el 4 de noviembre de 2026 a partir del DOCX actual y del código del proyecto. Es material de estudio independiente: no modifica la tesis. Describe la implementación inspeccionada, no certifica que todos los requisitos hayan superado pruebas en Quest. Los dos últimos ajustes de hologramas compilan, pero todavía necesitan comprobación visual en el dispositivo. El APK instalado puede corresponder a una compilación anterior.

## 1. La idea que debes poder explicar sin memorizar

Tu título es «Diseño y desarrollo de un prototipo de simulador de primeros auxilios en realidad virtual para la práctica guiada de reanimación cardiopulmonar (RCP)».

Una respuesta de presentación:

> Desarrollé un prototipo de realidad virtual para apoyar la práctica guiada de la secuencia de RCP en el contexto de COPE. El participante interactúa con un paciente virtual mediante sus manos, recibe instrucciones y demostraciones, ejecuta acciones y obtiene retroalimentación y un resumen del intento. El sistema observa posición, orientación, desplazamiento y tiempo. Su evaluación corresponde a esas variables virtuales y complementa la formación presencial.

La necesidad que aborda es disponer de práctica repetible, guía y retroalimentación básica, ajustadas al contexto documentado de COPE. No necesitas afirmar que reemplaza al instructor, que aumenta por sí solo la supervivencia o que certifica a una persona. La utilidad del prototipo y la eficacia clínica son preguntas diferentes.

Tu aporte de ingeniería está en convertir criterios e instrucciones en una interacción verificable: entrada de manos → condición de un paso → evento reconocido → indicador → evaluación → informe. El jurado puede seguir esa cadena.

## 2. Objetivos: qué hiciste y con qué lo defiendes

El objetivo general del documento es desarrollar un prototipo funcional orientado a la familiarización con la secuencia de RCP, repetición del procedimiento y retroalimentación básica durante su práctica guiada en COPE.

| Objetivo específico del documento, resumido | Cómo lo explicas | Evidencia que debes localizar |
|---|---|---|
| Analizar criterios de COPE y BLS/SVB | Identifiqué usuarios, secuencia, indicaciones y límites de lo observable. | Entrevistas, requisitos y referencias utilizadas. |
| Diseñar el flujo y sus criterios | Transformé las acciones en estados y condiciones para avanzar. | Diagramas, casos de uso y `CprPracticeSession`. |
| Modelar e integrar elementos 3D e interactivos | Integré paciente, escenario, manos guía, paneles y teléfono. | Escena de Unity, recursos y demostración en visor. Distingue integración de recursos de modelado original. |
| Implementar interacción, guía y retroalimentación | Programé reconocimiento de manos, ciclos, métricas, mensajes, audios y resultados. | Código, pruebas y recorrido funcional. |
| Validar con COPE y usuarios representativos | Recogí observaciones mediante instrumentos y las relacioné con mejoras. | Listas de cotejo, rúbricas, encuestas y fichas de revisión identificadas por versión. |

«Lo implementé» no demuestra automáticamente «lo validé». Para el quinto objetivo debes mostrar instrumentos y resultados, no solamente abrir Unity.

## 3. COPE, BLS/SVB y decisiones del software

Según tu documento, COPE usa BLS/SVB como referencia principal para RCP. BLS significa Basic Life Support; SVB es Soporte Vital Básico. Las entrevistas explican el contexto institucional y sus necesidades. Las guías aportan el fundamento sanitario; el código define cómo representar una parte de ese contenido en VR. El ATLS aparece como referencia contextual de atención al trauma, no como base principal del tutorial RCP.

La guía AHA 2025 distingue al público no sanitario de los profesionales: el reconocimiento por personas no sanitarias se basa en respuesta y respiración ausente o anormal, no en buscar pulso. Para adultos, describe una frecuencia de 100–120 compresiones/min, profundidad física de al menos 5 cm evitando superar 6 cm y retorno completo. Estos criterios clínicos sirven como referencia, pero el desplazamiento de manos que calcula tu aplicación no demuestra profundidad torácica real. Fuente: [AHA, soporte vital básico del adulto](https://cpr.heart.org/en/resuscitation-science/cpr-and-ecc-guidelines/adult-basic-life-support).

No presentes la selección implementada como un curso BLS completo. Tu alcance actual excluye la práctica de ventilaciones y desfibrilación. Treinta movimientos son el bloque de ejercicio configurado; completar ese bloque no significa que en una emergencia real se deba terminar la actuación. Tampoco significa que el prototipo implemente un ciclo completo 30:2.

| Decisión | Procedencia que debes explicar |
|---|---|
| Referencia de frecuencia 100–120/min | Referencia sanitaria documentada. |
| Zona verde de 5–6 cm virtuales | Adaptación educativa de una referencia; la variable medida es otra. |
| Derecha abajo e izquierda arriba | Convención de reconocimiento del software; no una obligación universal de la técnica. |
| Bloque de 30 ciclos | Diseño de esta práctica. |
| Dos movimientos del hombro de 0,7 s | Animación y condición de avance del software. |
| Radio, ángulos y tiempo de liberación de manos | Tolerancias de interacción. |
| Nota sobre 100 y descuentos de 2 puntos | Modelo de evaluación del prototipo, no escala clínica AHA. |
| Confirmar alineación con una almohada | Decisión del participante; no reconocimiento automático del objeto. |

## 4. Recorrido completo: lo que ve el usuario y lo que comprueba el sistema

### Antes de los pasos

`CprMenuWelcome` prepara bienvenida y ensayo de controles. El usuario puede escuchar las instrucciones o ir al menú. La familiarización comprueba giros de cabeza, presencia de manos y una selección de ensayo. Permite navegación y omisión; no constituye una evaluación de RCP. Después se recupera el menú principal.

`SceneLoader` carga `2_Tutorial_RCP`. `CprStationPreparation` permite elegir entorno virtual o realidad mixta y configurar apoyo opcional. La estación contiene al paciente y los paneles; se conserva su distribución y no se usa un panel que siga continuamente cada movimiento de cabeza.

Hay además un estado de preparación: permanecer en la zona prevista durante aproximadamente 4 s permite entrar a seguridad, una vez liberada la explicación. Es una condición de interacción del programa, no un paso clínico adicional.

### Los seis pasos

| Paso | Acción del participante | Qué comprueba realmente el código | Qué NO comprueba |
|---|---|---|---|
| 1. Seguridad | Observar ambos lados del entorno. | Dirección del visor o mirada hacia las zonas; mantiene cada observación unos 0,65 s y recuerda ambos lados. | No identifica peligros reales con visión artificial. |
| 2. Respuesta | Apoyar una mano en el hombro señalado. | Mano fiable cerca de la referencia; mantiene el contacto durante dos movimientos ilustrativos de 0,7 s cada uno. | No reconoce que pronunciaste la pregunta ni mide una sacudida física. |
| 3. Respiración | Orientarse hacia cara y pecho y seguir el aro. | Dirección del visor hacia el pecho: ángulo menor de 22°, distancia menor de 2,5 m; acumula 5 s de observación configurada, con límite de 10 s desde que empezó. | No usa eye tracking ni escucha respiración, latidos o voz. |
| 4. Ayuda | Tomar el teléfono con cualquiera de las manos y acercarlo al oído. | Agarre, proximidad al objeto y después proximidad a la cabeza durante aproximadamente 1 s. | No realiza una llamada ni comprueba lo que dices al operador. |
| 5. Manos | Derecha en la guía inferior e izquierda encima. | Distancias, orientación de palma y dedos, orden vertical y calibración de las referencias. | No valida fuerza, codos rectos ni todos los dedos entrelazados uno a uno. |
| 6. Compresiones | Bajar y retornar, siguiendo las referencias. | Ciclos observados, recorrido virtual, frecuencia, retorno, colocación e incidencias. Cierra al alcanzar 30 ciclos. | No mide fuerza ni profundidad real de un tórax. |

Las condiciones están en `CprPracticeSession.Tick`; las observaciones del entorno y teléfono en `CprScenarioActions`. El sistema no avanza simplemente por reproducir un video ni por pulsar «Siguiente». Las explicaciones y confirmaciones también deben haber terminado para permitir el avance normal.

En respuesta, una mano entra en contacto a menos de 9 cm de la referencia y se libera al alejarse más de 14 cm. Una pérdida de seguimiento superior a 0,25 s reinicia ese contacto. Estos radios se refieren al punto representativo de la mano, no a una prueba física de contacto de todos sus dedos.

## 5. Cómo obtiene la aplicación la posición de tus manos

`CprTrackedHands` consulta el seguimiento proporcionado por Meta. El programa usa referencias de muñeca y articulaciones de los dedos para construir una pose: posición y rotación.

La dirección de los dedos se estima desde la muñeca hacia la base del dedo medio. Otra dirección, entre índice y meñique, permite construir mediante producto vectorial una normal de la palma. La posición representativa aproxima el talón de la mano usando esas articulaciones. No es solo el centro visual de un modelo 3D.

Cada `CprHandSample` lleva la pose y banderas como `valid`, `reliable` y `controller`. Si se usan mandos, se aplica un desplazamiento configurado para aproximar la palma. La aproximación de un mando no equivale al esqueleto de una mano real.

Explicación oral:

> El visor suministra posiciones y orientaciones. Mi capa de entrada las convierte en una referencia común de mano para que el resto del sistema pueda comparar ubicación, orientación y movimiento. La guía visual no es el dato de entrada.

## 6. Colocación, hologramas y oclusiones

`CprFlow` gestiona la colocación. Primero reconoce la derecha, después la izquierda. `Matches` compara distancia, normal de palma y dirección de dedos. En la configuración actual, la captura inferior permite 8,5 cm y la superior 12 cm; los ángulos base son 45° para palma y 65° para dedos. La captura superior usa tolerancias adicionales definidas en código.

El orden también exige que la izquierda esté por encima, con separación lateral limitada. No basta con tener dos manos cerca una al lado de otra.

`LowerLatched` significa que la mano inferior fue reconocida; `StackLatched`, que se estableció la colocación completa. Los colores de la guía representan esos estados. Los hologramas son mallas con material transparente, colocadas en referencias anatómicas. No son imágenes captadas de tus manos.

La oclusión ocurre cuando una mano tapa la otra ante las cámaras. Para no romper constantemente la postura, el programa conserva el estado establecido y usa una mano visible y fiable para medir el movimiento, con una referencia de reposo propia para cada mano. La izquierda superior tiene prioridad salvo que se esté manteniendo una fuente continua válida.

Conservar la unión visual NO autoriza fabricar ciclos cuando no hay datos. Si faltan muestras fiables, la medición se suspende. Una mano oculta tampoco permite asegurar que su postura física sigue siendo correcta: es una limitación conocida.

Durante compresiones, la separación o alejamiento claramente observado debe mantenerse 0,18 s para liberar la unión. Después se pueden colocar nuevamente las manos dentro del mismo paso. No se borra el total de ciclos ni se reinicia automáticamente la explicación inicial.

Las guías de pecho se ubican desde la referencia anatómica y la superficie de contacto. La mano de hombro admite una postura editada; el último ajuste calcula su altura contra la superficie actual. En ambos casos hay que comprobar visualmente el resultado en la compilación que se vaya a presentar.

## 7. Qué es la «profundidad» y cómo sube la barra

La expresión más precisa en tu proyecto es **recorrido virtual de las manos**. No hay sensor de presión ni de fuerza.

Al colocar las manos, el detector captura una altura de reposo para cada una respecto del pecho. En cada muestra vuelve a calcular la distancia sobre la dirección normal al pecho.

```text
n = dirección que sale perpendicularmente del pecho
h_actual = producto_escalar(posición_mano - posición_pecho, n)
recorrido = h_reposo - h_actual
centímetros_virtuales = máximo(0, recorrido) × 100
```

Ejemplo: al calibrar, la mano está a 12 cm de la referencia; al bajar queda a 6,5 cm. El recorrido registrado es 12 − 6,5 = 5,5 cm virtuales. No importa que la estación se encuentre más alta en el mundo: se utilizan posiciones relativas. Si se cambia la altura o se recentra la estación, se pide renovar la colocación para recuperar una referencia coherente.

La barra gráfica tiene una escala de 0 a 8 cm. Su llenado y cursor se calculan aproximadamente así:

```text
fracción_barra = limitar(recorrido / 0,08, entre 0 y 1)
altura_gráfica = 243 × fracción_barra
```

Con 5,5 cm ocupa el 68,75 % de esa pista. La franja verde corresponde a 5–6 cm virtuales configurados. La señal del último ciclo muestra el máximo recorrido de ese ciclo; no es necesariamente la posición actual.

La lectura visual tiene un filtro breve para disminuir vibraciones: `alpha = 1 − exp(−dt / 0,025)`. Ese suavizado se usa para presentación; el pico del ciclo se calcula con la lectura del detector. Si falta seguimiento, se conserva la lectura con apariencia de dato retenido, en vez de inventar desplazamiento nuevo.

Respuesta al jurado:

> La barra no representa fuerza. Representa la diferencia entre la referencia de reposo y la posición observada de la mano, proyectada sobre la normal del pecho. Convierto metros a centímetros y lo presento en una escala visual. No existe una conversión de ese valor a presión o profundidad física real.

## 8. Qué cuenta como una compresión

`CprCompressionDetector` tiene fases de reposo, bajada y subida. No suma un ciclo por cada fotograma, por cada pitido o por tocar el pecho.

1. Comprueba calibración, seguimiento, ubicación y continuidad de las muestras.
2. Reconoce reposo para preparar el siguiente movimiento.
3. Al bajar al menos 1,8 cm, entra en fase de bajada y registra el inicio de presión para compararlo con el metrónomo.
4. Guarda el máximo recorrido alcanzado.
5. Si sube al menos 0,6 cm respecto del máximo, reconoce la fase de subida.
6. Si vuelve a 0,8 cm o menos de la referencia, emite un ciclo con retorno completo.
7. Si vuelve a bajar 1,2 cm desde el punto de retorno parcial antes de llegar al reposo, puede emitir el ciclo previo con retorno incompleto y reconocer la siguiente bajada.

Hay filtros para picos muy breves: al menos 0,16 s desde la activación del ciclo y 0,25 s entre eventos aceptados. Estos valores son criterios técnicos, no recomendaciones clínicas. Un movimiento extremadamente rápido puede ser rechazado como pico y no entrar al conteo. Dentro de lo que se reconoce, un ciclo rápido o corto puede contarse y calificarse como incorrecto.

**Reconocido no significa correcto.** El contador muestra ciclos observados; los aciertos de recorrido, frecuencia y retorno se evalúan por separado. Hacer 30 ciclos no asegura 100 puntos.

Si no cuenta, las preguntas de diagnóstico son: ¿hay seguimiento fiable?, ¿está establecida la colocación?, ¿se capturaron ambas referencias?, ¿se volvió al reposo?, ¿hay movimiento suficiente?, ¿está fuera de zona?, ¿la sesión está pausada o todavía explicando? No se debe concluir que el metrónomo dejó de funcionar.

## 9. Ritmo medido y pitido: dos cosas diferentes

El ritmo medido usa tiempos de ciclos terminados. Con N eventos:

```text
frecuencia = 60 × (N − 1) / (tiempo_último − tiempo_primero)
```

Se utilizan hasta los últimos 6 eventos; se requiere al menos 3 para presentar una frecuencia evaluable. Con seis eventos que abarcan 2,5 s hay cinco intervalos: 60 × 5 / 2,5 = 120/min. Si esos cinco intervalos abarcan 3 s, resulta 100/min. Si abarcan 2 s, resulta 150/min y queda fuera de la referencia.

No se divide simplemente el contador entre todo el tiempo de tutorial: eso mezclaría explicaciones, observación y práctica.

El metrónomo tiene otra función: dar una referencia estable. Se fija en el promedio del mínimo y máximo configurados: (100 + 120) / 2 = 110/min. Su intervalo es 60/110 ≈ 0,545 s. El sonido y el marcador usan un reloj compartido; el pitido coincide con la entrada del marcador en verde.

`CprPacing` compara el inicio observado de una bajada con el pulso de referencia. Admite 55 ms de adelanto y 120 ms de retraso antes de marcar adelantada o tardía. Identifica también bajadas adicionales en un mismo pulso. Las tolerancias son decisiones del diseño de interacción.

Puedes mantener 110/min y estar desfasado respecto del pitido: tu frecuencia puede ser adecuada aunque recibas un aviso de sincronización. **Los 25 puntos de ritmo usan la frecuencia calculada; no se resta otra penalización automática por cada destello rojo del metrónomo.**

## 10. Animaciones y demostraciones

Hay seis recursos de demostración, uno por paso. `CprLessonVideo` reproduce el clip local con `VideoPlayer`, lo dibuja en una `RenderTexture` y lo presenta en una `RawImage`. Usa bucle y audio desactivado porque las locuciones se reproducen por separado. Cuenta con una imagen de respaldo.

El paciente también tiene movimiento ilustrativo. El hombro realiza dos movimientos acotados; no se exige reproducir una sacudida exacta para que avance. La respuesta del pecho se calcula a partir del recorrido observado, multiplicado por 0,18 y limitado a 1,4 cm de desplazamiento visual. La animación se limita para evitar deformación excesiva.

**La animación no alimenta el detector.** Si el muñeco se mueve o el video reproduce compresiones, eso no crea eventos de alumno. La entrada viene de manos o mandos.

El desplome y traslado experimental siguen en el proyecto como código, pero la configuración actual tiene `stationaryPractice: 1`. No presentes el ragdoll o el arrastre como parte del flujo activo de esta estación.

## 11. Audios, errores y continuidad

`CprNarrator` utiliza la reproducción real del audio para determinar su final. Espera además 2 s de silencio para dar la locución por completa. Si el recurso no está disponible, mantiene texto y una duración alternativa estimada; eso es recuperación ante fallo, no el funcionamiento normal de una locución válida.

`CprStationAudio` coordina explicación, confirmación y señal de inicio. La sesión exige acción y permiso de avance, evitando cortar una explicación porque el participante actuó rápido.

Las correcciones de compresiones usan otra voz que no controla el bloqueo de explicación. `CprCorrectionVoice` no acumula una cola de errores antiguos: evalúa el problema actual. Exige que persista unos 0,65 s, deja al menos 8 s de silencio y evita repetir el mismo mensaje antes de 22 s. Si ya hay una voz hablando no la reinicia con cada error.

`CprLiveFeedback` presenta las últimas cinco líneas de incidencias. Los errores pueden activar un borde rojo tenue de 0,65 s, con separación mínima de 1,5 s entre activaciones. La pérdida técnica de seguimiento se informa sin descuento. El aviso rojo es retroalimentación; no demuestra por sí solo una penalización de puntos.

Pausa, pérdida de foco o recolocación suspenden la medición y evitan sumar durante la interrupción. Separar manos durante compresiones permite recuperarlas sin volver a la explicación inicial ni borrar los ciclos previos.

## 12. Puntuación: fórmulas y ejemplo

La nota sale de `CprScoreBreakdown.From`, no del color de un único indicador. Se necesita un mínimo de 3 ciclos, alguna muestra de frecuencia evaluable y tiempo de ubicación observada. Si no se cumplen esas condiciones, la nota general es «No evaluable», no cero por defecto.

Definimos `c = mínimo(1, ciclos / 30)` para el bloque configurado actual.

| Componente | Cálculo del código |
|---|---|
| Preparación, hasta 20 | 20 × proporción de los primeros cinco pasos completados × factor de errores de secuencia. |
| Colocación, hasta 20 | máximo(0, 20 − 2 × (alejamientos + separaciones)) × c. |
| Recorrido, hasta 25 | 25 × ciclos con recorrido adecuado / ciclos observados × c. |
| Ritmo, hasta 25 | 25 × muestras de frecuencia adecuadas / muestras de frecuencia evaluables × c. |
| Retorno, hasta 10 | 10 × ciclos con retorno completo / ciclos observados × c. |
| Pausas | Resta mínimo(10, 2 × pausas prolongadas). |
| Total | Suma los componentes, resta pausas, redondea y limita a 0–100. |

El factor de secuencia es `Clamp01(1 − SequenceErrors × 0,1)`. La existencia de un contador en una fórmula no prueba que cualquier intento de saltar un paso lo incremente: hay que examinar dónde se registra ese error. En el flujo actual el avance normal ya se restringe mediante estados.

Ejemplo hipotético, no resultado real de COPE: 30 ciclos, preparación completa, 2 incidencias de colocación, 24 recorridos adecuados, 21 de 28 muestras de frecuencia adecuadas, 27 retornos completos y 1 pausa.

```text
Preparación = 20
Colocación = 20 − 2×2 = 16
Recorrido = 25×24/30 = 20
Ritmo = 25×21/28 = 18,75
Retorno = 10×27/30 = 9
Pausas = 2
Total = redondear(81,75) = 82/100
```

En un intento de 15 ciclos, `c = 0,5`: los componentes de ejecución se reducen proporcionalmente. Se evita interpretar un intento breve como equivalente a completar el bloque. Los primeros ciclos todavía no tienen frecuencia evaluable, por lo que el denominador de ritmo puede diferir del número total de ciclos.

Un recorrido incorrecto normalmente representa puntos que no se obtienen en ese componente, no un descuento extra de 2. La incidencia continua de separación se registra al liberar el estado, no una vez por cada fotograma. El desglose por tipo de incidencia no vuelve a descontar lo ya aplicado en la nota.

Tu documento justifica el esquema inicial mediante el Anexo E. También deja pendiente el contraste institucional de los valores exactos antes de adoptarlos como escala. Defiéndelo como diseño explicable y revisable; no como ponderación prescrita por AHA.

## 13. Resultados, guardado y salida

El cierre distingue intento completado e interrumpido. Las recomendaciones se eligen según las incidencias observadas; si faltan datos, pide repetir sin atribuir una ejecución correcta o incorrecta inexistente.

`CprAttemptReport.Save` genera `RCP_<identificador>.json` y `.txt`. En el dispositivo usa la carpeta `RegistrosRCP` dentro de `Application.persistentDataPath`. El JSON sirve para datos estructurados; el TXT permite lectura humana. Contienen métricas, desglose e incidencias, entre otros datos. Esto no implica que exista una pantalla de historial ni un sistema de cuentas.

Se utiliza un archivo temporal para escribir antes de reemplazar el informe. El controlador intenta guardar al cerrar, al volver al menú y en otras transiciones. Hay tratamiento de errores de guardado. Para demostrar persistencia debes extraer un informe del APK real; leer el método no demuestra que el último dispositivo lo haya escrito correctamente.

Repetir crea una nueva sesión; volver al menú cambia de escena; salir llama a `Application.Quit()` en la aplicación construida. En el editor, salir detiene Play Mode. No confundas las tres operaciones.

## 14. Realidad mixta, apoyo y recentrado

`CprMixedReality` activa passthrough de Meta como fondo, oculta renderizadores de entorno que no deben verse y conserva estación, paciente y paneles. Con apoyo opcional cambia los materiales del paciente por un holograma tenue y oculta partes interiores identificadas, como dientes y ojos. Si passthrough no se inicializa, permite continuar en virtual.

El usuario coloca el objeto y confirma la alineación. No hay detección automática de almohada, mochila o maniquí. Añadir un apoyo no agrega medición de fuerza: se sigue leyendo el recorrido de las manos.

El recentrado espera que la nueva pose del visor se estabilice y recoloca la estación frente a la dirección horizontal del usuario. Mueve el conjunto para conservar la relación entre paciente y paneles. Al modificar referencias durante una práctica se suspende el detector y se solicita nueva colocación. Si hay apoyo en realidad mixta, puede requerir realineación.

## 15. Cómo explicar el código cuando te lo pidan

No abras un archivo enorme sin explicar qué vas a buscar. Usa esta ruta:

| Archivo dentro de `Assets/Scripts` | Responsabilidad | Qué método mostrar |
|---|---|---|
| [UI/SceneLoader.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/UI/SceneLoader.cs>) | Cambiar escena y salir. | `LoadScene`, `LoadMixedRealityTutorial`, `ExitApplication`. |
| [RCP/CprMenuWelcome.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprMenuWelcome.cs>) | Bienvenida y acceso al ensayo. | `Practice`. |
| [RCP/CprStationController.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprStationController.cs>) | Conectar entrada, sesión, presentación y guardado. | `Initialize`, `Update`, `SaveAttempt`. |
| [RCP/CprTrackedHands.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprTrackedHands.cs>) | Convertir seguimiento de Meta en muestras. | Construcción de `CprHandSample`. |
| [RCP/CprPracticeSession.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprPracticeSession.cs>) | Estado y avance de cada paso. | `Tick`, `ObservePlacement`, `OnCompression`, `Finish`. |
| [RCP/CprFlow.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprFlow.cs>) | Colocación y unión de manos. | `Matches`, `TickStack`, `TickCompression`. |
| [RCP/CprCompressionDetector.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprCompressionDetector.cs>) | Detectar bajada, retorno y frecuencia. | `CaptureRest`, `Tick`, `Emit`. |
| [RCP/CprPacing.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprPacing.cs>) | Comparar bajadas con referencia temporal. | `Tick`, `ObservePress`. |
| [RCP/CprStationLayout.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprStationLayout.cs>) | Mostrar medidores y resultados. | Bloque que usa `DisplayDepth` y `depthNeedle`. |
| [RCP/CprAttemptReport.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprAttemptReport.cs>) | Fórmula y persistencia de resultados. | `CprScoreBreakdown.From`, `Save`. |
| [RCP/CprCorrectionVoice.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprCorrectionVoice.cs>) | Evitar saturación de avisos. | `Candidate`, `Tick`. |
| [RCP/CprNarrator.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprNarrator.cs>) | Reproducir y esperar el audio. | `Play`, `Tick`, `Complete`. |
| [RCP/CprStationView.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprStationView.cs>) | Posicionar y colorear hologramas. | `Guides`, `AlignmentGuides`. |
| [RCP/CprStationPatient.cs](<E:/Tesis Pirmeros Auxilios VR/Primeros_Auxilios_VR/Assets/Scripts/RCP/CprStationPatient.cs>) | Contacto visual y movimiento del paciente. | `ContactPose`, `AuthoredShoulderSurface`, `SetCompression`. |

La configuración está en `Assets/Resources/RCP/TrainingSettings.asset`. Los valores serializados de ese recurso son los de la práctica; no siempre basta leer el valor inicial declarado en una clase. Al iniciar se clona la configuración para evitar que cambios posteriores alteren los criterios del intento activo.

Conceptos que debes saber explicar:

- **Clase:** reúne datos y comportamiento de una responsabilidad.
- **Estado:** situación actual, por ejemplo colocación o compresiones; determina qué acciones se aceptan.
- **Evento:** aviso de que ocurrió algo, por ejemplo un ciclo terminado. El detector informa y la sesión actualiza métricas.
- **Transform:** posición, rotación y escala; los hijos se mueven con su padre.
- **Pose:** posición y rotación de una referencia.
- **Vector:** dirección o desplazamiento; el producto escalar extrae cuánto movimiento ocurre en una dirección.
- **Coroutine:** permite esperar acciones o audios a lo largo de varios fotogramas sin congelar la aplicación.
- **ScriptableObject:** recurso de configuración reutilizable en Unity.
- **Shader/material:** definen cómo se dibuja una superficie, por ejemplo un holograma transparente.

## 16. Evidencias académicas que debes distinguir

El documento describe una revisión de V1 del 19 de septiembre de 2026 con diez alumnos y tres médicos. Para explicar esos resultados, conserva las poblaciones separadas: una respuesta de alumno representa 10 % de ese grupo, no 1/13. La encuesta descrita es de elaboración propia y no se calcula como SUS. Usa sus frecuencias y comentarios, sin inventar una nota global.

Según el documento: Anexo G reúne encuestas y fichas médicas; J presenta fotografías y vistas; K registra observación de desempeño; L contiene rúbricas de usabilidad. Debes conocer qué evidencia corresponde a cada versión y qué mediciones fueron aproximadas. Una firma de revisión no equivale a certificación clínica del simulador.

Las pruebas con entradas controladas permiten comprobar conteos y fórmulas reproducibles. Las pruebas en Quest verifican seguimiento, oclusión, audio, comodidad e interacción real. Las encuestas recogen percepciones. Cada instrumento responde una pregunta distinta.

No afirmes valores de FPS, latencia, eficacia educativa o ausencia absoluta de fallos si no tienes registros de esa medición. Tu demostración prueba un recorrido en condiciones concretas; no garantiza todas las situaciones posibles.

## 17. Preguntas difíciles y respuestas defendibles

**¿Cómo mide la fuerza?** No mide fuerza. Estima desplazamiento de manos con seguimiento espacial y una referencia de reposo.

**¿Cómo sabe que el paciente no respira?** Es la condición predefinida del caso. El sistema verifica la acción de observación del participante, no diagnostica una persona real.

**¿Cómo sabe que miré?** Usa orientación del visor y relación geométrica con los objetivos. No seguimiento ocular.

**¿Cómo sabe que pregunté si está bien?** No reconoce voz; lo indica y comprueba el contacto previsto. La formulación verbal no está evaluada automáticamente.

**¿Cómo sabe que entrelacé bien todos los dedos?** La guía muestra la postura y la lógica verifica colocación, orientación y superposición. No certifica cada contacto entre dedos.

**¿Por qué unas compresiones incorrectas cuentan?** Porque detectar un ciclo y calificarlo son operaciones diferentes. El resumen debe reflejar también los ciclos que necesitaron corrección.

**¿Por qué hay cero frecuencia al principio?** Aún no se reúnen tres eventos; la interfaz debe tratarlo como dato no evaluable, no como frecuencia física cero.

**¿El pitido es el corazón del paciente?** No. Es un metrónomo de referencia de 110 pulsos por minuto. No hay monitor cardíaco en el flujo activo.

**¿Qué pasa si se pierde una mano?** Se conserva la colocación establecida ante oclusión y se usa una fuente visible fiable cuando es posible. Sin datos válidos se suspende la medición y no se penaliza una pérdida técnica como error voluntario.

**¿Una almohada vuelve exacta la profundidad?** No. Aporta apoyo físico; el programa sigue midiendo movimiento virtual y el usuario confirma su alineación.

**¿Por qué no tiene login?** Los requisitos describen registros del intento y no exigen una plataforma de cuentas. Se generan informes identificados por intento sin necesidad de nombre.

**¿Dónde están los otros escenarios?** Están anunciados como próximos y deshabilitados; el alcance activo es RCP. No debes demostrarlos como implementados.

**¿Quién decidió los puntos?** Es un modelo de diseño documentado y relacionado con la consulta institucional; los pesos no son una escala AHA. El documento mantiene su revisión institucional pendiente antes de fijarlos como escala.

**¿Sirve para aprender mejor que una clase?** El trabajo permite mostrar funcionamiento y resultados de uso. Una comparación de eficacia del aprendizaje necesita un estudio específico que no se reemplaza con satisfacción.

**¿No tiene fallas?** Se realizaron pruebas y correcciones dentro de un alcance. Puedo mostrar el flujo y los casos comprobados, y distinguir las limitaciones del seguimiento y las verificaciones pendientes.

## 18. Orden de estudio y ensayo de defensa

Primero explica el propósito, alcance y cinco objetivos con tus palabras. Después recorre los seis pasos sin mirar código y di qué observa el sistema. Luego practica las fórmulas de recorrido, frecuencia y puntuación. Finalmente abre los archivos del mapa y localiza los métodos citados.

Ensayo práctico:

1. Abrir APK y mostrar bienvenida, menú y ensayo de controles.
2. Entrar al tutorial y explicar elección de modalidad.
3. Ejecutar seguridad, hombro, respiración, teléfono y manos, indicando la condición de avance.
4. Hacer ciclos: mostrar contador, frecuencia y recorrido como datos distintos.
5. Provocar una corrección controlada; recuperar sin borrar el avance.
6. Mostrar resumen, efecto en puntos y recomendaciones.
7. Repetir, volver y salir según lo que solicite el jurado.
8. Mostrar un informe del mismo APK si te preguntan por registro.
9. Abrir `Emit` si preguntan por ritmo; `Tick` del detector si preguntan por profundidad; `From` si preguntan por nota.

Antes de presentar, recuerda las diferencias documentales ya identificadas: almacenamiento temporal frente a informes persistentes; número antiguo de locuciones; liberación indicada como 0,45 s frente a 0,18 s; RN-03 debe remitir a estados de avance, y RN-08 al catálogo de mensajes. No atribuyas una versión distinta a las evidencias históricas. Esta guía no cambia esos pasajes de la tesis.

## 19. Matrices para estudiar los requisitos

Las tablas siguientes contienen los enunciados extraídos del DOCX y una orientación de demostración. Son una guía de defensa, no una declaración automática de cumplimiento. Para certificar cada fila se requiere registrar resultado esperado, observado, versión y evidencia.

### Requisitos de usuario

| Código | Enunciado del documento | Demostración o evidencia |
|---|---|---|
| RU-01 | El participante debe poder iniciar el simulador y acceder al menú principal. | Abrir el APK y mostrar el menú. SceneLoader y CprMenuWelcome. |
| RU-02 | El participante debe poder seleccionar e iniciar la práctica guiada de RCP. | Seleccionar Tutorial y comprobar carga de 2_Tutorial_RCP. SceneLoader. |
| RU-03 | El participante debe poder consultar instrucciones iniciales, objetivos e indicaciones de preparación. | Mostrar bienvenida, ensayo y explicaciones. CprMenuWelcome, CprFamiliarization y CprStationPreparation. |
| RU-04 | El participante debe visualizar un escenario de práctica y un paciente virtual. | Mostrar paciente y estación. CprStationController.Initialize. |
| RU-05 | El participante debe poder posicionarse frente al paciente e identificar la ubicación esperada de las manos. | Mostrar guías anatómicas y orientación inicial. CprStationView.Guides. |
| RU-06 | El participante debe recibir correcciones cuando su posición o la ubicación de sus manos no cumpla la condición observable definida. | Acercar la mano fuera de guía y corregir. CprFlow y mensajes de CprPracticeSession. |
| RU-07 | El participante debe poder realizar compresiones torácicas virtuales sobre la zona interactiva del paciente. | Hacer bajada y retorno y observar contador. CprCompressionDetector.Tick/Emit. |
| RU-08 | El participante debe recibir información sobre ubicación de manos, ritmo y referencia de compresión virtual durante la ejecución. | Separar o alejar manos y recuperarlas. CprFlow.TickCompression. Mostrar frecuencia después de tres ciclos. CprCompressionDetector.Emit. Bajar las manos y explicar referencia de reposo. CprCompressionDetector y CprStationLayout. |
| RU-09 | El sistema debe registrar los datos necesarios de la sesión para evaluar la ejecución del participante. | Mostrar eventos e informe del intento. CprAttemptReport.Save. Comprobar archivo en el dispositivo. |
| RU-10 | El participante debe recibir confirmación cuando la ejecución cumple los criterios observables definidos. | Mostrar confirmación visual de colocación y ejecución. CprStationView y CprStationLayout. |
| RU-11 | El participante debe recibir retroalimentación correctiva visual y auditiva cuando existan errores observables. | Provocar error persistente y mostrar texto, borde y voz espaciada. CprLiveFeedback y CprCorrectionVoice. |
| RU-12 | El participante debe poder completar la secuencia y consultar un resumen de desempeño, resultados y recomendaciones. | Completar intento y mostrar resumen. CprScoreBreakdown y CprStationLayout. Mostrar recomendaciones relacionadas con incidencias. CprPracticeContent.Recommendation. |
| RU-13 | El participante debe poder repetir la práctica o finalizar la sesión desde la etapa de cierre. | Repetir y comprobar contador e identificador nuevos. CprStationController y CprPracticeSession. Mostrar cierre, regreso y salida según la acción elegida. CprStationController y SceneLoader. |
| RU-14 | COPE debe poder revisar que las instrucciones y criterios representados sean pertinentes al contexto institucional y a la referencia BLS/SVB utilizada. | Mostrar instrucciones de versión identificada, entrevistas y ficha de revisión COPE. Es evidencia institucional, no un botón. |
| RU-15 | El sistema debe comunicar que la evaluación es una referencia de apoyo y no una certificación clínica ni una medición física exacta. | Señalar centímetros virtuales y alcance del resultado en el APK, no solo en la tesis. |

### Requisitos funcionales

| Código | Enunciado del documento | Demostración o evidencia |
|---|---|---|
| RF-01 | El sistema deberá inicializar el entorno VR y mostrar el menú principal. | Abrir el APK y mostrar el menú. SceneLoader y CprMenuWelcome. |
| RF-02 | El sistema deberá permitir seleccionar la práctica guiada de RCP desde el menú. | Seleccionar Tutorial y comprobar carga de 2_Tutorial_RCP. SceneLoader. |
| RF-03 | Al iniciar la práctica, el sistema deberá cargar el escenario y el paciente virtual. | Mostrar paciente y estación. CprStationController.Initialize. |
| RF-04 | El sistema deberá mostrar instrucciones iniciales, objetivos y pasos de preparación. | Mostrar bienvenida, ensayo y explicaciones. CprMenuWelcome, CprFamiliarization y CprStationPreparation. |
| RF-05 | El sistema deberá orientar al participante para posicionarse frente al paciente y colocar las manos. | Mostrar guías anatómicas y orientación inicial. CprStationView.Guides. |
| RF-06 | El sistema deberá verificar la posición del participante y la ubicación de las manos. | Mostrar zona inicial y tolerancias. ParticipantInPosition y CprFlow.Matches. No mide postura corporal completa. |
| RF-07 | Si la colocación no es válida, el sistema deberá mostrar correcciones de posición y orientación de las manos. | Acercar la mano fuera de guía y corregir. CprFlow y mensajes de CprPracticeSession. |
| RF-08 | El sistema deberá habilitar las compresiones cuando la condición de posicionamiento sea válida. | Comparar antes y después de colocación y explicación. CprPracticeSession.Tick. |
| RF-09 | El sistema deberá detectar eventos de compresión torácica virtual durante la práctica. | Hacer bajada y retorno y observar contador. CprCompressionDetector.Tick/Emit. |
| RF-10 | El sistema deberá verificar la ubicación de manos durante la ejecución de compresiones. | Separar o alejar manos y recuperarlas. CprFlow.TickCompression. |
| RF-11 | El sistema deberá calcular una referencia de compresiones por minuto a partir de los eventos detectados y el tiempo transcurrido. | Mostrar frecuencia después de tres ciclos. CprCompressionDetector.Emit. |
| RF-12 | El sistema deberá estimar una referencia de compresión a partir del desplazamiento virtual de manos o controladores. | Bajar las manos y explicar referencia de reposo. CprCompressionDetector y CprStationLayout. |
| RF-13 | El módulo de evaluación deberá registrar datos necesarios de la sesión, incluyendo eventos, aciertos, errores y métricas observables. | Mostrar eventos e informe del intento. CprAttemptReport.Save. Comprobar archivo en el dispositivo. |
| RF-14 | El módulo deberá determinar si la ejecución cumple los criterios observables configurados. | Contrastar un ciclo con los umbrales. CprCompressionEvent y CprScoreBreakdown.From. |
| RF-15 | Cuando la ejecución cumpla los criterios observables, el sistema deberá indicarlo al participante. | Mostrar confirmación visual de colocación y ejecución. CprStationView y CprStationLayout. |
| RF-16 | Cuando la ejecución no cumpla los criterios, el sistema deberá emitir retroalimentación visual y auditiva. | Provocar error persistente y mostrar texto, borde y voz espaciada. CprLiveFeedback y CprCorrectionVoice. |
| RF-17 | El sistema deberá controlar si la secuencia de práctica fue completada y permitir continuar cuando aún existan pasos pendientes. | Mostrar avance por acciones y cierre del bloque. CprPracticeSession.Tick/Finish. |
| RF-18 | Al completar la secuencia, el módulo de evaluación deberá generar un resumen de desempeño. | Completar intento y mostrar resumen. CprScoreBreakdown y CprStationLayout. |
| RF-19 | El simulador deberá mostrar resultados y recomendaciones generales al finalizar la práctica. | Mostrar recomendaciones relacionadas con incidencias. CprPracticeContent.Recommendation. |
| RF-20 | El sistema deberá permitir reiniciar una nueva práctica desde la etapa de resultados. | Repetir y comprobar contador e identificador nuevos. CprStationController y CprPracticeSession. |
| RF-21 | El sistema deberá permitir finalizar la sesión cuando el participante no desee repetir la práctica. | Mostrar cierre, regreso y salida según la acción elegida. CprStationController y SceneLoader. |
| RF-22 | El sistema no deberá presentar la referencia virtual como una medición clínica exacta ni como certificación formal. | Señalar centímetros virtuales y alcance del resultado en el APK, no solo en la tesis. |

### Requisitos no funcionales

| Código | Enunciado del documento | Demostración o evidencia |
|---|---|---|
| RNF-01 | Las instrucciones, menús y resultados deberán ser comprensibles para usuarios sin experiencia técnica en Unity. | Ensayo con usuarios nuevos y rúbrica de tareas; no basta tu facilidad para utilizarlo. |
| RNF-02 | El sistema deberá diferenciar de manera clara una confirmación, una advertencia y una corrección. | Mostrar diferencia entre confirmación, seguimiento ausente y error; contrastar comprensión con usuarios. |
| RNF-03 | La aplicación deberá responder con fluidez suficiente para no interrumpir la práctica guiada. | Registrar rendimiento y continuidad en Quest del APK presentado. No afirmar FPS sin medición. |
| RNF-04 | El prototipo deberá ejecutarse en el entorno VR definido para el proyecto y ser compatible con el visor Meta Quest utilizado. | Ejecutar en el visor previsto y registrar versión de APK y dispositivo. |
| RNF-05 | La lógica deberá separarse por responsabilidades: sesión, guía, escenario, interacción, evaluación, retroalimentación y resultados. | Explicar separación entre entrada, sesión, detector, vista, voz e informe. |
| RNF-06 | El usuario deberá poder completar, repetir o finalizar una sesión sin quedar en estados bloqueados. | Probar recorrido, pérdida de seguimiento, pausa, recuperación, reinicio y salida; conservar resultados. |
| RNF-07 | El sistema no deberá afirmar que mide fuerza, presión o profundidad clínica exacta. | Mostrar que los indicadores no se presentan como fuerza física o certificación. |
| RNF-08 | Los criterios aplicados durante la sesión deberán ser coherentes con los mostrados en resultados y retroalimentación. | Recalcular un informe y contrastarlo con pantalla; distinguir pitido de frecuencia puntuable. |
| RNF-09 | La arquitectura deberá permitir incorporar mejoras o nuevos escenarios sin rehacer completamente el sistema. | Mostrar configuración y responsabilidades separadas; no prometer escenarios nuevos sin trabajo. |
| RNF-10 | La secuencia y las instrucciones deberán poder ser revisadas de acuerdo con los criterios de COPE y la referencia BLS/SVB. | Presentar revisión institucional de instrucciones y criterios con versión identificada. |

### Reglas de negocio

| Código | Enunciado del documento | Demostración o evidencia |
|---|---|---|
| RN-01 | El participante es el usuario principal de la sesión y ejecuta las acciones de inicio, preparación, práctica, consulta de resultados, repetición o finalización. | Demostrar recorrido del participante y distinguirlo de revisión institucional. |
| RN-02 | La práctica debe mantener un único intento activo; los eventos registrados se asocian exclusivamente con ese intento hasta su cierre o reinicio. | Mostrar sesión activa e identificador; repetir crea datos nuevos. |
| RN-03 | El participante debe recorrer la secuencia definida para la práctica; un paso solo puede avanzar cuando se cumple su condición observable o la confirmación prevista. | Mostrar condiciones de avance. Referencia documental equivocada: cuadro 3.36; corresponde 3.40. |
| RN-04 | La fase de compresiones virtuales solo puede habilitarse después de presentar las instrucciones y validar el posicionamiento y la ubicación de las manos requeridos. | Antes de explicación y colocación, comprobar que todavía no se evalúan compresiones. |
| RN-05 | Las muestras sin seguimiento válido no deben interpretarse como errores del participante ni utilizarse para calcular eventos o indicadores de la práctica. | Ocultar manos: comprobar estado de seguimiento y ausencia de descuento técnico en informe. |
| RN-06 | La evaluación y la retroalimentación deben limitarse a acciones y datos que el visor pueda observar de forma válida durante la sesión. | Explicar entradas observables: no voz, codos, fuerza ni fisiología. |
| RN-07 | Cuando no existan datos suficientes para interpretar un indicador, el resultado debe mostrarse como no evaluable y explicar la causa, sin reemplazar la ausencia de información por un valor de cero. | Cerrar con cero, uno y dos ciclos; mostrar No evaluable y comparar con bloque suficiente. |
| RN-08 | Los mensajes deben diferenciar instrucciones, correcciones y confirmaciones, y deben corresponder con la condición observable que produjo cada respuesta. | Mostrar mensaje acorde a condición. Referencia equivocada: tabla 3.2; corresponde cuadro 3.34. |
| RN-09 | La referencia de compresión virtual no debe presentarse como una medición física exacta de fuerza, presión o profundidad ni como una valoración clínica. | Explicar fórmula de recorrido y alcance sin llamarla fuerza o profundidad real. |
| RN-10 | Los resultados del simulador tienen finalidad educativa y no sustituyen la supervisión del instructor, la evaluación práctica presencial ni la certificación formal de RCP. | Mostrar alcance educativo del resultado y papel del instructor. |
| RN-11 | Al repetir la práctica, el sistema debe restablecer los estados y datos del intento anterior antes de comenzar una nueva sesión. | Repetir y comprobar nuevos contadores y estados; informes anteriores pueden persistir. |
| RN-12 | Las modificaciones de la secuencia, las instrucciones o los criterios de interpretación deben revisarse con COPE y contrastarse con las referencias BLS/SVB y nacionales seleccionadas antes de la validación con participantes. | Mostrar revisión por COPE. Código modificado no demuestra revisión institucional de los cambios. |

## 20. Cómo leer los capítulos y explicar la metodología

El documento clasifica la investigación como aplicada, descriptiva y propositiva. Aplicada: construye un artefacto para una necesidad concreta. Descriptiva: caracteriza el contexto y las necesidades. Propositiva: diseña y desarrolla la solución. El enfoque de sistemas relaciona usuario, visor, escenario, entrada, guía y evaluación. No confundas tipo de investigación con organización del desarrollo de software.

La revisión documental y la entrevista semiestructurada recogen información; las listas de cotejo, rúbricas y encuestas examinan funcionamiento y uso. Una encuesta de satisfacción no reemplaza una prueba de conteo del detector.

| Parte de la tesis | Pregunta que debes poder responder |
|---|---|
| Introducción | ¿Qué problema abordas en COPE, para quién, con qué objetivo y límites? |
| Capítulo I | ¿Qué conceptos y antecedentes sustentan tus decisiones y qué límites muestran? |
| Capítulo II | ¿Qué encontraste en COPE y cómo se convirtió en necesidades y requisitos? |
| Capítulo III | ¿Cómo se diseñó, construyó, verificó y revisó el sistema? |
| Conclusiones | ¿Qué lograste respecto de los objetivos, con qué evidencia y hasta dónde llega la conclusión? |
| Anexos | ¿Dónde está la evidencia que respalda las afirmaciones? |

El estado del arte muestra que existen otros simuladores: tu aporte es la integración y adaptación trazable a COPE. Si una publicación usó maniquí o sensores, no atribuyas sus resultados a tu configuración de visor sin sensores físicos. No memorices porcentajes que no puedas localizar y explicar.

Distingue iteración de incremento: una iteración revisa y mejora; un incremento incorpora capacidades utilizables. Al explicar la metodología de desarrollo, señala la organización documentada y ejemplos reales de versiones, requisitos, pruebas y correcciones. No afirmes haber usado Scrum completo solo porque trabajaste por etapas.

Un requisito de usuario expresa la necesidad; uno funcional describe lo que hace el sistema; uno no funcional establece cualidades como usabilidad o fiabilidad; una regla de negocio restringe su comportamiento. Una historia de usuario añade quién necesita algo y para qué. Un caso de uso desarrolla actores, condiciones y secuencia de interacción. La trazabilidad relaciona todo ello con implementación y prueba.

Ejemplo completo: RU-08 pide información durante compresiones → RF-11 exige calcular ritmo → `CprCompressionDetector.Emit` calcula la frecuencia → la pantalla la muestra → una prueba con tiempos conocidos y una ejecución en Quest comprueban aspectos diferentes de ese requisito.

## 21. Práctica sin mirar la guía

1. Explica el proyecto en 45 segundos sin decir que mide fuerza.
2. Relaciona cada uno de tus cinco objetivos con una evidencia.
3. Di qué entrada observable permite avanzar en cada paso.
4. Calcula el recorrido si el reposo era 13 cm y la mano queda a 7,4 cm.
5. Calcula la frecuencia con seis eventos entre 10,0 y 12,5 segundos.
6. Explica por qué un ciclo puede contarse y resultar incorrecto.
7. Explica por qué el primer ciclo no permite calcular la frecuencia.
8. Explica qué ocurre si se oculta la mano inferior y qué ocurre si se pierden ambas.
9. Distingue verde de colocación, franja verde de recorrido y zona verde del metrónomo.
10. Explica la puntuación de un intento incompleto.
11. Localiza el guardado JSON/TXT y explica cómo demostrarías persistencia.
12. Distingue la revisión histórica de V1 de la implementación actual.
13. Responde qué funciones de los manuales quedan fuera del alcance activo.
14. Explica qué se conserva y qué se recalibra al recentrar.
15. Señala qué prueba aporta evidencia de usabilidad y cuál de exactitud de cálculo.

Respuestas de cálculo: pregunta 4 = 5,6 cm virtuales; pregunta 5 = 120 ciclos/min. Para las demás, explica entrada, condición, resultado y evidencia; no te limites a recitar nombres de clases.

## Fuentes de preparación

- [Documento del proyecto](<E:/Tesis Documentos/DOCUMENTO MAURICIO/MCM-Proyecto de Grado.docx>).
- Código actual de `Assets/Scripts/RCP`, `Assets/Scripts/UI/SceneLoader.cs` y configuración `Assets/Resources/RCP/TrainingSettings.asset`.
- [AHA 2025, Adult Basic Life Support](https://cpr.heart.org/en/resuscitation-science/cpr-and-ecc-guidelines/adult-basic-life-support), consultada el 4 de noviembre de 2026.
- Las afirmaciones sobre entrevistas y revisión de COPE se atribuyen al documento del autor; esta guía no constituye una nueva validación institucional.
