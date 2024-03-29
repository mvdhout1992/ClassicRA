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

namespace OpenRA.Mods.RA.Classic
{
	public class TargetableSubmarineInfo : TargetableUnitInfo, Requires<CloakInfo>
	{
		public readonly string[] CloakedTargetTypes = {};

		public override object Create( ActorInitializer init ) { return new TargetableSubmarine(init.self, this); }
	}

	public class TargetableSubmarine : TargetableUnit<TargetableSubmarineInfo>
	{
		public TargetableSubmarine(Actor self, TargetableSubmarineInfo info)
			: base(self, info) {}

		public override string[] TargetTypes
		{
			get { return Cloak.Cloaked ? info.CloakedTargetTypes
									   : info.TargetTypes;}
		}
	}
}
