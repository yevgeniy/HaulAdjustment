using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PickUpAndHaul
{
    [StaticConstructorOnStartup]
    public class ConstructMap : MapComponent
    {

        //IConstructible c
        public static HashSet<Thing> Constructibles = new HashSet<Thing>();

        static ConstructMap()
        {

        }

        public ConstructMap(Map map) : base(map)
        {
        }



        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                Log.Message("LENGTH OF CONSTRUCTS: " + Constructibles.Count());
                Constructibles.RemoveWhere(v => v == null || !v.Spawned);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref Constructibles, "nimm-constructables", LookMode.Reference);

            if (Constructibles == null)
                Constructibles = new HashSet<Thing>();
        }


        public static IHaulDestination GetLocationOfMatchingConstructable(Pawn pawn, Thing thing)
        {
            if (pawn == null)
                return null;

            foreach (var i in Constructibles)
            {
                if (i == null)
                    continue;


                if (!i.Spawned)
                {
                    continue;
                }

                if (i.Faction != pawn.Faction)
                {
                    continue;
                }

                var project = i as IConstructible;

                if (project == null)
                    continue;

                var numberOfThisThingNeeded = project.ThingCountNeeded(thing.def);
                if (numberOfThisThingNeeded > 0)
                {
                    return new ThingHaulDestination(i);
                }
            }

            return null;
        }

    }


    public class ThingHaulDestination : IHaulDestination
    {
        private Thing _thing;

        public ThingHaulDestination(Thing t)
        {
            _thing = t;
        }

        public Thing Thing => _thing;

        public static explicit operator Thing(ThingHaulDestination haulableDestination)
        {
            return haulableDestination.Thing;
        }

        public IntVec3 Position => Thing.Position;

        public Map Map => Thing.Map;

        public bool StorageTabVisible => true;

        public bool Accepts(Thing t)
        {
            var project = Thing as IConstructible;

            if (project == null)
                return false;

            var numberOfThisThingNeeded = project.ThingCountNeeded(t.def);
            if (numberOfThisThingNeeded > 0)
            {
                return true;
            }

            return false;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return null;
        }

        public StorageSettings GetStoreSettings()
        {
            return new StorageSettings
            {
                Priority = StoragePriority.Critical
            };

        }

        public void Notify_SettingsChanged()
        {

        }
    }

    public class ConstructableThingOwner : ThingOwner
    {
        public ConstructableThingOwner(Thing t)
        {
            Thing = t;
        }

        public override int Count => throw new NotImplementedException();

        public Thing Thing { get; set; }

        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
        {
            var project = Thing as IConstructible;

            var numberOfThisThingNeeded = project.ThingCountNeeded(item.def);
            return numberOfThisThingNeeded;
        }

        public override int IndexOf(Thing item)
        {
            throw new NotImplementedException();
        }

        public override bool Remove(Thing item)
        {
            throw new NotImplementedException();
        }

        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
        {
            throw new NotImplementedException();
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            throw new NotImplementedException();
        }

        public override Thing GetAt(int index)
        {
            throw new NotImplementedException();
        }
    }

}
