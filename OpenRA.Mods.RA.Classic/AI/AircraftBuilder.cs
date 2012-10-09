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
    class AircraftBuilder : AIBuilder, IAIBuilder
    {
        enum BuildState { ChooseItem, WaitForProduction, WaitForFeedback }
        BuildState state = BuildState.WaitForFeedback;

//       public readonly string[] UnitQueues = { "Vehicle", "Infantry", "Plane", "Ship" };
       int feedbacktime = 60; // time to update feedback state, in ticks

        public AircraftBuilder(IranAI AI)
        {
            this.AI = AI;
            this.world = AI.world;
        }

        public void Tick()
        {
            // Pick a free queue
            var queue = FindQueues("Plane").FirstOrDefault();
            if (queue == null)
            {
                AI.Debug("Can't find free queue.");
                return;
            }

            switch (state)
            {
                case BuildState.ChooseItem:
                    {
                        if (queue.CurrentItem() != null)
                        {
//                            AI.Debug("InfantryBuilder currentItem != null");
                            state = BuildState.WaitForProduction;
                            return;
                        }

                        String item = ChooseAircraft(queue);
                        if (item == null)
                        {
                            state = BuildState.WaitForFeedback;
                            //                            AI.Debug("No building to choose from to produce");
                        }
                        else
                        {
                            state = BuildState.WaitForProduction;
                            world.IssueOrder(Order.StartProduction(queue.self, item, 1));
//                            AI.Debug("Ordering infantry {0}", item);
                        }
                        break;
                    }
                case BuildState.WaitForProduction:
                    {
                        var currentBuilding = queue.CurrentItem();

                        // Somehow the queue is empty so check for feedback
                        if (currentBuilding == null) 
                        {
                            state = BuildState.WaitForFeedback;
                            /*AI.Debug("currentBuilding is null");*/ 
                         return; 
                        }
                        else if (currentBuilding.Paused)
                            world.IssueOrder(Order.PauseProduction(queue.self, currentBuilding.Item, false));

                        if (queue.CurrentDone)
                        {
                            state = BuildState.WaitForFeedback;
 //                           PlaceStructure(queue, currentBuilding);
                        }
                        break;
                    }
                case BuildState.WaitForFeedback:
                    {
                        if (AI.ticks % feedbacktime == 0)
                            state = BuildState.ChooseItem;
                        break;
                    }
            }   
        }

        String ChooseAircraft(ProductionQueue queue)
        {
            String item = null;

            if (AI.p.Country.Race == "soviet")
            {
                int random = AI.random.Next() % 3;

                if (random == 0)
                {
                    item = "yak";
                }
                else if (random == 1)
                {
                    item = "mig";
                }
                else if (random == 2)
                {
                    item = "hind";
                }
            }

            else if (AI.p.Country.Race == "allies")
            {
                    item = "heli";
            }

            if (item == "heli" || item == "hind")
            {
                var hpads = world.ActorsWithTrait<Building>()
                    .Where(a => a.Actor.Owner == AI.p && a.Actor.Info.Name == "hpad").Count();

                var helis = world.Actors
                    .Where(a => a.Owner == AI.p && a.Info.Name == "heli"
                    || a.Info.Name == "hind").Count();

                if ((hpads * 1) <= helis) // Max amount of allowed helis
                {
                    item = null;
                }
            }

            if (item == "mig" || item == "yak")
            {
                var aflds = world.ActorsWithTrait<Building>()
                    .Where(a => a.Actor.Owner == AI.p && a.Actor.Info.Name == "afld").Count();

                var planes = world.Actors
                    .Where(a => a.Owner == AI.p && a.Info.Name == "yak"
                    || a.Info.Name == "mig").Count();

                if ((aflds * 1) <= planes) // Max amount of allowed planes
                {
                    item = null;
                }
            }

            var buildableThings = queue.BuildableItems();
            if (buildableThings.Any(b => b.Name == item)) return item; // Return it only if we can actually build it
            return null;
        }
    }
}
