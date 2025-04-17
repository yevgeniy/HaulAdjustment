using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Verse;

namespace PickUpAndHaul
{

    public class CustomJobDriver_VehicalHaul : CustomJobDriver
    {
        Pawn worker;
        Pawn vehicle;
        private List<LocalTargetInfo> cells;
        private IntVec3 cell;

        public CustomJobDriver_VehicalHaul(Pawn worker, Pawn vehicle)
        {
            this.worker = worker;
            this.vehicle = vehicle;
        }

        public override bool TryMakePreToilReservations()
        {
            Log.Message($"mark hevicle busy ${this.worker} {this.vehicle}");
            this.vehicle.GetHaulInventoryComp().VehicleIsBusy = true;
            return true;
        }


        public override IEnumerable<CustomToil> MakeNewToils()
        {
            Log.Message($"START DRIVER HAUL {this.worker} {this.vehicle}");

            AddFinishAction(() =>
            {
                Log.Message($"FINISHED HAULING");
                this.vehicle.GetHaulInventoryComp().VehicleIsBusy = false;
            });
            AddFailCondition(() =>
            {
                return this.worker.Drafted || this.worker.Downed;
            });
            AddFailCondition(() =>
            {
                return false == this.vehicle.GetHaulInventoryComp().VehicleShouldBeUsedToHaul;
            });
            
            CustomToil exitVehicleAtEnd = ExitVehice();

            yield return Wait(2);
            
            yield return BoardVehicle();
            yield return TurnOnVehicle();
            yield return DriveToFromZone(exitVehicleAtEnd);
            yield return ExitVehice();
            yield return Wait(2);

            yield return WaitForPacked();

            yield return BoardVehicle();
            yield return DropWeight();
            yield return DriveBack();
            yield return UnloadAll();


            yield return exitVehicleAtEnd;
        }
        private CustomToil UnloadAll()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    Log.Message($"UNLOAD ALL");
                },
                tickAction = () =>
                {
                    var vehicle = this.vehicle;
                    var v = new VehiclePawnProxy(vehicle);

                    var firstItem = vehicle.inventory.innerContainer.FirstOrDefault();
                    if (firstItem != null)
                    {
                        vehicle.inventory.innerContainer.TryDrop(firstItem, ThingPlaceMode.Near, firstItem.stackCount, out var _);
                    }
                    else
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }


        private CustomToil DriveBack()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    Log.Message($"DRIVE BACK");
                    if (!Utils.TryFindValidToZone(out var toZone))
                    {
                        Log.Message($"----cant find valid to zone");
                        EndJob();
                        return;
                    }

                    if (!Utils.TryFindFittingCell(vehicle, toZone, out var cell))
                    {
                        Log.Message($"----cant find a valid cell to fit the vehicle");
                        EndJob();
                        return;
                    }

                    this.cell = cell;

