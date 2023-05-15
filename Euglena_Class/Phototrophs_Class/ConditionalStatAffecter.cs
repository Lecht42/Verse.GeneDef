using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using Verse.AI;
using System.Linq;
using System;
using System.Text;
using System.Threading;
using RimWorld.SketchGen;
using RimWorld.QuestGen;
using RimWorld.Planet;
using RimWorld.Planet;
using RimWorld.BaseGen;
using Verse.Sound;

namespace ConditionalStatAffecter_Euglena
{
    public static class IntVec3Extensions
    {
        public static bool InDarkness(this IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map))
            {
                return false;
            }
            if (!map.roofGrid.Roofed(cell))
            {
                return map.skyManager.CurSkyGlow <= 0.1f;
            }
            return true;
        }
    }
    public class ConditionalStatAffecter_InDarkness : ConditionalStatAffecter
    {
        public override string Label => "StatsReport_InDarkness".Translate();

        public override bool Applies(StatRequest req)
        {
            if (!ModsConfig.BiotechActive)
            {
                return false;
            }
            if (req.HasThing && req.Thing.Spawned)
            {
                return req.Thing.Position.InDarkness(req.Thing.Map);
            }
            return false;
        }
    }

    public static class PawnExtensionsUnderRain
    {
        public static bool IsUnderRain(this Pawn pawn)
        {
            return pawn.Map.weatherManager.RainRate > 0.0f && !pawn.Position.Roofed(pawn.Map);
        }
    }
    public class ConditionalStatAffecter_UnderRain : ConditionalStatAffecter
    {
        public override string Label => "StatsReport_UnderRain".Translate();

        public override bool Applies(StatRequest req)
        {
            if (!ModsConfig.BiotechActive)
            {
                return false;
            }
            if (req.HasThing && req.Thing.Spawned && req.Thing is Pawn pawn)
            {
                return pawn.IsUnderRain();
            }
            return false;
        }
    }

    public static class PawnExtensionsUnderMountainRoof
    {
        public static bool IsUnderMountainRoof(this Pawn pawn)
        {
            var roof = pawn.Map.roofGrid.RoofAt(pawn.Position);
            return roof == RoofDefOf.RoofRockThick || roof == RoofDefOf.RoofRockThin;
        }
    }
    public class ConditionalStatAffecter_UnderMountainRoof : ConditionalStatAffecter
    {
        public override string Label => "StatsReport_UnderMountainRoof".Translate();

        public override bool Applies(StatRequest req)
        {
            if (!ModsConfig.BiotechActive)
            {
                return false;
            }
            if (req.HasThing && req.Thing.Spawned && req.Thing is Pawn pawn)
            {
                return pawn.IsUnderMountainRoof();
            }
            return false;
        }
    }

    public static class PawnExtensionsIsNearPlants
    {
        public static bool IsNearPlants(this Pawn pawn, float radius)
        {
            foreach (var thing in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, radius, true))
            {
                if (thing is Plant)
                {
                    return true;
                }
            }

            return false;
        }
    }
    public class ConditionalStatAffecter_NearPlants : ConditionalStatAffecter
    {
        private const float CheckRadius = 3.0f;

        public override string Label => "StatsReport_NearPlants".Translate();

        public override bool Applies(StatRequest req)
        {
            if (!ModsConfig.BiotechActive)
            {
                return false;
            }
            if (req.HasThing && req.Thing.Spawned && req.Thing is Pawn pawn)
            {
                return pawn.IsNearPlants(CheckRadius);
            }
            return false;
        }
    }

    public static class PawnExtensionsIsOnFertileSoil
    {
        public static bool IsOnFertileSoil(this Pawn pawn)
        {
            return pawn.Position.GetTerrain(pawn.Map).fertility > 0.5;
        }
    }
    public class ConditionalStatAffecter_OnFertileSoil : ConditionalStatAffecter
    {
        public override string Label => "StatsReport_OnFertileSoil".Translate();

        public override bool Applies(StatRequest req)
        {
            if (!ModsConfig.BiotechActive)
            {
                return false;
            }
            if (req.HasThing && req.Thing.Spawned && req.Thing is Pawn pawn)
            {
                return pawn.IsOnFertileSoil();
            }
            return false;
        }
    }

}
