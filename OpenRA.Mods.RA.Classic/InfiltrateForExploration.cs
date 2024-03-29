#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;
using OpenRA.FileFormats;

namespace OpenRA.Mods.RA.Classic
{
	class InfiltrateForExplorationInfo : TraitInfo<InfiltrateForExploration> {}

	class InfiltrateForExploration : IAcceptSpy
	{
		public void OnInfiltrate(Actor self, Actor spy)
		{
			/* todo: changes for per-player shrouds:
			 * - apply this everywhere, not just on the victim's client
			 * - actually steal their exploration before resetting it
			 */
			if (self.World.LocalPlayer != null && self.World.LocalPlayer.Stances[self.Owner] == Stance.Ally)
			{
				var gpsWatcher = self.Owner.PlayerActor.TraitOrDefault<GpsWatcher>();
				if (gpsWatcher != null && (gpsWatcher.Granted || gpsWatcher.GrantedAllies)) 
					return;

				self.Owner.Shroud.ResetExploration();
			}
		}
	}
}
