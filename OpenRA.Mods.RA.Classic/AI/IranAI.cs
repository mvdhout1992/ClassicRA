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
using OpenRA.Mods.RA.Classic.Activities;

namespace OpenRA.Mods.RA.Classic.AI
{
    class IranAIInfo : IBotInfo, ITraitInfo
    {
        public readonly string Name = "Iran's AI";

        string IBotInfo.Name { get { return this.Name; } }
        public object Create(ActorInitializer init) { return new IranAI(this); }
    }

    class IranAI : ITick, IBot, INotifyDamage, INotifyProduction
    {
        public Player p;
        public World world;

        public IranAIInfo Info;
        IBotInfo IBot.Info { get { return this.Info; } }

        public SupportPowerManager SpecialPowers; // Atom bomb, Spy Plane, Paradrop etc
        public PowerManager Power; // Power
        public PlayerResources Resources; // Amount of cash

        public SquadManager squadmanager;

        public CPos BaseCenter; // Center of the base, for base builder(s)
        public CPos DefenseCenter; // Center where new base defenses should try to build around

        public int ticks; // Ticks that have expired since match start
        bool GameStarted; // Ticks() gets called before the match is started, so check

        public XRandom random;

        List<IAIBuilder> Builders;

        public IranAI(IranAIInfo Info)
        {
            this.Info = Info;
        }

        public void Activate(Player p)
        {
            this.p = p;
            this.world = p.World;
            GameStarted = true;

            random = new XRandom((int)p.PlayerActor.ActorID);

            SpecialPowers = p.PlayerActor.Trait<SupportPowerManager>();
            Power = p.PlayerActor.Trait<PowerManager>();
            Resources = p.PlayerActor.Trait<PlayerResources>();

            squadmanager = new SquadManager(this);

            // Initialize builders
            Builders = new List<IAIBuilder>() { new BaseBuilder(this), new DefenseBuilder(this),
                new InfantryBuilder(this), new VehicleBuilder(this),
                new AircraftBuilder(this), new ShipBuilder(this) };

            // Have the bot cheat, gets free 500 000 credits at the start of the match
            Resources.GiveCash(500000);
        }

        public void Tick(Actor self)
        {
            if (GameStarted == false) return;

            ticks++;

            if (ticks == 1)
            {
                DeployMCV(self);
                Debug("IranAI Loaded.");
            }

            if (ticks % 60 == 0) // Update rally points now and then
            {
                SetRallyPointsForNewProductionBuildings(self);
                SellDefenseIfLowPower();
            }

            foreach (var b in Builders)
                b.Tick();

            squadmanager.Tick();
        }

        void SellDefenseIfLowPower()
        {
            if (Power.PowerProvided < Power.PowerDrained)
            {
                var defenses = world.ActorsWithTrait<Building>()
                    .Where(a => a.Actor.Owner == p && (a.Actor.Info.Name == "sam"
                        || a.Actor.Info.Name == "agun" || a.Actor.Info.Name == "tsla"
                        || a.Actor.Info.Name == "gun"));

                var defense = defenses.FirstOrDefault().Actor;
                if (defense == null) return;

                Debug("low power, attempting to sell a base defense");
                world.IssueOrder(new Order("Sell", defense, false));
            }
        }

        void DeployMCV(Actor self)
        {
            // find our mcv and deploy it
            var mcv = self.World.Actors
                .FirstOrDefault(a => a.Owner == p && a.HasTrait<BaseBuilding>());

            if (mcv != null)
            {
                BaseCenter = DefenseCenter = mcv.Location;
                //Don't transform the mcv if it is a fact
                if (mcv.HasTrait<Mobile>())
                {
                    self.World.IssueOrder(new Order("DeployTransform", mcv, false));
                }
            }
            else
                Debug("Can't find BaseBuildUnit.");
        }

        public void Debug(string s, params object[] args)
        {
            //           if (Game.Settings.Debug.BotDebug)
            Game.Debug(String.Format("Bot {0}: {1}", this.p.ClientIndex, String.Format(s, args)));
        }

