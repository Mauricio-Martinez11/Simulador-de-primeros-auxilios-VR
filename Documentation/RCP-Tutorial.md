# Tutorial de RCP: manos enganchadas y traslado del paciente

**Modo experimental anterior.** Desde el 17/10/2026, el tutorial inicia en una estación fija con compresiones y evaluación. Consulta [la guía actual](RCP-Tutorial-Animado.md). Las instrucciones y los parámetros que siguen documentan únicamente ese modo experimental anterior. El traslado, el desplome y sus hologramas permanecen desactivados mediante `stationaryPractice`.

Escena: `Assets/Scenes/2_Tutorial_RCP.unity`. Entrada habitual: **Boobstrap → menú → Iniciar**. Unity 2022.3.62f2 / Meta XR SDK 201.

## Uso

1. Comprueba el entorno y confirma apuntando al botón con el láser: gatillo del mando o pinza con la mano. También puedes acercar la mano al botón (0,55 s). Mirarlo no lo activa.
2. En el ejercicio de traslado aparecen dos manos holográficas por turno. La secuencia ofrece tres opciones: dos manos en el brazo izquierdo, dos en el brazo derecho y dos en las piernas/tobillos. Acerca una mano a la opción indicada y **haz pinza o cierra los dedos**. Con mandos, mantén el botón de agarre.
3. Arrastra moviendo la mano. Puedes comenzar con cualquiera de las manos y añadir la otra al mismo brazo o al otro. Las guías se adaptan a la mano que agarra. El panel indica si está agarrada la izquierda, la derecha o ambas. El cuerpo completo puede levantarse, girar y caer; la pelvis no está fijada al suelo.
4. Al acercarlo y alinearlo con la silueta, esta cambia a verde. **Abre ambas manos o suelta los agarres** para colocarlo con un ajuste suave. Abrir solo una deja la otra agarrada. Soltar fuera del destino deja caer el cuerpo por gravedad.
5. Continúa con respuesta, observación del pecho y solicitud simulada de ayuda/DEA.
6. Coloca primero el talón de la **mano izquierda** en la guía del pecho y mantén 1,25 s. La guía verde queda enganchada.
7. Coloca la **derecha encima de la izquierda**, siguiendo la postura de dedos de la guía. Una vez capturada, la representación mantiene ambas manos en esa postura.
8. **Separa la derecha** para liberar la mano superior. **Aparta la izquierda** para liberar toda la postura. Puedes colocarlas de nuevo sin reiniciar.
9. Puedes volver a agarrar al paciente por los brazos después de colocarlo: se liberan las manos RCP y se vuelve al traslado.
10. Repetir práctica restablece el paciente y toda la secuencia. Volver al menú está disponible en cada paso.

El cuerpo usa un ragdoll articulado. Al activar el traslado, la animación se desactiva, cada segmento tiene su propio cuerpo rígido y las articulaciones bloquean la distancia entre huesos. Tirar del antebrazo transmite la fuerza al tronco, cadera y piernas; la malla conserva la longitud de sus huesos y no se queda pegada a un plano. Al soltar, el cuerpo conserva la inercia y descansa sobre el suelo mediante sus colisiones.

Las manos holográficas se vuelven verdes al agarrar. Solo aparece una pareja de guías por turno; acercarse detiene la secuencia en esa opción. Hay señales de sonido al capturar/colocar y vibración cuando se usan mandos compatibles. El botón **Ver desplome** permite observar la caída desde una postura de pie.

## Probar con Meta XR Simulator

El simulador sí permite agarrar al paciente. Entra en **Inputs**, cambia **Left input** y **Right input** a **Hand** y revisa **Input bindings → Hand actions**. La asignación predeterminada del simulador usa `4` para la pose de mano cerrada (agarre), `1` para volver a la mano abierta/apuntando y `3` para la pinza. Mueve cada mano con el teclado o el ratón, acércala a la guía verde y pulsa `4`; para soltar, pulsa `1` y sepárala. Si cambiaste las teclas, utiliza las que aparecen en **Input bindings**.

Si dejas **Controller** en uno de los lados, el icono del mando aparece en la vista. En ese caso mantén la tecla que figure para **Grip/Grab** en **Input bindings** mientras mueves ese mando simulado. La mano izquierda y la derecha se configuran por separado; ambas deben estar en **Hand** si quieres completar una opción con las dos manos.

