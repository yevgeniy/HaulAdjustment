using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PickUpAndHaul;

public class JobDriver_UnloadYourHauledInventory : JobDriver
{
    private int _countToDrop = -1;
    private int unloadDuration = 3;
    private int? progressBarDelay = null;

    private static FieldInfo countToTransferFieldInfo = AccessTools.Field(typeof(TransferableOneWay), "countToTransfer");

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true;
    }


    /// <summary>
    /// Find spot, reserve spot, pull thing out of inventory, go to spot, drop stuff, repeat.
    /// </summary>
    /// <returns></returns>
    public override IEnumerable<Toil> MakeNewToils()
    {
        Log.Message("UNLOAD DRIVER FOR: " + pawn);


        AddFinishAction(_ =>
        {
            CritDestinationsMap.RemoveVehicleHaul(job);
        });


        Toil beginWait = Toils_General.Wait(2);
        Toil endWait = Toils_General.Wait(2);
        Toil successToil = new()
        {
            initAction = () =>
            {
                Log.Message($"END JOB DRIVER for pawn {pawn}");
                //EndJobWith(JobCondition.Succeeded);
                Log.Message($"END END");
            }
        };


        Toil findNextDestinationForFirstItem = FindNextDestinationForFirstItem(beginWait, successToil);
        Toil pullItemFromInventoryToil = PullItemFromInventoryToil(beginWait);
        Toil carryToCellToil = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
        Toil carryToContainerToil = Toils_Haul.CarryHauledThingToContainer();
        Toil showProgressBarToil = ShowProgressBarToil();
        Toil depositToContainerToil = Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.None);
        Toil releaseReservation = ReleaseReservation();

        yield return beginWait;
        yield return findNextDestinationForFirstItem;
        yield return pullItemFromInventoryToil;

        var notAContainer = Toils_General.Wait(2);
        yield return Toils_Jump.JumpIf(notAContainer, () => false == job.GetTarget(TargetIndex.B).HasThing);

        yield return carryToContainerToil;
        yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(TargetIndex.B);
        yield return Toils_Jump.JumpIf(depositToContainerToil, () =>
        {
            return progressBarDelay == null;
        });

        yield return new Toil
        {
            debugName = "SET PROGRESSBAR DELAY",
            initAction = () =>
            {

                showProgressBarToil.defaultDuration = progressBarDelay.Value;
            }
        };
        yield return showProgressBarToil;
        var notAVehicle = Toils_General.Wait(2);
        if (ModCompatibilityCheck.VehicleIsActive)
        {
            yield return Toils_Jump.JumpIf(notAVehicle, () => DestinationNotVehicle());
            yield return DepositToVehicle();
            yield return Toils_Jump.Jump(releaseReservation);
        }

        yield return notAVehicle;
        yield return depositToContainerToil;
        yield return Toils_Jump.Jump(releaseReservation);


        yield return notAContainer;
        yield return carryToCellToil;
        
        yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCellToil, true);



        yield return releaseReservation;
        yield return Toils_General.Do(() =>
        {
            /*If for some reason pawn is still carrying anything, just drop it*/
            if (this.pawn.carryTracker.CarriedThing!=null)
            {
                this.pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var __);
            }
            
        });
        yield return Toils_Jump.Jump(beginWait);

        yield return successToil;
        yield return endWait;

    }

    private Toil FindNextDestinationForFirstItem(Toil beginWait, Toil successToil)
    {
        var t = Toils_General.Do(() =>
        {
            Log.Message("----FIND NEXT DESTINATION");

            /*TODO*/


            var hauledInventoryItem = pawn.inventory.innerContainer.FirstOrDefault(v => v.GetHaulInventoryComp().Hauling);

            if (hauledInventoryItem==null)
            {
                /*No more items.  Job is good?*/
                Log.Message("----No more items hauled");
                pawn.jobs.curDriver.JumpToToil(successToil);
                return;
            }

            var currentPriority = StoragePriority.Unstored;

            if (Utils.FindDestinationForThing(hauledInventoryItem, pawn, pawn.Map,
                currentPriority, false, out var destinationTarget, out var desiredCountAtDestination, out var _,
                out int? progressBarDelay)
            )
            {
                
                Log.Message("----desired count at destination: " + desiredCountAtDestination);

                job.SetTarget(TargetIndex.A, hauledInventoryItem);
                job.SetTarget(TargetIndex.B, destinationTarget);
                job.count = Math.Min(desiredCountAtDestination, hauledInventoryItem.def.stackLimit);

                /*reserve */
                if (destinationTarget.HasThing && VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(destinationTarget.Thing.GetType()))
                {
                    CritDestinationsMap.AddVehicleHaul(job, destinationTarget.Thing as Pawn, hauledInventoryItem, job.count);
                }
                else
                {
                    pawn.Reserve(TargetB, job);
                }

                this.progressBarDelay = progressBarDelay;

            }
            else
            {
                Log.Message("----could not find destination for item: " + hauledInventoryItem + " drop it");
                pawn.inventory.innerContainer.TryDrop(hauledInventoryItem, ThingPlaceMode.Near, hauledInventoryItem.stackCount, out _);
                
                pawn.jobs.curDriver.JumpToToil(beginWait);
                return;
            }

        });


        return t;
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
                    Log.Message("----ADDING TO VEHICLE " + vehicle.Thing + " " + item + " " + stackCount);
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
                    CritDestinationsMap.JustLoaded(job, vehicle.Thing as Pawn, item, stackCount);
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

    private Toil ShowProgressBarToil()
    {
        Toil reloadWait = ToilMaker.MakeToil("reload-wait");
        reloadWait.defaultCompleteMode = ToilCompleteMode.Delay;
        reloadWait.defaultDuration = 10; /* will be adjusted in previous toil */
        reloadWait.WithProgressBarToilDelay(TargetIndex.B);

        return reloadWait;
    }

    private Toil ReleaseReservation()
    {
        return new()
        {
            initAction = () =>
            {
                //if (pawn.Map.reservationManager.ReservedBy(job.targetA, pawn, pawn.CurJob))
                //{
                //    pawn.Map.reservationManager.Release(job.targetA, pawn, pawn.CurJob);
                //}

                //if (pawn.Map.reservationManager.ReservedBy(job.targetB, pawn, pawn.CurJob))
                //{
                //    pawn.Map.reservationManager.Release(job.targetB, pawn, pawn.CurJob);
                //}

            }
        };
    }

    private Toil PullItemFromInventoryToil(Toil startingToil)
    {
        Toil t = new()
        {
            initAction = () =>
            {

                var item = job.GetTarget(TargetIndex.A).Thing;
                var desiredShoulderCount = job.count;

                Log.Message("----PULL ITEM FROM INVENTORY: " + pawn + " " + item + " desired: " + desiredShoulderCount);

                /* First shoulder the item for which the destination was derived to give us a concrete
                 * def/stuff definition of the item going to that destination */
                pawn.inventory.innerContainer.TryTransferToContainer(item, pawn.carryTracker.innerContainer,
                        Math.Min(desiredShoulderCount,item.stackCount), out var shoulderedItem, true);

                if (shoulderedItem == null)
                {
                    Log.Message($"COULD NOT SHOULDER THE INITIAL ITEM FOR SOME REAONS {this.pawn} {item}");
                }

                
                var shoulderedCount = pawn.carryTracker.CarriedThing.stackCount;

                /* if shoulder count is under desired then keep looking at inventory to pull upto desired count*/
                var c = 0;
                while (shoulderedCount < desiredShoulderCount)
                {
                    c++;
                    if (c>1000)
                    {
                        Log.Message("TERMINATE LOOP!");
                        break;
                    }
                    var stillNeed = desiredShoulderCount - shoulderedCount;
                    var anotherInventoryItem = pawn.inventory.innerContainer.FirstOrDefault(v =>v.CanStackWith(item) && v.GetHaulInventoryComp().Hauling);

                    /*no more same item in inventory*/
                    if (anotherInventoryItem==null)
                    {
                        break;
                    }
                    
                    Log.Message($"----inventory item: {anotherInventoryItem} we still need: {stillNeed}");
                    
                    pawn.inventory.innerContainer.TryTransferToContainer(anotherInventoryItem, pawn.carryTracker.innerContainer,
                        desiredShoulderCount - shoulderedCount, out var attemptedShoulderedItem, true);

                    /*could not pull out new item out of inventory for some reason*/
                    if (attemptedShoulderedItem == null)
                    {
                        break;
                    }

                    shoulderedCount = pawn.carryTracker.CarriedThing.stackCount;
                }

                shoulderedItem = pawn.carryTracker.CarriedThing;
                shoulderedItem.GetHaulInventoryComp().Hauling = true;

                if (shoulderedItem==null)
                {
                    Log.Message($"NO ITEM ON SHOULDER! {this.pawn} {shoulderedItem}");
                    return;
                }

                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                {
                    Log.Message($"----Pawn {pawn} incapable of hauling, dropping {item}");

                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var __);
                    pawn.jobs.curDriver.JumpToToil(startingToil);
                    return;
                }

                /* At this point we shouldered the item. */
                job.SetTarget(TargetIndex.A, shoulderedItem);
                job.count = shoulderedItem.stackCount;

                pawn.Reserve(shoulderedItem, job, 1, shoulderedCount);

                /*TODO: don't know why this is called */
                //shoulderedItem.SetForbidden(false, false);

            }
        };
        t.defaultCompleteMode = ToilCompleteMode.Instant;
        return t;
    }
    public static Thing ExtractThing(IHaulDestination t, out LocalTargetInfo loc, out int? countNeeded, out int? progressBarDelay)
    {
        Thing thing = null;

        loc = null;
        countNeeded = null;
        progressBarDelay = null;

        if (t is null)
            return null;

        if (t is CriticalThingHaulDestination wrapper)
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

                    var destinationThing = ExtractThing(destination, out var _, out countNeeded, out progressBarDelay);
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
