# Menú fijo y recentrado del conjunto

2 de noviembre de 2026.

El usuario confirmó que volvió a escuchar audio después de cerrar y reabrir el APK instalado mediante ADB. No se instaló otra versión ni se cambió el volumen del visor. Los registros confirman salida multimedia sin silencio y foco XR adquirido; esto no identifica una causa única del silencio previo. Se conserva la implementación de audio previamente aplicada, sin nuevos cambios de audio en esta intervención.

## Correcciones

`CanvasBillboard` giraba cada fotograma un objeto padre cuyo Canvas estaba desplazado tres metros. Ese giro movía también la posición del menú. Se mantiene el componente para conservar referencias serializadas, pero ahora coloca el menú una vez delante del visor y lo deja fijo.

`CprWorldRecenter` escucha `OVRDisplay.RecenteredPose`, espera dos fotogramas para obtener la pose actualizada y notifica a las vistas. El evento existe en el registro real de Quest: `Recenter event detected`. No se implementa otro gesto de pinza que compita con la selección de botones.

- Menú: centro a tres metros delante de la dirección horizontal de la mirada.
- Bienvenida/familiarización/alineación: panel a metro y medio delante de la mirada.
- Estación: pecho a 85 cm delante de la mirada, conservando altura y posiciones relativas de paciente, guías, teléfono y pantallas.
- Durante compresiones, recolocar invalida la referencia de manos y pide colocarlas nuevamente sin penalizar ni borrar ciclos.
- Si se usa apoyo físico confirmado en realidad mixta, se solicita nuevamente la alineación manual. El apoyo físico no se mueve con el contenido virtual.

El paso de seguridad ahora acepta un giro horizontal de al menos 25 grados a cada lado, mantenido los 0,65 segundos ya establecidos. Conserva también la detección anterior de las zonas. Ya no exige apuntar hacia círculos bajos para reconocer que se observó el entorno. No se permite avanzar sin completar la acción ni se omite la explicación.

## Evidencia

31 comprobaciones de menú fijo, orientación en cuatro direcciones, pivote desplazado, altura, integridad del grupo, giros izquierda/derecha y recalibración sin penalización aprobadas. Regresiones de fiabilidad y sesión aprobadas. Registro: `Logs/V2-recenter.log`.

Registros obtenidos del APK instalado: `Logs/Quest-audio-regression.txt`, `Logs/Quest-audio-startup-live.txt`. No contienen esta modificación nueva. Falta generar el APK e instalarlo por parte del autor para confirmar el recentrado del conjunto en Quest. No se generó ni instaló APK automáticamente.
