using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PickUpAndHaul
{
    public class JobDriver_VehicleHaul : JobDriver
    {
        

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            
            return true;
        }
        public override IEnumerable<Toil> MakeNewToils()
        {
            CritDestinationsMap.StartVehicleHaulJob(TargetB.Pawn, TargetA.Pawn);
            /*This is just to kick off custom job driver for feature*/
            yield return Toils_General.Wait(10);
        }
    }
}
