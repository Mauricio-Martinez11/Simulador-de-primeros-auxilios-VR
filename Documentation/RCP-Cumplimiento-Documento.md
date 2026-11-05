# Comparación del prototipo con el documento de proyecto

Revisión: 17 de octubre de 2026. Fuente: `E:/Tesis Documentos/MCM-Perfil de Proyecto de Grado oficial.docx`, especialmente cuadros 3.9, 3.10, 3.32, 3.36, 3.40 y 3.58; secciones 3.15 y 3.16. Se revisaron los scripts, configuración y registros disponibles. Esta revisión no modifica el documento Word ni sustituye pruebas en Quest o aceptación de COPE.

**Conclusión:** el núcleo de la práctica está implementado. El cumplimiento completo del documento todavía tiene pendientes de presentación, coherencia de resultados, validación en dispositivo e institucional. No se atribuye un porcentaje de cumplimiento: presencia de código, prueba en editor y aceptación institucional son evidencias diferentes.

## Requisitos funcionales del cuadro 3.9

«Implementado» significa presente en la versión local, con el límite de que el recorrido de esta versión en Quest con manos reales sigue pendiente.

| RF | Estado observado | Evidencia o pendiente |
| --- | --- | --- |
| 01 Inicio | Implementado | Arranque, rig persistente y menú. |
| 02 Iniciar práctica | Implementado | Carga desde menú mediante `SceneLoader`. |
| 03 Escenario y paciente | Implementado | Estación fija, paciente preparado, referencias locales. |
| 04 Instrucciones y objetivos | Parcial | Se enseñan pasos; la bienvenida actual no desarrolla explícitamente objetivo, alcance y uso inicial de controles. |
| 05 Guía de posición | Implementado | Posición al costado y hologramas de manos. |
| 06 Verificar posición | Implementado | Posición del visor y muestras de manos; no valoración corporal completa. |
| 07 Corregir posición | Implementado | Mensajes por posición y ubicación; necesita comprobación de comprensión y tolerancias en visor. |
| 08 Habilitar práctica | Implementado | Colocación secuencial y calibración antes de contar. |
| 09 Detectar compresiones | Implementado | Detector de ciclos, sin alimentar el cálculo desde las animaciones. |
| 10 Ubicación durante práctica | Implementado | Zona válida, salida y recuperación. |
| 11 Ritmo | Implementado | Cálculo a partir de eventos y tiempos válidos. |
| 12 Recorrido virtual | Implementado | Desplazamiento respecto de referencia, sin fuerza física. |
| 13 Datos de sesión | Implementado en lo esencial | Eventos, tiempos acumulados y métricas en memoria; diferencias con el detalle propuesto del cuadro 3.32 se indican abajo. |
| 14 Evaluación | Implementado; revisión institucional pendiente | Criterios configurables y nota educativa. Los pesos no cuentan con validación de COPE registrada. |
| 15 Confirmación | Implementado | Colores, mensajes y señal sonora de acierto. |
| 16 Corrección | Implementado en forma básica | Mensajes visuales y tono de corrección. La asistencia verbal específica por tipo de error puede ampliarse dentro del trabajo de audio. |
| 17 Secuencia | Implementado | Avance por acciones y cierre del bloque. |
| 18 Resumen | Parcial | Muestra datos por criterio; falta cerrar la presentación de criterios sin datos y hacer explícito el conteo de correcciones de ubicación. |
| 19 Recomendaciones | Parcial | Proceden de errores registrados, pero un intento demasiado corto puede llegar al mensaje genérico de buen desempeño. |
| 20 Repetir | Implementado | Nuevo intento sin datos heredados. |
| 21 Finalizar | Implementado | Terminar intento, volver al menú y salir desde menú. |
| 22 Aviso de alcance | Parcial | Los medidores indican recorrido virtual y ausencia de profundidad física; falta una explicación visible y consistente del alcance educativo en bienvenida y resultados. |

## Ajustes de software identificados

