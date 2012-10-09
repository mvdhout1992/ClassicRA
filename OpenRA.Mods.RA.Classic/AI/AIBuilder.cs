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
using OpenRA.FileFormats;
using OpenRA.Mods.RA.Classic.Buildings;
using OpenRA.Mods.RA.Classic.Move;
using OpenRA.Traits;
using XRandom = OpenRA.Thirdparty.Random;
using System.Collections;
using OpenRA.Mods.RA.Classic.Air;

namespace OpenRA.Mods.RA.Classic.AI
{
    public interface IAIBuilder
    {
        void Tick();
    }
    // Provides helper functions for builder-type classes
    class AIBuilder
    {
        public World world;
        public IranAI AI;

        // category: "Building", "Defense"
        public IEnumerable<ProductionQueue> FindQueues(string category)
        {
            return world.ActorsWithTrait<ProductionQueue>()
                .Where(a => a.Actor.Owner == AI.p && a.Trait.Info.Type == category)
                .Select(a => a.Trait);
        }

        public bool HasAdequateItem(String name, int count)
        {
            var amount = world.ActorsWithTrait<Building>()
                .Where(a => a.Actor.Owner == AI.p && a.Actor.Info.Name == name).Count();

            return count <= amount ? true : false;
        }

        public bool NoBuildingsUnder(IEnumerable<CPos> cells)
        {
            var bi = world.WorldActor.Trait<BuildingInfluence>();
            return cells.All(c => bi.GetBuildingAt(c) == null);
        }

        public bool HasAdequatePower()
        {
//            AI.Debug("Current Power = {0}, drain = {1}", AI.Power.PowerProvided, AI.Power.PowerDrained);

            if (AI.Power.PowerDrained < 100)
                return true;

            return AI.Power.PowerProvided > 50 &&
                AI.Power.PowerProvided > AI.Power.PowerDrained + 100;
        }
    }
}
