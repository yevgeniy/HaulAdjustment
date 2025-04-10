using System.Linq;

namespace PickUpAndHaul;
public class HoldMultipleThings_Support
{
    public static bool OverAllowedGearCapacity(Pawn pawn) => MassUtility.GearMass(pawn) / MassUtility.Capacity(pawn) >= Settings.MaximumOccupiedCapacityToConsiderHauling;
    public static int CapacityAt(Pawn pawn, Thing thing, IntVec3 storeCell, Map map)
    {
        if (HoldMultipleThings_Support.CapacityAt(thing, storeCell, map, out var capacity))
        {
            
            Log.Message($"Found external capacity of {capacity}");
            return capacity;
        }
     
        //capacity = thing.def.stackLimit;

        capacity = storeCell.GetItemStackSpaceLeftFor(pawn.Map, thing.def);

        //var j=HaulAIUtility.HaulToCellStorageJob(pawn, thing, storeCell, true);
        //if (j == null)
        //    return 0;
     
        //capacity = j.count;
     

        //var preExistingThing = map.thingGrid.ThingAt(storeCell, thing.def);
        //Log.Message($"----a {preExistingThing} {capacity}");
        //if (preExistingThing != null)
        //{
        //    capacity = thing.def.stackLimit - preExistingThing.stackCount;
        //    Log.Message($"----g {capacity} {thing.def.stackLimit} {preExistingThing.stackCount}");
        //}

        return capacity;
    }
    // ReSharper disable SuspiciousTypeConversion.Global
    public static bool CapacityAt(Thing thing, IntVec3 storeCell, Map map, out int capacity)
	{
		capacity = 0;

		if ((map.haulDestinationManager.SlotGroupParentAt(storeCell) as ThingWithComps)?
		   .AllComps.FirstOrDefault(x => x is IHoldMultipleThings.IHoldMultipleThings)
		   is IHoldMultipleThings.IHoldMultipleThings compOfHolding)
		{
            return compOfHolding.CapacityAt(thing, storeCell, map, out capacity);
		}

        foreach (var t in storeCell.GetThingList(map))
		{
            if (t is IHoldMultipleThings.IHoldMultipleThings holderOfMultipleThings)
			{
                return holderOfMultipleThings.CapacityAt(thing, storeCell, map, out capacity);
			}
		}

		return false;
	}

	public static bool StackableAt(Thing thing, IntVec3 storeCell, Map map)
	{
		if ((map.haulDestinationManager.SlotGroupParentAt(storeCell) as ThingWithComps)?
		   .AllComps.FirstOrDefault(x => x is IHoldMultipleThings.IHoldMultipleThings)
		   is IHoldMultipleThings.IHoldMultipleThings compOfHolding)
		{
			return compOfHolding.StackableAt(thing, storeCell, map);
		}

		foreach (var t in storeCell.GetThingList(map))
		{
			if (t is IHoldMultipleThings.IHoldMultipleThings holderOfMultipleThings)
			{
				return holderOfMultipleThings.StackableAt(thing, storeCell, map);
			}
		}

		return false;
	}
}