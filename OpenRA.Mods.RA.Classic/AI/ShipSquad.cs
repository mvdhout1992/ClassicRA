using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

namespace OpenRA.Mods.RA.Classic.AI
{
    class ShipSquad : Squad, ISquad
    {
        public IranAI AI;
        Actor Target = null;

        public ShipSquad(IranAI AI, SquadManager manager, SquadType squadtype, SquadRole squadrole)
            : base(AI, manager, squadtype, squadrole)
        {
            this.AI = AI;
            squadtype = SquadType.Ship;
            SetMaxSquadSize(2);
//            isready = true; // Important for creating new squads, if this doesn't get set somewhere the squad size will be unlimited
        }

        public override void Tick()
        {
            if (members.Count() == 0) return;

            // Check if enemy unit is nearby
            if (AI.ticks % 25 == 0)
            {
                Actor leader = members.First();
                if (leader == null || leader.IsDead() || leader.Destroyed) return;

                var EnemyUnits = world.FindUnitsInCircle(leader.CenterLocation, 120)
                    .Where(a => AI.p.Stances[a.Owner] == Stance.Enemy);

                if (EnemyUnits != null && EnemyUnits.Count() != 0)
                {
                    EnemyNearby = true;
                    OnEnemyUnitsNearby(EnemyUnits);
                }
                else
                {
                    EnemyNearby = false;
                }
            }

            if (AI.ticks % 75 == 0 && !EnemyNearby)
            {
                if (IsReady() && squadrole == SquadRole.AttackBase) // we're ready to attack
                {
                    // if we have a target update our move-to location
                    if (Target != null && !Target.IsDead() && !Target.Destroyed)
                    {
                        CPos Location = Target.Location;
                        Move(Location, false, 8);
                    }
                    else // if we don't have a target find one
                    {
                        Target = AI.ChooseEnemyTarget("sub");
                        if (Target == null || Target.IsDead() || Target.Destroyed)
                        {
                            Target = null;
                            AI.Debug("ShipSquad: Target is null.");
                            return;
                        }
                        CPos Location = Target.Location;
                        Move(Location, false, 8);
                    }
                }
                if (!IsReady()) // While we're not ready check if squad is full, if it is set us to ready
                {
                    isready = IsFull();
                }
            }
        }

        override public void Attack(IEnumerable<Actor> targets, bool queue)
        {
            AI.Debug("ShipSquad: Attack() called.");
            base.Attack(targets, queue);
        }

        public override void OnHarvesterDamage(Actor harvester, AttackInfo e)
        {
        }
        public override void OnBaseDamage(Actor building, AttackInfo e)
        {
        }
    }
}

