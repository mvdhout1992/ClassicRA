#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

/*using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Mods.RA.Classic.Buildings;
using OpenRA.Mods.RA.Classic.Move;
using OpenRA.Traits;
using XRandom = OpenRA.Thirdparty.Random;
using System.Collections;
using OpenRA.Mods.RA.Classic.Air;

//

// BetaAI
// Contributors: JamesDunne, Earthpig, Mart0258, Mailaender, Chrisforbes, Valkirie
// 


namespace OpenRA.Mods.RA.Classic.AI
{
    class BetaAIInfo : IBotInfo, ITraitInfo
    {
        public readonly string Name = "Unnamed Bot";
        public readonly int AssignRolesInterval = 20;

        public readonly string[] UnitQueues = { "Vehicle", "Infantry", "Plane", "Ship" };
        public readonly Dictionary<string, string> generality = new Dictionary<string, string>()
        {
            {"weap", "Vehicle"},
            {"tent", "Infantry"},
            {"barr", "Infantry"},
            {"spen", "Ship"},
            {"syrd", "Ship"},
            {"dome", "Plane"},
            {"hpad", "Plane"},
            {"afld", "Plane"}
        };

        public readonly bool ShouldRepairBuildings = true;

        string IBotInfo.Name { get { return this.Name; } }

        [FieldLoader.LoadUsing("LoadUnits")]
        public readonly Dictionary<string, float> UnitsToBuild = null;

        [FieldLoader.LoadUsing("LoadBuildings")]
        public readonly Dictionary<string, float> BuildingFractions = null;

        [FieldLoader.LoadUsing("LoadSquadSize")]
        public readonly Dictionary<string, int> SquadSize = null;

        [FieldLoader.LoadUsing("LoadBuildingLimits")]
        public readonly Dictionary<string, int> BuildingLimits = null;

        [FieldLoader.LoadUsing("LoadTweaks")]
        public readonly Dictionary<string, float> Tweaks = null;

        [FieldLoader.LoadUsing("LoadAffinities")]
        public Dictionary<string, string[]> Affinities = null;

        [FieldLoader.LoadUsing("LoadTicketsLimits")]
        public readonly Dictionary<string, int> TicketsLimits = null;

        static object LoadActorList(MiniYaml y, string field)
        {
            return y.NodesDict[field].Nodes.ToDictionary(
                t => t.Key,
                t => FieldLoader.GetValue<float>(field, t.Value.Value));
        }

        static object LoadOtherList(MiniYaml y, string field)
        {
            return y.NodesDict[field].Nodes.ToDictionary(
                t => t.Key,
                t => FieldLoader.GetValue<int>(field, t.Value.Value));
        }

        static object LoadListList(MiniYaml y, string field)
        {
            return y.NodesDict[field].Nodes.ToDictionary(
                t => t.Key,
                t => FieldLoader.GetValue<string[]>(field, t.Value.Value));
        }

        static object LoadAffinities(MiniYaml y) { return LoadListList(y, "Affinities"); }
        static object LoadBuildingLimits(MiniYaml y) { return LoadOtherList(y, "BuildingLimits"); }
        static object LoadTweaks(MiniYaml y) { return LoadActorList(y, "Tweaks"); }
        static object LoadSquadSize(MiniYaml y) { return LoadOtherList(y, "SquadSize"); }
        static object LoadUnits(MiniYaml y) { return LoadActorList(y, "UnitsToBuild"); }
        static object LoadBuildings(MiniYaml y) { return LoadActorList(y, "BuildingFractions"); }
        static object LoadTicketsLimits(MiniYaml y) { return LoadOtherList(y, "TicketsLimits"); }

        public object Create(ActorInitializer init) { return new BetaAI(this); }
    }

    class Enemy { public int Aggro; }



    class BetaAI : ITick, IBot, INotifyDamage
    {
        bool enabled;
        public int ticks;
        public int assignRolesTicks = 0;
        public Player p;
        PowerManager playerPower;
        SupportPowerManager playerSupport;
        PlayerResources playerResource;
        public readonly BuildingInfo rallypointTestBuilding;
        public BetaAIInfo Info;

        public Cache<Player, Enemy> aggro = new Cache<Player, Enemy>(_ => new Enemy());

        public CPos baseCenter;

        public XRandom random;
        public BaseBuilder[] builders;

        const int MaxBaseDistance = 40;
        public const int feedbackTime = 60;

        public string general;

        public World world { get { return p.PlayerActor.World; } }
        IBotInfo IBot.Info { get { return this.Info; } }

        public enum SquadTypes
        {
            Land = 0,
            Plane = 1,
            Ship = 2
        }

        public interface ISquad
        {
            void React();
            void Move(CPos target, bool queue, int dist);
            SquadTypes GetSquadType();
            List<Actor> GetSquadMembers();
        }

        List<ISquad> squads = new List<ISquad>();

        public BetaAI(BetaAIInfo Info)
        {
            this.Info = Info;
            this.rallypointTestBuilding = Rules.Info["silo"].Traits.Get<BuildingInfo>(); // so wrong
        }

        public void BotDebug(string s, params object[] args)
        {
//            if (Game.Settings.Debug.BotDebug)

            Game.Debug(String.Format("Bot {0}: {1}", this.p.ClientIndex, String.Format(s, args)));
        }

        public void Activate(Player p)
        {
            this.p = p;
            enabled = true;
            playerSupport = p.PlayerActor.Trait<SupportPowerManager>();
            playerPower = p.PlayerActor.Trait<PowerManager>();
            playerResource = p.PlayerActor.Trait<PlayerResources>();
            playerResource.GiveCash(500000);

            builders = new BaseBuilder[] {
                                new BaseBuilder( this, "Building", q => ChooseBuildingToBuild(q, true) ),
                                new BaseBuilder( this, "Defense", q => ChooseDefenseToBuild(q, false) ) };

            assignRolesTicks = Info.AssignRolesInterval;

            random = new XRandom((int)p.PlayerActor.ActorID);

            general = Info.UnitQueues[random.Next(0, Info.UnitQueues.Length - 2)];

            Game.Debug("test BotDebug");
//            BotChat(p.PlayerActor, false, "I am proudly using BetaAI.");
        }

        int GetPowerProvidedBy(ActorInfo building)
        {
            var bi = building.Traits.GetOrDefault<BuildingInfo>();
            if (bi == null) return 0;
            return bi.Power;
        }

        ActorInfo ChooseRandomUnitToBuild(ProductionQueue queue)
        {
            float value = 0.0F;

            var buildableThings = queue.BuildableItems();
            if (!buildableThings.Any()) return null;

            var myUnits = world.ActorsWithTrait<IMove>().Where(a => a.Actor.Owner == p).Select(a => a.Actor).ToList();
            foreach (var frac in Info.UnitsToBuild)
            {
                float tweak = (float)random.NextDouble() * Info.Tweaks["rand_u"];
                if (Info.generality.ContainsKey(frac.Key) && Info.generality[frac.Key] == general)
                    value = tweak;
                else if (Info.generality.ContainsKey(frac.Key))
                    value = -tweak;
                else
                    value = 0.0F;

                if (buildableThings.Any(b => b.Name == frac.Key))
                    if (myUnits.Count(a => a.Info.Name == frac.Key) < (frac.Value + value) * myUnits.Count())
                        return buildableThings.Where(b => b.Name == frac.Key).FirstOrDefault();
            }
            return null;
        }

        int countBuilding(string frac, Player owner)
        {
            return world.ActorsWithTrait<Building>().Where(a => a.Actor.Owner == owner && a.Actor.Info.Name == frac).Count();
        }

        public bool HasAdequateNumber(string frac, Player owner)
        {
            int count = countBuilding(frac, owner);

            if (frac == "fix" && count >= Info.BuildingLimits[frac] * countBuilding("weap", owner))
                return false;
            if (frac == "hpad" && count > 0)
                if (count != world.ActorsWithTrait<Helicopter>().Where(a => a.Actor.Owner == owner).Count())
                    return false;
            if (frac == "afld" && count > 0)
                if (count != world.ActorsWithTrait<Plane>().Where(a => a.Actor.Owner == owner).Count())
                    return false;

            if (Info.BuildingLimits.ContainsKey(frac))
                if (count < Info.BuildingLimits[frac] && ticks >= Info.TicketsLimits["iteration" + count])
                    return true;
                else
                    return false;

            return true;
        }

        bool shouldSell(Actor a, int money)
        {
            var h = a.TraitOrDefault<Health>();
            var si = a.Info.Traits.Get<SellableInfo>();
            var cost = a.GetSellValue();
            var refund = (cost * si.RefundPercent * (h == null ? 1 : h.HP)) / (100 * (h == null ? 1 : h.MaxHP));

            if (playerResource.Cash >= money)
                return false;
            return true;
        }

        public bool HasAdequateProc()
        {
            if (countBuilding("proc", p) == 0 && (countBuilding("powr", p) > 0 || countBuilding("apwr", p) > 0))
                return false;
            return true;
        }

        public bool HasAdequateFact()
        {
            if (countBuilding("fact", p) == 0 && countBuilding("weap", p) > 0)
                return false;
            return true;
        }

        void BuildRandom(string category)
        {
            if (!HasAdequateProc()) // Stop building until economy is back on
                return;

            var queue = FindQueues(category).FirstOrDefault(q => q.CurrentItem() == null);
            if (queue == null)
                return;

            var unit = ChooseRandomUnitToBuild(queue);

            // should work
            int t = countBuilding("proc", p);
            var u = world.Actors.Where(a => a.Owner == p && a.Info.Name == "harv").Count();
            if (t > u)
                unit = Rules.Info["harv"];

            if (!HasAdequateFact())
                unit = Rules.Info["mcv"];

            if (unit != null)
            {
                if (unit.Name == "mig" || unit.Name == "yak")
                {
                    int count = countBuilding("afld", p);
                    var myUnits_plane = world.ActorsWithTrait<Plane>().Where(a => a.Actor.Owner == p).Count();
                    if (myUnits_plane >= count)
                        return;
                }
                else if (unit.Name == "hind" || unit.Name == "heli" || unit.Name == "tran")
                {
                    int count = countBuilding("hpad", p);
                    var myUnits_copter = world.ActorsWithTrait<Helicopter>().Where(a => a.Actor.Owner == p).Count();
                    if (myUnits_copter >= count)
                        return;
                }
                else if (unit.Name == "harv")
                {
                    var myUnits_harv = world.ActorsWithTrait<Harvester>().Where(a => a.Actor.Owner == p).Count();
                    int count = countBuilding("proc", p);
                    if (myUnits_harv >= count * 2)
                        return;
                }
                else if (unit.Name == "ss")
                {
                    var enemyUnits = world.ActorsWithTrait<Mobile>().Where(a => a.Actor.Owner != p && p.Stances[a.Actor.Owner] == Stance.Enemy);
                    var enemyUnits_ship = enemyUnits.Where(a => a.Actor.Info.Name.Equals("spen") || a.Actor.Info.Name.Equals("syrd")).Count();
                    if (enemyUnits_ship == 0)
                        return;
                }
                world.IssueOrder(Order.StartProduction(queue.self, unit.Name, 1));
            }
        }

        public void HandleSuperpowers(Actor self)
        {
            foreach (var pow in p.PlayerActor.Trait<SupportPowerManager>().Powers)
            {
                switch (pow.Key)
                {
                    case "NukePowerInfoOrder":
                        Actor nuke = world.Actors.Where(a => a.HasTrait<NukePower>() && a.Owner == p).FirstOrDefault();
                        if (nuke == null)
                            break;
                        var timer_n = pow.Value.RemainingTime;
                        var weapon_n = nuke.Trait<NukePower>();
                        if (timer_n == 0)
                        {
                            weapon_n.Activate(nuke, new Order() { TargetLocation = ChooseEnemyTarget("nuke").CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_n.Info.ChargeTime * 25;
                        }
                        break;
                    case "GpsPowerInfoOrder": // useless
                        Actor gps = world.Actors.Where(a => a.HasTrait<GpsPower>() && a.Owner == p).FirstOrDefault();
                        if (gps == null)
                            break;
                        var timer_g = pow.Value.RemainingTime;
                        var weapon_g = gps.Trait<GpsPower>();
                        if (timer_g == 0)
                        {
                            weapon_g.Activate(gps, new Order());
                            pow.Value.RemainingTime = weapon_g.Info.ChargeTime * 25;
                        }
                        break;
                    case "AirstrikePowerInfoOrder":
                        Actor airstrike = world.Actors.Where(a => a.HasTrait<AirstrikePower>() && a.Owner == p).FirstOrDefault();
                        if (airstrike == null)
                            break;
                        var timer_a = pow.Value.RemainingTime;
                        var weapon_a = airstrike.Trait<AirstrikePower>();
                        if (timer_a == 0)
                        {
                            weapon_a.Activate(airstrike, new Order() { TargetLocation = ChooseEnemyTarget(null).CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_a.Info.ChargeTime * 25;
                        }
                        break;
                    case "ParatroopersPowerInfoOrder":
                        Actor para = world.Actors.Where(a => a.HasTrait<ParatroopersPower>() && a.Owner == p).FirstOrDefault();
                        if (para == null)
                            break;
                        var timer_p = pow.Value.RemainingTime;
                        var weapon_p = para.Trait<ParatroopersPower>();
                        if (timer_p == 0)
                        {
                            weapon_p.Activate(para, new Order() { TargetLocation = ChooseEnemyTarget(null).CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_p.Info.ChargeTime * 25;
                        }
                        break;
                    case "SpyPlanePowerInfoOrder":
                        Actor spy = world.Actors.Where(a => a.HasTrait<SpyPlanePower>() && a.Owner == p).FirstOrDefault();
                        if (spy == null)
                            break;
                        var timer_s = pow.Value.RemainingTime;
                        var weapon_s = spy.Trait<SpyPlanePower>();
                        if (timer_s == 0)
                        {
                            weapon_s.Activate(spy, new Order() { TargetLocation = ChooseEnemyTarget("nuke").CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_s.Info.ChargeTime * 25;
                        }
                        break;
                    case "IronCurtainPowerInfoOrder":
                        Actor iron = world.Actors.Where(a => a.HasTrait<IronCurtainPower>() && a.Owner == p).FirstOrDefault();
                        if (iron == null)
                            break;
                        var timer_i = pow.Value.RemainingTime;
                        var weapon_i = iron.Trait<IronCurtainPower>();
                        if (timer_i == 0)
                        {
                            Squad actorsi = squads.Where(a => a.IsFull()).Random(random);

                            if (actorsi == null)
                                break;

                            Actor actori = actorsi.units.FirstOrDefault();

                            if (actori == null)
                                break;

                            weapon_i.Activate(iron, new Order() { TargetLocation = actori.CenterLocation.ToCPos() });
                            pow.Value.RemainingTime = weapon_i.Info.ChargeTime * 25;
                        }
                        break;
                    case "ChronoshiftPowerInfoOrder": // :'(
                        Actor chrono = world.Actors.Where(a => a.HasTrait<ChronoshiftPower>() && a.Owner == p).FirstOrDefault();
                        if (chrono == null)
                            break;
                        var timer_c = pow.Value.RemainingTime;
                        var weapon_c = chrono.Trait<ChronoshiftPower>();
                        if (timer_c == 0)
                        {
                            Squad actors = null;

                            foreach (var squad in squads.Where(a => a.type == "Assault" && a.IsFull()))
                                actors = squad;

                            if (actors == null)
                                break;

                            var target_c = ChooseEnemyTarget("nuke");

                            List<Pair<Actor, CPos>> location = new List<Pair<Actor, CPos>>();

                            var bi = Rules.Info["silo"].Traits.Get<BuildingInfo>();

                            foreach (var unit in actors.units)
                            {
                                for (var k = 0; k < 10; k++)
                                    foreach (var t in world.FindTilesInCircle(target_c.CenterLocation.ToCPos(), k))
                                        if (NoBuildingsUnder(Util.ExpandFootprint(FootprintUtils.Tiles("silo", bi, t), false)))
                                        {
                                            location.Add(new Pair<Actor, CPos>(unit, t));
                                            world.IssueOrder(new Order("Attack", unit, false) { TargetActor = target_c });
                                        }
                            }

                            Scripting.RASpecialPowers.Chronoshift(self.World, location, chrono, -1, false);
                            pow.Value.RemainingTime = weapon_c.Info.ChargeTime * 25;
                        }
                        break;
                }
            }
        }

        public void BotChat(Actor self, bool team, string str)
        {
            var t = new Order(team ? "TeamChat" : "Chat", self, false) { IsImmediate = false, TargetString = str };
            self.World.IssueOrder(t);
        }

        public void Tick(Actor self)
        {

            // 9000 ticks = 6min
            if (!enabled)
                return;

            ticks++;

            if (ticks == 1)
                DeployMcv(self);

            if (ticks % feedbackTime == 0)
                foreach (var q in Info.UnitQueues)
                    BuildRandom(q);

            AssignRolesToUnits(self);
            SetRallyPointsForNewProductionBuildings(self);
            HandleSuperpowers(self);

            foreach (var b in builders)
                b.Tick();
        }

        Actor attackTarget;
        CPos attackTargetLocation;
        Actor repairTarget;

        bool IsRallyPointValid(CPos x)
        {
            // this is actually WRONG as soon as BetaAI is building units with a variety of
            // movement capabilities. (has always been wrong)
            return world.IsCellBuildable(x, rallypointTestBuilding);
        }

        void SetRallyPointsForNewProductionBuildings(Actor self)
        {
            var buildings = world.ActorsWithTrait<RallyPoint>()
                .Where(rp => rp.Actor.Owner == p &&
                    !IsRallyPointValid(rp.Trait.rallyPoint)).ToArray();

            foreach (var a in buildings)
            {
                CPos newRallyPoint = ChooseRallyLocationNear(self, a.Actor.Location);
                world.IssueOrder(new Order("SetRallyPoint", a.Actor, false) { TargetLocation = newRallyPoint });
            }
        }

        CPos ChooseRallyLocationNear(Actor self, CPos startPos)
        {
            List<CPos> possibleRallyPoints = new List<CPos>();

            if (self.Info.Name.Equals("spen") || self.Info.Name.Equals("syrd"))
                possibleRallyPoints = world.FindTilesInCircle(startPos, 8).Where(a => world.GetTerrainType(new CPos(a.X, a.Y)).Equals("Water")).ToList();
            else
                possibleRallyPoints = world.FindTilesInCircle(startPos, 8).Where(IsRallyPointValid).ToList();

            if (possibleRallyPoints.Count == 0)
                return startPos;

            return possibleRallyPoints.Random(random);
        }

        void DeployMcv(Actor self)
        {
            // find our mcv and deploy it
            var mcv = self.World.Actors
                .FirstOrDefault(a => a.Owner == p && a.HasTrait<BaseBuilding>());

            if (mcv != null)
            {
                baseCenter = mcv.Location;
                defenseCenter = baseCenter;
                //Don't transform the mcv if it is a fact
                if (mcv.HasTrait<Mobile>())
                {
                    world.IssueOrder(new Order("DeployTransform", mcv, false));
                }
            }
            else
                BotDebug("AI: Can't find BaseBuildUnit.");
        }

        internal IEnumerable<ProductionQueue> FindQueues(string category)
        {
            return world.ActorsWithTrait<ProductionQueue>()
                .Where(a => a.Actor.Owner == p && a.Trait.Info.Type == category)
                .Select(a => a.Trait);
        }

        public void Damaged(Actor self, AttackInfo e)
        {
            if (!enabled) return;
            if (e.Attacker.Destroyed) return;
            if (!e.Attacker.HasTrait<ITargetable>()) return;

            if (Info.ShouldRepairBuildings && self.HasTrait<RepairableBuilding>())
            {
                if (e.DamageState > DamageState.Light && e.PreviousDamageState <= DamageState.Light)
                {
                    if (e.Attacker.HasTrait<IHasLocation>())
                    {
                        defenseCenter = e.Attacker.CenterLocation.ToCPos(); // may be used for counter attack
                        foreach (Squad squad in squads.Where(a => !a.IsFull() && a.units.Count > 0))
                            squad.Move(defenseCenter, true, 4);
                    }
                    repairTarget = self; // may be used by engineers
                    world.IssueOrder(new Order("RepairBuilding", self.Owner.PlayerActor, false) { TargetActor = self });
                }
            }

            if (e.Attacker != null && e.Damage > 0 && p.Stances[e.Attacker.Owner] == Stance.Enemy)
                aggro[e.Attacker.Owner].Aggro += e.Damage;
        }
    }
} */