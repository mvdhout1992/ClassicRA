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

namespace OpenRA.Mods.RA.Classic.AI
{
    class HarvesterDefenseSquad : Squad, ISquad
    {
        public IranAI AI;
        public Actor harvester = null; // the harvester to protect

        public HarvesterDefenseSquad(IranAI AI, SquadManager manager, SquadType squadtype, SquadRole squadrole)
            : base(AI, manager, squadtype, squadrole)
        {

            this.AI = AI;
            squadtype = SquadType.Tank;
            SetMaxSquadSize(2);
        }

        public override void Tick()
        {
            base.Tick();

            // Every few seconds move close to a Harvester to protect
            if (AI.ticks % 25 == 0)
            {
                SetSquadRole(SquadRole.DefendHarvesters); // Try to force squad role

                if (harvester == null)
                {
                    harvester = FindHarvesterToProtect();
                }
                if (!EnemyNearby && harvester != null)
                {
                    MoveToHarvester(harvester);
                }
            } 
        }

        public void MoveToHarvester(Actor Harvester)
        {
            Move(Harvester.Location, false, 8);
        }

        public Actor FindHarvesterToProtect()
        {
            var units = world.FindUnitsInCircle(AI.BaseCenter.ToPPos(), 100);
            if (units != null && units.Count() > 0)
            {
                var harvesters = AI.world.Actors.Where(a => a.HasTrait<Harvester>() && a.Owner == AI.p);
                if (harvesters != null && harvesters.Count() > 0)
                {
                    foreach (Actor h in harvesters)
                    {
                        if (!manager.squads.OfType<HarvesterDefenseSquad>().Any(s => s.harvester == h))
                        {
                            return h;
                        }
                    }
                }
            }
            return null;
        }

        public override void OnHarvesterDamage(Actor harvester, AttackInfo e)
        {
            AI.Debug("Harvester under attack, tanksquad");

            if (!EnemyNearby && harvester != this.harvester)
            {
                this.harvester = harvester;
                MoveToHarvester(harvester);
            }
            else
            {
                List<Actor> wrapper = new List<Actor>() { e.Attacker };
                Attack(wrapper, false);
            }
        }
    }
}
