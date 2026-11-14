# Arquitectura — FundingRulesPanel (MVP)

**Elección:** Add-on (`AddOnBase` + ventana `NTWindow`), **no** indicador de chart.

## Por qué Add-on

| Criterio | Add-on | Indicador |
|----------|--------|-----------|
| Acceso a `Account` / posiciones / realized | Natural | Incómodo / frágil |
| UI tipo dashboard (fuera del precio) | Ventana propia | Overlay en un chart |
| Consistencia multi-día + CSV | Encaja | Posible pero peor UX |
| Entrega MVP con features 1–8 del brief | Sí | Solo un subconjunto estable |

El informe de viabilidad ya recomendaba add-on como mejor fit; el indicador solo tenía sentido como prototipo ultra-mínimo de PnL+DD. Este MVP incluye consistencia, días y alertas → **AddOnBase**.

## Archivos

| Archivo | Rol |
|---------|-----|
| `AddOns/FundingRulesPanel.cs` | `AddOnBase`, menú Control Center → New |
| `AddOns/FundingRulesWindow.cs` | UI WPF + suscripción a cuenta |
| `AddOns/FundingRulesCalculator.cs` | Lógica pura (DD, size, 50%, días, estados) |
| `AddOns/FundingRulesPersistence.cs` | CSV de PnL por día de sesión |

## Datos

- **PnL día** = Δ `RealizedProfitLoss` desde ancla de sesión + `UnrealizedProfitLoss`.
- **Día de sesión** = fecha según `TimeZoneId` + hora de reset (default 18:00 ET).
- **Historial** = `FundingRules_{cuenta}.csv` en `UserDataDirectory` (Documents\NinjaTrader 8\).
- **Sizing** = `floor(riesgo$ / (stopTicks × PointValue × TickSize))` con techo 40 micros / 4 minis.

## Fuera de alcance (intencional)

- Señales de entrada.
- Bloqueo duro de órdenes (usar **Risk Management nativo** NT8).
- Automatización de trading / auto-flatten.