        public Actor ChooseEnemyTarget(string type)
        {
            var liveEnemies = world.Players
                .Where(q => p != q && p.Stances[q] == Stance.Enemy)
                .Where(q => p.WinState == WinState.Undefined && q.WinState == WinState.Undefined);

            if (!liveEnemies.Any())
                return null;

            var leastLikedEnemies = liveEnemies.Random(random);
            while (leastLikedEnemies.ClientIndex == 0)
            {
                leastLikedEnemies = liveEnemies.Random(random);
            }

            //            Debug("leastLikedEnemies ID = {0}", leastLikedEnemies.ClientIndex);

            Player enemy = null;
            if (leastLikedEnemies == null)
                enemy = liveEnemies.FirstOrDefault();

            enemy = leastLikedEnemies;
            if (enemy == null) Debug("Enemy is null");

            /* pick something worth attacking owned by that player */
            List<Actor> targets = null;

            switch (type)
            {
                case "base":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && a.Info.Name == "fact").ToList();
                    break;

                case "nuke":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && a.HasTrait<RepairableBuilding>()).ToList();
                    break;

                case "air":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && !(a.HasTrait<Helicopter>() || a.HasTrait<Plane>())).ToList();
                    break;

                case "sub":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && (a.Info.Name.Equals("ca") || a.Info.Name.Equals("dd") || a.Info.Name.Equals("msub") || a.Info.Name.Equals("pt") || a.Info.Name.Equals("spen") || a.Info.Name.Equals("syrd"))).ToList();
                    break;

                case "long":
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed).ToList();
                    break;

                default:
                    targets = world.Actors.Where(a => a.Owner == enemy && a.HasTrait<IHasLocation>() && !a.Destroyed && !a.HasTrait<RepairableBuilding>() && !(a.Info.Name.Equals("ca") || a.Info.Name.Equals("dd") || a.Info.Name.Equals("msub") || a.Info.Name.Equals("pt"))).ToList();
                    break;
            }
            Actor target = null;

            if (targets.Any() && BaseCenter != null)
                target = targets.ClosestTo(BaseCenter.ToPPos());

            return target;
        }

        bool IsRallyPointValid(CPos x)
        {
            // this is actually WRONG as soon as BetaAI is building units with a variety of
            // movement capabilities. (has always been wrong)
            return world.IsCellBuildable(x, Rules.Info["silo"].Traits.Get<BuildingInfo>());
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
                possibleRallyPoints = world.FindTilesInCircle(startPos, 16).Where(a => world.GetTerrainType(new CPos(a.X, a.Y)).Equals("Water")).ToList();
            else
                possibleRallyPoints = world.FindTilesInCircle(startPos, 8).Where(IsRallyPointValid).ToList();

            if (possibleRallyPoints.Count == 0)
                return startPos;

            return possibleRallyPoints.Random(random);
        }

        // Repair our buildings and set DefenseCenter position etc
        public void Damaged(Actor self, AttackInfo e)
        {
            if (!GameStarted) return;

            squadmanager.Damaged(self, e);

            if (e.Attacker.Destroyed || !e.Attacker.HasTrait<ITargetable>()
                 || e.Attacker.IsDead() || !e.Attacker.HasTrait<IHasLocation>()) return;

            if (self.HasTrait<RepairableBuilding>() && self.Trait<RepairableBuilding>().Repairer == null)
            {
                DefenseCenter = e.Attacker.CenterLocation.ToCPos();
                self.Trait<RepairableBuilding>().RepairBuilding(self, p);

                if (e.DamageState > DamageState.Light && e.PreviousDamageState <= DamageState.Light)
                {
                    if (!e.Attacker.Destroyed && !e.Attacker.IsDead() && e.Attacker.HasTrait<IHasLocation>())
                    {
                        //                        defenseCenter = e.Attacker.CenterLocation.ToCPos(); /* may be used for counter attack */
                        //                       foreach (Squad squad in squads.Where(a => !a.isFull() && a.units.Count > 0))
                        //                          squad.Move(defenseCenter, true, 4);
                    }
                    //                    repairTarget = self; /* may be used by engineers */
//                    world.IssueOrder(new Order("RepairBuilding", self.Owner.PlayerActor, false) { TargetActor = self });
                }
            }
        }
        public void UnitProduced(Actor self, Actor other, CPos exit)
        {
            squadmanager.UnitProduced(self, other, exit);
        }

        public bool AllHarvesterAssigned()
        {
            int squadscount = squadmanager.squads.OfType<HarvesterDefenseSquad>().Count();
            int harvestercount = world.Actors.Where(a => a.HasTrait<Harvester>() && a.Owner == p).Count();

            if (squadscount >= harvestercount)
                return true;

            return false;
        }
    }
}
