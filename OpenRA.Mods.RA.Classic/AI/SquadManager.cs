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
using OpenRA.Mods.RA.Classic.Render;


namespace OpenRA.Mods.RA.Classic.AI
{
    class SquadManager
    {
        IranAI AI;
        public World world;

        int feedbacktime = 60; // time to update feedback state, in ticks
        public int lastbasedamagetick;
        public int lastharvdamagetick;

        public List<ISquad> squads;

        public SquadManager(IranAI AI)
        {
            this.AI = AI;
            this.world = AI.world;
            this.squads = new List<ISquad>();
        }

        public void Tick()
        {
            if (AI.ticks % feedbacktime == 0)
            {
                CleanSquads();
                AssignRoleToFreeUnits();
            }

                foreach (ISquad squad in squads)
                    squad.Tick();
        }

        bool IsInSquad(Actor unit)
        {
            foreach (ISquad squad in squads)
                if (squad.GetSquadMembers().Contains(unit))
                    return true;
            return false;
        }

        // Remove dead units from squad
        public void CleanSquads()
        {
            squads.ForEach(S => S.GetSquadMembers().RemoveAll(a => a.Destroyed || a.IsDead()));

            foreach (ISquad squad in squads)
            {
                if (squad.GetSquadMembers().Count() == 0)
                {
//                    AI.Debug("Removing empty squad from squads list");
                    squads.Remove(squad);
                    break; // Need to break or the squads foreach enumaration will become invalid and crash the game
                }
            }
        }

        public void AddToSquad(Actor unit, SquadType squadtype)
        {                  
            // Check if there's a a not full land-type squad and join it
            // I should be able to do this without the double query..
            if (squads.Where(a => !a.IsReady() && a.GetSquadType() == squadtype).Any())
            { 
                squads.Where(a => !a.IsReady() && a.GetSquadType() == squadtype)
                    .FirstOrDefault().GetSquadMembers().Add(unit);
            }
            else // Otherwise create a new land-type squad, add our unit
            { // and add the squad to the squads list


                // Make sure we always have at least two defending squads
                ISquad newsquad = CreateSquad(squadtype);

                newsquad.GetSquadMembers().Add(unit);
                squads.Add(newsquad);
            }
        }

        ISquad CreateSquad(SquadType squadtype)
        {
/*            if (squads.Where(s => s.GetSquadRole() == SquadRole.DefendBase).Count() < 2)
                newsquad = new Squad(AI, this, squadtype, SquadRole.DefendBase);
            else */

        if (squadtype == SquadType.Tank)
            return new HarvesterDefenseSquad(AI, this, squadtype, SquadRole.AttackBase);
        else if (squadtype == SquadType.Air)
            return new AirSquad(AI, this, squadtype, SquadRole.AttackBase);
        else if (squadtype == SquadType.Land)
            return new Squad(AI, this, squadtype, SquadRole.AttackBase);
        else if (squadtype == SquadType.Ship)
            return new ShipSquad(AI, this, squadtype, SquadRole.AttackBase);

        return null; // Shouldn't happen
        }

        public void AssignRoleToFreeUnits()
        {
            var freeunits = world.ActorsWithTrait<IMove>()
                .Where(a => a.Actor.Owner == AI.p && !a.Actor.HasTrait<BaseBuilding>() 
                    && !IsInSquad(a.Actor)).Select(a => a.Actor).ToList();

            foreach (Actor unit in freeunits)
            {
                if (IsInfantry(unit))
                {
                    AddToSquad(unit, SquadType.Land);
                }
                else if (IsAircraft(unit))
                {
                    AddToSquad(unit, SquadType.Air);
                }
                else if (IsVehicle(unit))
                {
                    int random = AI.random.Next() % 3;

                    if (random == 0)
                        AddToSquad(unit, SquadType.Land);
                    else if ((random == 1 || random == 2) && !AI.AllHarvesterAssigned())
                        AddToSquad(unit, SquadType.Tank);
                }

                else if (IsShip(unit))
                {
                    AddToSquad(unit, SquadType.Ship);
                }
            }
        }

        public bool IsVehicle(Actor unit)
        {
            String name = unit.Info.Name;

            switch (name)
            {
                case "ttnk": return true;
                case "1tnk": return true;
                case "2tnk": return true;
                case "3tnk": return true;
                case "4tnk": return true;
                case "jeep": return true;
                case "apc": return true;
                case "v2rl": return true;
                case "arty": return true;
            }

            return false;
        }
        public bool IsAircraft(Actor unit)
        {
            String name = unit.Info.Name;

            switch (name)
            {
                case "yak": return true;
                case "heli": return true;
                case "hind": return true;
                case "mig": return true;
            }

            return false;
        }

        public bool IsShip(Actor unit)
        {
            String name = unit.Info.Name;

            switch (name)
            {
                case "dd": return true;
                case "ss": return true;
                case "pt": return true;
                case "ca": return true;
                case "msub": return true;
            }

            return false;
        }

// Check by RenderInfantry trait..not sure if this catches all infantry
        public bool IsInfantry(Actor unit)
        {
            return unit.HasTrait<RenderInfantry>();
 //           String name = unit.Info.Name;
        }

        public void OnBaseDamage(Actor building, AttackInfo e)
        {
            squads.ForEach(s => s.OnBaseDamage(building, e));
        }

        public void OnHarvesterDamage(Actor building, AttackInfo e)
        {
            squads.ForEach(s => s.OnHarvesterDamage(building, e));
        }

        public void UnitProduced(Actor self, Actor other, CPos exit)
        {
            return;
        }

        public void Damaged(Actor self, AttackInfo e)
        {
            if (e.Attacker.Destroyed || !e.Attacker.HasTrait<ITargetable>() 
                || e.Attacker.IsDead() || !e.Attacker.HasTrait<IHasLocation>()
                || (e.Damage < 0)) return;

            if (self.HasTrait<RepairableBuilding>()) // Building being attacked,
            {
                if (AI.ticks - lastbasedamagetick > 50)
                    OnBaseDamage(self, e); // Only call every two seconds at most

                lastbasedamagetick = AI.ticks;
            }
            if (self.HasTrait<Harvester>())
            {
                if (AI.ticks - lastharvdamagetick > 50)
                    OnHarvesterDamage(self, e); //  Only call every two seconds at most
            }

/*              if (e.DamageState > DamageState.Light && e.PreviousDamageState <= DamageState.Light)
                {
                    if (!e.Attacker.Destroyed && !e.Attacker.IsDead() && e.Attacker.HasTrait<IHasLocation>())
                    {
                    }
                }*/

        }
    }
}