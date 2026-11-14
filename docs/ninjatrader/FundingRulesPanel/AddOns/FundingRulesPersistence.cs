//
// FundingRulesPersistence.cs
// Persistencia simple de PnL por día de sesión (CSV) para consistencia y conteo de días.
//
#region Using declarations
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.FundingRules
{
	/// <summary>
	/// Guarda/carga realized PnL por fecha de sesión.
	/// Archivo: Documents\NinjaTrader 8\user\FundingRules_{account}.csv
	/// </summary>
	public static class FundingRulesPersistence
	{
		public static string GetFilePath(string userDataDir, string accountName)
		{
			string safe = string.IsNullOrEmpty(accountName) ? "default" : accountName;
			foreach (char c in Path.GetInvalidFileNameChars())
				safe = safe.Replace(c, '_');
			return Path.Combine(userDataDir ?? Path.GetTempPath(), "FundingRules_" + safe + ".csv");
		}

		public static Dictionary<DateTime, double> Load(string path)
		{
			var map = new Dictionary<DateTime, double>();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return map;

			try
			{
				foreach (string line in File.ReadAllLines(path))
				{
					if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
						continue;
					string[] parts = line.Split(',');
					if (parts.Length < 2)
						continue;
					DateTime day;
					double pnl;
					if (!DateTime.TryParse(parts[0].Trim(), CultureInfo.InvariantCulture,
						DateTimeStyles.None, out day))
						continue;
					if (!double.TryParse(parts[1].Trim(), NumberStyles.Any,
						CultureInfo.InvariantCulture, out pnl))
						continue;
					map[day.Date] = pnl;
				}
			}
			catch
			{
				// archivo corrupto → mapa vacío; el usuario puede re-registrar
			}
			return map;
		}

		public static void Save(string path, Dictionary<DateTime, double> map)
		{
			if (string.IsNullOrEmpty(path) || map == null)
				return;

			try
			{
				string dir = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				var sb = new StringBuilder();
				sb.AppendLine("# date,realized_pnl  (FundingRulesPanel MVP)");
				foreach (var kv in map)
				{
					sb.Append(kv.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
					sb.Append(',');
					sb.AppendLine(kv.Value.ToString("G17", CultureInfo.InvariantCulture));
				}
				File.WriteAllText(path, sb.ToString());
			}
			catch
			{
				// sin permisos de escritura — el panel sigue en modo solo-hoy
			}
		}

		/// <summary>
		/// Actualiza o inserta el PnL realizado de un día y guarda.
		/// </summary>
		public static void UpsertDay(string path, Dictionary<DateTime, double> map, DateTime day, double realizedPnl)
		{
			if (map == null)
				return;
			map[day.Date] = realizedPnl;
			Save(path, map);
		}
	}
}
