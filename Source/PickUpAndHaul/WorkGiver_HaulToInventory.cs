using System.Linq;
using static PickUpAndHaul.WorkGiver_HaulToInventory;
using Verse;
using static UnityEngine.GraphicsBuffer;
using Verse.Noise;

namespace PickUpAndHaul;
public class WorkGiver_HaulToInventory : WorkGiver_HaulGeneral
{
    //Thanks to AlexTD for the more dynamic search range
    //And queueing
    //And optimizing
    //private const float SEARCH_FOR_OTHERS_RANGE_FRACTION = 0.5f;

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
        => base.ShouldSkip(pawn, forced)
        || pawn.Faction != Faction.OfPlayerSilentFail
        || !Settings.IsAllowedRace(pawn.RaceProps)
        || pawn.GetComp<CompHauledToInventory>() == null
        || pawn.IsQuestLodger()
        || HoldMultipleThings_Support.OverAllowedGearCapacity(pawn);

    public static bool GoodThingToHaul(Thing t, Pawn pawn)
        => Utils.OkThingToHaul(t, pawn)
        && IsNotCorpseOrAllowed(t);
        //&& !t.IsInValidBestStorage();

    

    public static bool IsNotCorpseOrAllowed(Thing t) => Settings.AllowCorpses || t is not Corpse;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        var list = pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling();
        Comparer.rootCell = pawn.Position;
        list.Sort(Comparer);
        return list;
    }

    private static Utils.ThingPositionComparer Comparer { get; } = new();


    public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        => Utils.OkThingToHaul(thing, pawn)
        && IsNotCorpseOrAllowed(thing)
        && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced)
        && StoreUtility.TryFindBestBetterStorageFor(thing, pawn, pawn.Map, StoreUtility.CurrentStoragePriorityOf(thing), pawn.Faction, out _, out _, false);

    

    //public List<Thing> GetThingsCloseBy(Pawn pawn, Thing firstThing, Map map, StoreTarget firstStoreTarget)
    //{
    //    var haulables = new List<Thing>(map.listerHaulables.ThingsPotentiallyNeedingHauling());
    //    Comparer.rootCell = firstThing.Position;
    //    haulables.Sort(Comparer);

    //    var distanceToHaul = (firstStoreTarget.Position - firstThing.Position).LengthHorizontal * SEARCH_FOR_OTHERS_RANGE_FRACTION;
    //    var distanceToSearchMore = Math.Max(12f, distanceToHaul);

    //    if (haulables == null || !haulables.Any())
    //    {
    //        return null;
    //    }

    //    var maxDistanceSquared = distanceToSearchMore * distanceToSearchMore;


    //    var items = new List<Thing>();
    //    var center = firstThing.Position;
    //    var peMode = PathEndMode.ClosestTouch;
    //    var traverseParams = TraverseParms.For(pawn);
    //    while (FindClosestThing(haulables, center, out var i) is { } closestThing)
    //    {
    //        haulables.RemoveAt(i);
    //        if (!closestThing.Spawned)
    //        {
    //            continue;
    //        }

    //        if ((center - closestThing.Position).LengthHorizontalSquared > maxDistanceSquared)
    //        {
    //            break;
    //        }

    //        if (!map.reachability.CanReach(center, closestThing, peMode, traverseParams))
    //        {
    //            continue;
    //        }

    //        if (validator == null || validator(closestThing))
    //        {
    //            return closestThing;
    //        }
    //    }

    //    return null;

    //}

    //pick up stuff until you can't anymore,
    //while you're up and about, pick up something and haul it
    //before you go out, empty your pockets
    public override Job JobOnThing(Pawn pawn, Thing item, bool forced = false)
    {
        Log.Message("-------------");
        Log.Message("WORK GIVER START for: " + pawn);


        if (pawn.GetComp<CompHauledToInventory>() is null)
        {
            Log.Message("--pawn missing storage component.  Haul noramlly");
            return HaulAIUtility.HaulToStorageJob(pawn, item);
        }


        var map = pawn.Map;

        var currentPriority = StoreUtility.CurrentStoragePriorityOf(item);

        if (Utils.FindDestinationForThing(
            item, pawn, map, currentPriority, forced,
            out var destinationTarget, out var count, out var interjectJob, out var _))
        {
            //if (interjectJob != null)
            //{
            //    Log.Message("--interjecting job: " +  thing);
            //    return HaulAIUtility.HaulToStorageJob(pawn, thing);
            //}


            Log.Message("--haul to inventory job: " + item + " " + destinationTarget);
            var j= JobMaker.MakeJob(PickUpAndHaulJobDefOf.HaulToInventory, item, destinationTarget);
            j.count = Math.Min( count, item.stackCount);

            return j;

        }

        /* Did not find anything to haul.  Just in case, let noral process take over */
        Log.Message("--count not fined good destination: " + item);
        return HaulAIUtility.HaulToStorageJob(pawn, item);





        //Find what fits in inventory, set nextThingLeftOverCount to be 
        //var nextThingLeftOverCount = 0;

        //job.targetQueueA = new List<LocalTargetInfo>() { thing }; //more things
        //job.targetQueueB = new List<LocalTargetInfo>() { storeTarget }; //more storage; keep in mind the job doesn't use it, but reserve it so you don't over-haul
        //job.countQueue = new List<int>();//thing counts

        //var ceOverweight = false;

        //Find extra things than can be hauled to inventory, queue to reserve them
        //var haulUrgentlyDesignation = PickUpAndHaulDesignationDefOf.haulUrgently;
        //var isUrgent = ModCompatibilityCheck.AllowToolIsActive && designationManager.DesignationOn(thing)?.def == haulUrgentlyDesignation;

        //var haulables = new List<Thing>(map.listerHaulables.ThingsPotentiallyNeedingHauling());
        //Comparer.rootCell = thing.Position;
        //haulables.Sort(Comparer);

        //var nextThing = thing;
        //var lastThing = thing;

        //var storeCellCapacity = new Dictionary<StoreTarget, CellAllocation>()
        //{
        //    [storeTarget] = new(nextThing, capacityStoreCell)
        //};
        //skipTargets = new() { storeTarget };
        //skipCells = new();
        //skipThings = new();
        //if (storeTarget.container != null)
        //{
        //    skipThings.Add(storeTarget.container);
        //}
        //else
        //{
        //    skipCells.Add(storeTarget.cell);
        //}

        //bool Validator(Thing t)
        //    => (!isUrgent || designationManager.DesignationOn(t)?.def == haulUrgentlyDesignation)
        //    && GoodThingToHaul(t, pawn) && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, false); //forced is false, may differ from first thing

        //haulables.Remove(thing);

        //do
        //{
        //    if (AllocateThingAtCell(storeCellCapacity, pawn, nextThing, job))
        //    {
        //        lastThing = nextThing;
        //        encumberance += AddedEncumberance(pawn, nextThing);

        //        if (encumberance > 1 || ceOverweight)
        //        {
        //            //can't CountToPickUpUntilOverEncumbered here, pawn doesn't actually hold these things yet
        //            nextThingLeftOverCount = CountPastCapacity(pawn, nextThing, encumberance);
        //            Log.Message($"Inventory allocated, will carry {nextThing}:{nextThingLeftOverCount}");
        //            break;
        //        }
        //    }
        //}
        //while ((nextThing = GetClosestAndRemove(lastThing.Position, map, haulables, PathEndMode.ClosestTouch,
        //    TraverseParms.For(pawn), distanceToSearchMore, Validator)) != null);

        //if (nextThing == null)
        //{
        //    skipCells = null;
        //    skipThings = null;
        //    //skipTargets = null;
        //    return job;
        //}

        ////Find what can be carried
        ////this doesn't actually get pickupandhauled, but will hold the reservation so others don't grab what this pawn can carry
        //haulables.RemoveAll(t => !t.CanStackWith(nextThing));

        //var carryCapacity = pawn.carryTracker.MaxStackSpaceEver(nextThing.def) - nextThingLeftOverCount;
        //if (carryCapacity == 0)
        //{
        //    Log.Message("Can't carry more, nevermind!");
        //    skipCells = null;
        //    skipThings = null;
        //    //skipTargets = null;
        //    return job;
        //}
        //Log.Message($"Looking for more like {nextThing}");

        //while ((nextThing = GetClosestAndRemove(nextThing.Position, map, haulables,
        //       PathEndMode.ClosestTouch, TraverseParms.For(pawn), 8f, Validator)) != null)
        //{
        //    carryCapacity -= nextThing.stackCount;

        //    if (AllocateThingAtCell(storeCellCapacity, pawn, nextThing, job))
        //    {
        //        break;
        //    }

        //    if (carryCapacity <= 0)
        //    {
        //        var lastCount = job.countQueue.Pop() + carryCapacity;
        //        job.countQueue.Add(lastCount);
        //        Log.Message($"Nevermind, last count is {lastCount}");
        //        break;
        //    }
        //}

        //skipCells = null;
        //skipThings = null;
        ////skipTargets = null;
        //return job;
    }

    //private static bool HaulToHopperJob(Thing thing, IntVec3 targetCell, Map map)
    //{
    //    if (thing.def.IsNutritionGivingIngestible
    //        && thing.def.ingestible.preferability is FoodPreferability.RawBad or FoodPreferability.RawTasty)
    //    {
    //        var thingList = targetCell.GetThingList(map);
    //        for (var i = 0; i < thingList.Count; i++)
    //        {
    //            if (thingList[i].def == ThingDefOf.Hopper)
    //            {
    //                return true;
    //            }
    //        }
    //    }
    //    return false;
    //}

    //public struct StoreTarget : IEquatable<StoreTarget>
    //{
    //    public IntVec3 cell;
    //    public Thing container;
    //    public IntVec3 Position => container?.Position ?? cell;

    //    public StoreTarget(IntVec3 cell)
    //    {
    //        this.cell = cell;
    //        container = null;
    //    }
    //    public StoreTarget(Thing container)
    //    {
    //        cell = default;
    //        this.container = container;
    //    }

    //    public bool Equals(StoreTarget other) => container is null ? other.container is null && cell == other.cell : container == other.container;
    //    public override int GetHashCode() => container?.GetHashCode() ?? cell.GetHashCode();
    //    public override string ToString() => container?.ToString() ?? cell.ToString();
    //    public override bool Equals(object obj)
    //    {
    //        return obj is StoreTarget target
    //            ? Equals(target)
    //            : obj is Thing thing
    //                ? container == thing
    //                : obj is IntVec3 intVec && cell == intVec;
    //    }
    //    public static bool operator ==(StoreTarget left, StoreTarget right) => left.Equals(right);
    //    public static bool operator !=(StoreTarget left, StoreTarget right) => !left.Equals(right);
    //    public static implicit operator LocalTargetInfo(StoreTarget target) => target.container != null ? target.container : target.cell;
    //}

    //public static Thing GetClosestAndRemove(IntVec3 center, Map map, List<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null)
    //{
    //    if (searchSet == null || !searchSet.Any())
    //    {
    //        return null;
    //    }

    //    var maxDistanceSquared = maxDistance * maxDistance;

    //    while (FindClosestThing(searchSet, center, out var i) is { } closestThing)
    //    {
    //        searchSet.RemoveAt(i);
    //        if (!closestThing.Spawned)
    //        {
    //            continue;
    //        }

    //        if ((center - closestThing.Position).LengthHorizontalSquared > maxDistanceSquared)
    //        {
    //            break;
    //        }

    //        if (!map.reachability.CanReach(center, closestThing, peMode, traverseParams))
    //        {
    //            continue;
    //        }

    //        if (validator == null || validator(closestThing))
    //        {
    //            return closestThing;
    //        }
    //    }

    //    return null;
    //}

 

    //public class CellAllocation
    //{
    //    public Thing allocated;
    //    public int capacity;

    //    public CellAllocation(Thing a, int c)
    //    {
    //        allocated = a;
    //        capacity = c;
    //    }
    //}



    //public static bool Stackable(Thing nextThing, KeyValuePair<StoreTarget, CellAllocation> allocation)
    //    => nextThing == allocation.Value.allocated
    //    || allocation.Value.allocated.CanStackWith(nextThing)
    //    || HoldMultipleThings_Support.StackableAt(nextThing, allocation.Key.cell, nextThing.Map);

    //public static bool AllocateThingAtCell(Dictionary<StoreTarget, CellAllocation> storeCellCapacity, Pawn pawn, Thing nextThing, Job job)
    //{
    //    var map = pawn.Map;
    //    var allocation = storeCellCapacity.FirstOrDefault(kvp =>
    //        kvp.Key is var storeTarget
    //        && (storeTarget.container?.TryGetInnerInteractableThingOwner().CanAcceptAnyOf(nextThing)
    //        ?? storeTarget.cell.GetSlotGroup(map).parent.Accepts(nextThing))
    //        && Stackable(nextThing, kvp));
    //    var storeCell = allocation.Key;

    //    //Can't stack with allocated cells, find a new cell:
    //    if (storeCell == default)
    //    {
    //        var currentPriority = StoreUtility.CurrentStoragePriorityOf(nextThing);
    //        if (TryFindBestBetterStorageFor(nextThing, pawn, map, currentPriority, pawn.Faction, out var nextStoreCell, out var haulDestination, out var innerInteractableThingOwner))
    //        {
    //            if (nextStoreCell != default(IntVec3))
    //            {
    //                storeCell = new(nextStoreCell);
    //                job.targetQueueB.Add(nextStoreCell);

    //                storeCellCapacity[storeCell] = new(nextThing, CapacityAt(nextThing, nextStoreCell, map));

    //                Log.Message($"New cell for unstackable {nextThing} = {nextStoreCell}");
    //            }
    //            else
    //            {
    //                var destinationAsThing = ExtractThing(haulDestination, out var _);
    //                storeCell = new(destinationAsThing);
    //                job.targetQueueB.Add(destinationAsThing);

    //                storeCellCapacity[storeCell] = new(nextThing, innerInteractableThingOwner.GetCountCanAccept(nextThing));

    //                Log.Message($"New haulDestination for unstackable {nextThing} = {haulDestination}");
    //            }
    //        }
    //        else
    //        {
    //            Log.Message($"{nextThing} can't stack with allocated cells");

    //            if (job.targetQueueA.NullOrEmpty())
    //            {
    //                job.targetQueueA.Add(nextThing);
    //            }

    //            return false;
    //        }
    //    }

    //    job.targetQueueA.Add(nextThing);
    //    var count = nextThing.stackCount;
    //    storeCellCapacity[storeCell].capacity -= count;
    //    Log.Message($"{pawn} allocating {nextThing}:{count}, now {storeCell}:{storeCellCapacity[storeCell].capacity}");

    //    while (storeCellCapacity[storeCell].capacity <= 0)
    //    {
    //        var capacityOver = -storeCellCapacity[storeCell].capacity;
    //        storeCellCapacity.Remove(storeCell);

    //        Log.Message($"{pawn} overdone {storeCell} by {capacityOver}");

    //        if (capacityOver == 0)
    //        {
    //            break;  //don't find new cell, might not have more of this thing to haul
    //        }

    //        var currentPriority = StoreUtility.CurrentStoragePriorityOf(nextThing);
    //        if (TryFindBestBetterStorageFor(nextThing, pawn, map, currentPriority, pawn.Faction, out var nextStoreCell, out var nextHaulDestination, out var innerInteractableThingOwner))
    //        {
    //            if (innerInteractableThingOwner is null)
    //            {
    //                storeCell = new(nextStoreCell);
    //                job.targetQueueB.Add(nextStoreCell);

    //                var capacity = CapacityAt(nextThing, nextStoreCell, map) - capacityOver;
    //                storeCellCapacity[storeCell] = new(nextThing, capacity);

    //                Log.Message($"New cell {nextStoreCell}:{capacity}, allocated extra {capacityOver}");
    //            }
    //            else
    //            {
    //                var destinationAsThing = ExtractThing(nextHaulDestination, out var _);
    //                storeCell = new(destinationAsThing);
    //                job.targetQueueB.Add(destinationAsThing);

    //                var capacity = innerInteractableThingOwner.GetCountCanAccept(nextThing) - capacityOver;

    //                storeCellCapacity[storeCell] = new(nextThing, capacity);

    //                Log.Message($"New haulDestination {nextHaulDestination}:{capacity}, allocated extra {capacityOver}");
    //            }
    //        }
    //        else
    //        {
    //            count -= capacityOver;
    //            job.countQueue.Add(count);
    //            Log.Message($"Nowhere else to store, allocated {nextThing}:{count}");
    //            return false;
    //        }
    //    }
    //    job.countQueue.Add(count);
    //    Log.Message($"{nextThing}:{count} allocated");
    //    return true;
    //}

    //public static HashSet<StoreTarget> skipTargets;
    //public static HashSet<IntVec3> skipCells;
    //public static HashSet<Thing> skipThings;

    //public static bool TryFindBestBetterStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, out IHaulDestination haulDestination, out ThingOwner innerInteractableThingOwner)
    //{
    //    var storagePriority = StoragePriority.Unstored;
    //    innerInteractableThingOwner = null;
    //    if (TryFindBestBetterStoreCellFor(t, carrier, map, currentPriority, faction, out var foundCell2))
    //    {
    //        storagePriority = foundCell2.GetSlotGroup(map).Settings.Priority;
    //    }

    //    if (!TryFindBestBetterNonSlotGroupStorageFor(t, carrier, map, currentPriority, faction, out var haulDestination2))
    //    {
    //        haulDestination2 = null;
    //    }

    //    if (storagePriority == StoragePriority.Unstored && haulDestination2 == null)
    //    {
    //        foundCell = IntVec3.Invalid;
    //        haulDestination = null;
    //        return false;
    //    }

    //    if (haulDestination2 != null && (storagePriority == StoragePriority.Unstored || (int)haulDestination2.GetStoreSettings().Priority > (int)storagePriority))
    //    {
    //        foundCell = IntVec3.Invalid;
    //        haulDestination = haulDestination2;

    //        if (haulDestination2 is not Thing destinationAsThing)
    //        {
    //            Verse.Log.Error($"{haulDestination2} is not a valid Thing. Pick Up And Haul can't work with this");
    //        }
    //        else
    //        {
    //            innerInteractableThingOwner = destinationAsThing.TryGetInnerInteractableThingOwner();
    //        }

    //        if (innerInteractableThingOwner is null)
    //        {
    //            Verse.Log.Error($"{haulDestination2} gave null ThingOwner during lookup in Pick Up And Haul's WorkGiver_HaulToInventory");
    //        }

    //        return true;
    //    }

    //    foundCell = foundCell2;
    //    haulDestination = foundCell2.GetSlotGroup(map).parent;
    //    return true;
    //}

    //public static bool TryFindBestBetterStoreCellFor(Thing thing, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell)
    //{
    //    var haulDestinations = map.haulDestinationManager.AllGroupsListInPriorityOrder;
    //    for (var i = 0; i < haulDestinations.Count; i++)
    //    {
    //        var slotGroup = haulDestinations[i];
    //        if (slotGroup.Settings.Priority <= currentPriority || !slotGroup.parent.Accepts(thing))
    //        {
    //            continue;
    //        }

    //        var cellsList = slotGroup.CellsList;

    //        for (var j = 0; j < cellsList.Count; j++)
    //        {
    //            var cell = cellsList[j];
    //            if (skipCells.Contains(cell))
    //            {
    //                continue;
    //            }

    //            if (StoreUtility.IsGoodStoreCell(cell, map, thing, carrier, faction) && cell != default)
    //            {
    //                foundCell = cell;

    //                skipCells.Add(cell);

    //                return true;
    //            }
    //        }
    //    }
    //    foundCell = IntVec3.Invalid;
    //    return false;
    //}

    //public static float AddedEncumberance(Pawn pawn, Thing thing)
    //    => thing.stackCount * thing.GetStatValue(StatDefOf.Mass) / MassUtility.Capacity(pawn);

    //public static int CountPastCapacity(Pawn pawn, Thing thing, float encumberance)
    //    => (int)Math.Ceiling((encumberance - 1) * MassUtility.Capacity(pawn) / thing.GetStatValue(StatDefOf.Mass));

    //public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IHaulDestination haulDestination, bool acceptSamePriority = false)
    //{
    //    var allHaulDestinationsListInPriorityOrder = map.haulDestinationManager.AllHaulDestinationsListInPriorityOrder;
    //    var intVec = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
    //    var num = float.MaxValue;
    //    var storagePriority = StoragePriority.Unstored;
    //    haulDestination = null;
    //    for (var i = 0; i < allHaulDestinationsListInPriorityOrder.Count; i++)
    //    {
    //        var iHaulDestination = allHaulDestinationsListInPriorityOrder[i];

    //        if (iHaulDestination is ISlotGroupParent || (iHaulDestination is Building_Grave && !t.CanBeBuried()))
    //        {
    //            continue;
    //        }

    //        var priority = iHaulDestination.GetStoreSettings().Priority;
    //        if ((int)priority < (int)storagePriority || (acceptSamePriority && (int)priority < (int)currentPriority) || (!acceptSamePriority && (int)priority <= (int)currentPriority))
    //        {
    //            break;
    //        }

    //        float num2 = intVec.DistanceToSquared(iHaulDestination.Position);
    //        if (num2 > num || !iHaulDestination.Accepts(t))
    //        {
    //            continue;
    //        }

    //        var thing = ExtractThing(iHaulDestination, out var _);
    //        if (thing != null)
    //        {
    //            if (skipThings.Contains(thing) || thing.Faction != faction)
    //            {
    //                continue;
    //            }

    //            if (carrier != null)
    //            {
    //                if (thing.IsForbidden(carrier)
    //                    || !carrier.CanReserveNew(thing)
    //                    || !carrier.Map.reachability.CanReach(intVec, thing, PathEndMode.ClosestTouch, TraverseParms.For(carrier)))
    //                {
    //                    continue;
    //                }
    //            }
    //            else if (faction != null)
    //            {
    //                if (thing.IsForbidden(faction) || map.reservationManager.IsReservedByAnyoneOf(thing, faction))
    //                {
    //                    continue;
    //                }
    //            }

    //            skipThings.Add(thing);
    //        }
    //        else
    //        {
    //            //not supported. Seems dumb
    //            continue;

    //            //if (carrier != null && !carrier.Map.reachability.CanReach(intVec, iHaulDestination.Position, PathEndMode.ClosestTouch, TraverseParms.For(carrier)))
    //            //	continue;
    //        }

    //        num = num2;
    //        storagePriority = priority;
    //        haulDestination = iHaulDestination;
    //    }

    //    return haulDestination != null;
    //}
    
    
}

public static class PickUpAndHaulDesignationDefOf
{
    public static DesignationDef haulUrgently = DefDatabase<DesignationDef>.GetNamedSilentFail("HaulUrgentlyDesignation");
}