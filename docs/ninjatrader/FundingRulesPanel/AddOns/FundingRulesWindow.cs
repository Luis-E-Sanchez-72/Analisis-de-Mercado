//
// FundingRulesWindow.cs
// Ventana WPF del panel de reglas de fondeo (UI en español, código en inglés).
//
#region Using declarations
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.AddOns.FundingRules;
#endregion

namespace NinjaTrader.NinjaScript.AddOns
{
	/// <summary>
	/// Dashboard MVP: PnL día, DD restante, stop/meta/cap, sizing, consistencia 50%, días.
	/// No bloquea órdenes — usar Risk Management nativo NT8 para hard limits.
	///
	/// PnL del día = (Realized actual − Realized al inicio de sesión) + Unrealized.
	/// Historial por día: CSV en UserDataDirectory (se actualiza al refrescar / al cruzar sesión).
	/// </summary>
	public class FundingRulesWindow : NTWindow, IDisposable
	{
		// ——— Parámetros configurables (defaults del plan operativo) ———
		public double DailyDdMax { get; set; } = 1000;
		public double SoftStop { get; set; } = -650;
		public double DailyGoal { get; set; } = 450;
		public double SoftCap { get; set; } = 750;
		public DateTime ChallengeStartDate { get; set; } = DateTime.Today;
		public double RiskPerTrade { get; set; } = 200;
		public int StopTicks { get; set; } = 80;
		public int MaxMicros { get; set; } = 40;
		public int MaxMinis { get; set; } = 4;
		public int MinTradingDays { get; set; } = 3;
		public int MaxTradingDays { get; set; } = 15;
		public double ConsistencyLimitPercent { get; set; } = 50;
		public double ConsistencyWarningPercent { get; set; } = 45;
		public double NearDdAlert { get; set; } = 400;
		public double CriticalDdAlert { get; set; } = 300;
		public int SessionResetHour { get; set; } = 18;
		public int SessionResetMinute { get; set; } = 0;
		public string TimeZoneId { get; set; } = "Eastern Standard Time";
		public bool SoundAlertsEnabled { get; set; } = false;

		// ——— UI ———
		private ComboBox _accountCombo;
		private ComboBox _instrumentCombo;
		private TextBox _txtDdMax, _txtSoftStop, _txtGoal, _txtSoftCap;
		private TextBox _txtRisk, _txtStopTicks, _txtStartDate;
		private TextBox _txtResetHour, _txtNearDd;
		private CheckBox _chkSound;
		private TextBlock _lblBanner;
		private TextBlock _lblDayPnl, _lblDdLeft, _lblSize, _lblConsistency;
		private TextBlock _lblDays, _lblRoom, _lblPosition, _lblDetail;
		private Border _bannerBorder;
		private Button _btnRefresh;
		private Button _btnCommitDay;

		private Account _account;
		private DispatcherTimer _timer;
		private FundingPanelStatus _lastStatus = FundingPanelStatus.Ok;
		private bool _disposed;
		private Dictionary<DateTime, double> _dailyMap = new Dictionary<DateTime, double>();
		private string _persistPath;
		private DateTime _sessionAnchorDate = DateTime.MinValue;
		private double _realizedAtSessionStart;
		private bool _sessionAnchored;

		private static readonly SolidColorBrush BrushOk = CreateBrush(0x1B, 0x5E, 0x20);
		private static readonly SolidColorBrush BrushWarn = CreateBrush(0xE6, 0x51, 0x00);
		private static readonly SolidColorBrush BrushDanger = CreateBrush(0xB7, 0x1C, 0x1C);
		private static readonly SolidColorBrush BrushGoal = CreateBrush(0x0D, 0x47, 0xA1);
		private static readonly SolidColorBrush BrushPanel = CreateBrush(0xFA, 0xFA, 0xF8);
		private static readonly SolidColorBrush BrushText = CreateBrush(0x1A, 0x1A, 0x1A);
		private static readonly SolidColorBrush BrushMuted = CreateBrush(0x5C, 0x5C, 0x5C);

