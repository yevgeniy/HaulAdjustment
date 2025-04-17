using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PickUpAndHaul
{
    public class VehiclePawnProxy
    {
        private readonly Pawn _vehicle;
        

        public VehiclePawnProxy(Pawn vehicle)
        {
            _vehicle = vehicle;
        }

        public Thing Thing { get { return _vehicle; } }

        public Faction Faction { get
            {
                return _vehicle.Faction; 
            }
        }



        public static Assembly[] Assemblies => AppDomain.CurrentDomain.GetAssemblies();
        public static Type VehiclePawnType = Assemblies.SelectMany(v => v.GetTypes()).FirstOrDefault(v => v.Name == "VehiclePawn");
        private PropertyInfo allPawnsAboardPropInfo = VehiclePawnType.GetProperty("AllPawnsAboard", BindingFlags.Public | BindingFlags.Instance);
        private static Type HandlingTypeFlagsType = Assemblies.SelectMany(assembly => assembly.GetTypes())
                    .FirstOrDefault(v => v.Name == "HandlingTypeFlags");
        private static Type VehicleReservationManagerType = Assemblies.SelectMany(assembly => assembly.GetTypes())
                    .FirstOrDefault(v => v.Name == "VehicleReservationManager");


        public List<TransferableOneWay> CargoToLoad
        {
            get
            {
                return ClassMaster.GetValueOnInstance<List<TransferableOneWay>>(Thing, "cargoToLoad");
            }
        }

        public bool CanAccept(Thing thing, out int? count)
        {
            Log.Message("CAN ACCEPT? " + thing + " on: " + Thing);
            count = null;

            var transferable = GetTransferable(CargoToLoad, thing);

            if (transferable != null && transferable.countToTransfer>0)
            { 
                count = transferable.CountToTransfer;
                return true;
            }
            return false;

        }

        public int AddOrTransfer(Thing thing, int count, Pawn carryer=null)
        {
            Log.Message("ATTEMPTING TO ADD: " + thing + " " + count + " to: " + Thing);
            return ClassMaster.Call<int>(
                Thing, 
                "AddOrTransfer", 
                new object[] { thing, count, carryer }, 
                new Type[] { typeof(Thing), typeof(int), typeof(Pawn)}
            );
        }

        public static TransferableOneWay GetTransferable(List<TransferableOneWay> transferables, Thing thing)
        {
            foreach (TransferableOneWay transferable in transferables)
            {
                foreach (Thing transferableThing in transferable.things)
                {
                    if (transferableThing == thing)
                    {
                        return transferable;
                    }
                }
            }
            //Unable to find thing instance, match on def
            foreach (TransferableOneWay transferable in transferables)
            {
                foreach (Thing transferableThing in transferable.things)
                {
                    if (transferableThing.def == thing.def)
                    {
                        return transferable;
                    }
                }
            }
            return null;
        }

        public List<Pawn> AllPawnsAboard
        {
            get
            {
                Log.Message($"----get all pawns aboard {Thing}");
                return allPawnsAboardPropInfo.GetValue(Thing) as List<Pawn>;
                //return ClassMaster.GetValueOnInstance<List<Pawn>>(Thing, "AllPawnsAboard");
            }
        }

        public object NextAvailableHandler()
        {
            Type nullableType = typeof(Nullable<>).MakeGenericType(HandlingTypeFlagsType);

            return ClassMaster.Call<object>(Thing, "NextAvailableHandler",
                new object[] { null, false }, new Type[] { nullableType, typeof(bool) });
        }
        public void PromptToBoardVehicle(Pawn pawn, object handler)
        {
            ClassMaster.Call(
               Thing,
               "PromptToBoardVehicle",
               new object[] { pawn, handler }
            );
        }
        public bool Drafted
        {
            set
            {
                var ignition = ClassMaster.GetValue(Thing, "ignition");
                ClassMaster.SetValue(ignition, "Drafted", true);
            }
        }
        public void DisembarkAll()
        {
            ClassMaster.Call(
               Thing,
               "DisembarkAll",
               new object[] { }
            );
        }

        public void GoTo(IntVec3 targetPosition)
        {
            Drafted = true;

            Job job = new Job(JobDefOf.Goto, targetPosition)
            {
                locomotionUrgency = LocomotionUrgency.Jog,
                expiryInterval = 999999999
            };

            var p = (Thing as Pawn);
            if (p.jobs.curDriver != null)
            {
                p.jobs.curDriver.EndJobWith(JobCondition.InterruptForced);
            }
            p.jobs.StartJob(job);

        }

        public bool FitsOnCell(IntVec3 cell)
        {
            return ClassMaster.CallStatic<bool>(
               "Ext_Vehicles",
               "FitsOnCell",
               new object[] { this.Thing,  cell }
            );

            
        }
        public bool IsOverloaded
        {
            get
            {
                var p = Thing as Pawn;
                return MassUtility.IsOverEncumbered(p);
            }
        }

        public void ScheduleItemsToLoad(List<Thing> items)
        {
            List<TransferableOneWay> transferables = new List<TransferableOneWay>();
            foreach (var i in items)
            {
                AddToTransferables(transferables, i, true);
            }

            ClassMaster.SetValue(Thing, "cargoToLoad", transferables);


            var comp = Thing.Map.GetComponent(VehicleReservationManagerType);
            ClassMaster.Call(comp, "RegisterLister", new object[] { Thing, "LoadVehicle" });


        }

        private static void AddToTransferables(List<TransferableOneWay> transferables, Thing t, bool setToTransferMax = false)
        {
            TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatching(t, transferables, TransferAsOneMode.PodsOrCaravanPacking);
            if (transferableOneWay == null)
            {
                transferableOneWay = new TransferableOneWay();
                transferables.Add(transferableOneWay);
            }
            if (transferableOneWay.things.Contains(t))
            {
                Log.Message("ERROR! Tried to add the same thing twice to TransferableOneWay: " + t);
                return;
            }
            transferableOneWay.things.Add(t);
            if (setToTransferMax)
            {
                transferableOneWay.AdjustTo(transferableOneWay.CountToTransfer + t.stackCount);
            }
        }
    }
}
