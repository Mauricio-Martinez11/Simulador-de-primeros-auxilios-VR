# Recuperación de manos durante la narración y hologramas

2 de noviembre de 2026.

## Bloqueo del paso de compresiones

Durante la explicación se procesaba la separación de manos, pero no la nueva colocación. Se añadió lectura de colocación secuencial también durante la explicación, sin registrar ciclos ni interrumpir la voz. Al recuperar derecha e izquierda se actualizan las referencias reales del detector. Si el alumno termina de escuchar con las manos separadas, puede recolocarlas dentro del paso de compresiones sin reiniciar automáticamente otra explicación completa. Las pérdidas de seguimiento no equivalen a separación observada.

Las guías muestran primero la derecha y luego la izquierda al reconocer la mano inferior. La prueba reproduce separación y recolocación durante audio, separación al finalizar audio y tres ciclos posteriores; no se atribuye penalización a la separación previa al comienzo.

## Apariencia

La altura de las guías se calcula con el talón de la mano inferior sobre la camiseta. Antes la envolvente de los dedos de ambas manos podía elevar toda la postura. Se regeneraron las mallas con menor apertura lateral de dedos, flexión revisada de la mano superior y pulgares recogidos. Se conservaron los GUID de los recursos.

En modo apoyo mixto se ocultan renderizadores internos (ojos, dientes, lengua y conductos lagrimales), conservando su estado original para restaurarlo. Un shader exclusivo del paciente usa profundidad de superficie y descarte de caras posteriores; las guías de manos conservan su material independiente. La superficie sigue siendo tenue para ver el apoyo real.

## Validación

`CprRegripChecks.Run`: recuperación y tres compresiones tras la explicación aprobadas; regresiones existentes de fiabilidad y sesión aprobadas. `CprRegripChecks.Preview`: inspección de contacto y exterior del rostro, y comprobación de ocultación de las piezas internas reales del modelo. Registro `Logs/V2-regrip.log`; capturas de revisión en la copia de validación.

No se generó APK. Queda comprobar en Quest la postura percibida, oclusiones reales y el paso de recolocación durante la voz con el APK actualizado.

## Postura según referencias fotográficas (2 de noviembre)

Se revisó la guía desde arriba y de lado: mano derecha inferior semiabierta con separación moderada de dedos; izquierda superior con flexión por falange, desplazamiento lateral progresivo de 8 mm en los dedos para alternarlos con los inferiores y descenso de 8 mm de la malla para acercar las palmas. Los pulgares se ajustan por lado. Los cambios son visuales; se conserva el orden y los umbrales del reconocimiento. No se exige que el seguimiento reconozca cada dedo entrelazado.

Recursos regenerados y copiados al proyecto manteniendo GUID: StackedLeftHand.asset y StackedRightHand.asset. Capturas: RCP-postura-entrelazada-superior.png y RCP-postura-entrelazada-lateral.png. La postura es una aproximación ilustrativa de las referencias; queda revisar su percepción estereoscópica en Quest.

### Aclaración de apertura de dedos

El usuario aclaró con dos imágenes que «más arriba» se refería a la postura: primera mano semiabierta y luego entrelazar la segunda; no se desplaza el punto de contacto sobre el pecho. Se amplió el abanico de la mano inferior (índice a meñique: -17, -5, 10 y 24 grados respecto al eje longitudinal) y se redujo la flexión superior a 8/43/18 grados por falange. Recursos regenerados conservando sus GUID. Capturas actuales: RCP-postura-abierta-superior.png y RCP-postura-abierta-lateral.png. Registro de validación: Logs/V2-open-hand-checks.log. La comprobación visual en Quest permanece pendiente.
