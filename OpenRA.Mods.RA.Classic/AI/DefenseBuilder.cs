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
    class DefenseBuilder : AIBuilder, IAIBuilder
    {
        enum BuildState { ChooseItem, WaitForProduction, WaitForFeedback }
        BuildState state = BuildState.WaitForFeedback;

        const int MaxBaseDistance = 40;
        
        const int MaxAntiAirDefense = 3;
        const int MaxCheapDefense = 5;
        const int MaxExpensiveDefense = 3;

        int feedbacktime = 60; // time to update feedback state, in ticks

        public DefenseBuilder(IranAI AI)
        {
            this.AI = AI;
            this.world = AI.world;
        }

        public void Tick()
        {
            // Pick a free queue
            var queue = FindQueues("Defense").FirstOrDefault();
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
//                            AI.Debug("DefenseBuilder currentItem != null");
                            state = BuildState.WaitForProduction;
                            return;
                        }

                        String item = ChooseStructure(queue);
                        if (item == null)
                        {
                            state = BuildState.WaitForFeedback;
                            //                            AI.Debug("No building to choose from to produce");
                        }
                        else
                        {
                            state = BuildState.WaitForProduction;
                            world.IssueOrder(Order.StartProduction(queue.self, item, 1));
//                            AI.Debug("Ordering building {0}", item);
                        }
                        break;
                    }
                case BuildState.WaitForProduction:
                    {
                        var currentBuilding = queue.CurrentItem();

                        if (currentBuilding == null)
                        {
                            state = BuildState.WaitForFeedback;
                            /*AI.Debug("currentBuilding is null");*/
                            return;
                        }

                        if (currentBuilding.Paused)
                            world.IssueOrder(Order.PauseProduction(queue.self, currentBuilding.Item, false));

                        if (queue.CurrentDone)
                        {
                            if (!HasAdequatePower()) return;

                            state = BuildState.WaitForFeedback;
                            PlaceStructure(queue, currentBuilding);
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

        public void PlaceStructure(ProductionQueue queue, ProductionItem currentBuilding)
        {
            CPos? location = ChooseBuildLocation(currentBuilding.Item);

            if (location == null)
            {
                AI.Debug("AI: Nowhere to place or no adequate number {0}".F(currentBuilding.Item));
                world.IssueOrder(Order.CancelProduction(queue.self, currentBuilding.Item, 1));
            }
            else
                world.IssueOrder(new Order("PlaceBuilding", AI.p.PlayerActor, false)
                {
                    TargetLocation = location.Value,
                    TargetString = currentBuilding.Item
                });
            //            if (!HasAdequateNumber(currentBuilding.Item, ai.p))
            //                world.IssueOrder(Order.CancelProduction(queue.self, currentBuilding.Item, 1));
        }

        public CPos? ChooseBuildLocation(String name)
        {
            var bi = Rules.Info[name].Traits.Get<BuildingInfo>();
            if (bi == null) return null;

            Actor owner = AI.ChooseEnemyTarget("base");

            PPos EnemyPos = (new PPos(0, 0));
            if (owner == null)
            {
                AI.Debug("owner is null");
            }
            else
            {
                EnemyPos = owner.CenterLocation;
            }
            var tlist = world.FindTilesInCircle(AI.BaseCenter, MaxBaseDistance).OrderBy(t => (new PPos(t.ToPPos().X, t.ToPPos().Y) - EnemyPos).LengthSquared);

            if (!tlist.Any()) return null;


            var defenses = world.ActorsWithTrait<Building>()
.Where(a => a.Actor.Owner == AI.p && (a.Actor.Info.Name == "sam"
    || a.Actor.Info.Name == "agun" || a.Actor.Info.Name == "tsla"
    || a.Actor.Info.Name == "gun" || a.Actor.Info.Name == "hbox"
    || a.Actor.Info.Name == "pbox" || a.Actor.Info.Name == "ftur"));

            if (defenses != null)
            {
                foreach (TraitPair<Building> p in defenses)
                {
                    Actor building = p.Actor;
                    var tiles = world.FindTilesInCircle(building.CenterLocation.ToCPos(), 5);
                    tlist = tlist.Except(tiles).OrderBy(a => (new PPos(a.ToPPos().X, a.ToPPos().Y) - EnemyPos).LengthSquared);
                }
            }

            foreach (var t in tlist)
                if (world.CanPlaceBuilding(name, bi, t, null))
                    if (bi.IsCloseEnoughToBase(world, AI.p, name, t))
                        if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(name, bi, t), false)))
                            return t;

            //            if (actorType == "syrd" || actorType == "spen")
            //                tried.Add(Rules.Info[actorType].Name);
            return null;
        }

        // pbox hbox gun agun ftur tsla sam
        String ChooseStructure(ProductionQueue queue)
        {
            if (!HasAdequateItem("weap", 1)) return null; // Don't start producing until we have a War Factory
            String item = null;
            var buildableThings = queue.BuildableItems();

            if (AI.p.Country.Race == "soviet")
            {
                int random = AI.random.Next() % 3;

                if (random == 0)
                {
                    if (!HasAdequateItem("ftur", MaxCheapDefense)) item = "ftur";
                }
                else if (random == 1)
                {
                    if (!HasAdequateItem("tsla", MaxExpensiveDefense)) item = "tsla";
                } 
                else if (random == 2)
                {
                    if (!HasAdequateItem("sam", MaxAntiAirDefense)) item = "sam";
                } 
            }

            else if (AI.p.Country.Race == "allies")
            {
                int random = AI.random.Next() % 4;

                if (random == 0)
                {
                    if (!HasAdequateItem("pbox", MaxCheapDefense)) item = "pbox";
                }
                else if (random == 1)
                {
                    if (!HasAdequateItem("gun", MaxExpensiveDefense)) item = "gun";
                } 
                else if (random == 2)
                {
                    if (!HasAdequateItem("agun", MaxAntiAirDefense)) item = "agun";
                }
                else if (random == 3)
                {
                    if (!HasAdequateItem("hbox", MaxCheapDefense)) item = "hbox";
                } 
            }

            if (buildableThings.Any(b => b.Name == item)) return item;
            return null;
        }
    }
}
