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
using OpenRA.Traits;
using OpenRA.Mods.RA.Classic.Activities;
using System.Threading;

/*
namespace OpenRA.Mods.RA.Classic.AI
{
    class BaseBuilder
    {
        enum BuildState { ChooseItem, WaitForProduction, WaitForFeedback }

        BuildState state = BuildState.WaitForFeedback;
        string category;
        BetaAI ai;
        int lastThinkTick;
        Func<ProductionQueue, ActorInfo> chooseItem;

        public BaseBuilder(BetaAI ai, string category, Func<ProductionQueue, ActorInfo> chooseItem)
        {
            this.ai = ai;
            this.category = category;
            this.chooseItem = chooseItem;
        }

        public void Tick()
        {
            // Pick a free queue
            var queue = ai.FindQueues(category).FirstOrDefault();
            if (queue == null)
                return;

            var currentBuilding = queue.CurrentItem();
            switch (state)
            {
                case BuildState.ChooseItem:
                    {
                        var item = chooseItem(queue);
                        if (item == null)
                        {
                            state = BuildState.WaitForFeedback;
                            lastThinkTick = ai.ticks;
                        }
                        else
                        {
                            if (ai.HasAdequateNumber(item.Name, ai.p)) // C'mon...
                            {
                                state = BuildState.WaitForProduction;
                                ai.world.IssueOrder(Order.StartProduction(queue.self, item.Name, 1));
                            }
                        }
                    }
                    break;

                case BuildState.WaitForProduction:
                    if (currentBuilding == null) return;	// let it happen..

                    else if (currentBuilding.Paused)
                        ai.world.IssueOrder(Order.PauseProduction(queue.self, currentBuilding.Item, false));
                    else if (currentBuilding.Done)
                    {
                        state = BuildState.WaitForFeedback;
                        lastThinkTick = ai.ticks;

                        // place the building
                        string type = "";
                        if (currentBuilding.Item.Equals("sam") || currentBuilding.Item.Equals("agun") || currentBuilding.Item.Equals("ftur") || currentBuilding.Item.Equals("tsla") || currentBuilding.Item.Equals("gun") || currentBuilding.Item.Contains("hbox") || currentBuilding.Item.Contains("pbox"))
                            type = "defense";
                        else if (currentBuilding.Item.Equals("proc"))
                            type = "resource";
                        CPos? location = ai.ChooseBuildLocation(currentBuilding.Item, type);

                        if (location == null) // C'mon...
                        {
                            this.ai.BotDebug("AI: Nowhere to place or no adequate number {0}".F(currentBuilding.Item));
                            ai.world.IssueOrder(Order.CancelProduction(queue.self, currentBuilding.Item, 1));
                        }
                        else
                            ai.world.IssueOrder(new Order("PlaceBuilding", ai.p.PlayerActor, false)
                            {
                                TargetLocation = location.Value,
                                TargetString = currentBuilding.Item
                            });
                    }

                    if (!ai.HasAdequateNumber(currentBuilding.Item, ai.p))
                        ai.world.IssueOrder(Order.CancelProduction(queue.self, currentBuilding.Item, 1));

                    break;

                case BuildState.WaitForFeedback:
                    if (ai.ticks - lastThinkTick > BetaAI.feedbackTime)
                        state = BuildState.ChooseItem;
                    break;
            }
        }
        ActorInfo ChooseBuildingToBuild(ProductionQueue queue, bool buildPower)
        {
            float value = 0.0F;

            var buildableThings = queue.BuildableItems();

            if (!HasAdequatePower())    // try to maintain 20% excess power
            {
                if (!buildPower) return null;

                // find the best thing we can build which produces power
                return buildableThings.Where(a => GetPowerProvidedBy(a) > 0)
                    .OrderByDescending(a => GetPowerProvidedBy(a)).FirstOrDefault();
            }

            //            if (playerResource.AlertSilo)
            //               return Rules.Info["silo"]; // Force silo construction on Alert
            if (!HasAdequateProc())
                return Rules.Info["proc"];

            var myBuildings = world.ActorsWithTrait<Building>().Where(a => a.Actor.Owner == p).ToArray();
            float r = 0.0F;
            string ret = "";

            foreach (var frac in Info.BuildingFractions)
            {
                float tweak = (float)random.NextDouble() * Info.Tweaks["rand_b"];
                if (Info.generality.ContainsKey(frac.Key) && Info.generality[frac.Key] == general)
                    value = tweak;
                else if (Info.generality.ContainsKey(frac.Key))
                    value = -tweak;
                else
                    value = 0.0F;

                if (buildableThings.Any(b => b.Name == frac.Key))
                    if (playerPower.ExcessPower >= Rules.Info[frac.Key].Traits.Get<BuildingInfo>().Power)
                        if (HasAdequateNumber(frac.Key, p)) // C'mon...
                        {
                            float r2 = (float)random.NextDouble() * (frac.Value + value) * 100;
                            if (r2 > r)
                            {
                                r = r2;
                                ret = frac.Key;
                            }
                        }
            }

            if (Rules.Info.ContainsKey(ret))
                return Rules.Info[ret];

            return null;
        }
        ActorInfo ChooseDefenseToBuild(ProductionQueue queue, bool buildPower)
        {
            float value = 0.0F;

            var buildableThings = queue.BuildableItems();

            if (!HasAdequatePower())    // try to maintain 20% excess power
            {
                if (!buildPower) return null;

                // find the best thing we can build which produces power
                return buildableThings.Where(a => GetPowerProvidedBy(a) > 0)
                    .OrderByDescending(a => GetPowerProvidedBy(a)).FirstOrDefault();
            }

            var myBuildings = world.ActorsWithTrait<Building>().Where(a => a.Actor.Owner == p).ToArray();
            foreach (var frac in Info.BuildingFractions)
            {
                float tweak = (float)random.NextDouble() * Info.Tweaks["rand_b"];
                if (Info.generality.ContainsKey(frac.Key) && Info.generality[frac.Key] == general)
                    value = tweak;
                else if (Info.generality.ContainsKey(frac.Key))
                    value = -tweak;
                else
                    value = 0.0F;

                if (buildableThings.Any(b => b.Name == frac.Key))
                    if (myBuildings.Count(a => a.Actor.Info.Name == frac.Key) < (frac.Value + value) * myBuildings.Length && playerPower.ExcessPower >= Rules.Info[frac.Key].Traits.Get<BuildingInfo>().Power)
                        if (HasAdequateNumber(frac.Key, p)) // C'mon...
                            return Rules.Info[frac.Key];
            }

            return null;
        }


        bool NoBuildingsUnder(IEnumerable<CPos> cells)
        {
            var bi = world.WorldActor.Trait<BuildingInfluence>();
            return cells.All(c => bi.GetBuildingAt(c) == null);
        }

        // AI improvement, should reduce lag
        List<string> tried = new List<string>();
        bool firstbuild = true;
        CPos defenseCenter;

        public CPos? ChooseBuildLocation(string actorType, string type)
        {
            if (tried.Contains(Rules.Info[actorType].Name))
                return null;

            var bi = Rules.Info[actorType].Traits.Get<BuildingInfo>();

            if (bi == null)
                return null;

            if (type == "defense")
            {
                Actor owner = ChooseEnemyTarget("base");
                for (var k = MaxBaseDistance; k >= 0; k--)
                {
                    if (owner != null)
                    {
                        var tlist = world.FindTilesInCircle(defenseCenter, k).OrderBy(a => (new PPos(a.ToPPos().X, a.ToPPos().Y) - owner.CenterLocation).LengthSquared);
                        foreach (var t in tlist)
                            if (world.CanPlaceBuilding(actorType, bi, t, null))
                                if (bi.IsCloseEnoughToBase(world, p, actorType, t))
                                    if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(actorType, bi, t), false)))
                                        return t;
                    }
                }
            }
            else if (type == "resource") // dirty piece of shit
            {
                var owner = world.FindTilesInCircle(baseCenter, MaxBaseDistance).Where(a => world.GetTerrainType(new CPos(a.X, a.Y)) == "Ore" || world.GetTerrainType(new CPos(a.X, a.Y)) == "Gems").First();
                for (var k = MaxBaseDistance; k >= 0; k--)
                {
                    if (owner != null)
                    {
                        var tlist = world.FindTilesInCircle(defenseCenter, k).OrderBy(a => (new PPos(a.ToPPos().X, a.ToPPos().Y) - new CPos(owner.X, owner.Y).ToPPos()).LengthSquared);
                        foreach (var t in tlist)
                            if (world.CanPlaceBuilding(actorType, bi, t, null))
                                if (bi.IsCloseEnoughToBase(world, p, actorType, t))
                                    if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(actorType, bi, t), false)))
                                        return t;
                    }
                }
            }
            else
            {
                for (var k = 0; k < MaxBaseDistance; k++)
                    foreach (var t in world.FindTilesInCircle(baseCenter, k))
                        if (world.CanPlaceBuilding(actorType, bi, t, null))
                            if (bi.IsCloseEnoughToBase(world, p, actorType, t) || firstbuild)
                                if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles(actorType, bi, t), false)))
                                {
                                    firstbuild = false;
                                    return t;
                                }
            }

            if (actorType == "syrd" || actorType == "spen")
                tried.Add(Rules.Info[actorType].Name);

            return null;
        }

        bool HasAdequatePower()
        {
            return playerPower.PowerProvided > 50 &&
                playerPower.PowerProvided > playerPower.PowerDrained * 1.2;
        }
    }
} */