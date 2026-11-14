# Viabilidad: indicador / add-on NinjaTrader para fondeo $50K

**Proyecto:** Análisis de Mercado  
**Usuario:** Luis Emiro Sánchez  
**Fecha:** 2026-11-13  
**Complementa:** [`estrategia-fondeo-50k.md`](./estrategia-fondeo-50k.md)

---

## 1. ¿Para qué un indicador aquí?

La estrategia de evaluación depende menos de un “señalizador de entradas” y más de **disciplina de riesgo y reglas de cuenta**:

- DD diario restante ($1,000)
- PnL del día
- Consistencia 50% (mejor día vs profit total)
- Conteo de días operados (3–15)
- Tamaño máximo sugerido según stop en ticks y riesgo $ fijo

Un panel en NinjaTrader 8 (NT8) puede **bajar fricción cognitiva** y reducir fails por overtrading o por ignorar consistencia — no sustituye el edge del setup.

---

## 2. Qué debería medir / mostrar

### 2.1 Núcleo (MVP)

| Widget | Fuente de datos | Comportamiento |
|--------|-----------------|----------------|
| PnL día (realized ± unrealized) | Account / Position | Color verde/rojo; refresh en cada tick o al cierre de orden |
| DD diario restante | `1,000 − max(0, −PnL_día)` (o equity vs start-of-day) | Alerta visual < $400; alarma < $300 |
| Stop diario blando | Parámetro (ej. −650) | Banner “STOP DÍA” si se cruza |
| Meta diaria / soft cap | Parámetros (+450 / +750) | “META OK — no más riesgo” |
| Riesgo usado hoy | Suma de pérdidas o peak adverse | Informativo |
| Tamaño máx. sugerido | `floor(riesgo_$ / (stopTicks × tickValue))` + techo 40 micros / 4 minis | Input: instrumento, stop en ticks, riesgo $ |

### 2.2 Consistencia y calendario (MVP+)

| Widget | Notas |
|--------|--------|
| Profit total evaluación | Requiere historial desde fecha de inicio del challenge |
| Mejor día de profit | Max de PnL por día de trading |
| % del mejor día / total | Alerta si > 45% (margen antes del 50%) |
| “Cuánto puedo ganar hoy sin romper 50%” | `2 × (total − mejor) − pnl_hoy` (aproximación; validar definición exacta de la firma) |
| Días operados / restantes | Contar días con trades desde `StartDate` |

### 2.3 Alertas

- Acercamiento a stop diario blando
- Meta diaria alcanzada
- Soft cap de consistencia
- Tamaño de posición actual > tamaño sugerido
- (Opcional) minutos a noticia marcada manualmente

**Fuera de alcance del MVP:** señales de entrada (breakout, order flow, etc.). Eso es otra pieza y no es lo que desbloquea el pase de la evaluación.

---

## 3. Indicador vs estrategia / add-on vs Risk Management nativo

| Enfoque | Qué es en NT8 | Pros | Contras | Encaje |
|---------|---------------|------|---------|--------|
| **Indicador** (`Indicator`) | Overlay o panel en el chart | Rápido de prototipar; visible junto al precio | Acceso a Account a veces limitado/awkward; no bloquea órdenes; un chart ≠ cuenta | Bueno para **display** de sizing y niveles de riesgo *estimados* |
| **Add-on** (`AddOnBase`) | Ventana propia / tools menu | Mejor para Account, multi-instrumento, UI de dashboard; puede escuchar AccountItem/Position updates | Más código, ciclo de vida, debugging | **Mejor fit** para panel de reglas de fondeo |
| **Estrategia** (`Strategy`) | Automatiza o asiste órdenes | Puede aplanar / rechazar entradas si se programa | En evaluación muchos traders operan **manual**; risk “en estrategia” no protege orders manuales del chart trader | Útil solo si Luis automatiza o usa botones de la estrategia |
| **Risk Management nativo NT8** | Control Center → Risk | Límites de pérdida diaria, tamaño máx. por orden/cuenta a nivel plataforma | No calcula consistencia 50%, días de challenge, ni “tamaño según stop en ticks” de forma educativa | **Usar siempre** como red de seguridad dura (max loss / max qty) **además** de cualquier custom |

### Recomendación de arquitectura

**Híbrido parcial:**

1. **Nativo NT8 Risk:** max loss diario ≤ $700–$1,000; max qty acorde (ej. 15–20 MNQ, no 40).
2. **Add-on (preferido) o indicador avanzado:** dashboard de consistencia, días, PnL, tamaño sugerido, alertas.
3. **No** construir una estrategia de señales como primer entregable.

---

## 4. Complejidad, datos y limitaciones NinjaScript

### 4.1 Complejidad estimada

| Pieza | Esfuerzo relativo | Comentario |
|-------|-------------------|------------|
| Panel PnL día + DD restante | Baja–media | Patrón conocido; Account.Get(AccountItem, Currency) |
| Tamaño sugerido por tick value | Baja | Instrument.MasterInstrument.PointValue / TickSize |
| Alertas sonoras / rearmado diario | Baja–media | Timers; detección de sesión |
| Historial profit por día + consistencia 50% | Media–alta | Hay que **persistir** o reconstruir desde executions; reset a StartDate del challenge |
| Contar “días de trading” | Media | Agrupar executions por fecha de sesión (timezone ET) |
| Bloquear trading al violar regla | Alta | Add-on + account order interception o depender de Risk nativo; frágil y fácil de malinterpretar |

