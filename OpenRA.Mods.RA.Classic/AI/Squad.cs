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
    public enum SquadType
    {
        Land,
        Tank,
        Air,
        Ship,
    }

    public enum SquadRole
    {
        AttackBase = 0,
        DefendBase,
        DefendHarvesters,
        Other = -1
    }

    public interface ISquad
    {
//       void React();
//        void Move(CPos target, bool queue, int dist);
        SquadType GetSquadType();
        SquadRole GetSquadRole();
        void SetSquadRole(SquadRole squadrole);
        List<Actor> GetSquadMembers();
        bool IsReady();
        bool IsFull();
        void Tick();
        void OnBaseDamage(Actor building, AttackInfo e);
        void OnHarvesterDamage(Actor harvester, AttackInfo e);
        void Move(CPos location, bool queue, int dist);
        void Attack(IEnumerable<Actor> targets, bool queue);
        void Defend(CPos location, bool queue);
    }

    class Squad : ISquad
    {
        public SquadManager manager;
        IranAI AI;
        public World world;

        public List<Actor> members;
        public SquadType squadtype;
        public SquadRole squadrole;

        public bool EnemyNearby = false;
        public bool isready = false; // Once the squad is full, it's ready forever

        Actor Target = null;

        int MaxSquadSize = 8;
        const int BaseDefenseTime = 25 * 3; // FPS * sec, how long to stay in base defense role

        public Squad(IranAI AI, SquadManager manager, SquadType squadtype, SquadRole squadrole)
        {
            this.AI = AI;
            this.manager = manager;
            this.squadtype = squadtype;
            this.world = AI.world;
            this.squadrole = squadrole;
            members = new List<Actor>();
        }

        virtual public void Tick()
        {
            if (members.Count() == 0) return;

            // Check if enemy unit is nearby
            if (AI.ticks % 25 == 0)
            {
                Actor leader = members.First();
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
                        Target = AI.ChooseEnemyTarget("nuke");
                        CPos Location = Target.Location;
                        Move(Location, false, 8);
                    }
                }
                if (!IsReady()) // While we're not ready check if squad is full, if it is set us to ready
                {
                    isready = IsFull();
                }
                if ((squadrole == SquadRole.DefendBase) && (AI.ticks - manager.lastbasedamagetick > BaseDefenseTime))
                {
                    AI.Debug("Squad: No attack for {0} secs, switching to attack mode.", BaseDefenseTime / 25);

                    squadrole = SquadRole.AttackBase;
                }
                if (squadrole == SquadRole.DefendBase)
                {
//                    AI.Debug("Squad in DefendBase role");
                }
            }
        }

        public virtual void Move(CPos target, bool queue, int dist)
        {
            if (target == null || members.Count() == 0)
                return;
            Actor leader = members.First(); // units.ClosestTo(target.ToPPos());
            if (leader == null)
                return;
            var ownUnits = world.FindUnitsInCircle(leader.CenterLocation, Game.CellSize * dist).
                Where(a => a.Owner == members.FirstOrDefault().Owner && this.members.Contains(a)).ToList();

            if (ownUnits.Count < members.Count)
            {
                world.IssueOrder(new Order("Stop", leader, false));
                foreach (var unit in members.Where(a => !ownUnits.Contains(a)))
                    world.IssueOrder(new Order("Move", unit, false) { TargetLocation = leader.CenterLocation.ToCPos() });
            }
            else
            {
                world.IssueOrder(new Order("Move", leader, queue) { TargetLocation = target });
                foreach (Actor unit in members.Where(a => a != leader))
                    world.IssueOrder(new Order("Move", unit, queue) { TargetLocation = leader.CenterLocation.ToCPos() });
            }
        }

        // Gets called while Move() is being executed and there's an enemy in dist / 2 of the circle
        virtual public void OnEnemyUnitsNearby(IEnumerable<Actor> enemyunits)
        {
//            AI.Debug("Found enemy unit nearby, count = {0}.", enemyunits.Count());

            Attack(enemyunits, false);
        }

        public SquadType GetSquadType()
        {
            return squadtype;
        }

        public void SetMaxSquadSize(int size)
        {
            MaxSquadSize = size;
        }

        public SquadRole GetSquadRole()
        {
            return squadrole;
        }

        public void SetSquadRole(SquadRole squadrole)
        {
            this.squadrole = squadrole;
        }

        public List<Actor> GetSquadMembers()
        {
            return members;
        }

        public bool IsReady()
        {
            return isready;
        }

        public bool IsFull()
        {
            if (members.Count >= MaxSquadSize)
                return true;
            else
                return false;
        }
        virtual public void OnBaseDamage(Actor building, AttackInfo e)
        {
 //           if (manager.squads.Where(s => s.GetSquadRole() == SquadRole.DefendBase).Count() < 2)
            {
                AI.Debug("switching to DefendBase role, base under attack");
                squadrole = SquadRole.DefendBase;
//                Move(building.Location, false, 8);
                Attack(new[]{e.Attacker}, false);
            }
            if (squadrole == SquadRole.DefendBase)
            {
 //               Move(building.Location, false, 8);
                Attack(new[] { e.Attacker }, false);
            }
        }

        virtual public void OnHarvesterDamage(Actor harvester, AttackInfo e)
        {

        }

        virtual public void Attack(IEnumerable<Actor> targets, bool queue)
        {
             var firstenemy = targets.First();
             if (firstenemy == null || firstenemy.IsDead() || firstenemy.Destroyed) return;

            foreach (Actor unit in members)
            {
                if (unit.IsDead() || unit.Destroyed) continue;

                if (firstenemy.HasTrait<CrushableInfantry>() && manager.IsVehicle(unit) && unit.HasTrait<Mobile>()
                    && unit.TraitOrDefault<Mobile>().Info.Crushes.Contains("infantry"))
                    world.IssueOrder(new Order("Move", unit, false) { TargetLocation = firstenemy.CenterLocation.ToCPos() });

                    world.IssueOrder(new Order("Attack", unit, false) { TargetActor = firstenemy });
            }
        }

        virtual public void Defend(CPos location, bool queue)
        {

        }
    }
}