1. **Presentación y uso inicial.** Añadir una introducción breve al objetivo y a cómo usar manos/controles opcionales, manteniendo el avance sin botones pedido por el autor. Referencias: RF-04, cuadro 3.36. La bienvenida de `CprPracticeContent` actualmente se limita a situarse al costado y seguir las guías.
2. **Resultados sin datos.** `CprStationLayout` aún construye expresiones `0 / 0` en recorrido, ritmo o retorno cuando no hay muestras. La nota global sí se oculta, pero cada criterio también debe indicar «No evaluable» y su causa, conforme a RN-07 y sección 3.11.4.
3. **Recomendaciones y ubicación.** `CprPracticeContent.Recommendation` puede devolver «Buen trabajo» con pocos ciclos y sin ritmo evaluable. Debe distinguir intento corto/datos insuficientes de un desempeño comprobado. `LocationErrors` se registra y influye en recomendaciones, pero su cantidad no aparece explícitamente en el resumen propuesto por el cuadro 3.40.
4. **Alcance educativo visible.** Integrar un aviso corto coherente en introducción y resultados sobre qué se observa y qué no se mide. El texto actual del panel de recorrido ayuda, pero no cubre toda la presentación descrita en RF-22 y sección 3.14.6.
5. **Concordancia con el diseño detallado.** El cuadro 3.32 propone identificador temporal, inicio/fin e intervalos de seguimiento con comienzo y fin. La implementación usa una instancia por intento, tiempos relativos, eventos y acumulados; no conserva todas esas estructuras explícitas. Se debe completar ese detalle o documentar y justificar la simplificación. Esto no implica añadir base de datos.

## Validación pendiente

| Área | Qué falta para cerrarla |
| --- | --- |
| Interacción en Quest 3S | Recorrido completo solo con manos: selección, superposición, pérdidas de seguimiento, teléfono, compresiones, altura, pausa, repetición y salida. Registrar incidencias de la versión examinada. |
| Rendimiento y estabilidad | Medir tiempos por fotograma, CPU y memoria en la compilación del visor; repetir sesiones y transiciones. Fijar la meta según la configuración antes de medir. RNF-03, 04 y 06. |
| Legibilidad y comodidad | Comprobar distancias, tamaño de texto, alcance, postura y comprensión con usuarios. RNF-01 y 02. |
| Pertinencia de COPE | Revisar secuencia, mano inicial impuesta, demostraciones, parámetros didácticos, pesos de nota y recomendaciones. Registrar observaciones y resolución. RU-14, RNF-10 y sección 3.16.1. |
| Usabilidad con alumnos | Aplicar las tareas, fichas y encuestas del anexo VII. El documento prevé 10–15 participantes; se debe informar la cantidad efectiva y los resultados, sin inventar datos. |
| Cierre documental | Actualizar el capítulo de implementación y trazabilidad a estación fija, cuatro pantallas, teléfono y avance por acciones. Registrar versión, configuración y evidencia de la compilación evaluada. |

Las 96 comprobaciones de lógica y 449 de escena/UI/vídeo registradas verifican entradas controladas en Unity. Son evidencia técnica de desarrollo; no acreditan por sí solas rendimiento en Quest, usabilidad con alumnos ni aprobación de COPE. Véase `RCP-validacion-tutorial-animado.txt`.

## Cambios autorizados y funciones fuera del alcance

- El desplome, traslado y destino holográfico se desactivaron por petición del autor. Se conserva el modo experimental; reactivarlo no es un pendiente de la estación actual. El documento debe reflejar esa decisión.
- Pedir ayuda y un DEA sí figura en el cuadro 3.50. Ahora se representa con un teléfono virtual para evitar botones de avance. No realiza llamadas reales.
- La desfibrilación interactiva y otros procedimientos de primeros auxilios se excluyen del alcance principal en la sección 1.8.1 y en 3.6.4. Las imágenes de referencia de pantallas no amplían ese alcance por sí mismas.
- No se requieren cuentas, base de datos, historial persistente de alumnos, reconocimiento de voz, sensores físicos ni certificación clínica.
- La evaluación automática de postura corporal completa no está respaldada por los datos de manos/visor y el propio documento delimita esa inferencia.
- Ya existen ocho locuciones de guía y tonos básicos. Quedan sujetos a revisión de calidad, comprensión y correspondencia con los eventos; los pendientes anteriores son adicionales al trabajo de audio.

## Orden sugerido de cierre

Completar los ajustes de introducción/resultados y alinear el documento; probar la versión en Quest con manos reales y perfilarla; revisar contenido y puntuación con COPE; aplicar las pruebas con participantes y registrar correcciones y aceptación de la versión final.
