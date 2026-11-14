# Estrategia de operación — Cuenta de fondeo $50K

**Proyecto:** Análisis de Mercado  
**Usuario:** Luis Emiro Sánchez  
**Fecha:** 2026-11-13  
**Alcance:** Evaluación / challenge con las reglas exactas indicadas abajo. No se inventan reglas de firmas concretas no dadas.

---

## Reglas de la cuenta (fuente: usuario)

| Parámetro | Valor |
|-----------|--------|
| Capital | $50,000 |
| Drawdown máximo diario | $1,000 |
| Consistencia | Máximo 50% del profit total en un solo día (para pasar la prueba) |
| Días de trading | Mínimo 3 · Máximo 15 |
| Tamaño máximo | 4 contratos o 40 microcontratos |
| Activos | NQ/MNQ, ES/MES, GC/MGC, CL/MCL |

### Supuestos explícitos (no dados por el usuario)

1. **Profit target de evaluación:** se asume **$3,000** como objetivo típico de challenges “50K” en la industria prop. Si la firma real exige otro monto, recalibrar metas diarias; la lógica de riesgo y consistencia se mantiene.
2. **Tipo de DD diario:** se interpreta como pérdida máxima neta del día (realized + unrealized según cierre de sesión de la firma). Si la firma usa trailing o DD estático sobre equity, el stop operativo diario debe ser aún más conservador.
3. **“Día de trading”:** día calendario con al menos una operación ejecutada (no solo órdenes canceladas).
4. **Comisiones / fees:** no modeladas en detalle; restar ~$1–4 por lado en micros y más en minis al planificar R:R neto.
5. **Horario de reset del DD diario:** típico ~17:00–18:00 ET; confirmar con la firma.

---

## 1. Interpretación práctica de cada regla

### 1.1 Drawdown máximo diario = $1,000

- Es el **techo duro de supervivencia del día**. Un solo trade mal dimensionado o un stop corrido puede liquidar la sesión (y a menudo la cuenta).
- Condiciona el **riesgo por trade**:
  - Conservador: **0.25–0.35%** del capital ≈ **$125–$175** por trade.
  - Agresivo aún viable: **máx. ~$250–$300** por trade (25–30% del DD diario), dejando buffer para slippage y 2–3 trades perdedores.
- Condiciona el **R:R**: con stops pequeños en puntos, hace falta tamaño en micros o un R:R ≥ 1:1.5–1:2 para que el expectancy cubra wins/losses.
- Condiciona la **frecuencia**: tras 2–3 pérdidas encadenadas cerca del tope de riesgo diario, **parar**. Overtrading cerca del DD es la causa #1 de fail en challenges.

**Regla operativa sugerida:** stop diario blando a **−$600 / −$700** (60–70% del límite). El 30–40% restante es colchón de slippage, spread y gap.

### 1.2 Consistencia: máx. 50% del profit total en un día

- Impide “pasar en un día de suerte”. Ejemplo con target $3,000: **ningún día puede aportar más de $1,500** del PnL acumulado al momento del pase (la definición exacta suele ser: el mejor día ≤ 50% del profit total neto al cerrar la evaluación).
- Condiciona a **acumular en varios días** con PnL diario relativamente parejo.
- Implica: si un día ganas $1,800 y el total es $2,200, ese día es ~82% → **no pasas** aunque ya hayas superado el target. Necesitas más profit en otros días (o un target más alto efectivo) hasta que el mejor día ≤ 50%.
- Condiciona el **tamaño y la meta diaria**: mejor varios días de +$400 / +$600 / +$700 que un día de +$2,000 y tres flat.

### 1.3 Mínimo 3 / máximo 15 días

- **Mínimo 3:** obliga a repartir el profit. Plan mínimo viable: 3 días productivos + buffer.
- **Máximo 15:** hay presión de tiempo pero no de “pasar mañana”. Evita forzar setups mediocres los últimos días; si llegas al día 12–13 lejos del target, reduce tamaño y busca solo A+ setups, o acepta reset (según política de la firma — no asumida aquí).
- Frecuencia ideal: **operar 5–10 días** de calidad, no llenar 15 por obligación.

### 1.4 Tamaño máximo: 4 minis o 40 micros

- Define el **apalancamiento máximo**, no el tamaño óptimo.
- Con DD $1,000, **4 NQ o 4 CL** es peligroso: un movimiento adverso corto puede consumir el DD casi de inmediato.
- Los micros permiten granularidad (1–10 MNQ/MES) alineada con el riesgo por trade de $150–$250.

