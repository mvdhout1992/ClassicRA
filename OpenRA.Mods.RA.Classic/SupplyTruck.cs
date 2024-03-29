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
using System.Drawing;
using OpenRA.Mods.RA.Classic.Activities;
using OpenRA.Mods.RA.Classic.Buildings;
using OpenRA.Mods.RA.Classic.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Classic
{
	class SupplyTruckInfo : ITraitInfo
	{
		public readonly int Payload = 500;
		public object Create(ActorInitializer init) { return new SupplyTruck(this); }
	}

	class SupplyTruck : IIssueOrder, IResolveOrder, IOrderVoice
	{
		SupplyTruckInfo Info;

		public SupplyTruck(SupplyTruckInfo info)
		{
			Info = info;
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get { yield return new SupplyTruckOrderTargeter(); }
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID == "DeliverSupplies")
				return new Order(order.OrderID, self, queued) { TargetActor = target.Actor };

			return null;
		}

		public string VoicePhraseForOrder(Actor self, Order order)
		{
			return "Move";
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "DeliverSupplies")
			{
				self.SetTargetLine(Target.FromOrder(order), Color.Yellow);
				self.CancelActivity();
				self.QueueActivity(new Enter(order.TargetActor));
				self.QueueActivity(new DonateSupplies(order.TargetActor, Info.Payload));
			}
		}

		class SupplyTruckOrderTargeter : UnitTraitOrderTargeter<Building>
		{
			public SupplyTruckOrderTargeter()
				: base("DeliverSupplies", 5, "enter", false, true)
			{
			}

			public override bool CanTargetActor(Actor self, Actor target, bool forceAttack, bool forceQueued, ref string cursor)
			{
				if (!base.CanTargetActor(self, target, forceAttack, forceQueued, ref cursor)) return false;
				if (target.AppearsHostileTo(self)) return false;
				if (!target.HasTrait<AcceptsSupplies>()) return false;

				IsQueued = forceQueued;
				return true;
			}
		}
	}
}
