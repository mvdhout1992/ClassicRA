#region Copyright & License Information
/*
 * Copyright 2007-2012 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Mods.RA.Classic.Move;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Classic.Activities
{
	class CaptureActor : Activity
	{
		Actor target;

		public CaptureActor(Actor target) { this.target = target; }

		public override Activity Tick(Actor self)
		{
			if (IsCanceled) return NextActivity;
			if (target == null || !target.IsInWorld || target.IsDead()) return NextActivity;
			if (target.Owner == self.Owner) return NextActivity;

			// Need to be next to building, TODO: stop capture when going away
			var mobile = self.Trait<Mobile>();
			var nearest = target.OccupiesSpace.NearestCellTo(mobile.toCell);
			if ((nearest - mobile.toCell).LengthSquared > 2)
				return Util.SequenceActivities(new MoveAdjacentTo(Target.FromActor(target)), this);

			if (!target.Trait<Capturable>().BeginCapture(target, self))
				return NextActivity;

			var capturesInfo = self.Info.Traits.Get<CapturesInfo>();
			if (capturesInfo != null && capturesInfo.WastedAfterwards)
				self.World.AddFrameEndTask(w => self.Destroy());

			return this;
		}
	}
}