### 1.5 Universo de activos

Solo futuros líquidos de equity index, oro y crudo (mini/micro). Priorizar los que ofrezcan **rango intradía predecible + spreads estrechos + horarios claros** para el estilo elegido (ver §4).

---

## 2. E-mini vs micros — comparación y recomendación

Valores de tick de referencia (CME; redondeados a práctica común):

| Instrumento | Tick | Valor/tick | Valor por punto | Control DD $1,000 |
|-------------|------|------------|-----------------|-------------------|
| NQ | 0.25 | $5 | $20 | 1 contrato ≈ $20/pt → 50 pts = $1,000 |
| MNQ | 0.25 | $0.50 | $2 | 10 MNQ ≈ 1 NQ; granularidad fina |
| ES | 0.25 | $12.50 | $50 | 1 contrato ≈ $50/pt → 20 pts = $1,000 |
| MES | 0.25 | $1.25 | $5 | 10 MES ≈ 1 ES |
| GC | 0.10 | $10 | $100/unidad ($1) | Muy sensible al tamaño |
| MGC | 0.10 | $1 | $10/unidad | Mejor para DD diario |
| CL | 0.01 | $10 | $1,000/unidad ($1) | 1 CL: 0.10 = $100; alto riesgo |
| MCL | 0.01 | $1 | $100/unidad | Preferible vs CL en challenge |

**Margen típico (conceptual, no cifra de firma):** micros suelen exigir ~1/10 del margen del mini. En una cuenta simulada de prop el “margen” lo impone el límite de contratos, no el broker retail; aun así, el **riesgo en dólares por tick** es lo que importa para el DD.

### Recomendación de instrumentos prioritarios

1. **Primario: MNQ** — liquidez alta, rango amplio en RTH/London–NY overlap, tamaño escalable 2–15 contratos sin saltar a riesgo de mini.
2. **Secundario: MES** — algo más “tranquilo” que NQ; útil si se busca menos ruido y stops en puntos más estables.
3. **Situacional: MGC** — buenos rangos en Asia/Londres; evitar alrededor de datos US fuertes si no es el setup.
4. **Situacional con cautela: MCL** — volatilidad y gaps de noticias (EIA, geopolítica); solo con tamaño muy pequeño y horarios filtrados.
5. **Evitar como default en evaluación: NQ/ES/GC/CL a 2–4 contratos** — el DD diario se consume demasiado rápido; reservar 1 mini solo si el stop en puntos es muy estrecho y el edge está probado (poco recomendable al inicio).

**Conclusión:** operar la evaluación casi exclusivamente en **micros (MNQ ± MES)**, tamaño 4–12 micros según volatilidad del día, techo blando ~15–20 micros aunque el límite sea 40.

---

## 3. Perfil de riesgo diario / sesión

### 3.1 Límites numéricos sugeridos (con target asumido $3,000)

| Concepto | Valor sugerido | Notas |
|----------|----------------|-------|
| Stop diario (blando) | −$600 a −$700 | Parar el día; no “recuperar” |
| Meta diaria | +$350 a +$600 | Ver §5; techo de consistencia |
| Techo diario de profit (soft) | ≈ +$800 a +$1,000 | Evitar un día que luego viole 50% |
| Riesgo por trade | $150–$250 | 1R fijo en dólares |
| Trades máximos / día | 3–5 | Tras 3 pérdidas o meta alcanzada → stop |
| Trades A+ solo | 1–3 | Calidad > cantidad |
| R:R mínimo de entrada | 1:1.5 (ideal 1:2) | Si no hay espacio al target, no entrar |

### 3.2 Respetar la consistencia del 50%

Fórmula de control en vivo:

\[
\text{Máx. permitido hoy (aprox.)} = \max\bigl(0,\ 2 \times P_{\text{otros días}} - P_{\text{hoy parcial}}\bigr)
\]

Más simple en la práctica:

1. Lleva un Excel/Notion: profit por día.
2. Tras cada día, calcula: `mejor_día / profit_total`. Debe tender a **≤ 0.45** con margen (no rozar 0.50).
3. Si un día “se dispara” (+$1,200 temprano): **dejar de agregar tamaño**; proteger y cerrar sesión. Luego planifica días siguientes para **diluir** ese día bajo el 50%.
4. Nunca intentes “igualar” un día enorme arriesgando más: diluye con días verdes moderados.

Ejemplo sano (target $3,000):

