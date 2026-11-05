# Correcciones del tutorial — 2 noviembre 2026

- Paso 1: marcadores de observación a izquierda y derecha a altura del visor al entrar; se mantienen fijos después.
- Paso 2: contacto del hombro colorea la guía verde incluso durante la explicación. La guía se eleva hasta la superficie de cuerpo/ropa para evitar ocultar el pulgar.
- Paso 3: sustituida la tarjeta adicional por un aro de progreso en el pecho. Indica mirada activa y segundos observados.
- Paso 4: eliminado del guion y de ambos archivos Step04.wav el cierre sobre llamada simulada.
- Paso 6: una separación registrada no cambia de paso, no reinicia narración ni contador. La recolocación secuencial calibra de nuevo el reposo y permite continuar el mismo intento. Derecha y luego izquierda se ponen verdes individualmente, también durante la explicación inicial.
- Separación: evidencia fiable durante 0,18 segundos, en vez de 0,45. Una pérdida de seguimiento no se interpreta como separación. Ajustar altura o recentrar durante compresiones tampoco devuelve al paso anterior.

## Validación
Unity 2022.3.62f2, copia aislada Logs/InteractionValidation. CprRegripChecks.Run: 12 verificaciones de fuentes, 35 de flujo, 104 de estación, 41 de fiabilidad y pruebas específicas de recuperación. Se comprobó 3 ciclos -> separación -> recolocación -> 6 ciclos sin reiniciar explicación, así como 30 ciclos con varias incidencias.
Log: Logs/V2-continuous-compressions-final.log. Las comprobaciones usan poses sintéticas; falta comprobar ergonomía y seguimiento real en Quest. No se generó APK.
