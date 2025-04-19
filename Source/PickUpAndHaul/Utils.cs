using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Noise;

namespace PickUpAndHaul
{
    public static class Utils
    {
        public static Assembly[] Assemblies = AppDomain.CurrentDomain.GetAssemblies();
        public static Type VehicleReservationManagerType = Assemblies.SelectMany(v => v.GetTypes()).FirstOrDefault(v => v.Name == "VehicleReservationManager");
        public static MethodInfo VehicleListersMeth = VehicleReservationManagerType.GetMethod("VehicleListers", BindingFlags.Public | BindingFlags.Instance);

        public static class VehicleReservationType
        {
            public const string LoadVehicle = "LoadVehicle";
            public const string LoadTurret = "LoadVehicleForTurret";
            public const string Refuel = "Refuel";
            public const string Repair = "Repair";
            public const string Upgrade = "Upgrade";
            public const string LoadUpgradeMaterials = "LoadUpgradeMaterials";
        }

        public static IEnumerable<object> GetVehiclePackingListers(Map map)
        {
            if (map == null)
                return null;

            var vehicleReservationManager = map.GetComponent(VehicleReservationManagerType);
            var listers = VehicleListersMeth.Invoke(vehicleReservationManager, new object[] { VehicleReservationType.LoadVehicle });

            return listers as IEnumerable<object>;
        }

        public static CompHauledToInventory GetHaulInventoryComp(this Thing thing)
        {
            if (!thing.TryGetComp<CompHauledToInventory>(out var comp))
            {
                Log.Message($"NO COMP ON {thing}");
                return null;
            }
            return comp;
        }

        public class ThingPositionComparer : IComparer<Thing>
        {
            public IntVec3 rootCell;
            public int Compare(Thing x, Thing y) => (x.Position - rootCell).LengthHorizontalSquared.CompareTo((y.Position - rootCell).LengthHorizontalSquared);
        }

        public static bool FindDestinationForThing(
            Thing thing, Pawn pawn, Map map, StoragePriority currentPriority, bool forced, out LocalTargetInfo destinationTarget, out int count, out Job interjectJob, out int? progressBarDelay)
        {
            Log.Message("----Look at thing: " + thing);

            interjectJob = null;
            destinationTarget = default(LocalTargetInfo);
            count = 0;
            progressBarDelay = null;

            /*If item is already inside our inventory then we should not be checking these things*/
            if (false == InInventory(thing, pawn))
            {
                if (false == OkThingToHaul(thing, pawn) || false == HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced))
                {
                    Log.Message("----not hauling this: " + thing);
                    return false;
                }

                if (MassUtility.IsOverEncumbered(pawn) || HoldMultipleThings_Support.OverAllowedGearCapacity(pawn))
                {
                    Log.Message("----already overencumbered: " + thing);
                    return false;
                }
            }

            if (StoreUtility.TryFindBestBetterStorageFor(thing, pawn, map, currentPriority, pawn.Faction, out var targetCell, out var haulDestination, true))
            {

                if (haulDestination is ISlotGroupParent)
                {
                    Log.Message("----dest is cell: " + targetCell);
                    destinationTarget = new LocalTargetInfo(targetCell);
                    count = HoldMultipleThings_Support.CapacityAt(pawn, thing, targetCell, map);
                    Log.Message("----count: " + count);
                    if (count == 0)
                        return false;
                    return true;

                    /* TODO: i want to know why we hate hooper jobs so much */
                    //if (false /* HaulToHopperJob(thing, targetCell, map)*/ )
                    //{
                    //    Log.Message("----dest is hopper: " + haulDestination);
                    //    interjectJob = HaulAIUtility.HaulToStorageJob(pawn, thing);
                    //    return true;
                    //}
                    //else
                    //{
                    //    Log.Message("----dest is cell: " + targetCell);
                    //    destinationTarget = new LocalTargetInfo(targetCell);
                    //    count = HoldMultipleThings_Support.CapacityAt(thing, targetCell, map);
                    //    return true;
                    //}
                }
                else if (ExtractThingFromHaulDestination(haulDestination, out var destinationAsThing, out var nonSlotGroupThingOwner, out int? progBarDel) != null && nonSlotGroupThingOwner != null)
                {
                    Log.Message("----dest is a thing with container: " + destinationAsThing);
                    destinationTarget = new LocalTargetInfo(destinationAsThing);
                    count = nonSlotGroupThingOwner.GetCountCanAccept(thing);
                    Log.Message("----count: " + count);
                    progressBarDelay = progBarDel;
                    return true;
                }

                Verse.Log.Error("Don't know how to handle HaulToStorageJob for storage " + haulDestination.ToStringSafe() + ". thing=" + thing.ToStringSafe());
                return false;
            }

