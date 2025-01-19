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

        private Assembly[] assemblies => AppDomain.CurrentDomain.GetAssemblies();

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

        public int AddOrTransfer(Thing thing, int count)
        {
            Log.Message("ATTEMPTING TO ADD: " + thing + " " + count + " to: " + Thing);
            return ClassMaster.Call<int>(
                Thing, 
                "AddOrTransfer", 
                new object[] { thing, count, null }, 
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
    }
}
