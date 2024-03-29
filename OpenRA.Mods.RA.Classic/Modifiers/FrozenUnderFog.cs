﻿#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Classic
{
	class FrozenUnderFogInfo : TraitInfo<FrozenUnderFog> {}

	class FrozenUnderFog : IRenderModifier, IVisibilityModifier
	{
		public bool IsVisible(Actor self)
		{
			return Shroud.GetVisOrigins(self).Any(o => self.World.LocalShroud.IsVisible(o));
		}

		Renderable[] cache = { };
		public IEnumerable<Renderable> ModifyRender(Actor self, IEnumerable<Renderable> r)
		{
			if (IsVisible(self))
				cache = r.ToArray();
			return cache;
		}
	}
}