| Día | PnL | Acumulado | Mejor día / total |
|-----|-----|-----------|-------------------|
| 1 | +500 | 500 | 100% (aún no importa) |
| 2 | +550 | 1,050 | 52% → seguir |
| 3 | +500 | 1,550 | 35% |
| 4 | +600 | 2,150 | 28% |
| 5 | +500 | 2,650 | 23% |
| 6 | +400 | 3,050 | 20% → **pase limpio** |

---

## 4. Estrategia recomendada para pasar en 3–15 días

### 4.1 Enfoque general

- **Estilo:** intradía direccional con confirmación (no scalping hiperactivo; no overnight en evaluación salvo regla explícita — **asumir flat al cierre**).
- **Edge:** pocos setups de alta confluencia en horarios de liquidez; gestión mecánica de 1R.
- **Objetivo de calendario:** pasar en **6–10 días de trading** productivos dentro de la ventana 3–15.

### 4.2 Setups de alto edge (checklist de entrada)

Operar solo si se cumplen **≥ 3** de:

1. Nivel clave previo (VWAP, high/low de sesión, PDH/PDL, zona de igualdad de máximos/mínimos).
2. Alineación con sesgo de apertura / estructura de 15–30 min (HH-HL o LH-LL).
3. Disparador: ruptura con volumen relativo o rechazo claro (pin / engulfing en 1–5 min) en dirección del sesgo.
4. Stop técnico **atrás de estructura**, no “número redondo arbitrario”, y riesgo en $ dentro de $150–$250.
5. Espacio libre hasta el próximo nivel ≥ 1.5R.

**Evitar:** medias móviles como único filtro, martingala, averaging down, noticias de alto impacto sin plan.

### 4.3 Horarios / sesiones favorables (ET)

| Activo | Ventanas preferidas | Evitar / reducir |
|--------|---------------------|------------------|
| MNQ / MES | 9:35–11:30; 14:00–15:45 (continuaciones) | 8:20–9:35 caos post-open sin nivel; lunch 12:00–13:30 |
| MGC | 3:00–5:00 (Asia tardía/Londres); 8:00–10:00 | FOMC / CPI si no es setup de noticia |
| MCL | 9:00–11:00; post-inventarios solo si es el plan | EIA sin tamaño; geopolítica abierta |

**Sesión principal recomendada para Luis:** RTH US en **MNQ**, 1–2 horas de foco (p. ej. 9:45–11:15), luego stop de sesión si no hay A+.

### 4.4 Gestión de posición

1. Entrada con tamaño fijo en $ de riesgo (no “siempre 10 micros”).
2. Calcular: `contratos = riesgo_$ / (stop_en_ticks × valor_tick)`.
3. Al +1R: opción A mover a BE; opción B scale-out 50% y runner a 2R (elegir **una** regla y no mezclar).
4. Máximo 1 add-on solo si es pullback a favor y el riesgo total de la idea sigue ≤ 1R inicial.
5. Flat obligatorio antes del reset del DD / cierre de sesión definido.

### 4.5 Ritmo hacia el pase

- Días 1–2: tamaño **más pequeño** (mitad), validar ejecución y nervios.
- Días 3–8: tamaño estándar, meta +$400–$600.
- Si al día 10 el acumulado < 60% del target: **no aumentar riesgo**; filtrar más. El max 15 días no justifica forzar.

---

## 5. Escenarios numéricos simples

**Asunción:** profit target = **$3,000**. Capital $50k solo contextualiza el %; el DD diario ($1,000) es el ancla real.

### Escenario A — Conservador (recomendado)

- Meta/día: **$400**
- Días verdes netos necesarios: **≈ 8** (8 × 400 = 3,200)
- Riesgo/trade: **$150** · Trades/día: **2–3**
- Expectancy necesaria: con winrate 45% y R:R 1:2 → expectancy positiva suficiente.
- Consistencia: fácil de mantener si ningún día supera ~$700–$800.

### Escenario B — Moderado (6 días)

- Meta/día: **$500–$550**
- 6 días × $500 = $3,000
- Riesgo/trade: **$200** · Máx. 3 trades
- Soft cap diario: **$700** para no crear un “día estrella” problemático.

### Escenario C — Mínimo 3 días (no recomendado, solo teórico)

- Necesitarías ~$1,000/día y el mejor día ≤ 50% del total ⇒ en exactamente 3 días el máximo por día es **$1,000** si el total es $3,000 y los tres son iguales.
- Cualquier desviación (un día a $1,400) obliga a subir el total o añadir días.
- Alto estrés + cerca del DD diario → **evitar** como plan base.

