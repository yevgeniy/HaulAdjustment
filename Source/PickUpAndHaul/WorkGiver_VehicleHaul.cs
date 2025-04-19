using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PickUpAndHaul
{
    public class WorkGiver_VehicleHaul : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {

            if (CritDestinationsMap.TryGetHaulingVehicle(out var vehicle))
            {
                yield return vehicle;
            }
        }
            

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {

            if (!VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(t.GetType()))
            {
                return false;
            }
            Log.Message($"WorkGiver_VehicleHaul.HasJobOnThing {pawn} {t}");

            var vehicle = t;

            var comp = vehicle.GetHaulInventoryComp();
            if (comp == null)
            {
                Log.Message($"--vehicle does not have hauling comp");
                return false;
            }
            Log.Message($"{comp.VehicleShouldBeUsedToHaul} {comp.VehicleIsBusy}");


            if (!comp.VehicleShouldBeUsedToHaul)
                return false;

            if (comp.VehicleIsBusy)
                return false;

            if (pawn.workSettings != null && pawn.workSettings.GetPriority(WorkTypeDefOf.Hauling) > 0)
            {
                return true;
            }

            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing item, bool forced = false)
        {
            return JobMaker.MakeJob(PickUpAndHaulJobDefOf.HaulAdj_VehicleHaul_Job, pawn, item as Pawn);
        }
    }
}
