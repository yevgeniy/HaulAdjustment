using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PickUpAndHaul;

public class JobDriver_UnloadYourHauledInventory : JobDriver
{
    

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
        yield return Toils_General.Wait(2);
    }


 




}