### Escenario D — Sin target fijo conocido

Si la firma no publica target o difiere:

1. Operar con **perfil de riesgo de §3** intacto.
2. Acumular en tramos de **$500** de equity de evaluación.
3. Cada tramo: revisar ratio consistencia y distancia al DD.
4. Dejar de escalar tamaño aunque el profit crezca (anti-overconfidence).

### Ejemplo de sizing MNQ

- Stop = 20 puntos MNQ = 80 ticks × $0.50 = **$40 por microcontrato**.
- Riesgo deseado $200 ⇒ **5 MNQ**.
- Target 1:2 = 40 puntos ⇒ **+$400** si se llena el target completo en esa idea (antes de comisiones).

Mismo stop en **1 NQ** = $400 de riesgo (por encima del presupuesto diario cómodo) → ilustra por qué micros son prioritarios.

---

## 6. Riesgos operativos y checklists

### 6.1 Riesgos

| Riesgo | Impacto | Mitigación |
|--------|---------|------------|
| Noticias (CPI, FOMC, NFP, EIA) | Slippage, gaps, DD instantáneo | Calendario económico; flat 3–5 min antes/después o no operar el día |
| Slippage / cola de órdenes | Riesgo real > 1R | Stops un tick más allá de estructura; evitar thin books en MCL/MGC off-hours |
| Overtrading | Muerte por mil cortes | Cap 3–5 trades; journal obligatorio |
| Revenge trading post-loss | Viola stop diario | Hard rule: 2 losses seguidas → pausa 30 min; 3 → día cerrado |
| Un solo día oversized | Falla consistencia 50% | Soft cap de profit diario |
| Plataforma / desconexión | Posición huérfana | OCO brackets; alerta móvil; flat si hay duda |
| Cambiar de activo mid-sesión | Pérdida de foco | Un primario (MNQ) por sesión |

### 6.2 Checklist pre-sesión

- [ ] Días usados / días restantes (máx. 15)
- [ ] Profit acumulado y **mejor día** (proyección consistencia)
- [ ] Calendario de noticias del día (marcar rojo/amarillo)
- [ ] Sesgo: niveles PDH/PDL, VWAP previo, zonas A+
- [ ] Riesgo/trade en $ y tamaño máx. en micros para el ATR de hoy
- [ ] Stop diario y meta diaria escritos
- [ ] Plataforma: conexión, brackets, hotkeys, cuenta correcta (sim/eval)

### 6.3 Checklist post-sesión

- [ ] PnL neto del día anotado
- [ ] Recalcular mejor_día / total
- [ ] ¿Se respetó stop/meta/nº trades? (sí/no + causa)
- [ ] Screenshot de trades A+ y errores
- [ ] Decisión mañana: tamaño igual / −25% / día off
- [ ] Flat confirmado; sin órdenes resting no deseadas

---

## 7. Conclusión — Plan operativo de 1 página

**Objetivo:** aprobar evaluación $50K en **6–10 días** de trading (ventana 3–15), target asumido **$3,000**, sin violar DD diario $1,000 ni consistencia 50%.

**Instrumento:** **MNQ** (backup MES). No usar 4 minis. Tamaño típico **4–10 MNQ** según stop en puntos.

**Riesgo:** $150–$200 por idea · stop diario **−$650** · meta **+$450** · soft cap **+$750** · máx. **4 trades**/día · flat al cierre.

**Método:** solo setups A+ en **9:45–11:15 ET** (y opcional 14:00–15:30). Entrada con confluencia nivel + estructura + disparador; R:R ≥ 1:1.5; sin noticias rojas.

**Consistencia:** journal diario; si un día ≥ $900, cerrar y diluir con días verdes siguientes hasta mejor_día ≤ 45% del total.

**Pacing:** días 1–2 half-size; luego tamaño estándar; nunca aumentar riesgo por “voy tarde” en la ventana de 15 días.

**Hard stops de conducta:** 3 losses → fin del día; meta alcanzada → fin del día; duda → no trade.

**Herramienta de apoyo:** ver informe de viabilidad NinjaTrader (`indicador-ninjatrader-viabilidad.md`) para panel de DD restante, PnL día y regla 50%.

---

*Documento de investigación estratégica. No constituye consejo financiero. Ajustar números si el profit target real o la definición de DD de la firma difieren de los supuestos marcados.*