            Log.Message("----no place found");
            return false;
        }

        public static bool InInventory(Thing thing, Pawn pawn)
        {
            return pawn.inventory.innerContainer.Any(v => v == thing);
        }

        public static bool OkThingToHaul(Thing t, Pawn pawn)
        {
            return t.Spawned && pawn.CanReserve(t) && !t.IsForbidden(pawn);
        }

        public static Thing ExtractThingFromHaulDestination(IHaulDestination t, out Thing thing, out ThingOwner nonSlotGroupThingOwner, out int? progBarDel)
        {
            nonSlotGroupThingOwner = null;
            thing = null;
            progBarDel = null;
            if (t is CriticalThingHaulDestination wrapper)
            {
                thing = wrapper.Thing;
                progBarDel = wrapper.ProgressBarDelay;

            }
            else if (t is Thing tt)
            {
                thing = tt;
            }

            if (thing != null)
            {
                nonSlotGroupThingOwner = thing.TryGetInnerInteractableThingOwner();
            }


            return thing;
        }

        public static bool FindClosestThing(IntVec3 center, Map map, Pawn pawn, HashSet<Thing> seen, int maxDistance, Func<Thing, bool> validator, out Thing foundItem)
        {
            foundItem = GenClosest.ClosestThingReachable(
                   center,
                   map,
                   ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                   PathEndMode.Touch,
                   TraverseParms.For(pawn),
                   maxDistance: maxDistance,
                   validator: (i) => false == seen.Contains(i) && validator(i)
                );


            return foundItem != null;

        }

        public static bool TryFindValidToLocation(Pawn vehicle, out IntVec3 to, out string notification)
        {
            var toSpots = vehicle.Map.listerBuildings.allBuildingsColonist.Where(v =>
            {
                return v.def == PickUpAndHaulJobDefOf.HaulAdj_VehicleHaul_Spot;
            }).ToList();

            to = default(IntVec3);
            notification = "";
            if (toSpots.Count > 1)
            {
                notification = $"More than one TO HAUL spot: {string.Join(", ", toSpots)}";
                return false;
            }

            if (toSpots.Count==0)
            {
                notification = "Set a TO HAUL spot.";
                return false;
            }

            to = toSpots[0].Position;
            return true;
        }
        public static bool TryFindValidFromZones(out List<SlotGroup> froms)
        {
            froms = Find.Maps.SelectMany(v => v.haulDestinationManager.AllGroupsListForReading).Where(v => v.GetName().Split(' ')[0].ToLowerInvariant() == "from")
                .Where(v => v.HeldThings.Count() > 0).ToList();

            return froms.Count != 0;
        }
        public static bool TryFindValidFromZone(out SlotGroup from)
        {
            from = null;
            if (!TryFindValidFromZones(out var zones))
            {
                return false;
            }

            from = zones.First();


            return true;
        }
        public static bool TryFindFittingCell(Pawn vehicle, SlotGroup zone, out IntVec3 cell)
        {
            cell = default(IntVec3);

            var v = new VehiclePawnProxy(vehicle);
            foreach (var c in zone.CellsList)
            {
                if (v.FitsOnCell(c) && vehicle.Map.reachability.CanReach(vehicle.Position, c, PathEndMode.OnCell, TraverseParms.For(vehicle)))
                {
                    cell = c;
                    return true;
                }
            }

            return false;
        }

        public static bool CanGetToCell(Pawn vehicle, IntVec3 cell)
        {
            var v = new VehiclePawnProxy(vehicle);

            return v.FitsOnCell(cell) && vehicle.Map.reachability.CanReach(vehicle.Position, cell, PathEndMode.OnCell, TraverseParms.For(vehicle));
        }
    }




}
