﻿#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Mods.RA.Classic.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Classic.Air
{
	class FallsToEarthInfo : TraitInfo<FallsToEarth>
	{
		[WeaponReference]
		public readonly string Explosion = null;

		public readonly bool Spins = true;
		public readonly bool Moves = false;
	}

	class FallsToEarth : INotifyKilled
	{
		public void Killed(Actor self, AttackInfo e)
		{
			self.Trait<Health>().RemoveOnDeath = false;

			self.CancelActivity();
			self.QueueActivity(new FallToEarth(self, self.Info.Traits.Get<FallsToEarthInfo>()));
		}
	}

	class FallToEarth : Activity
	{
		int acceleration = 0;
		int spin = 0;
		FallsToEarthInfo info;

		public FallToEarth(Actor self, FallsToEarthInfo info)
		{
			this.info = info;
			if (info.Spins)
				acceleration = self.World.SharedRandom.Next(2) * 2 - 1;
		}

		public override Activity Tick(Actor self)
		{
			var aircraft = self.Trait<Aircraft>();
			if (aircraft.Altitude <= 0)
			{
				if (info.Explosion != null)
					Combat.DoExplosion(self, info.Explosion, self.CenterLocation, 0);

				self.Destroy();
				return null;
			}

			if (info.Spins)
			{
				spin += acceleration;
				aircraft.Facing = (aircraft.Facing + spin) % 256;
			}

			if (info.Moves)
				FlyUtil.Fly(self, aircraft.Altitude);

			aircraft.Altitude--;

			return this;
		}

		// Cannot be cancelled
		public override void Cancel( Actor self ) { }
	}
}