		public FundingRulesWindow()
		{
			Caption = "Reglas de fondeo — Panel MVP";
			Width = 420;
			Height = 740;
			MinWidth = 360;
			MinHeight = 560;
			Content = BuildUi();
			Loaded += OnLoaded;
			Closing += OnClosing;
		}

		private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
		{
			var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
			brush.Freeze();
			return brush;
		}

		private UIElement BuildUi()
		{
			var root = new ScrollViewer
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Background = BrushPanel
			};

			var stack = new StackPanel { Margin = new Thickness(14) };

			_bannerBorder = new Border
			{
				Background = BrushOk,
				CornerRadius = new CornerRadius(4),
				Padding = new Thickness(12, 10, 12, 10),
				Margin = new Thickness(0, 0, 0, 12)
			};
			_lblBanner = new TextBlock
			{
				Text = "OK",
				FontSize = 16,
				FontWeight = FontWeights.SemiBold,
				Foreground = Brushes.White,
				TextWrapping = TextWrapping.Wrap
			};
			_bannerBorder.Child = _lblBanner;
			stack.Children.Add(_bannerBorder);

			stack.Children.Add(SectionTitle("Cuenta e instrumento"));
			_accountCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 6) };
			_accountCombo.SelectionChanged += (s, e) => AttachAccount();
			stack.Children.Add(Labeled("Cuenta", _accountCombo));

			_instrumentCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 10), IsEditable = true };
			foreach (var name in new[] { "MNQ", "MES", "MGC", "MCL", "NQ", "ES", "GC", "CL" })
				_instrumentCombo.Items.Add(name);
			_instrumentCombo.SelectedIndex = 0;
			_instrumentCombo.SelectionChanged += (s, e) => RefreshAll();
			_instrumentCombo.LostFocus += (s, e) => RefreshAll();
			stack.Children.Add(Labeled("Instrumento (sizing)", _instrumentCombo));

			stack.Children.Add(SectionTitle("Estado del día"));
			_lblDayPnl = MetricBlock("PnL del día");
			_lblDdLeft = MetricBlock("DD diario restante");
			_lblPosition = MetricBlock("Posición actual");
			stack.Children.Add(_lblDayPnl);
			stack.Children.Add(_lblDdLeft);
			stack.Children.Add(_lblPosition);

			stack.Children.Add(SectionTitle("Tamaño sugerido"));
			_lblSize = MetricBlock("Contratos");
			stack.Children.Add(_lblSize);

			stack.Children.Add(SectionTitle("Consistencia 50%"));
			_lblConsistency = MetricBlock("Mejor día / total");
			_lblRoom = MetricBlock("Máx. restante hoy (sin romper 50%)");
			stack.Children.Add(_lblConsistency);
			stack.Children.Add(_lblRoom);

			stack.Children.Add(SectionTitle("Calendario challenge"));
			_lblDays = MetricBlock("Días operados");
			stack.Children.Add(_lblDays);

			_lblDetail = new TextBlock
			{
				TextWrapping = TextWrapping.Wrap,
				Foreground = BrushMuted,
				FontSize = 11,
				Margin = new Thickness(0, 4, 0, 12)
			};
			stack.Children.Add(_lblDetail);

			stack.Children.Add(SectionTitle("Parámetros"));
			_txtDdMax = ParamBox(DailyDdMax.ToString("0"));
			_txtSoftStop = ParamBox(SoftStop.ToString("0"));
			_txtGoal = ParamBox(DailyGoal.ToString("0"));
			_txtSoftCap = ParamBox(SoftCap.ToString("0"));
			_txtRisk = ParamBox(RiskPerTrade.ToString("0"));
			_txtStopTicks = ParamBox(StopTicks.ToString());
			_txtStartDate = ParamBox(ChallengeStartDate.ToString("yyyy-MM-dd"));
			_txtResetHour = ParamBox(SessionResetHour.ToString());
			_txtNearDd = ParamBox(NearDdAlert.ToString("0"));

			stack.Children.Add(ParamRow("DD máx. diario ($)", _txtDdMax));
			stack.Children.Add(ParamRow("Stop diario blando ($)", _txtSoftStop));
			stack.Children.Add(ParamRow("Meta diaria ($)", _txtGoal));
			stack.Children.Add(ParamRow("Soft cap profit ($)", _txtSoftCap));
			stack.Children.Add(ParamRow("Riesgo $ / trade", _txtRisk));
			stack.Children.Add(ParamRow("Stop (ticks)", _txtStopTicks));
			stack.Children.Add(ParamRow("Inicio challenge (yyyy-MM-dd)", _txtStartDate));
			stack.Children.Add(ParamRow("Hora reset sesión (0-23, zona)", _txtResetHour));
			stack.Children.Add(ParamRow("Alerta DD restante < ($)", _txtNearDd));

			_chkSound = new CheckBox
			{
				Content = "Alertas de sonido (opcional)",
				IsChecked = SoundAlertsEnabled,
				Margin = new Thickness(0, 4, 0, 8),
				Foreground = BrushText
			};
			stack.Children.Add(_chkSound);

			_btnRefresh = new Button
			{
				Content = "Aplicar parámetros y refrescar",
				Margin = new Thickness(0, 4, 0, 6),
				Padding = new Thickness(10, 6, 10, 6)
			};
			_btnRefresh.Click += (s, e) =>
			{
				ApplyParametersFromUi();
				LoadPersistence();
				EnsureSessionAnchor(force: true);
				RefreshAll();
			};
			stack.Children.Add(_btnRefresh);

			_btnCommitDay = new Button
			{
				Content = "Fijar PnL realizado de hoy en historial",
				Margin = new Thickness(0, 0, 0, 8),
				Padding = new Thickness(10, 6, 10, 6),
				ToolTip = "Guarda el realizado de la sesión actual en el CSV de consistencia (útil al cerrar el día)."
			};
			_btnCommitDay.Click += (s, e) => CommitTodayRealized();
			stack.Children.Add(_btnCommitDay);

			var note = new TextBlock
			{
				Text = "MVP informativo: no bloquea órdenes. Configura Risk nativo NT8 (max loss / max qty) como red dura. "
					+ "Historial de consistencia en CSV bajo UserDataDirectory. Valida hora de reset con tu firma.",
				TextWrapping = TextWrapping.Wrap,
				FontSize = 11,
				Foreground = BrushMuted,
				Margin = new Thickness(0, 8, 0, 0)
			};
			stack.Children.Add(note);

			root.Content = stack;
			return root;
		}

		private static TextBlock SectionTitle(string text)
		{
			return new TextBlock
			{
				Text = text,
				FontSize = 13,
				FontWeight = FontWeights.SemiBold,
				Foreground = BrushText,
				Margin = new Thickness(0, 6, 0, 6)
			};
		}

		private static TextBlock MetricBlock(string placeholder)
		{
			return new TextBlock
			{
				Text = placeholder + ": —",
				FontSize = 14,
				Foreground = BrushText,
				Margin = new Thickness(0, 0, 0, 4),
				TextWrapping = TextWrapping.Wrap
			};
		}

		private static TextBox ParamBox(string value)
		{
			return new TextBox { Text = value, MinWidth = 100 };
		}

		private static UIElement Labeled(string label, UIElement child)
		{
			var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
			sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = BrushMuted });
			sp.Children.Add(child);
			return sp;
		}

		private static UIElement ParamRow(string label, TextBox box)
		{
			var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			var lbl = new TextBlock
			{
				Text = label,
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 12,
				Foreground = BrushText,
				TextWrapping = TextWrapping.Wrap
			};
			Grid.SetColumn(lbl, 0);
			Grid.SetColumn(box, 1);
			grid.Children.Add(lbl);
			grid.Children.Add(box);
			return grid;
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			PopulateAccounts();
			ApplyParametersFromUi();
			_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_timer.Tick += (s, args) => RefreshAll();
			_timer.Start();
			RefreshAll();
		}

		private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			try { CommitTodayRealized(); }
			catch { }
			Dispose();
		}

		private void PopulateAccounts()
		{
			_accountCombo.Items.Clear();
			foreach (Account acct in Account.AccountCollection)
			{
				if (acct == null) continue;
				_accountCombo.Items.Add(acct);
			}
			_accountCombo.DisplayMemberPath = "Name";
			if (_accountCombo.Items.Count > 0)
				_accountCombo.SelectedIndex = 0;
		}

		private void AttachAccount()
		{
			DetachAccount();
			_account = _accountCombo.SelectedItem as Account;
			if (_account == null)
				return;

			_account.AccountItemUpdate += OnAccountItemUpdate;
			_account.PositionUpdate += OnPositionUpdate;
			_account.ExecutionUpdate += OnExecutionUpdate;

			LoadPersistence();
			_sessionAnchored = false;
			EnsureSessionAnchor(force: true);
			RefreshAll();
		}

		private void DetachAccount()
		{
			if (_account == null)
				return;
			_account.AccountItemUpdate -= OnAccountItemUpdate;
			_account.PositionUpdate -= OnPositionUpdate;
			_account.ExecutionUpdate -= OnExecutionUpdate;
			_account = null;
		}

		private void OnAccountItemUpdate(object sender, AccountItemEventArgs e)
		{
			Dispatcher.InvokeAsync(RefreshAll);
		}

		private void OnPositionUpdate(object sender, PositionEventArgs e)
		{
			Dispatcher.InvokeAsync(RefreshAll);
		}

		private void OnExecutionUpdate(object sender, ExecutionEventArgs e)
		{
			Dispatcher.InvokeAsync(RefreshAll);
		}

		private string UserDataDirectory
		{
			get
			{
				try
				{
					if (!string.IsNullOrEmpty(Globals.UserDataDirectory))
						return Globals.UserDataDirectory;
				}
				catch { }
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					"NinjaTrader 8");
			}
		}

		private void LoadPersistence()
		{
			string acctName = _account != null ? _account.Name : "default";
			_persistPath = FundingRulesPersistence.GetFilePath(UserDataDirectory, acctName);
			_dailyMap = FundingRulesPersistence.Load(_persistPath);

			// Filtrar días anteriores al inicio del challenge
			DateTime start = ChallengeStartDate.Date;
			var filtered = new Dictionary<DateTime, double>();
			foreach (var kv in _dailyMap)
			{
				if (kv.Key.Date >= start)
					filtered[kv.Key.Date] = kv.Value;
			}
			_dailyMap = filtered;
		}

		private void ApplyParametersFromUi()
		{
			DailyDdMax = ParseDouble(_txtDdMax.Text, DailyDdMax);
			SoftStop = ParseDouble(_txtSoftStop.Text, SoftStop);
			DailyGoal = ParseDouble(_txtGoal.Text, DailyGoal);
			SoftCap = ParseDouble(_txtSoftCap.Text, SoftCap);
			RiskPerTrade = ParseDouble(_txtRisk.Text, RiskPerTrade);
			StopTicks = (int)ParseDouble(_txtStopTicks.Text, StopTicks);
			NearDdAlert = ParseDouble(_txtNearDd.Text, NearDdAlert);
			SessionResetHour = (int)Math.Max(0, Math.Min(23, ParseDouble(_txtResetHour.Text, SessionResetHour)));
			SoundAlertsEnabled = _chkSound.IsChecked == true;

			DateTime parsed;
			if (DateTime.TryParse(_txtStartDate.Text, out parsed))
				ChallengeStartDate = parsed.Date;
		}

		private static double ParseDouble(string text, double fallback)
		{
			double v;
			if (double.TryParse(text, System.Globalization.NumberStyles.Any,
				System.Globalization.CultureInfo.InvariantCulture, out v))
				return v;
			if (double.TryParse(text, out v))
				return v;
			return fallback;
		}

		private TimeZoneInfo GetTimeZone()
		{
			try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
			catch
			{
				try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
				catch { return TimeZoneInfo.Local; }
			}
		}

		private double GetAccountItem(AccountItem item)
		{
			if (_account == null)
				return 0;
			try { return _account.Get(item, Currency.UsDollar); }
			catch { return 0; }
		}

		/// <summary>
		/// Ancla el realized al cambio de día de sesión para calcular el delta del día.
		/// </summary>
		private void EnsureSessionAnchor(bool force)
		{
			TimeZoneInfo tz = GetTimeZone();
			DateTime todaySession = FundingRulesCalculator.GetSessionDate(
				DateTime.UtcNow, tz, SessionResetHour, SessionResetMinute);

			if (force || !_sessionAnchored || todaySession != _sessionAnchorDate)
			{
				// Si cambiamos de sesión y había un día anterior anclado, persistir su realizado
				if (_sessionAnchored && todaySession != _sessionAnchorDate && _account != null)
				{
					double prevRealizedDay = GetAccountItem(AccountItem.RealizedProfitLoss) - _realizedAtSessionStart;
					// Al cruzar, el realized ya puede incluir el nuevo día; mejor usar lo último guardado en mapa
					if (_dailyMap.ContainsKey(_sessionAnchorDate))
						FundingRulesPersistence.UpsertDay(_persistPath, _dailyMap, _sessionAnchorDate, _dailyMap[_sessionAnchorDate]);
				}

				_sessionAnchorDate = todaySession;
				_realizedAtSessionStart = GetAccountItem(AccountItem.RealizedProfitLoss);

				// Si ya había un valor guardado hoy (reapertura del panel), ajustar ancla
				// para que el delta coincida: realizedNow - anchor = savedToday
				if (_dailyMap.ContainsKey(todaySession))
				{
					double saved = _dailyMap[todaySession];
					double realizedNow = GetAccountItem(AccountItem.RealizedProfitLoss);
					_realizedAtSessionStart = realizedNow - saved;
				}

				_sessionAnchored = true;
			}
		}

		private void CommitTodayRealized()
		{
			if (_account == null)
				return;
			EnsureSessionAnchor(force: false);
			double realizedNow = GetAccountItem(AccountItem.RealizedProfitLoss);
			double realizedToday = realizedNow - _realizedAtSessionStart;
			FundingRulesPersistence.UpsertDay(_persistPath, _dailyMap, _sessionAnchorDate, realizedToday);
			RefreshAll();
		}

		private double GetDayPnl(out double realizedToday, out double unrealized)
		{
			EnsureSessionAnchor(force: false);
			unrealized = GetAccountItem(AccountItem.UnrealizedProfitLoss);
			double realizedNow = GetAccountItem(AccountItem.RealizedProfitLoss);
			realizedToday = realizedNow - _realizedAtSessionStart;
			// Mantener mapa en vivo para consistencia
			_dailyMap[_sessionAnchorDate] = realizedToday;
			return realizedToday + unrealized;
		}

		private string GetSelectedInstrumentName()
		{
			if (_instrumentCombo.SelectedItem != null)
				return _instrumentCombo.SelectedItem.ToString();
			return string.IsNullOrWhiteSpace(_instrumentCombo.Text) ? "MNQ" : _instrumentCombo.Text.Trim();
		}

		private void TryGetInstrumentSpecs(string name, out double tickSize, out double pointValue)
		{
			tickSize = 0.25;
			pointValue = 2;

			try
			{
				Instrument instr = Instrument.GetInstrument(name);
				if (instr != null && instr.MasterInstrument != null)
				{
					tickSize = instr.MasterInstrument.TickSize;
					pointValue = instr.MasterInstrument.PointValue;
					return;
				}
			}
			catch { }

			switch ((name ?? string.Empty).ToUpperInvariant())
			{
				case "MNQ": tickSize = 0.25; pointValue = 2; break;
				case "NQ": tickSize = 0.25; pointValue = 20; break;
				case "MES": tickSize = 0.25; pointValue = 5; break;
				case "ES": tickSize = 0.25; pointValue = 50; break;
				case "MGC": tickSize = 0.1; pointValue = 10; break;
				case "GC": tickSize = 0.1; pointValue = 100; break;
				case "MCL": tickSize = 0.01; pointValue = 100; break;
				case "CL": tickSize = 0.01; pointValue = 1000; break;
			}
		}

		private void RefreshAll()
		{
			if (_disposed)
				return;

			TimeZoneInfo tz = GetTimeZone();
			DateTime todaySession = FundingRulesCalculator.GetSessionDate(
				DateTime.UtcNow, tz, SessionResetHour, SessionResetMinute);

			double realizedToday, unrealized;
			double dayPnl = 0;
			if (_account != null)
				dayPnl = GetDayPnl(out realizedToday, out unrealized);
			else
			{
				realizedToday = 0;
				unrealized = 0;
			}

			double ddLeft = FundingRulesCalculator.DailyDdRemaining(DailyDdMax, dayPnl);

			_lblDayPnl.Text = string.Format(
				"PnL del día: {0:C2}  (realizado {1:C2} · abierto {2:C2})",
				dayPnl, realizedToday, unrealized);
			_lblDayPnl.Foreground = dayPnl >= 0 ? BrushOk : BrushDanger;

			_lblDdLeft.Text = string.Format("DD diario restante: {0:C2}  /  máx {1:C0}", ddLeft, DailyDdMax);
			if (ddLeft <= CriticalDdAlert)
				_lblDdLeft.Foreground = BrushDanger;
			else if (ddLeft <= NearDdAlert)
				_lblDdLeft.Foreground = BrushWarn;
			else
				_lblDdLeft.Foreground = BrushText;

			int netQty = 0;
			string posInstr = "—";
			if (_account != null)
			{
				try
				{
					foreach (Position pos in _account.Positions)
					{
						if (pos == null || pos.MarketPosition == MarketPosition.Flat)
							continue;
						netQty += pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
						if (pos.Instrument != null)
							posInstr = pos.Instrument.FullName;
					}
				}
				catch { }
			}
			_lblPosition.Text = string.Format("Posición actual: {0}  ({1})", netQty, posInstr);

			string instrName = GetSelectedInstrumentName();
			double tickSize, pointValue;
			TryGetInstrumentSpecs(instrName, out tickSize, out pointValue);
			double tickValue = FundingRulesCalculator.GetTickValue(pointValue, tickSize);
			bool isMicro = FundingRulesCalculator.IsMicroInstrument(instrName);
			SizeSuggestion size = FundingRulesCalculator.SuggestSize(
				RiskPerTrade, StopTicks, tickValue, isMicro, MaxMicros, MaxMinis);

			_lblSize.Text = string.Format(
				"Máx. sugerido: {0} {1}  ·  riesgo/contrato {2:C2}  ·  tick {3:C2}  ·  techo {4} {5}",
				size.SuggestedContracts, instrName, size.RiskPerContract, size.TickValue,
				size.CapApplied, size.CapLabel);
			_lblSize.Foreground = (Math.Abs(netQty) > size.SuggestedContracts && size.SuggestedContracts > 0)
				? BrushWarn : BrushText;

			var consistency = FundingRulesCalculator.ComputeConsistency(
				_dailyMap, todaySession, unrealized,
				ConsistencyLimitPercent, ConsistencyWarningPercent,
				MinTradingDays, MaxTradingDays);

			string bestDateStr = consistency.BestDayDate.HasValue
				? consistency.BestDayDate.Value.ToString("yyyy-MM-dd") : "—";

			_lblConsistency.Text = string.Format(
				"Total: {0:C2}  ·  Mejor día: {1:C2} ({2})  ·  {3:0.0}%",
				consistency.TotalProfit, consistency.BestDayProfit, bestDateStr, consistency.BestDayPercent);

			if (consistency.OverConsistencyLimit)
				_lblConsistency.Foreground = BrushDanger;
			else if (consistency.NearConsistencyLimit)
				_lblConsistency.Foreground = BrushWarn;
			else
				_lblConsistency.Foreground = BrushText;

			if (double.IsPositiveInfinity(consistency.RoomLeftToday))
				_lblRoom.Text = "Máx. restante hoy (50%): N/A (primer día — el % será 100% hasta diluir)";
			else
				_lblRoom.Text = string.Format(
					"Puedo ganar hoy sin romper 50%: {0:C2}  (techo día ≈ {1:C2})",
					Math.Max(0, consistency.RoomLeftToday), consistency.MaxAllowedToday);

			_lblDays.Text = string.Format(
				"Días operados: {0}  ·  Restantes (máx {1}): {2}  ·  Mínimo requerido: {3}",
				consistency.TradingDaysUsed, MaxTradingDays,
				consistency.TradingDaysRemaining, MinTradingDays);

			if (consistency.TradingDaysUsed >= MaxTradingDays)
				_lblDays.Foreground = BrushDanger;
			else if (consistency.TradingDaysUsed >= MaxTradingDays - 2)
				_lblDays.Foreground = BrushWarn;
			else
				_lblDays.Foreground = BrushText;

			double nearSoft = SoftStop * 0.5;
			if (ddLeft <= NearDdAlert)
				nearSoft = Math.Max(nearSoft, -DailyDdMax + NearDdAlert);

			FundingPanelStatus status = FundingRulesCalculator.ResolveStatus(
				dayPnl, SoftStop, nearSoft, DailyGoal, SoftCap, consistency, MaxTradingDays);

			ApplyBanner(status);

			_lblDetail.Text = string.Format(
				"Sesión trading: {0}  ·  Reset {1:00}:{2:00} ({3})  ·  Cuenta: {4}\nArchivo historial: {5}",
				todaySession.ToString("yyyy-MM-dd"),
				SessionResetHour, SessionResetMinute, tz.Id,
				_account != null ? _account.Name : "(ninguna)",
				_persistPath ?? "—");

			MaybePlaySound(status);
			_lastStatus = status;
		}

		private void ApplyBanner(FundingPanelStatus status)
		{
			_lblBanner.Text = FundingRulesCalculator.StatusLabelEs(status);
			switch (status)
			{
				case FundingPanelStatus.SoftStopHit:
				case FundingPanelStatus.ConsistencyBreach:
				case FundingPanelStatus.DaysExhausted:
					_bannerBorder.Background = BrushDanger;
					break;
				case FundingPanelStatus.NearSoftStop:
				case FundingPanelStatus.ConsistencyWarning:
				case FundingPanelStatus.SoftCapHit:
					_bannerBorder.Background = BrushWarn;
					break;
				case FundingPanelStatus.DailyGoalHit:
					_bannerBorder.Background = BrushGoal;
					break;
				default:
					_bannerBorder.Background = BrushOk;
					break;
			}
		}

		private void MaybePlaySound(FundingPanelStatus status)
		{
			if (!SoundAlertsEnabled || status == _lastStatus)
				return;
			if (status == FundingPanelStatus.Ok || status == FundingPanelStatus.DailyGoalHit)
				return;
			try { System.Media.SystemSounds.Exclamation.Play(); }
			catch { }
		}

		public void Dispose()
		{
			if (_disposed)
				return;
			_disposed = true;
			if (_timer != null)
			{
				_timer.Stop();
				_timer = null;
			}
			DetachAccount();
		}
	}
}
