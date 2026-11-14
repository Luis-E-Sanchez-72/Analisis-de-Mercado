//
// FundingRulesCalculator.cs
// Lógica pura de reglas de fondeo (DD, sizing, consistencia, días).
// Sin dependencias de UI — fácil de validar y ajustar.
//
#region Using declarations
using System;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.FundingRules
{
	/// <summary>
	/// Resultado del cálculo de tamaño sugerido.
	/// </summary>
	public sealed class SizeSuggestion
	{
		public int SuggestedContracts { get; set; }
		public double RiskPerContract { get; set; }
		public double TickValue { get; set; }
		public int CapApplied { get; set; }
		public string CapLabel { get; set; }
	}

	/// <summary>
	/// Snapshot de consistencia 50% y calendario del challenge.
	/// </summary>
	public sealed class ConsistencySnapshot
	{
		public double TotalProfit { get; set; }
		public double BestDayProfit { get; set; }
		public DateTime? BestDayDate { get; set; }
		public double BestDayPercent { get; set; }
		public double MaxAllowedToday { get; set; }
		public double RoomLeftToday { get; set; }
		public int TradingDaysUsed { get; set; }
		public int TradingDaysRemaining { get; set; }
		public bool NearConsistencyLimit { get; set; }
		public bool OverConsistencyLimit { get; set; }
	}

	/// <summary>
	/// Estados operativos del panel (semáforo).
	/// </summary>
	public enum FundingPanelStatus
	{
		Ok,
		NearSoftStop,
		SoftStopHit,
		DailyGoalHit,
		SoftCapHit,
		ConsistencyWarning,
		ConsistencyBreach,
		DaysExhausted
	}

	/// <summary>
	/// Cálculos de reglas de cuenta de fondeo $50K (MVP).
	/// Supuesto de consistencia: mejor_día / profit_total ≤ 50%.
	/// Día de sesión: fecha de calendario en la zona configurada tras el reset hour.
	/// </summary>
	public static class FundingRulesCalculator
	{
		/// <summary>
		/// Valor de un tick en moneda de la cuenta ≈ PointValue × TickSize.
		/// Ej. MNQ: 2 × 0.25 = $0.50.
		/// </summary>
		public static double GetTickValue(double pointValue, double tickSize)
		{
			if (tickSize <= 0 || pointValue <= 0)
				return 0;
			return pointValue * tickSize;
		}

		/// <summary>
		/// Contratos sugeridos: floor(riesgo$ / (stopTicks × tickValue)),
		/// con techo de micros o minis según el instrumento.
		/// </summary>
		public static SizeSuggestion SuggestSize(
			double riskDollars,
			int stopTicks,
			double tickValue,
			bool isMicro,
			int maxMicros,
			int maxMinis)
		{
			var result = new SizeSuggestion
			{
				TickValue = tickValue,
				CapApplied = isMicro ? maxMicros : maxMinis,
				CapLabel = isMicro ? "micros" : "minis"
			};

			if (stopTicks <= 0 || tickValue <= 0 || riskDollars <= 0)
			{
				result.SuggestedContracts = 0;
				result.RiskPerContract = 0;
				return result;
			}

			result.RiskPerContract = stopTicks * tickValue;
			int raw = (int)Math.Floor(riskDollars / result.RiskPerContract);
			int cap = isMicro ? maxMicros : maxMinis;
			result.SuggestedContracts = Math.Max(0, Math.Min(raw, cap));
			result.CapApplied = cap;
			return result;
		}

		/// <summary>
		/// Heurística micro vs mini por nombre de instrumento (MasterInstrument.Name).
		/// </summary>
		public static bool IsMicroInstrument(string instrumentName)
		{
			if (string.IsNullOrEmpty(instrumentName))
				return true; // por defecto micros (recomendación del plan)

			string n = instrumentName.ToUpperInvariant();
			// Micros CME comunes del universo del challenge
			if (n == "MNQ" || n == "MES" || n == "MGC" || n == "MCL"
				|| n == "MYM" || n == "M2K" || n.StartsWith("M") && n.Length <= 3)
				return true;

			return false;
		}

		/// <summary>
		/// DD diario restante: DailyDdMax − pérdida neta del día (solo si PnL &lt; 0).
		/// Si PnL ≥ 0, el DD restante es el máximo completo.
		/// </summary>
		public static double DailyDdRemaining(double dailyDdMax, double dayPnl)
		{
			double lossUsed = Math.Max(0, -dayPnl);
			return dailyDdMax - lossUsed;
		}

		/// <summary>
		/// Fecha de "día de trading" según hora de reset en la zona horaria dada.
		/// Ej.: reset 18:00 ET → a las 17:30 ET sigue siendo el día de sesión anterior;
		/// a las 18:05 ET empieza el nuevo día de sesión.
		/// </summary>
		public static DateTime GetSessionDate(DateTime utcNow, TimeZoneInfo tz, int resetHourLocal, int resetMinuteLocal)
		{
			DateTime local = TimeZoneInfo.ConvertTimeFromUtc(
				DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);

			DateTime sessionDate = local.Date;
			var resetToday = sessionDate.AddHours(resetHourLocal).AddMinutes(resetMinuteLocal);
			if (local < resetToday)
				sessionDate = sessionDate.AddDays(-1);

			return sessionDate.Date;
		}

		/// <summary>
		/// Agrupa PnL realizado por día de sesión a partir de pares de timestamps + PnL por ejecución/trade.
		/// </summary>
		public static Dictionary<DateTime, double> BuildDailyRealizedMap(
			IEnumerable<Tuple<DateTime, double>> executionPnlsUtc,
			TimeZoneInfo tz,
			int resetHour,
			int resetMinute,
			DateTime startDate)
		{
			var map = new Dictionary<DateTime, double>();
			if (executionPnlsUtc == null)
				return map;

			DateTime start = startDate.Date;
			foreach (var item in executionPnlsUtc)
			{
				DateTime sessionDay = GetSessionDate(item.Item1.ToUniversalTime(), tz, resetHour, resetMinute);
				if (sessionDay < start)
					continue;

				if (!map.ContainsKey(sessionDay))
					map[sessionDay] = 0;
				map[sessionDay] += item.Item2;
			}
			return map;
		}

		/// <summary>
		/// Consistencia: mejor día ≤ 50% del profit total.
		/// Máx. permitido hoy (si hoy sería el mejor): = suma de los demás días (T_other).
		/// Room left = max(0, T_other − pnl_hoy).
		/// </summary>
		public static ConsistencySnapshot ComputeConsistency(
			Dictionary<DateTime, double> dailyRealized,
			DateTime todaySession,
			double todayUnrealized,
			double consistencyLimitPercent,
			double warningPercent,
			int minTradingDays,
			int maxTradingDays)
		{
			var snap = new ConsistencySnapshot();
			if (dailyRealized == null)
				dailyRealized = new Dictionary<DateTime, double>();

			// PnL de hoy = realizado del mapa + unrealized abierto
			double todayRealized = 0;
			if (dailyRealized.ContainsKey(todaySession))
				todayRealized = dailyRealized[todaySession];

			double todayTotal = todayRealized + todayUnrealized;

			// Totales excluyendo el "hoy" live (usamos todayTotal como día actual)
			double otherSum = 0;
			double bestOther = double.NegativeInfinity;
			DateTime? bestOtherDate = null;
			int daysWithTrades = 0;

			foreach (var kv in dailyRealized)
			{
				if (kv.Key == todaySession)
					continue;
				otherSum += kv.Value;
				daysWithTrades++;
				if (kv.Value > bestOther)
				{
					bestOther = kv.Value;
					bestOtherDate = kv.Key;
				}
			}

			// Contar hoy como día operado si hay actividad (realized o posición abierta con PnL)
			bool todayCounts = dailyRealized.ContainsKey(todaySession) || Math.Abs(todayUnrealized) > 0.0001;
			if (todayCounts)
				daysWithTrades++;

			double totalProfit = otherSum + todayTotal;

			// Mejor día entre histórico y hoy
			double bestDay = todayTotal;
			DateTime? bestDate = todaySession;
			if (!double.IsNegativeInfinity(bestOther) && bestOther > bestDay)
			{
				bestDay = bestOther;
				bestDate = bestOtherDate;
			}
			if (double.IsNegativeInfinity(bestOther) && !todayCounts)
			{
				bestDay = 0;
				bestDate = null;
			}

			snap.TotalProfit = totalProfit;
			snap.BestDayProfit = bestDay;
			snap.BestDayDate = bestDate;
			snap.BestDayPercent = totalProfit > 0 ? (bestDay / totalProfit) * 100.0 : 0;
			snap.TradingDaysUsed = daysWithTrades;
			snap.TradingDaysRemaining = Math.Max(0, maxTradingDays - daysWithTrades);

			// Máximo PnL final de hoy sin violar 50% si hoy es/será el mejor día: D ≤ T_other
			// (ver estrategia-fondeo-50k.md y viabilidad)
			double tOther = otherSum;
			snap.MaxAllowedToday = Math.Max(0, tOther);
			// Si aún no hay otros días, el % será 100% inevitablemente — no bloquear; room = soft cap lo maneja la UI
			if (daysWithTrades <= 1 && otherSum == 0)
				snap.MaxAllowedToday = double.PositiveInfinity;

			snap.RoomLeftToday = double.IsPositiveInfinity(snap.MaxAllowedToday)
				? double.PositiveInfinity
				: snap.MaxAllowedToday - todayTotal;

			snap.OverConsistencyLimit = totalProfit > 0
				&& snap.BestDayPercent > consistencyLimitPercent + 0.0001;
			snap.NearConsistencyLimit = totalProfit > 0
				&& snap.BestDayPercent >= warningPercent
				&& !snap.OverConsistencyLimit;

			return snap;
		}

		/// <summary>
		/// Determina el estado semáforo prioritario del panel.
		/// </summary>
		public static FundingPanelStatus ResolveStatus(
			double dayPnl,
			double softStop,
			double nearSoftStopThreshold,
			double dailyGoal,
			double softCap,
			ConsistencySnapshot consistency,
			int maxTradingDays)
		{
			if (consistency != null && consistency.TradingDaysUsed >= maxTradingDays)
				return FundingPanelStatus.DaysExhausted;

			if (dayPnl <= softStop)
				return FundingPanelStatus.SoftStopHit;

			if (consistency != null && consistency.OverConsistencyLimit)
				return FundingPanelStatus.ConsistencyBreach;

			if (dayPnl >= softCap)
				return FundingPanelStatus.SoftCapHit;

			if (dayPnl >= dailyGoal)
				return FundingPanelStatus.DailyGoalHit;

			if (consistency != null && consistency.NearConsistencyLimit)
				return FundingPanelStatus.ConsistencyWarning;

			// softStop es negativo (ej. -650); "cerca" cuando PnL ≤ softStop + buffer (ej. -650+350=-300)
			if (dayPnl <= nearSoftStopThreshold)
				return FundingPanelStatus.NearSoftStop;

			return FundingPanelStatus.Ok;
		}

		public static string StatusLabelEs(FundingPanelStatus status)
		{
			switch (status)
			{
				case FundingPanelStatus.SoftStopHit: return "STOP DÍA — deja de operar";
				case FundingPanelStatus.NearSoftStop: return "Cerca del stop diario";
				case FundingPanelStatus.DailyGoalHit: return "META OK — no más riesgo";
				case FundingPanelStatus.SoftCapHit: return "SOFT CAP — protege consistencia";
				case FundingPanelStatus.ConsistencyWarning: return "Consistencia cerca del 50%";
				case FundingPanelStatus.ConsistencyBreach: return "Consistencia > 50% — diluir";
				case FundingPanelStatus.DaysExhausted: return "Máx. días de trading alcanzado";
				default: return "OK";
			}
		}
	}
}
