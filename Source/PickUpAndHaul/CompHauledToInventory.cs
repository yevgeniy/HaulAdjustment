using System.Linq;
using Verse.AI;
using static Unity.Burst.Intrinsics.X86.Avx;

namespace PickUpAndHaul;

[StaticConstructorOnStartup]
public class CompHauledToInventory : ThingComp
{

    static CompHauledToInventory()
    {
        var defs = DefDatabase<ThingDef>.AllDefs;
        foreach (var i in defs)
        {
            i.comps.Add(new CompProperties
            {
                compClass = typeof(CompHauledToInventory)
            });
        }
    }
    private bool hauling;
    public Pawn Vehicle => this.parent as Pawn;

    public bool Hauling
    {
        get
        {
            return this.hauling;
        }
        set
        {
            this.hauling = value;
        }
    }

    private bool vehicleShouldBeUsedToHaul;
    public bool VehicleShouldBeUsedToHaul
    {
        get
        {
            return this.vehicleShouldBeUsedToHaul;
        }
        set
        {
            this.vehicleShouldBeUsedToHaul = value;
            

            CritDestinationsMap.UseVehicleToHaul[this] = value;
        }
    }
    public void ActivateLocalHaul(IntVec3 from, IntVec3 to)
    {
        Messages.Message($"SETTING HAUL FROM LOCATION {from}", MessageTypeDefOf.NeutralEvent);
        VehicleIsBusy = false;
        this.fromLocation = from;
        this.toLocation = to;
        VehicleShouldBeUsedToHaul = true;
    }

    private bool vehicleIsBusy;
    public bool VehicleIsBusy
    {
        get
        {
            return this.vehicleIsBusy;
        }
        set
        {
            this.vehicleIsBusy = value;
        }
    }

    


    private bool allPacked;
    public bool AllPacked
    {
        get
        {
            return this.allPacked;
        }
        set
        {
            this.allPacked = value;
        }
    }
    
    private IntVec3 fromLocation;
    private IntVec3 toLocation;

    public IntVec3 FromLocation { get
        {
            return this.fromLocation;
        } }

    public IntVec3 ToLocation
    {
        get
        {
            return this.toLocation;
        }
    }

    



    public void CustomTick()
    {

    }
    public bool HasPotentialWork()
    {

        if (this.parent == null)
        {
            Log.Message($"NO PARENT ON COMP");
            return false;
        }

        if (this.parent is Pawn pawn)
        {
            var validFrom = this.fromLocation != default(IntVec3);
            var validTo = Utils.TryFindValidToLocation(this.parent as Pawn, out var _, out var __);

            Log.Message($"---is busy? {VehicleIsBusy}");
            return false == VehicleIsBusy
                && validFrom
                && validTo;
        }

        Log.Message($"NOT A VEHICLE {this.parent}");
        return false;

    }

    public override void PostExposeData()
    {

        base.PostExposeData();
        Scribe_Values.Look(ref hauling, "comp-hauled-toinv-defs");
        Scribe_Values.Look(ref vehicleShouldBeUsedToHaul, "comp-vehicleShouldBeUsedToHaul");
        Scribe_Values.Look(ref vehicleIsBusy, "comp-vehicleIsBusy");
        Scribe_Values.Look(ref allPacked, "comp-allPacked");
        Scribe_Values.Look(ref fromLocation, "comp-fromCell");
        Scribe_Values.Look(ref toLocation, "comp-toLocation");
        
        if (vehicleShouldBeUsedToHaul)
            CritDestinationsMap.UseVehicleToHaul[this] = vehicleShouldBeUsedToHaul;
    }

    public bool TrySetToLocation(IntVec3 to)
    {
        if (!Utils.CanGetToCell(this.parent as Pawn, to))
        {
            return false;
        }

        this.toLocation = to;
        return true;
    }
}