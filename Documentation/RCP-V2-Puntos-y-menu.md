# Cierre de menú y explicación de puntos V2

## Resultados

«Ver incidencias» muestra cantidad y efecto en puntos por tipo: manos fuera del pecho, separación, recorrido corto/excesivo, ritmo lento/rápido, retorno incompleto y pausas. El mismo cálculo alimenta la pantalla, el TXT y los campos `scoreImpacts` e `impactSummary` del JSON.

No cambia la puntuación. El efecto de colocación incluye la proporción de ciclos completados y su límite compartido. Si el límite se agota, su efecto se distribuye entre alejamiento y separación según sus cantidades; no se atribuye el límite completo a ambos tipos. Las pausas tienen un máximo de diez puntos. Recorrido, ritmo y retorno muestran puntos no obtenidos, sin volver a descontarlos del resultado.

Se distinguen los ciclos pendientes de las incidencias y se conserva «Sin nota global» si faltan datos. La pérdida técnica muestra cero puntos de descuento. Los importes se presentan antes del redondeo final de la nota.

Ejemplo de comprobación: 30 ciclos, 5 alejamientos, 5 separaciones y 24 recorridos cortos producen efectos de 10 + 10 + 20 = 40 puntos y una nota de 60, con los demás criterios correctos. Se verifican también saturación de límites, coincidencia entre pantalla/TXT/JSON y espacio del texto en el panel.

## Menú

- «Realidad mixta» (antes «Escenario real») abre el tutorial con la opción mixta seleccionada. Espera la bienvenida completa y conserva la alternativa virtual si no se puede iniciar passthrough. La solicitud se consume una sola vez.
- «Otros escenarios · Próximamente» queda deshabilitado. No existen otros escenarios implementados en esta entrega; no se presenta como acceso disponible sin respuesta.
- «Practicar controles» repite la familiarización.
- «Salir» está conectado a `QuitGame`: `Application.Quit()` en la aplicación compilada, y salida de Play Mode en Unity. No es un botón para volver al menú.

No se generó un APK. El cierre de proceso y el passthrough físico en Quest siguen requiriendo comprobación en el dispositivo.

## Validación realizada

Aprobadas 13 comprobaciones de efectos de puntuación y conexiones del menú, 21 de reproducción/navegación, las 92 de V2 y regresiones anteriores. También se ejecutó el botón Salir de la escena real en Play Mode y se confirmó el retorno a modo edición. Registros: `Logs/V2-menu-impact-play.log` y `Logs/V2-exit-button.log`.
