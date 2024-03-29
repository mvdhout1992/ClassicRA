﻿#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.FileFormats;
using OpenRA.Traits;
using OpenRA.Mods.RA.Classic.Move;

namespace OpenRA.Mods.RA.Classic
{
	public class HarvesterHuskModifierInfo : ITraitInfo, Requires<HarvesterInfo>
	{
		[ActorReference]
		public readonly string FullHuskActor = null;
		public readonly int FullnessThreshold = 50;

		public object Create( ActorInitializer init ) { return new HarvesterHuskModifier(this); }
	}

	public class HarvesterHuskModifier : IHuskModifier
	{
		HarvesterHuskModifierInfo Info;
		public HarvesterHuskModifier(HarvesterHuskModifierInfo info)
		{
			Info = info;
		}

		public string HuskActor(Actor self)
		{
			return self.Trait<Harvester>().Fullness > Info.FullnessThreshold ? Info.FullHuskActor : null;
		}
	}
}
