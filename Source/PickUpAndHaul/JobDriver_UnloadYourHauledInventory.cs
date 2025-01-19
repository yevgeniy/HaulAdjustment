using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace PickUpAndHaul;

public class JobDriver_UnloadYourHauledInventory : JobDriver
{
	private int _countToDrop = -1;
	private int _unloadDuration = 3;
	private int? _progressBarDelay=null;
    
	private static FieldInfo countToTransferFieldInfo = AccessTools.Field(typeof(TransferableOneWay), "countToTransfer");


    public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look<int>(ref _countToDrop, "countToDrop", -1);
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

	/// <summary>
	/// Find spot, reserve spot, pull thing out of inventory, go to spot, drop stuff, repeat.
	/// </summary>
	/// <returns></returns>
	public override IEnumerable<Toil> MakeNewToils()
	{
		if (ModCompatibilityCheck.ExtendedStorageIsActive)
		{
			_unloadDuration = 20;
		}

		var begin = Toils_General.Wait(_unloadDuration);
		yield return begin;

		var carriedThings = pawn.TryGetComp<CompHauledToInventory>().GetHashSet();
		yield return FindTargetOrDrop(carriedThings);
		yield return PullItemFromInventory(carriedThings, begin);

		var releaseReservation = ReleaseReservation();
		var carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);

		// Equivalent to if (TargetB.HasThing)
		yield return Toils_Jump.JumpIf(carryToCell, TargetIsCell);

		var carryToContainer = Toils_Haul.CarryHauledThingToContainer();
		yield return carryToContainer;
		yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(TargetIndex.B);
		

