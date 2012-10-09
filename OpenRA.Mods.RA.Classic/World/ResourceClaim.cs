using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenRA.Mods.RA.Classic
{
	public sealed class ResourceClaim
	{
		public readonly Actor Claimer;
		public CPos Cell;

		public ResourceClaim(Actor claimer, CPos cell) { Claimer = claimer; Cell = cell; }
	}
}
