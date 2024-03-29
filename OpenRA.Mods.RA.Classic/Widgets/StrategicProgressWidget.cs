#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Classic.Widgets
{
	public class StrategicProgressWidget : Widget
	{
		bool Initialised = false;
		readonly World world;

		[ObjectCreator.UseCtor]
		public StrategicProgressWidget(World world)
		{
			IsVisible = () => true;
			this.world = world;
		}

		public override void Draw()
		{
			if (!Initialised)
				Init();

			if (!IsVisible()) return;

			var rb = RenderBounds;
			var offset = int2.Zero;

			var svc = world.Players.Select(p => p.PlayerActor.TraitOrDefault<StrategicVictoryConditions>()).FirstOrDefault();

			var totalWidth = svc.Total * 32;
			var curX = -totalWidth / 2;

			foreach (var a in svc.AllPoints)
			{
				WidgetUtils.DrawRGBA(ChromeProvider.GetImage("strategic", "critical_unowned"), offset + new float2(rb.Left + curX, rb.Top));

				if (WorldUtils.AreMutualAllies(a.Actor.Owner, world.LocalPlayer))
					WidgetUtils.DrawRGBA(ChromeProvider.GetImage("strategic", "player_owned"), offset + new float2(rb.Left + curX, rb.Top));
				else if (!a.Actor.Owner.NonCombatant)
					WidgetUtils.DrawRGBA(ChromeProvider.GetImage("strategic", "enemy_owned"), offset + new float2(rb.Left + curX, rb.Top));

				curX += 32;
			}

			offset += new int2(0, 32);

			if (world.LocalPlayer == null) return;
			var pendingWinner = FindFirstWinningPlayer(world);
			if (pendingWinner == null) return;
			var winnerSvc = pendingWinner.PlayerActor.Trait<StrategicVictoryConditions>();

			var isVictory = pendingWinner == world.LocalPlayer || !WorldUtils.AreMutualAllies(pendingWinner, world.LocalPlayer);
			var tc = "Strategic {0} in {1}".F(
				isVictory ? "victory" : "defeat",
				WidgetUtils.FormatTime(winnerSvc.TicksLeft));

			var font = Game.Renderer.Fonts["Bold"];

			var size = font.Measure(tc);
			font.DrawTextWithContrast(tc, offset + new float2(rb.Left - size.X / 2 + 1, rb.Top + 1), Color.White, Color.Black, 1);
			offset += new int2(0, size.Y + 1);
		}

		public Player FindFirstWinningPlayer(World world)
		{
			// loop through all players, see who is 'winning' and get the one with the shortest 'time to win'
			int shortest = int.MaxValue;
			Player shortestPlayer = null;

			foreach (var p in world.Players.Where(p => !p.NonCombatant))
			{
				var svc = p.PlayerActor.Trait<StrategicVictoryConditions>();

				if (svc.Holding && svc.TicksLeft > 0 && svc.TicksLeft < shortest)
				{
					shortest = svc.TicksLeft;
					shortestPlayer = p;
				}
			}

			return shortestPlayer;
		}

		void Init()
		{
			var visible = world.ActorsWithTrait<StrategicVictoryConditions>().Any() &&
				world.ActorsWithTrait<StrategicPoint>().Any();

			IsVisible = () => visible;
			Initialised = true;
		}
	}
}
