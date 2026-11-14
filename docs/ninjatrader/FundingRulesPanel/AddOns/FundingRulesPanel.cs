//
// FundingRulesPanel.cs
// Add-on NinjaTrader 8 — panel de reglas de fondeo (cuenta challenge $50K).
// Elección de arquitectura: AddOnBase (no Indicator) — ver ARCHITECTURE.md
//
#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
#endregion

namespace NinjaTrader.NinjaScript.AddOns
{
	/// <summary>
	/// Punto de entrada del add-on. Registra el ítem en Control Center → New
	/// y abre <see cref="FundingRulesWindow"/>.
	/// </summary>
	public class FundingRulesPanel : AddOnBase
	{
		private NTMenuItem _menuItem;
		private NTMenuItem _parentMenu;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "Panel MVP de reglas de fondeo: PnL día, DD, consistencia 50%, sizing, días.";
				Name = "FundingRulesPanel";
			}
			else if (State == State.Terminated)
			{
				if (_menuItem != null && _parentMenu != null)
				{
					try
					{
						RunOnUi(() =>
						{
							if (_parentMenu.Items.Contains(_menuItem))
								_parentMenu.Items.Remove(_menuItem);
						});
					}
					catch { }
					_menuItem = null;
					_parentMenu = null;
				}
			}
		}

		/// <summary>
		/// Se llama cuando NT crea una ventana (p. ej. Control Center).
		/// Añadimos el menú solo en el Control Center.
		/// </summary>
		protected override void OnWindowCreated(Window window)
		{
			ControlCenter cc = window as ControlCenter;
			if (cc == null)
				return;

			try
			{
				// Menú "New" del Control Center (patrón estándar de add-ons NT8)
				_parentMenu = cc.FindFirst("ControlCenterMenuItemNew") as NTMenuItem;
				if (_parentMenu == null)
					return;

				_menuItem = new NTMenuItem
				{
					Header = "Reglas de fondeo (MVP)",
					Style = Application.Current.TryFindResource("MainMenuItem") as Style
				};
				_menuItem.Click += OnMenuClick;
				_parentMenu.Items.Add(_menuItem);
			}
			catch
			{
				// Si el menú no se registra, el usuario aún puede compilar y abrir
				// buscando errores en NinjaScript Editor → Output.
			}
		}

		protected override void OnWindowDestroyed(Window window)
		{
			if (window is ControlCenter && _menuItem != null && _parentMenu != null)
			{
				try
				{
					_menuItem.Click -= OnMenuClick;
					if (_parentMenu.Items.Contains(_menuItem))
						_parentMenu.Items.Remove(_menuItem);
				}
				catch { }
				_menuItem = null;
				_parentMenu = null;
			}
		}

		private void OnMenuClick(object sender, RoutedEventArgs e)
		{
			OpenPanel();
		}

		/// <summary>
		/// Abre (o enfoca) la ventana del panel.
		/// </summary>
		public static void OpenPanel()
		{
			RunOnUi(() =>
			{
				foreach (Window w in Application.Current.Windows)
				{
					FundingRulesWindow existing = w as FundingRulesWindow;
					if (existing != null)
					{
						existing.Activate();
						return;
					}
				}

				var win = new FundingRulesWindow();
				win.Show();
			});
		}

		private static void RunOnUi(Action action)
		{
			if (Application.Current == null || action == null)
				return;
			if (Application.Current.Dispatcher.CheckAccess())
				action();
			else
				Application.Current.Dispatcher.Invoke(action);
		}
	}
}
