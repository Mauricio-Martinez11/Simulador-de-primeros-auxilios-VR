# Audio inicial en Quest y presentación de bienvenida

2 de noviembre de 2026. Incidencia comunicada: no se escucha la familiarización inicial del menú en el APK de Quest; no se llegó a las compresiones. No se dispone de registro del dispositivo, por lo que no se atribuye una causa única confirmada.

Cambios:
- La bienvenida coloca un único AudioListener activo en su cámara. Cubre entradas directas y cámaras cuyo listener estaba ausente o deshabilitado.
- La voz espera AudioDataLoadState.Loaded, un listener activo y un reloj DSP iniciado; reintenta el arranque sin consumir la duración de la explicación.
- Las locuciones se importan con precarga y PCM para evitar la dependencia de descodificación comprimida durante el inicio.
- La narración 2D usa prioridad alta, volumen unitario y omite efectos y reverberación espacial. No modifica el volumen del sistema de las Quest.
- Ante un cambio de configuración de audio, retoma la muestra observada antes del cambio. Se conservan pausa, silencio elegido y espera de dos segundos al terminar.
- Si falla el arranque tras ocho segundos activos, presenta el texto y registra la clave, estado de carga y disponibilidad de salida en el registro Unity.
- Diseño claro con encabezado por etapa, instrucciones grandes, tarjeta de estado y botón centrado cuando solo existe una opción; contraste azul oscuro/blanco y esquinas redondeadas.

Validación en dispositivo pendiente: generar e instalar el APK actualizado, abrir desde el arranque normal y escuchar «Mira a tu alrededor», mostrar manos y selección por láser. Comprobar después bienvenida, pausa/reanudación, ritmo y ambas modalidades. Si permanece el silencio, consultar el registro del APK y comprobar volumen/salida del sistema. No se generó APK automáticamente.

Verificación realizada: 24 comprobaciones de reproducción/navegación aprobadas, incluyendo señal no nula en AudioListener.GetOutputData, recuperación tras cambio de configuración y listener único en la cámara de bienvenida. También pasan 92 comprobaciones de V2, 13 de puntos/menú y regresiones anteriores. Diseño inspeccionado en `RCP-bienvenida-clara-V2.png`. Registro: `Logs/V2-audio-startup.log`. Esta prueba del mezclador de Unity no acredita por sí sola la salida física de las Quest.
