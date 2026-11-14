# Instalación y uso — Panel reglas de fondeo (NinjaTrader 8 MVP)

**Usuario:** Luis Emiro Sánchez  
**Add-on:** `FundingRulesPanel`  
**Código:** [`docs/ninjatrader/FundingRulesPanel/`](./ninjatrader/FundingRulesPanel/)  
**Arquitectura:** Add-on (`AddOnBase`), no indicador — ver [`ARCHITECTURE.md`](./ninjatrader/FundingRulesPanel/ARCHITECTURE.md)

---

## 1. Requisitos

- NinjaTrader 8 (versión reciente con soporte de AddOns WPF).
- Cuenta conectada (Sim, Playback o la connection de la firma/eval).
- Compilar NinjaScript en la máquina de Luis (este entorno cloud **no** puede compilar contra el SDK de NT8).

---

## 2. Instalación

### Opción A — Copiar fuentes (recomendada para MVP)

1. Cierra NinjaTrader 8 si está abierto.
2. Copia **estos cuatro archivos** a:
   `Documents\NinjaTrader 8\bin\Custom\AddOns\`

   - `FundingRulesPanel.cs`
   - `FundingRulesWindow.cs`
   - `FundingRulesCalculator.cs`
   - `FundingRulesPersistence.cs`

   Origen en el Project store:
   `docs/ninjatrader/FundingRulesPanel/AddOns/`

3. Abre NinjaTrader 8.
4. Ve a **New → NinjaScript Editor**.
5. Pulsa **Compile** (F5). Debe compilar sin errores.
6. Si NT pide reiniciar tras add-ons nuevos, reinicia NinjaTrader.

### Opción B — Importar carpeta/zip

1. Empaqueta la carpeta `FundingRulesPanel` (o solo los `.cs` de `AddOns/`) en un `.zip`.
2. En NT8: **Tools → Import → NinjaScript Add-On…** y selecciona el zip.
3. Compila y reinicia si lo indica el asistente.

> Si al importar hay conflicto de namespaces, usa la Opción A (copia manual).

---

## 3. Abrir el panel

1. En **Control Center**, menú **New**.
2. Elige **Reglas de fondeo (MVP)**.
3. Selecciona la **cuenta** de evaluación/sim.
4. Elige el **instrumento** para el calculador de tamaño (default MNQ).
5. Ajusta parámetros si tu firma difiere del plan (−650 / +450 / +750, etc.).
6. Pulsa **Aplicar parámetros y refrescar**.
7. Define **Inicio challenge (yyyy-MM-dd)** el día real de arranque.

---

## 4. Qué muestra (features MVP)

| # | Feature | Dónde / cómo |
|---|---------|----------------|
| 1 | PnL del día (realized ± unrealized) | Bloque “Estado del día” |
| 2 | DD diario restante (base $1000) | Mismo bloque; param “DD máx. diario” |
| 3 | Stop diario blando (−650) + banner | Banner superior rojo “STOP DÍA” |
| 4 | Meta diaria / soft cap (+450 / +750) | Banner azul/naranja |
| 5 | Tamaño máx. sugerido | `floor(riesgo$ / (ticks × tick$))` + techo 40/4 |
| 6 | Consistencia 50% + “cuánto puedo ganar hoy” | Bloque consistencia |
| 7 | Días operados / restantes (3–15) | Bloque calendario |
| 8 | Alertas visuales (+ sonido opcional) | Banner + checkbox sonido |

Al cerrar el día (o la ventana), usa **Fijar PnL realizado de hoy en historial** para persistir el CSV de consistencia.

---

## 5. Parámetros configurables

| Parámetro | Default | Notas |
|-----------|---------|--------|
| DD máx. diario | 1000 | Soft display; hard limit → Risk nativo |
| Stop diario blando | −650 | Banner STOP DÍA |
| Meta diaria | 450 | Banner META OK |
| Soft cap profit | 750 | Evitar día estrella |
| Riesgo $ / trade | 200 | Input del sizing |
| Stop (ticks) | 80 | Ej. 20 pts MNQ = 80 ticks |
| Inicio challenge | hoy | Filtra historial CSV |
| Hora reset sesión | 18 | Zona default Eastern; confirmar con firma |
| Alerta DD restante &lt; | 400 | Color warning / critical 300 |
| Sonido | off | Opcional |

Zona horaria en código: `Eastern Standard Time` (Windows). Ajustar `TimeZoneId` en `FundingRulesWindow` si hace falta.

---

## 6. Risk nativo NT8 (obligatorio junto al panel)

El add-on **no bloquea** órdenes. Configura además:

1. Control Center → **Tools → Options → Trading** (o **Risk**) según tu build.
2. **Max daily loss** ≤ $700–$1000 (alineado al soft stop o al DD de la firma).
3. **Max order/account quantity** acorde (ej. 15–20 MNQ, no 40).

Así tienes red dura + panel educativo/disciplinario.

---

## 7. Checklist de aceptación rápida

- [ ] Compila en NinjaScript Editor sin errores.
- [ ] Aparece menú **New → Reglas de fondeo (MVP)**.
- [ ] PnL día cambia con posición abierta (unrealized).
- [ ] Con PnL ≤ −650 el banner muestra STOP DÍA.
- [ ] Con PnL ≥ +450 / +750 cambian meta / soft cap.
- [ ] Sizing MNQ stop 80 ticks, riesgo $200 → 5 contratos.
- [ ] Tras 2+ días en CSV, “puedo ganar hoy” refleja techo 50%.
- [ ] Contador de días sube al operar / fijar historial.

---

## 8. Limitaciones conocidas

- No hay compilación verificada en este cloud agent (sin NT8 instalado).
- Consistencia depende del CSV + ancla de `RealizedProfitLoss`; si abres el panel a mitad de sesión por primera vez, pulsa aplicar/refrescar y, al cerrar el día, **Fijar PnL…**.
- Definición exacta de “día” y del 50% puede diferir por firma — valida reset y fórmula.
- Comisiones: según lo que reporte la connection en `RealizedProfitLoss`.
- No hay señales, auto-flatten ni multi-cuenta avanzada.

---

## 9. Desinstalación

1. Cierra NT8.
2. Borra los cuatro `.cs` de `bin\Custom\AddOns\`.
3. (Opcional) Borra `Documents\NinjaTrader 8\FundingRules_*.csv`.
4. Abre NT8 y compila de nuevo.