**Veredicto de complejidad global del MVP add-on:** media (días–corto tramo de desarrollo para un NinjaScript competente; más si Luis parte de cero).

### 4.2 Datos necesarios

- `Account` seleccionado (sim/eval): unrealized, realized, cash, etc.
- `Position` actual: quantity, avg price, instrument
- `Execution` / trade history: para PnL por día histórico y conteo de días
- `Instrument` specs: TickSize, PointValue (para MNQ/MES/MGC/MCL)
- Parámetros de usuario: DD max, stop blando, meta, soft cap, start date, riesgo $ por trade, stop en ticks

### 4.3 Limitaciones relevantes de NinjaScript / NT8

1. **Indicadores** están pensados para series de precio; el estado de cuenta es ciudadano de segunda. Muchos “risk indicators” de foro son frágiles entre versiones.
2. **Cuentas de prop / connections** pueden reportar PnL con delays o convenciones distintas (commission included or not). Calibrar.
3. **Definición de “día”** (reset 17:00 ET vs medianoche) debe ser parámetro; si no coincide con la firma, el DD “restante” engaña.
4. **Consistencia 50%** no es nativa: hay que implementar la fórmula y **documentar el supuesto**; la firma puede medir solo días con profit, o closed PnL only, etc.
5. Un indicador **no impide** pulsar Buy Market; la protección dura es Risk Management nativo o disciplina.
6. Distribución: add-ons propios requieren import `.zip` / compile en NinjaScript Editor; mantenimiento en cada update mayor de NT.
7. **Backtest** del add-on de riesgo de cuenta no valida el edge del mercado; solo la UX.

---

## 5. Recomendación

### Veredicto: **Parcial — sí al MVP de riesgo/reglas; no al “indicador de entradas” como prioridad**

| Pregunta | Respuesta |
|----------|-----------|
| ¿Conviene construir algo en NT8? | **Sí, parcial**: un **add-on dashboard** (o indicador panel si se quiere MVP ultra-simple) orientado a reglas de fondeo |
| ¿Sustituye Risk nativo? | **No** — combinar ambos |
| ¿Indicador de señales para pasar el challenge? | **No prioritario**; el cuello de botella es riesgo/consistencia/frecuencia |
| ¿ROI para Luis (diseñador, no dev NT full-time)? | Alto si el panel evita 1 fail por overtrading; bajo si se convierte en proyecto infinito de señales |

### MVP concreto (lista de features)

**Must have**

1. Selector de Account  
2. PnL del día (realized + unrealized)  
3. DD diario restante vs $1,000 (parametrizable)  
4. Estados: OK / cerca del stop / STOP DÍA / META DÍA  
5. Calculadora de contratos sugeridos (stop ticks × tick value × riesgo $)  
6. Parámetros: stop blando, meta, soft cap, max contracts  

**Should have**

7. Registro / reconstrucción de PnL por día desde `StartDate`  
8. Mejor día, total, % consistencia + “max win restante hoy”  
9. Contador días operados / días restantes (máx. 15)  
10. Alertas sonoras + color + mensaje en UI  

**Won’t have (v1)**

- Señales de entrada / order flow  
- Auto-flatten agresivo (opcional v2; preferir Risk nativo)  
- Multi-cuenta compleja  
- Cloud sync / mobile  

### Siguiente paso si Luis quiere desarrollarlo

1. **Validar** con la firma (o su FAQ) la hora de reset del DD y la definición exacta de consistencia 50%.  
2. Activar **Risk Management nativo** mañana (1 hora de trabajo, cero código).  
3. Prototipo: **indicador panel** solo con PnL día + DD restante + size calculator (validar que Account data es confiable en su connection).  
4. Si el prototipo es estable → promover a **Add-on** con historial y consistencia.  
5. Diseñar UI simple (Luis): una columna, tipografía clara, estados semáforo — sin dashboard sobrecargado.  
6. Checklist de aceptación: simular día con −$650, +$450, día oversized vs 50%, y cambio de MNQ↔MES.  

**Alternativa sin código:** Google Sheet + atajos + Risk nativo cubre el 80% del valor del MVP; el add-on aporta sobre todo **visibilidad in-platform y alertas en el momento del click**.

---

## 6. Relación con el plan operativo

El plan de `estrategia-fondeo-50k.md` ya define los números que el panel debe mostrar:

- Stop blando −$650 · meta +$450 · soft cap +$750 · riesgo $150–$200 · MNQ prioritario  

El software **refuerza** ese plan; no lo inventa.

---

*Análisis de viabilidad técnica/product. Los límites exactos de Account API pueden variar según versión NT8 y tipo de connection; validar en el entorno real de Luis antes de comprometer un desarrollo largo.*

---

## 7. Estado MVP (2026-11-13)

**Entregado:** código Add-on `FundingRulesPanel` (AddOnBase, no indicador) con features must/should del §5.

| Ruta | Contenido |
|------|-----------|
| [`ninjatrader/FundingRulesPanel/`](./ninjatrader/FundingRulesPanel/) | Fuentes `.cs` + `ARCHITECTURE.md` + `CHANGELOG.md` |
| [`ninjatrader-mvp-instalacion.md`](./ninjatrader-mvp-instalacion.md) | Instalación, uso, Risk nativo, limitaciones |

**No incluido (por diseño):** señales, bloqueo duro de órdenes, automatización.  
**Siguiente validación humana:** compilar en NT8 de Luis y contrastar reset/consistencia con la firma.
