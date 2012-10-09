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
    class BaseBuilder : AIBuilder, IAIBuilder
    {
        enum BuildState { ChooseItem, WaitForProduction, WaitForFeedback }
        BuildState state = BuildState.WaitForFeedback;

        const int MaxBaseDistance = 15;
        bool firstbuild = true;
        int feedbacktime = 60; // time to update feedback state, in ticks

        public BaseBuilder(IranAI AI)
        {
            this.AI = AI;
            this.world = AI.world;
        }

        public void Tick()
        {
            // Pick a free queue
            var queue = FindQueues("Building").FirstOrDefault();
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
//                            AI.Debug("BaseBuilder currentItem != null");
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

                        else if (currentBuilding.Paused)
                            world.IssueOrder(Order.PauseProduction(queue.self, currentBuilding.Item, false));

                        if (queue.CurrentDone)
                        {
//                            state = BuildState.WaitForFeedback;
                            PlaceStructure(queue, currentBuilding);                          
                        }
                        state = BuildState.WaitForFeedback; // DEBUG ADDED
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
            CPos? location = null;

            if (currentBuilding.Item.Equals("proc"))
            {
                location = ChooseRefineryBuildLocation(currentBuilding.Item);
            }
            else
            {
                location = ChooseBuildLocation(currentBuilding.Item);
            }

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


//                for (var k = 0; k < MaxBaseDistance; k++)
//                    foreach (var t in world.FindTilesInCircle(AI.BaseCenter, k))
            var tiles = world.FindTilesInCircle(AI.BaseCenter, MaxBaseDistance).OrderBy(t => (t - AI.BaseCenter).LengthSquared);

            if (!tiles.Any()) return null;

            foreach (var t in tiles)
                        if (world.CanPlaceBuilding(name, bi, t, null))
                            if (bi.IsCloseEnoughToBase(world, AI.p, name, t) || firstbuild )
                                if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(name, bi, t), false)))
                                {
                                    firstbuild = false;
                                    return t;
                                }
//            if (actorType == "syrd" || actorType == "spen")
//                tried.Add(Rules.Info[actorType].Name);
            return null;
        }
        public CPos? ChooseRefineryBuildLocation(String name)
        {
            var bi = Rules.Info[name].Traits.Get<BuildingInfo>();
            if (bi == null) return null;

            var owner = world.FindTilesInCircle(AI.BaseCenter, MaxBaseDistance).Where(a => world.GetTerrainType(new CPos(a.X, a.Y)) == "Ore" || world.GetTerrainType(new CPos(a.X, a.Y)) == "Gems").FirstOrDefault();
                if (owner != null)
                {
                    var tiles = world.FindTilesInCircle(AI.BaseCenter, MaxBaseDistance).OrderBy(a => (new PPos(a.ToPPos().X, a.ToPPos().Y) - new CPos(owner.X, owner.Y).ToPPos()).LengthSquared);
                    
                    if (!tiles.Any()) return null;
                    
                    foreach(var t in tiles)
                        if (world.CanPlaceBuilding(name, bi, t, null))
                            if (bi.IsCloseEnoughToBase(world, AI.p, name, t))
                                if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(name, bi, t), false)))
                                    return t;
                }
            //            if (actorType == "syrd" || actorType == "spen")
            //                tried.Add(Rules.Info[actorType].Name);
            return null;
        }

        String ChooseStructure(ProductionQueue queue)
        {
            String item = null;
            var buildableThings = queue.BuildableItems();

            if (!HasAdequateItem("powr", 1)) { return "powr"; }

            if (!HasAdequatePower())
                if (buildableThings.Any(b => b.Name == "apwr"))
                    return "apwr";
                else
                    return "powr";

            if (AI.p.Country.Race == "soviet")
                if (!HasAdequateItem("barr", 1)) { return "barr"; }
            if (AI.p.Country.Race == "allies")
                if (!HasAdequateItem("tent", 1)) { return "tent"; }
                
            if (!HasAdequateItem("proc", 1)) { return "proc"; }

            if (!HasAdequateItem("weap", 1)) { return "weap"; }

            if (!HasAdequateItem("apwr", 1)) { return "apwr"; }

            if (!HasAdequateItem("weap", 2)) { return "weap"; }

            if (!HasAdequateItem("dome", 1)) { return "dome"; }

            if (AI.p.Country.Race == "soviet")
                if (!HasAdequateItem("afld", 1)) { return "afld"; }
            if (AI.p.Country.Race == "allies")
                if (!HasAdequateItem("hpad", 1)) { return "hpad"; }

            if (AI.p.Country.Race == "soviet")
                if (!HasAdequateItem("afld", 2)) { return "afld"; }
            if (AI.p.Country.Race == "allies")
                if (!HasAdequateItem("hpad", 2)) { return "hpad"; }

            if (AI.p.Country.Race == "soviet")
                if (!HasAdequateItem("spen", 1) && (ChooseBuildLocation("spen") !=  null) ) { return "spen"; }
            if (AI.p.Country.Race == "allies")
                if (!HasAdequateItem("syrd", 1) && (ChooseBuildLocation("syrd") != null)) { return "syrd"; }

            if (!HasAdequateItem("weap", 3)) { return "weap"; }

            if (!HasAdequateItem("apwr", 2)) { return "apwr"; }

            if (AI.p.Country.Race == "soviet")
                if (!HasAdequateItem("stek", 1)) { return "stek"; }
            if (AI.p.Country.Race == "allies")
                if (!HasAdequateItem("atek", 1)) { return "atek"; }

            if (!HasAdequateItem("apwr", 3)) { return "apwr"; }

            if (!HasAdequateItem("proc", 2)) { return "proc"; }

            return item;
        }
    }
}