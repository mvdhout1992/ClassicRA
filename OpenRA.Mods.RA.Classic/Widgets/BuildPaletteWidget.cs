#region Copyright & License Information
/*
 * Copyright 2007-2012 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Mods.RA.Classic.Buildings;
using OpenRA.Mods.RA.Classic.Orders;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Classic.Widgets
{
	class BuildPaletteWidget : Widget
	{
		public int Columns = 2;
		public int Rows = 8;

		ProductionQueue CurrentQueue;
		List<ProductionQueue> VisibleQueues;

		bool paletteOpen = false;
		Dictionary<string, Sprite> iconSprites;

		float2 paletteOpenOrigin = new float2(Game.viewport.Width - 215, 280);
		float2 paletteClosedOrigin = new float2(Game.viewport.Width - 16, 280);
		float2 paletteOrigin;

		int paletteAnimationLength = 7;
		int paletteAnimationFrame = 0;
		bool paletteAnimating = false;

		List<Pair<Rectangle, Action<MouseInput>>> buttons = new List<Pair<Rectangle,Action<MouseInput>>>();
		List<Pair<Rectangle, Action<MouseInput>>> tabs = new List<Pair<Rectangle, Action<MouseInput>>>();
		Animation cantBuild;
		Animation clock;

		readonly WorldRenderer worldRenderer;
		readonly World world;

		[ObjectCreator.UseCtor]
		public BuildPaletteWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;

			cantBuild = new Animation("clock");
			cantBuild.PlayFetchIndex("idle", () => 0);
			clock = new Animation("clock");
			paletteOrigin = paletteClosedOrigin;
			VisibleQueues = new List<ProductionQueue>();
			CurrentQueue = null;

			iconSprites = Rules.Info.Values
				.Where(u => u.Traits.Contains<BuildableInfo>() && u.Name[0] != '^')
				.ToDictionary(
					u => u.Name,
					u => Game.modData.SpriteLoader.LoadAllSprites(
						u.Traits.Get<TooltipInfo>().Icon ?? (u.Name + "icon"))[0]);
		}

		public override Rectangle EventBounds
		{
			get { return new Rectangle((int)(paletteOrigin.X) - 24, (int)(paletteOrigin.Y), 215, Math.Max(48 * numActualRows, 40 * tabs.Count + 9)); }
		}

		public override void Tick()
		{
			VisibleQueues.Clear();

			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(p => p.Actor.Owner == world.LocalPlayer)
				.Select(p => p.Trait);

			if (CurrentQueue != null && CurrentQueue.self.Destroyed)
				CurrentQueue = null;

			foreach (var queue in queues)
			{
				if (queue.AllItems().Count() > 0)
					VisibleQueues.Add(queue);
				else if (CurrentQueue == queue)
					CurrentQueue = null;
			}
			if (CurrentQueue == null)
				CurrentQueue = VisibleQueues.FirstOrDefault();

			TickPaletteAnimation(world);
		}

		void TickPaletteAnimation(World world)
		{
			if (!paletteAnimating)
				return;

			// Increment frame
			if (paletteOpen)
				paletteAnimationFrame++;
			else
				paletteAnimationFrame--;

			// Calculate palette position
			if (paletteAnimationFrame <= paletteAnimationLength)
				paletteOrigin = float2.Lerp(paletteClosedOrigin, paletteOpenOrigin, paletteAnimationFrame * 1.0f / paletteAnimationLength);

			// Play palette-open sound at the start of the activate anim (open)
			if (paletteAnimationFrame == 1 && paletteOpen)
				Sound.PlayNotification(null, "Sounds", "BuildPaletteOpen", null);

			// Play palette-close sound at the start of the activate anim (close)
			if (paletteAnimationFrame == paletteAnimationLength + -1 && !paletteOpen)
				Sound.PlayNotification(null, "Sounds", "BuildPaletteClose", null);

			// Animation is complete
			if ((paletteAnimationFrame == 0 && !paletteOpen)
					|| (paletteAnimationFrame == paletteAnimationLength && paletteOpen))
			{
				paletteAnimating = false;
			}
		}

		public void SetCurrentTab(ProductionQueue queue)
		{
			if (!paletteOpen)
				paletteAnimating = true;

			paletteOpen = true;
			CurrentQueue = queue;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (e.Event == KeyInputEvent.Up) return false;
			if (e.KeyName == "tab")
			{
				TabChange(e.Modifiers.HasModifier(Modifiers.Shift));
				return true;
			}

			return DoBuildingHotkey(e.KeyName, world);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event != MouseInputEvent.Down)
				return false;

			if (mi.Button == MouseButton.WheelDown)
			{
				TabChange(false);
				return true;
			}
			if (mi.Button == MouseButton.WheelUp)
			{
				TabChange(true);
				return true;
			}

			var action = tabs.Where(a => a.First.Contains(mi.Location))
				.Select(a => a.Second).FirstOrDefault();
			if (action == null && paletteOpen)
				action = buttons.Where(a => a.First.Contains(mi.Location))
					.Select(a => a.Second).FirstOrDefault();

			if (action == null)
				return false;

			action(mi);
			return true;
		}

		public override void Draw()
		{
			if (!IsVisible()) return;
			// todo: fix

			int paletteHeight = DrawPalette(CurrentQueue);
			DrawBuildTabs(world, paletteHeight);
		}

		int numActualRows = 5;
		int DrawPalette(ProductionQueue queue)
		{
			buttons.Clear();

			string paletteCollection = "palette-" + world.LocalPlayer.Country.Race;
			float2 origin = new float2(paletteOrigin.X + 9, paletteOrigin.Y + 9);
			var x = 0;
			var y = 0;

			if (queue != null)
			{
				var buildableItems = queue.BuildableItems().ToArray();
                var allBuildables = queue.BuildableItems().OrderBy(a => a.Traits.Get<BuildableInfo>().BuildPaletteOrder).ToArray();

				var overlayBits = new List<Pair<Sprite, float2>>();
				var textBits = new List<Pair<float2, string>>();
				numActualRows = Math.Max((allBuildables.Count() + Columns - 1) / Columns, Rows);

				// Palette Background
				WidgetUtils.DrawRGBA(ChromeProvider.GetImage(paletteCollection, "top"), new float2(origin.X + 55, origin.Y + 31));
				for (var w = 0; w < numActualRows; w++)
					WidgetUtils.DrawRGBA(
						ChromeProvider.GetImage(paletteCollection, "bg-" + (w % 4).ToString()),
						new float2(origin.X + 55, (origin.Y + 40) + 48 * w));
				WidgetUtils.DrawRGBA(ChromeProvider.GetImage(paletteCollection, "bottom"),
					new float2(origin.X + 55, (origin.Y + 39) + 48 * numActualRows));


				// Icons
				string tooltipItem = null;
				foreach (var item in allBuildables)
				{
					var rect = new RectangleF((origin.X + 64) + x * 64, (origin.Y + 40) + 48 * y, 64, 48);
					var drawPos = new float2(rect.Location);
					WidgetUtils.DrawSHP(iconSprites[item.Name], drawPos, worldRenderer);

					var firstOfThis = queue.AllQueued().FirstOrDefault(a => a.Item == item.Name);

					if (rect.Contains(Viewport.LastMousePos))
						tooltipItem = item.Name;

					var overlayPos = drawPos + new float2(32, 16);

					if (firstOfThis != null)
					{
						clock.PlayFetchIndex("idle",
							() => (firstOfThis.TotalTime - firstOfThis.RemainingTime)
								* (clock.CurrentSequence.Length - 1) / firstOfThis.TotalTime);
						clock.Tick();
						WidgetUtils.DrawSHP(clock.Image, drawPos, worldRenderer);

						if (queue.CurrentItem() == firstOfThis)
							textBits.Add( Pair.New( overlayPos, GetOverlayForItem(firstOfThis) ) );


						var repeats = queue.AllQueued().Count(a => a.Item == item.Name);
						if (repeats > 1 || queue.CurrentItem() != firstOfThis)
							textBits.Add(Pair.New(overlayPos + new float2(-24, -14), repeats.ToString()));
					}
                    else
                        if (queue.AllQueued().Any())
                        overlayBits.Add(Pair.New(cantBuild.Image, drawPos));
//                    else
//						if (!buildableItems.Any(a => a.Name == item.Name))
               

					var closureName = buildableItems.Any(a => a.Name == item.Name) ? item.Name : null;
					buttons.Add(Pair.New(new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), HandleClick(closureName, world)));

					if (++x == Columns) { x = 0; y++; }
				}
				if (x != 0) y++;

				foreach (var ob in overlayBits)
					WidgetUtils.DrawSHP(ob.First, ob.Second, worldRenderer);

				var font = Game.Renderer.Fonts["TinyBold"];
				foreach (var tb in textBits)
				{
					var size = font.Measure(tb.Second);
					font.DrawTextWithContrast(tb.Second, tb.First - new float2( size.X / 2, 0 ),
						Color.White, Color.Black, 1);
				}

				// Tooltip
				if (tooltipItem != null && !paletteAnimating && paletteOpen)
					DrawProductionTooltip(world, tooltipItem,
						new float2(Game.viewport.Width, origin.Y + numActualRows * 48 + 9).ToInt2());
			}

			// Palette Dock
			WidgetUtils.DrawRGBA(ChromeProvider.GetImage(paletteCollection, "dock-top"),
				new float2(Game.viewport.Width - 14, origin.Y - 23));

			for (int i = 0; i < numActualRows; i++)
				WidgetUtils.DrawRGBA(ChromeProvider.GetImage(paletteCollection, "dock-" + (i % 4).ToString()),
					new float2(Game.viewport.Width - 14, origin.Y + 48 * i));

			WidgetUtils.DrawRGBA(ChromeProvider.GetImage(paletteCollection, "dock-bottom"),
				new float2(Game.viewport.Width - 14, origin.Y - 1 + 48 * numActualRows));

			return 48 * y + 9;
		}

		string GetOverlayForItem(ProductionItem item)
		{
			if (item.Paused) return "ON HOLD";
			if (item.Done) return "READY";
			return WidgetUtils.FormatTime(item.RemainingTimeActual);
		}

		Action<MouseInput> HandleClick(string name, World world)
		{
			return mi => {
				Sound.PlayNotification(null, "Sounds", "TabClick", null);

				if (name != null)
					HandleBuildPalette(world, name, (mi.Button == MouseButton.Left));
			};
		}

		Action<MouseInput> HandleTabClick(ProductionQueue queue, World world)
		{
			return mi => {
				if (mi.Button != MouseButton.Left)
					return;

				Sound.PlayNotification(null, "Sounds", "TabClick", null);
				var wasOpen = paletteOpen;
				paletteOpen = (CurrentQueue == queue && wasOpen) ? false : true;
				CurrentQueue = queue;
				if (wasOpen != paletteOpen)
					paletteAnimating = true;
			};
		}

		static string Description( string a )
		{
			// hack hack hack - going to die soon anyway
			if (a == "barracks")
				return "Infantry production";
			if (a == "vehicleproduction")
				return "Vehicle production";
			if (a == "techcenter")
				return "Tech Center";
			if (a == "anypower")
				return "Power Plant";

			ActorInfo ai;
			Rules.Info.TryGetValue(a.ToLowerInvariant(), out ai);
			if (ai != null && ai.Traits.Contains<TooltipInfo>())
				return ai.Traits.Get<TooltipInfo>().Name;

			return a;
		}

		void HandleBuildPalette( World world, string item, bool isLmb )
		{
			var unit = Rules.Info[item];
			var producing = CurrentQueue.AllQueued().FirstOrDefault( a => a.Item == item );

			if (isLmb)
			{
				if (producing != null && producing == CurrentQueue.CurrentItem())
				{
					if (producing.Done)
					{
						if (unit.Traits.Contains<BuildingInfo>())
							world.OrderGenerator = new PlaceBuildingOrderGenerator(CurrentQueue.self, item);
						else
							StartProduction( world, item );
						return;
					}

					if (producing.Paused)
					{
						world.IssueOrder(Order.PauseProduction(CurrentQueue.self, item, false));
						return;
					}
				}

				StartProduction(world, item);
			}
			else
			{
				if (producing != null)
				{
					// instant cancel of things we havent really started yet, and things that are finished
					if (producing.Paused || producing.Done || producing.TotalCost == producing.RemainingCost)
					{ 
						Sound.PlayNotification(world.LocalPlayer, "Speech", CurrentQueue.Info.CancelledAudio, world.LocalPlayer.Country.Race);
						int numberToCancel = Game.GetModifierKeys().HasModifier(Modifiers.Shift) ? 5 : 1;

						world.IssueOrder(Order.CancelProduction(CurrentQueue.self, item, numberToCancel));
					}
					else
					{
						Sound.PlayNotification(world.LocalPlayer, "Speech", CurrentQueue.Info.OnHoldAudio, world.LocalPlayer.Country.Race);
						world.IssueOrder(Order.PauseProduction(CurrentQueue.self, item, true));
					}
				}
			}
		}

		void StartProduction( World world, string item )
		{

			Sound.PlayNotification(world.LocalPlayer, "Speech", CurrentQueue.Info.QueuedAudio, world.LocalPlayer.Country.Race);
			world.IssueOrder(Order.StartProduction(CurrentQueue.self, item,
				Game.GetModifierKeys().HasModifier(Modifiers.Shift) ? 5 : 1));
		}

		void DrawBuildTabs( World world, int paletteHeight)
		{
			const int tabWidth = 24;
			const int tabHeight = 40;
			var x = paletteOrigin.X + 87;
			var y = paletteOrigin.Y;

			tabs.Clear();

			foreach (var queue in VisibleQueues)
			{
				string[] tabKeys = { "normal", "ready", "selected" };
				var producing = queue.CurrentItem();
				var index = queue == CurrentQueue ? 2 : (producing != null && producing.Done) ? 1 : 0;

				var race = world.LocalPlayer.Country.Race;
				WidgetUtils.DrawRGBA(ChromeProvider.GetImage("tabs-"+tabKeys[index], race+"-"+queue.Info.Type), new float2(x, y));

				var rect = new Rectangle((int)x,(int)y,(int)tabWidth,(int)tabHeight);
				tabs.Add(Pair.New(rect, HandleTabClick(queue, world)));

				if (rect.Contains(Viewport.LastMousePos))
				{
					var text = queue.Info.Type;
					var font = Game.Renderer.Fonts["Bold"];
					var sz = font.Measure(text);
					WidgetUtils.DrawPanelPartial("dialog4",
						Rectangle.FromLTRB((int)rect.Left - sz.X - 30, (int)rect.Top, (int)rect.Left - 5, (int)rect.Bottom),
						PanelSides.All);

					font.DrawText(text, new float2(rect.Left - sz.X - 20, rect.Top + 12), Color.White);
				}

				x += tabWidth;
			}
		}

		void DrawRightAligned(string text, int2 pos, Color c)
		{
			var font = Game.Renderer.Fonts["Bold"];
			font.DrawText(text, pos - new int2(font.Measure(text).X, 0), c);
		}

		void DrawProductionTooltip(World world, string unit, int2 pos)
		{
			pos.Y += 15;

			var pl = world.LocalPlayer;
			var p = pos.ToFloat2() - new float2(297, -3);

			var info = Rules.Info[unit];
			var tooltip = info.Traits.Get<TooltipInfo>();
			var buildable = info.Traits.Get<BuildableInfo>();
			var cost = info.Traits.Get<ValuedInfo>().Cost;
			var canBuildThis = CurrentQueue.CanBuild(info);

			var longDescSize = Game.Renderer.Fonts["Regular"].Measure(tooltip.Description.Replace("\\n", "\n")).Y;
			if (!canBuildThis) longDescSize += 8;

			WidgetUtils.DrawPanel("dialog4", new Rectangle(Game.viewport.Width - 300, pos.Y, 300, longDescSize + 65));

			Game.Renderer.Fonts["Bold"].DrawText(
				tooltip.Name + ((buildable.Hotkey != null)? " ({0})".F(buildable.Hotkey.ToUpper()) : ""),
											       p.ToInt2() + new int2(5, 5), Color.White);

			var resources = pl.PlayerActor.Trait<PlayerResources>();
			var power = pl.PlayerActor.Trait<PowerManager>();

			DrawRightAligned("${0}".F(cost), pos + new int2(-5, 5),
				(resources.DisplayCash + resources.DisplayOre >= cost ? Color.White : Color.Red ));

			var lowpower = power.PowerState != PowerState.Normal;
			var time = CurrentQueue.GetBuildTime(info.Name)
				* ((lowpower)? CurrentQueue.Info.LowPowerSlowdown : 1);
			DrawRightAligned(WidgetUtils.FormatTime(time), pos + new int2(-5, 35), lowpower ? Color.Red: Color.White);

			var bi = info.Traits.GetOrDefault<BuildingInfo>();
			if (bi != null)
				DrawRightAligned("{1}{0}".F(bi.Power, bi.Power > 0 ? "+" : ""), pos + new int2(-5, 20),
					((power.PowerProvided - power.PowerDrained) >= -bi.Power || bi.Power > 0)? Color.White: Color.Red);

			p += new int2(5, 35);
			if (!canBuildThis)
			{
				var prereqs = buildable.Prerequisites
					.Select( a => Description( a ) );
				Game.Renderer.Fonts["Regular"].DrawText(
					"Requires {0}".F(prereqs.JoinWith(", ")),
					p.ToInt2(),
					Color.White);

				p += new int2(0, 8);
			}

			p += new int2(0, 15);
			Game.Renderer.Fonts["Regular"].DrawText(tooltip.Description.Replace("\\n", "\n"),
				p.ToInt2(), Color.White);
		}

		bool DoBuildingHotkey(string key, World world)
		{
			if (!paletteOpen) return false;
			if (CurrentQueue == null) return false;

			var toBuild = CurrentQueue.BuildableItems().FirstOrDefault(b => b.Traits.Get<BuildableInfo>().Hotkey == key);

			if ( toBuild != null )
			{
				Sound.PlayNotification(null, "Sounds", "TabClick", null);
				HandleBuildPalette(world, toBuild.Name, true);
				return true;
			}

			return false;
		}

		void TabChange(bool shift)
		{
			var queues = VisibleQueues.Concat(VisibleQueues);
			if (shift) queues = queues.Reverse();
			var nextQueue = queues.SkipWhile( q => q != CurrentQueue )
				.ElementAtOrDefault(1);
			if (nextQueue != null)
				SetCurrentTab( nextQueue );
		}
	}
}