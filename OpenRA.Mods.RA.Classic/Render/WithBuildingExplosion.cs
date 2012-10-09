#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Mods.RA.Classic.Buildings;
using OpenRA.Mods.RA.Classic.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Classic.Render
{
	class WithBuildingExplosionInfo : TraitInfo<WithBuildingExplosion> { }
	class WithBuildingExplosion : INotifyKilled
	{
		public void Killed(Actor self, AttackInfo e)
		{
			var bi = self.Info.Traits.Get<BuildingInfo>();
			FootprintUtils.UnpathableTiles(self.Info.Name, bi, self.Location).Do(
				t => self.World.AddFrameEndTask(
					w => w.Add(new Explosion(w, Traits.Util.CenterOfCell(t), "building", false, 0))));
		}
	}
}