                    Log.Message($"----found valid cell {cell}");
                    var v = new VehiclePawnProxy(vehicle);
                    v.GoTo(cell);
                },
                tickAction = () =>
                {
                    var vehicle = this.vehicle;
                    var destinationCell = this.cell;
                    Log.Message($"----current positions: {vehicle.Position} {destinationCell}");
                    if (vehicle.Position == destinationCell)
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }



        private CustomToil DropWeight()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    Log.Message($"IS VEHICLE READY TO DRIVE BACK");

                },
                tickAction = () =>
                {
                    var vehicle = this.vehicle;
                    var v = new VehiclePawnProxy(vehicle);
                    if (!v.IsOverloaded)
                    {
                        Log.Message($"----weight good.");
                        ReadyForNextToil();
                        return;
                    }

                    var firstItem = vehicle.inventory.innerContainer.First();
                    Log.Message($"----still overweight dropping {firstItem}");

                    vehicle.inventory.innerContainer.TryDrop(firstItem, ThingPlaceMode.Near, firstItem.stackCount, out var _);

                }
            };
        }


        private CustomToil WaitForPacked()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    Log.Message($"WAITING FOR VEHICLE PACKED");

                    this.vehicle.GetHaulInventoryComp().AllPacked = false;

                    var job = new Job_PackHauler();
                    job.targetA = this.vehicle;
                    job.targetQueueB = this.cells;

                    this.worker.jobs.StopAll();
                    this.worker.jobs.StartJob(job);
                },
                tickAction = () =>
                {
                    if (this.vehicle.GetHaulInventoryComp().AllPacked)
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }

        private CustomToil DriveToFromZone(CustomToil ifNoMoreFromZones)
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    Log.Message($"DRIVE TO FROM ZONE");
                    if (!Utils.TryFindValidFromZone(out var fromZone))
                    {
                        Log.Message($"----cant find another from zone");
                        SetNextToil(ifNoMoreFromZones);
                        ReadyForNextToil();

                        return;
                    }

                    this.cells = fromZone.CellsList.Select(v => new LocalTargetInfo(v)).ToList();

                    if (!Utils.TryFindFittingCell(vehicle, fromZone, out var cell))
                    {
                        Log.Message($"----cant find a valid cell to fit the vehicle");
                        SetNextToil(ifNoMoreFromZones);
                        ReadyForNextToil();

                        return;
                    }
                    this.cell = cell;


                    Log.Message($"----found valid cell {cell}");
                    var v = new VehiclePawnProxy(vehicle);
                    v.GoTo(cell);
                },
                tickAction = () =>
                {
                    var vehicle = this.vehicle;
                    var destinationCell = this.cell;
                    Log.Message($"----current positions: {vehicle.Position} {destinationCell}");
                    if (vehicle.Position == destinationCell)
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }
        private CustomToil TurnOnVehicle()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    var v = new VehiclePawnProxy(vehicle);
                    v.Drafted = true;

                    ReadyForNextToil();
                }
            };
            
        }

        private CustomToil ExitVehice()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    var v = new VehiclePawnProxy(vehicle);

                    v.Drafted = false;

                    v.DisembarkAll();

                    ReadyForNextToil();
                }
            };
        }
        private CustomToil BoardVehicle()
        {


            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    var worker = this.worker;
                    Log.Message($"LOAD PAWN IN VEHICLE {worker} {vehicle}");
                    var vehicleProxy = new VehiclePawnProxy(vehicle);

                    var boarded = vehicleProxy.AllPawnsAboard;
                    if (boarded.Count > 0 && boarded.Contains(worker))
                    {
                        Log.Message($"--worker already border boarded");
                        ReadyForNextToil();
                        return;
                    }

                    Log.Message($"--telling pawn {worker} to board");
                    var handler = vehicleProxy.NextAvailableHandler();
                    Log.Message($"----hander {handler}");

                    if (worker.carryTracker.CarriedThing!=null)
                    {
                        worker.carryTracker.TryDropCarriedThing(worker.Position, ThingPlaceMode.Near, out var _);
                    }
                    vehicleProxy.PromptToBoardVehicle(worker, handler);
                },
                tickAction = () =>
                {
                    Log.Message($"waiting");
                    var vehicle = this.vehicle;
                    var worker = this.worker;
                    var vehicleProxy = new VehiclePawnProxy(vehicle);
                    var boarded = vehicleProxy.AllPawnsAboard;
                    if (boarded.Count > 0 && boarded.Contains(worker))
                    {
                        Log.Message($"--worker already border boarded");
                        this.ReadyForNextToil();
                        return;
                    }
                }
            };
        }


        private CustomToil Wait(int v)
        {
            var cur = 0;
            return new CustomToil
            {
                tickAction = () =>
                {
                    cur++;
                    if (cur == v)
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }


    }

    public class JobDriver_PackHauler : JobDriver
    {
        public override string GetReport()
        {
            if (this.job.def==null)
            {
                Log.Message($"SOMEHOW THIS JOB GOT NO DEF");
                this.job.def= new JobDef
                {
                    driverClass = typeof(JobDriver_PackHauler)
                };
            }
            return "Using vehicle to haul.";
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            
            return true;
        }
        public override IEnumerable<Toil> MakeNewToils()
        {
            
            var packStart = Toils_General.Wait(2);
            var packEnd = Toils_General.Wait(2);
            Toil findThingToLoad = FindThingToLoad(packEnd);
            Toil gotoThing = Toils_Goto.Goto(TargetIndex.C, PathEndMode.Touch);
            Toil startHaul = Toils_Haul.StartCarryThing(TargetIndex.C, false, true);
            Toil goToVehicle = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil depositIntoVehicle = DepositIntoVehicle();


            yield return packStart;
            yield return Toils_Jump.JumpIf(packEnd, () => MassUtility.IsOverEncumbered(TargetA.Pawn));
            yield return findThingToLoad;
            yield return gotoThing;
            yield return startHaul;
            yield return goToVehicle;
            yield return CreateTransfereRechord();
            yield return ShowProgressBarToil();
            yield return depositIntoVehicle;
            yield return Toils_Jump.Jump(packStart);
            yield return packEnd;

            yield return Toils_General.Do(() =>
            {
                TargetA.Pawn.GetComp<CompHauledToInventory>().AllPacked = true;
            });
        }

        private Toil ShowProgressBarToil()
        {
            Toil reloadWait = ToilMaker.MakeToil("reload-wait");
            reloadWait.defaultCompleteMode = ToilCompleteMode.Delay;
            reloadWait.defaultDuration = 25;
            reloadWait.WithProgressBarToilDelay(TargetIndex.A);

            return reloadWait;
        }

        private Toil CreateTransfereRechord()
        {
            return Toils_General.Do(() =>
            {
                
                var vehicle = TargetA.Pawn;
                var worker = this.pawn;
                Log.Message($"CREATING RECORD {worker.carryTracker.CarriedThing} {worker.carryTracker.CarriedThing.stackCount}");
                var v = new VehiclePawnProxy(vehicle);
                v.ScheduleItemsToLoad(new List<Thing>() { worker.carryTracker.CarriedThing });

            });
        }

        private Toil FindThingToLoad(Toil noMoreThingsToLoad)
        {
            return Toils_General.Do(() =>
            {
                var cells = this.job.targetQueueB;
                var worker = this.pawn;
                var firstthing = cells.SelectMany(v => v.Cell.GetThingList(worker.Map))
                    .Where(v => 
                        v.def.category==ThingCategory.Item
                        && HaulAIUtility.PawnCanAutomaticallyHaulFast(worker, v, false)
                        && pawn.CanReserve(v)
                        && !v.IsForbidden(worker))
                    .FirstOrDefault();

                if (firstthing == null)
                {
                    SetNextToil(noMoreThingsToLoad);
                    return;
                }

                this.pawn.Reserve(firstthing, this.job);
                this.job.SetTarget(TargetIndex.C, firstthing);
                this.job.count = firstthing.stackCount;

            });
        }

        private static FieldInfo countToTransferFieldInfo = AccessTools.Field(typeof(TransferableOneWay), "countToTransfer");
        private Toil DepositIntoVehicle()
        {

            return new Toil
            {
                initAction = () =>
                {
                    var vehicle = TargetA.Pawn;
                    var worker = this.pawn;
                    Log.Message($"PLACE IN VEHICLE");
                    var item = worker.carryTracker.CarriedThing;
                    Log.Message($"----item {item}");
                    if (item is null || item.stackCount == 0)
                    {
                        Log.Message($"----incompatible {item}");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    else
                    {
                        int stackCount = item.stackCount;
                        var v = new VehiclePawnProxy(vehicle);
                        Log.Message("----ADDING TO VEHICLE " + v.Thing + " " + item + " " + stackCount);
                        
                        int result = v.AddOrTransfer(item, stackCount);
                        TransferableOneWay transferable = VehiclePawnProxy.GetTransferable(v.CargoToLoad, item);
                        if (transferable != null)
                        {
                            int count = transferable.CountToTransfer - stackCount;
                            countToTransferFieldInfo.SetValue(transferable, count);
                            if (transferable.CountToTransfer <= 0)
                            {
                                v.CargoToLoad.Remove(transferable);
                            }


                        }
                    }
                }
            };

        }


    }

    
    
    public class Job_PackHauler : Job
    {
        public Job_PackHauler()
        {
            this.count = 1;
            this.def = new JobDef
            {
                driverClass = typeof(JobDriver_PackHauler)
            };
        }
    }


}