El código acepta la mano sintética del simulador únicamente en el editor y conserva el seguimiento medido como prioridad en el visor. Por eso la misma guía, el agarre verde, el arrastre y el soltado funcionan en ambos modos. Más controles del simulador: [teclado y asignaciones de entrada](https://developers.meta.com/horizon/documentation/unity/xrsim-input-keyboard/) y [gestos de mano](https://developers.meta.com/horizon/documentation/native/xrsim-hand-tracking/).

## Oclusión y seguimiento

La entrada de Meta conserva siempre las mediciones originales. El enganche es un estado de la interacción y de su representación; no modifica el seguimiento del visor.

- La izquierda debe haberse colocado con seguimiento válido. La derecha no puede iniciar la postura.
- Una vez capturada la izquierda, la derecha puede completar la colocación aunque la oculte. La mano visible en la zona correcta sostiene el enganche.
- Después de enganchar las dos, se tolera que una permanezca oculta mientras la otra siga en la zona de la postura.
- Si desaparecen ambas durante 0,8 s, se libera la postura. La separación observada tiene un filtro de 0,12 s para evitar parpadeos por un fotograma inestable.
- El panel indica cuándo se está conservando la postura durante una oclusión.
- En el arrastre, una pérdida breve de seguimiento conserva la selección hasta 0,6 s y desactiva el tirón de esa mano mientras el cuerpo sigue respondiendo a la gravedad. Al recuperarla cerca, el agarre vuelve a seguir su posición real. Si reaparece a más de 65 cm del contacto, se suelta: no se guarda una separación permanente entre mano y brazo.
- Basta un dedo curvado con datos fiables para reconocer el puño; la pinza también sirve. La apertura debe observarse durante 0,1 s para que un fotograma aislado no suelte el brazo.
- Perder el seguimiento no cuenta como abrir la mano y no aprueba el destino. Tras una pérdida prolongada, abre y vuelve a cerrar para agarrar.
- Perder el foco del visor cancela los agarres, el ajuste en curso y el enganche RCP.

Si una mano oculta se separa sin volver a ser visible, no es posible medir esa separación; la liberación se decide al recuperar seguimiento o al cumplirse las condiciones anteriores.

## Configuración

**Primeros auxilios → RCP → Seleccionar configuración** abre `Assets/Resources/RCP/TrainingSettings.asset`.

| Parámetro | Valor inicial / función |
| --- | --- |
| Capture Radius / Release Radius | 8,5 / 14 cm; entrada y mantenimiento de la postura. |
| Placement Hold | 1,25 s de colocación. |
| Stacked Hand Height | 3,5 cm entre los talones de las guías. |
| Lower Hand Occlusion Grace | 3 s sin evidencia que sostenga la izquierda capturada. |
| Both Hands Lost Grace | 0,8 s sin ninguna mano visible. |
| Separation Hold | 0,12 s de separación observada para soltar. |
| Hand Separation | 16 cm de distancia máxima entre las manos capturadas; también se verifica orden vertical y separación lateral. |
| Arm Grab Radius | 16 cm alrededor de los puntos de agarre en los antebrazos. |
| Grab Threshold / Grab Release Threshold | 0,65 / 0,30; cierre y apertura con histéresis. |
| Grab Tracking Grace | 0,6 s de tolerancia durante el arrastre. |
| Grab Open Hold | 0,1 s de apertura observada para soltar. |
| Patient Destination Offset | X = -1,15 m lateral; Z = 0,35 m longitudinal respecto al paciente inicial. |
| Patient Snap Radius / Angle | 28 cm / 25° para habilitar el ajuste. |
| Patient Snap Duration | 0,4 s de ajuste al soltar. |
| Grab Spring / Damping / Max Force | 60000 / 900 / 3000; articulaciones de agarre resueltas junto con el cuerpo. |
| Grab Target Speed | 6 m/s; límite de desplazamiento del objetivo físico de cada mano. |
| Patient Mass / Friction | 72 kg / 0,35. |
| Grab Guide Seconds / Show Ankle Grips | 2,4 s por pareja / permite desactivar los tobillos. |
| Controller Palm Offset / Euler | Calibración aproximada de la palma desde el mando. |
| Controller Stack Extra | 6,5 cm adicionales de separación física entre mandos. |

En **RCP_Tutorial** se pueden asignar `Patient Destination`, `Left Arm Anchor` y `Right Arm Anchor`. Sin asignarlas, se generan el destino y los agarres a partir de la configuración y los huesos `CC_Base_*_Forearm/Hand`.

El destino manual representa la posición y rotación finales de la raíz del paciente: conserva su orientación tumbada y ajusta solo el giro sobre el suelo. La altura final se mantiene en el plano inicial. Las anclas de pecho/hombro deben pertenecer al paciente si han de acompañar el traslado.

## Modelos y alcance

Las manos abiertas y entrelazadas se hornean desde las mallas del SDK de Meta. En la postura apilada se elevan los dedos de la izquierda y se flexionan las falanges de la derecha. La guía muestra la postura solicitada; no evalúa ópticamente cada dedo entrelazado.

El holograma del paciente se genera a partir de sus mallas visibles y de su pose tumbada inicial, sin copiar scripts, cámaras ni colisiones. El ragdoll tiene 16 cuerpos rígidos y 15 articulaciones anatómicas. Los cuerpos físicos son independientes del FBX y tienen escala unitaria; al retargetear se conservan las posiciones y escalas locales originales de los huesos. Las articulaciones de agarre transmiten el movimiento de cada mano al conjunto sin mover directamente los huesos ni fijar la cadera.

Cada segmento tiene un volumen de colisión. Los brazos superiores colisionan con el pecho, incluidos los pares conectados por el hombro. Los demás pares vecinos que se solapan en su bisagra se excluyen; los no vecinos colisionan. La zona lumbar y el pecho tienen límites separados de flexión y giro para evitar que se pliegue toda la cintura. Los volúmenes son aproximaciones de la anatomía y la ropa; no se simula tela.

El FBX incluye una piel completa debajo de la camiseta y los jeans. Se utiliza `Resources/RCP/PatientClothedBody.asset`, una copia de la malla con los triángulos cubiertos omitidos y el cuello conservado. Se mantienen todos los materiales válidos: asignar `null` a un material provocaba la superficie rosa en Quest. La copia se hornea en el editor porque el FBX no es legible por CPU en Android; no necesita modificar materiales ni leer vértices durante la práctica. Al destruir el ragdoll se restaura la malla original. El menú **Regenerar piel cubierta por ropa** actualiza este recurso si cambia el FBX.

Los botones del panel usan `Button`, `GraphicRaycaster`, `PointableCanvas` y una superficie `RayInteractable` limitada al panel, como el menú principal. El mismo láser del rig permite seleccionar con manos o mandos; la superficie acompaña la posición y rotación del panel. Los clics se bloquean durante la pérdida de foco y el breve cambio de paso. El `EventSystem` y su módulo de Meta se reutilizan si ya existen.

El perfil de brazos limita la muñeca a 20°/25° de flexión en un solo eje, bloquea su torsión axial y el giro lateral, y limita el codo a una flexión de 5–115°. Ese pequeño margen de extensión permite absorber errores del solver durante los impactos. En el hombro se separa la torsión del húmero (±35°) del movimiento de elevación. Son topes conservadores de esta interacción, no una evaluación de movilidad clínica. Hay resistencia y amortiguación suaves alrededor de una postura relajada para evitar que la mano caiga de golpe hasta el tope, manteniendo el cuerpo dinámico.

La mano del paciente queda en una postura pasiva: dedos ligeramente flexionados y pulgar recogido, como una mano que cuelga sin tensión. Esta pose se aplica a las cadenas de dedos del modelo cuando se sincroniza el ragdoll; no deforma los huesos ni cambia la malla del paciente. Los valores pueden ajustarse en `TrainingSettings` (`relaxedFingerCurl`, `relaxedFingerCurlDistal`, `relaxedThumbCurl`).

Los ejes de muñeca y codo se calculan con la dirección del antebrazo de cada lado. Los límites X se convierten al sentido de rotación de `ConfigurableJoint`; esta conversión evita que el recorrido amplio de codos y rodillas se permita hacia atrás. La resistencia se aplica mediante las [fuerzas de articulación de Unity](https://docs.unity3d.com/2022.3/Documentation/Manual/configurable-joints-driving-forces.html), sin recortar únicamente la rotación de la malla.

El traslado es un ejercicio de manipulación del escenario y no valida una técnica clínica de movilización. La práctica de RCP guía la secuencia y la colocación; no mide fuerza, profundidad, ritmo, ventilaciones, codos ni hombros.

## Verificación

Corrección de presentación (30/09/2026): se comprobaron 33 casos de malla/materiales y botones, además de las 202 comprobaciones de RCP y ragdoll. Se enviaron eventos de selección por `RayInteractable → PointableCanvas → EventSystem → Button`, con identificadores de puntero distintos, panel desplazado y girado, liberación del clic, bloqueo durante espera y reutilización del módulo. Son pruebas del editor; la verificación dentro de Quest queda para el APK que genere el usuario. Registro: [presentación Quest](RCP-validacion-quest-presentacion.txt).

Menús disponibles:

- **Verificar secuencia y detección**: orden izquierda/derecha, oclusión, recuperación, separación, reinicio, foco y secuencia previa.
- **Verificar agarre y traslado**: entra en Play Mode y ejecuta pruebas con el modelo real en escenas físicas aisladas: caída, arrastre, derecha primero, izquierda después, elevación con dos manos, soltar una y ambas, colisiones propias, oclusión, snap y reinicio.
- **Verificar integración del proyecto**: verifica la secuencia de RCP y las referencias de escena, materiales y mallas. Las pruebas físicas se ejecutan desde su menú específico.
- **Regenerar postura entrelazada**: vuelve a hornear las dos mallas apiladas manteniendo sus referencias.
- **Regenerar modelos holográficos**: regenera las manos abiertas y las apiladas.

Se utiliza una copia aislada de Unity para ejecutar las comprobaciones y generar las vistas de revisión; no se guarda ni sustituye la escena que esté abierta en el editor del usuario.

Las 59 comprobaciones registradas anteriormente correspondían al traslado anterior y no demostraban el funcionamiento del ragdoll. La validación actual se registra por separado en `Logs/InteractionValidation/Ragdoll-validation.txt`, con medidas de desplazamiento, separación de agarre y penetración entre volúmenes. El ejecutor conserva su estado al recargar el dominio al entrar en Play Mode.

Corrección verificada: **202 comprobaciones correctas (30 de RCP, 104 de ragdoll/agarre y 68 de postura de brazos)** con el FBX real. Se probó la elevación con pasos físicos de 0,02 s y 1/72 s. El tirón arrastró la pelvis 1,317 m; el máximo error de unión entre articulaciones fue 4 mm; la separación medida al terminar la elevación con ambas manos fue 3,61 cm; la penetración máxima medida entre brazos y torso durante la prueba de contacto fue 1,4 mm. La muñeca quedó limitada a 25° y el codo a 0–115° durante las cargas de torsión, elevación y caída. También pasaron el agarre con la derecha primero, incorporación de la izquierda, recuperación tras oclusión, liberación individual y límites de cintura. Estas cifras describen las pruebas reproducibles del editor, no mediciones del visor.

Resultado conservado: [registro de validación](RCP-validacion-ragdoll.txt). Vistas de la malla real en las pruebas: [elevación con dos manos](RCP-ragdoll-dos-manos.png) y [caída después de soltar](RCP-ragdoll-caida.png). Para las capturas se horneó la pose de cada instante, evitando reutilizar el búfer de piel de una captura anterior dentro del mismo fotograma del editor.

Vistas de revisión: [manos enganchadas](RCP-manos-enganchadas.png) y [traslado al holograma](RCP-traslado-holograma.png).

La prueba con unas Quest físicas sigue pendiente: oclusión real, detección de pinza/puño, comodidad del alcance, calibración de mandos y legibilidad dentro del visor. No se ha generado ni instalado un APK.

## Base del contenido previo de RCP

- [American Heart Association: Adult Basic Life Support, 2025](https://cpr.heart.org/en/resuscitation-science/cpr-and-ecc-guidelines/adult-basic-life-support).
- [American Red Cross: Hands-Only CPR](https://www.redcross.org/take-a-class/cpr/performing-cpr/hands-only-cpr).

