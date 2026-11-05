# Estado de la lista de cierre V2 y ajuste del medidor

Revisión del 1 de noviembre de 2026, basada en el código local, escenas y comprobaciones de Unity. No equivale a aprobación institucional ni a prueba en Quest. No se generó un APK.

## Medidor de recorrido

El cursor anterior medía solo 5 unidades de Canvas y se ocultaba fuera del estado de medición. Además, la lectura retenida podía seguir considerándose fresca inmediatamente después de suspender el detector. Estos hallazgos explican problemas de presentación, pero no prueban por sí solos la causa de cada pérdida observada en las gafas.

Ahora el cursor tiene contorno oscuro, marca blanca de 9 unidades, relleno lateral continuo y una marca ámbar del último ciclo completado. Permanece visible en reposo, espera y pausa. Una muestra perdida conserva la posición anterior en gris y muestra «Lectura retenida / Muestra las manos»; fuera de zona solicita volver al pecho. La calibración nueva restablece la referencia. No se generan compresiones ni desplazamientos estimados cuando falta seguimiento. El medidor sigue mostrando desplazamiento virtual, no fuerza.

Se conserva el filtro de muestras existente y no se modificaron el metrónomo, los umbrales ni la fórmula de puntuación. La versión de layout pasa de 6 a 7 para reconstruir también las pantallas de escenas ya guardadas.

Validación: 23 comprobaciones nuevas del medidor y cierres con 0, 1, 2 y 30 ciclos; 92 comprobaciones V2 y regresiones previas aprobadas. Registro: `Logs/V2-travel-meter.log`. Pruebas sintéticas: queda reproducir el caso con las manos reales del usuario en Quest.

## Lista del usuario

| Trabajo | Estado | Evidencia y pendiente |
| --- | --- | --- |
| Salir y opciones vacías | Parcial | Salir tiene evento persistente QuitGame; Ajustes se convierte en Practicar controles. B_Escenario_Real y B_Otros_Escenarios siguen activos con eventos vacíos y no se encontró conexión por script. Falta resolver esos accesos. La realidad mixta sí se elige dentro del tutorial. |
| Orden de manos | Implementado; validar imitación en Quest | Derecha debajo e izquierda encima en voz, texto, guías y demostración reflejada. |
| Ensayo de manos y selección | Implementado; validar con principiantes | Familiarización antes del menú y repetición desde Practicar controles; no puntúa. |
| Tiempos y voz | Implementado y probado en editor | Finalización de AudioSource, acción observada, confirmación y espera; prueba anterior de reproducción/navegación aprobada (17 comprobaciones). |
| No evaluable e intentos cortos | Corregido y probado en editor | Cierres con 0, 1, 2 y 30 ciclos: no inventa nota con muestras insuficientes; recomendaciones diferenciadas y criterios sin datos. |
| Explicar descuentos | Parcial | Pantalla de incidencias, desglose y fórmula en informe; prueba sintética reproduce nota 60 y exportación. Falta mostrar de manera directa el efecto individual de cada incidencia, considerando proporcionalidad y límites, y contrastar un informe real. |
| Corrección auditiva y reorientación | Parcial | Locuciones para ritmo, recorrido, retorno, pérdida/recuperación y alejamiento. Falta cerrar todos los casos de reorientación espacial y verificar comprensión y recuperación en visor. |
| Legibilidad, repetir voz y ambientación | Parcial | Paneles fijos y Repetir voz disponibles; comprobaciones visuales en editor. Falta validación de lectura, alcance, comodidad y ambientación con usuarios en Quest. |
| Batería de interacción en Quest | Pendiente de evidencia | Teléfono, manos superpuestas, oclusión, medidor actualizado, altura, pausa, reinicio y salida; registrar esperado, observado y resolución. |
| Rendimiento e informes del dispositivo | Pendiente | Existe exportación TXT/JSON y prueba de ida/vuelta sintética. Faltan mediciones y archivos extraídos de una compilación identificada del visor. |
| COPE y documentación/versionado | Pendiente de cierre | Hay guion y notas técnicas; faltan aprobación/observaciones de COPE, sincronización del documento final y evidencias vinculadas a una versión. |

No se declara esta V2 final. La tabla distingue implementación, pruebas automatizadas y validación real. La revisión histórica `RCP-Cumplimiento-Documento.md` conserva sus hallazgos de octubre; no debe usarse como estado actual de estos puntos ya corregidos.

## Actualización posterior: puntos y menú

Se implementó el efecto por tipo de incidencia en pantalla e informes, y se resolvieron los accesos vacíos del menú: realidad mixta abre el tutorial; otros escenarios queda identificado como «Próximamente» y deshabilitado. Véase `RCP-V2-Puntos-y-menu.md`. Esto sustituye los pendientes de software de las filas correspondientes de la tabla anterior; la comprobación física en Quest permanece pendiente.
