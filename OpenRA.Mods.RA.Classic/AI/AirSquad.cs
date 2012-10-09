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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenRA.Mods.RA.Classic.Buildings;
using OpenRA.Traits;
using OpenRA.Mods.RA.Classic.Activities;
using OpenRA.Mods.RA.Classic.Air;

namespace OpenRA.Mods.RA.Classic.AI
{
    class AirSquad : Squad, ISquad
    {
        public IranAI AI;
        public Actor harvester = null; // the harvester to protect

        public AirSquad(IranAI AI, SquadManager manager, SquadType squadtype, SquadRole squadrole)
            : base(AI, manager, squadtype, squadrole)
        {
            this.AI = AI;
            squadtype = SquadType.Air;
            SetMaxSquadSize(1);
            isready = true; // Important for creating new squads, if this doesn't get set somewhere the squad size will be unlimited
        }

        public override void Tick()
        {
            if ((AI.ticks % 25 != 0) || (members.Count() < 1)) return;

            Actor unit = members.First();

            if (unit == null || unit.IsDead() || unit.Destroyed) return;

            Activity act = unit.GetCurrentActivity();

            if (act == null && !unit.TraitOrDefault<LimitedAmmo>().FullAmmo())
            {
//                new Rearm(unit);
                world.IssueOrder(new Order("ReturnToBase", unit, false));
            }

            if (act == null)
            {
                AI.Debug("AirSquad null activity");
            }
            else if (act != null)
            {
                AI.Debug("Activity is {0}", act.ToString());
            }

            if (act != null && 
                (act.GetType() == typeof(Air.FlyCircle) ||act.GetType() == typeof(Air.HeliFly) ) ) 
            {
                AI.Debug("Aircraft not doing anything, ammo: {0} {1}",
                    unit.TraitOrDefault<LimitedAmmo>().FullAmmo(),
                    unit.TraitOrDefault<LimitedAmmo>().HasAmmo());

                new Rearm(unit);
//                world.IssueOrder(new Order("ReturnToBase", unit, false));
            }
            
            // if full ammo or has some ammo while still flying
            if ( unit.TraitOrDefault<LimitedAmmo>().FullAmmo()
                || (unit.TraitOrDefault<LimitedAmmo>().HasAmmo() &&  act != null && 
                (act.GetType() == typeof(Air.Fly) || act.GetType() == typeof(Air.HeliFly) )))
            {
//                float range = unit.TraitOrDefault<AttackBase>().GetMaximumRange();

//                var enemynearby = world.FindUnitsInCircle(unit.CenterLocation, Game.CellSize * (int)range)
                var enemynearby = world.Actors.Where(a1 => !a1.Destroyed && !a1.IsDead() && a1.HasTrait<ITargetable>() 
                        && !manager.IsAircraft(a1) && unit.Owner.Stances[a1.Owner] == Stance.Enemy).ToList();

                if (!enemynearby.Any())
                {
                    return;
                }

                Attack(enemynearby, false);
            }               
        }

        override public void Attack(IEnumerable<Actor> targets, bool queue)
        {
            Actor unit = members.First();

            if (unit.Info.Name == "yak" || unit.Info.Name == "hind")
            {
                targets = targets.Where(a => manager.IsInfantry(a));
            }
            if (unit.Info.Name == "mig" || unit.Info.Name == "heli")
            {
                targets = targets.Where(a => manager.IsVehicle(a));
            }

            if (!targets.Any())
                return;

            Actor target = targets.ClosestTo(unit.CenterLocation);

            world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target });
        }

        public override void OnHarvesterDamage(Actor harvester, AttackInfo e)
        {
        }
        public override void OnBaseDamage(Actor building, AttackInfo e)
        {
        }
    }
}
