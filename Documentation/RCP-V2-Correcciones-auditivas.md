# Correcciones auditivas durante compresiones

Revisión: 3 de noviembre de 2026.

CprStationAudio usa un narrador independiente para las correcciones. No modifica los bloqueos de explicación, el detector, el conteo, la puntuación ni el reloj del metrónomo. La pausa y la pérdida de foco suspenden la voz; silenciar afecta a ambas voces. Repetir voluntariamente una explicación detiene la corrección; terminar cambia a resultados.

CprCorrectionVoice comprueba el problema actual, exige 0,65 s de persistencia, deja 8 s de silencio entre avisos y al menos 22 s entre inicios del mismo aviso. No mantiene una cola de errores. Prioriza colocación y seguimiento; después ritmo, retorno y recorrido. La fase se usa como orientación adicional. Los avisos de seguimiento son técnicos, sin descuento.

Recursos de voz: Separate, Away, Lost, Fast, Slow, Return, Shallow, Deep y Timing. Se actualizaron Separate y Fast y se generó Timing con la misma voz local del tutorial.

Validación: CprCorrectionVoiceChecks.Run en Unity Editor. Control de saturación, ausencia de cola y solapamiento, silencio/mute, existencia de clips y 26 ciclos contados mientras se activa la voz. Regresión de compresiones: 110 cpm/18 ciclos; 150 cpm/25 ciclos; 190 cpm/30 ciclos, con errores de velocidad detectados. Pruebas de recolocación y dos movimientos del hombro satisfactorias. Registro: Logs/V2-correction-voice-editor.txt.

La primera ejecución incluyó una prueba antigua de navegación del menú que exige foco; falló al ejecutarse con el editor sin foco. Las pruebas finales se limitaron al comportamiento modificado y a sus regresiones. No se alteró el menú.

No se generó APK. Falta la comprobación auditiva de esta modificación en Quest.

Documento MCM-Proyecto de Grado: condición del hombro actualizada en la tabla y explicación; petición de ayuda sin DEA; guía del Anexo I coherente con los dos movimientos y el indicador circular de respiración. Referencias teóricas a DEA conservadas.