		var depositToContainer= Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.None);
        var showProgressBar = ShowProgressBar();



		yield return Toils_Jump.JumpIf(depositToContainer, () =>
		{
			return _progressBarDelay == null;
        });

		yield return new Toil
		{
			debugName = "SET PROGRESSBAR DELAY",
			initAction = () =>
			{
				
				showProgressBar.defaultDuration = _progressBarDelay.Value;
            }
		};
		yield return showProgressBar;

		if ( ModCompatibilityCheck.VehicleIsActive)
		{
            yield return Toils_Jump.JumpIf(depositToContainer, () => DestinationNotVehicle());
            yield return DepositToVehicle();
            yield return Toils_Jump.Jump(releaseReservation);
        }

        yield return depositToContainer;
		yield return Toils_Jump.Jump(releaseReservation);

		
		yield return carryToCell;
		yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);

		//If the original cell is full, PlaceHauledThingInCell will set a different TargetIndex resulting in errors on yield return Toils_Reserve.Release.
		//We still gotta release though, mostly because of Extended Storage.
		yield return releaseReservation;
		yield return Toils_Jump.Jump(begin);
	}

    private Toil DepositToVehicle()
    {
        return new Toil
        {
            initAction = () =>
            {
				var item = job.targetA.Thing;
                if (item is null || item.stackCount == 0)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable, true);
                }
                else
                {
                    int stackCount = item.stackCount; //store before transfer for transferable recache
					var vehicle = new VehiclePawnProxy(job.targetB.Thing as Pawn);
                    int result = vehicle.AddOrTransfer(item, stackCount);
                    TransferableOneWay transferable = VehiclePawnProxy.GetTransferable(vehicle.CargoToLoad, item);
                    if (transferable != null)
                    {
                        int count = transferable.CountToTransfer - stackCount;
                        countToTransferFieldInfo.SetValue(transferable, count);
                        if (transferable.CountToTransfer <= 0)
                        {
                            vehicle.CargoToLoad.Remove(transferable);
                        }
                    }
                }
            }
        };
    }

    private bool DestinationNotVehicle()
    {
		return HarmonyPatches.VehiclePawnType.IsAssignableFrom(job.targetB.Thing.GetType()) 
			== false;
    }

    private bool TargetIsCell() => !TargetB.HasThing;

	private Toil ShowProgressBar()
	{
        Toil reloadWait = ToilMaker.MakeToil("reload-wait");
        reloadWait.defaultCompleteMode = ToilCompleteMode.Delay;
		reloadWait.defaultDuration = 10;
        reloadWait.WithProgressBarToilDelay(TargetIndex.B);
        
        return reloadWait;
    }

    private Toil ReleaseReservation()
	{
		return new()
		{
			initAction = () =>
			{
				if (pawn.Map.reservationManager.ReservedBy(job.targetB, pawn, pawn.CurJob)
				    && !ModCompatibilityCheck.HCSKIsActive)
				{
					pawn.Map.reservationManager.Release(job.targetB, pawn, pawn.CurJob);
				}
			}
		};
	}

	private Toil PullItemFromInventory(HashSet<Thing> carriedThings, Toil wait)
	{
		return new()
		{
			initAction = () =>
			{
				var thing = job.GetTarget(TargetIndex.A).Thing;
				if (thing == null || !pawn.inventory.innerContainer.Contains(thing))
				{
					carriedThings.Remove(thing);
					pawn.jobs.curDriver.JumpToToil(wait);
					return;
				}
				if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !thing.def.EverStorable(false))
				{
					Log.Message($"Pawn {pawn} incapable of hauling, dropping {thing}");
					pawn.inventory.innerContainer.TryDrop(thing, ThingPlaceMode.Near, _countToDrop, out thing);
					EndJobWith(JobCondition.Succeeded);
					carriedThings.Remove(thing);
				}
				else
				{
					pawn.inventory.innerContainer.TryTransferToContainer(thing, pawn.carryTracker.innerContainer,
						_countToDrop, out thing);
					job.count = _countToDrop;
					job.SetTarget(TargetIndex.A, thing);
					carriedThings.Remove(thing);
				}

				if (ModCompatibilityCheck.CombatExtendedIsActive)
				{
					CompatHelper.UpdateInventory(pawn);
				}

				thing.SetForbidden(false, false);
			}
		};
	}
    public static Thing ExtractThing(IHaulDestination t, out LocalTargetInfo loc, out int? countNeeded, out int? progressBarDelay)
    {
        Thing thing = null;
		
		loc = null;
		countNeeded = null;
		progressBarDelay = null;
		
		if (t is null)
			return null;

        if (t is ThingHaulDestination wrapper)
        {
            thing = wrapper.Thing;
			loc = new LocalTargetInfo(thing);
			countNeeded = wrapper.CountNeeded;
			Log.Message("PROGRESS BAR DELAY: " + wrapper.ProgressBarDelay);
			progressBarDelay = wrapper.ProgressBarDelay;
        }
        else if (t is Thing tt)
        {
            thing = tt;
        }

        return thing;
    }

    private Toil FindTargetOrDrop(HashSet<Thing> carriedThings)
	{
		return new()
		{
			initAction = () =>
			{
				var unloadableThing = FirstUnloadableThing(pawn, carriedThings);

				if (unloadableThing.Count == 0)
				{
					if (carriedThings.Count == 0)
					{
						EndJobWith(JobCondition.Succeeded);
					}
					return;
				}

				var currentPriority = StoragePriority.Unstored; // Currently in pawns inventory, so it's unstored
				int? countNeeded = null;
				if (StoreUtility.TryFindBestBetterStorageFor(unloadableThing.Thing, pawn, pawn.Map, currentPriority,
					    pawn.Faction, out var cell, out var destination))
				{
					job.SetTarget(TargetIndex.A, unloadableThing.Thing);
                    
					var destinationThing = ExtractThing(destination, out var _, out countNeeded, out _progressBarDelay);
					Log.Message("DESTINATION: " + destinationThing + " " + cell + " " + destination);
                    if (cell == IntVec3.Invalid)
					{	
                        job.SetTarget(TargetIndex.B, destinationThing);
					}
					else
					{
						job.SetTarget(TargetIndex.B, cell);
					}

					Log.Message($"{pawn} found destination {job.targetB} for thing {unloadableThing.Thing}");
					if (!pawn.Map.reservationManager.Reserve(pawn, job, job.targetB))
					{
						Log.Message(
							$"{pawn} failed reserving destination {job.targetB}, dropping {unloadableThing.Thing}");
						pawn.inventory.innerContainer.TryDrop(unloadableThing.Thing, ThingPlaceMode.Near,
							unloadableThing.Thing.stackCount, out _);
						EndJobWith(JobCondition.Incompletable);
						return;
					}
					_countToDrop = countNeeded.HasValue ? countNeeded.Value : unloadableThing.Thing.stackCount;
				}
				else
				{
					Log.Message(
						$"Pawn {pawn} unable to find hauling destination, dropping {unloadableThing.Thing}");
					pawn.inventory.innerContainer.TryDrop(unloadableThing.Thing, ThingPlaceMode.Near,
						unloadableThing.Thing.stackCount, out _);
					EndJobWith(JobCondition.Succeeded);
				}
			}
		};
	}

	private static ThingCount FirstUnloadableThing(Pawn pawn, HashSet<Thing> carriedThings)
	{
		var innerPawnContainer = pawn.inventory.innerContainer;

		foreach (var thing in carriedThings.OrderBy(t => t.def.FirstThingCategory?.index).ThenBy(x => x.def.defName))
		{
			//find the overlap.
			if (!innerPawnContainer.Contains(thing))
			{
				//merged partially picked up stacks get a different thingID in inventory
				var stragglerDef = thing.def;
				carriedThings.Remove(thing);

				//we have no method of grabbing the newly generated thingID. This is the solution to that.
				for (var i = 0; i < innerPawnContainer.Count; i++)
				{
					var dirtyStraggler = innerPawnContainer[i];
					if (dirtyStraggler.def == stragglerDef)
					{
						return new ThingCount(dirtyStraggler, dirtyStraggler.stackCount);
					}
				}
			}
			return new ThingCount(thing, thing.stackCount);
		}
		return default;
	}
}
