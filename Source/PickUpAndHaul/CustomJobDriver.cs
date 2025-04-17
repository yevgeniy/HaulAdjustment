using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PickUpAndHaul
{

    public abstract class CustomJobDriver
    {

        public List<CustomToil> toils;
        public CustomToil currentToil;
        public bool done;

        public abstract IEnumerable<CustomToil> MakeNewToils();
        public abstract bool TryMakePreToilReservations();


        public bool TryActivate()
        {
            var madeReservations = TryMakePreToilReservations();

            if (!madeReservations)
                return false;

            this.toils = MakeNewToils().ToList();
            this.currentToil = this.toils.First();
            this.currentToil.needInit = true;

            return true;
        }        

        public void Tick()
        {
            var shouldFail = this.failConditions.Any(v => v());
            if (shouldFail)
            {
                EndJob();
                return;
            }

            if (this.currentToil.needInit && this.currentToil.initAction!=null)
            {
                this.currentToil.initAction();
                this.currentToil.needInit = false;
            }
            else
            {
                if (this.currentToil.tickAction != null)
                    this.currentToil.tickAction();
            }

            if (this.currentToil.isComplete)
            {
                var valid= TrySetNextToil();
                if (!valid)
                {
                    EndJob();
                }

            }

        }

        private bool TrySetNextToil()
        {
            if (this.toils.Last()==this.currentToil)
            {
                return false;
            }

            var curindex = this.toils.FindIndex(v=>v==this.currentToil);
            if (curindex==-1)
            {
                Log.Message("COULD NOT FIND CURREN TOIL");
                return false;
            }

            curindex++;
            this.currentToil = this.toils[curindex];
            this.currentToil.needInit = true;
            this.currentToil.isComplete = false;

            return true;
        }
        public void SetNextToil(CustomToil toil)
        {
            this.currentToil = toil;
            this.currentToil.needInit = true;
            this.currentToil.isComplete = false;
        }

        List<Action> finishActions = new List<Action>();
        public void AddFinishAction(Action c)
        {
            this.finishActions.Add(c);
        }

        List<Func<bool>> failConditions = new List<Func<bool>>();
        public void AddFailCondition(Func<bool> c)
        {
            this.failConditions.Add(c);
        }

        
        public void ReadyForNextToil()
        {
            if (this.currentToil!=null)
                this.currentToil.isComplete = true;
        }

        public void EndJob()
        {
            this.done = true;
            this.finishActions.ForEach(v => v());
        }

    }
    public class CustomToil
    {
        public CustomJobDriver driver;
        public bool needInit;
        public Action initAction;
        public Action tickAction;
        public bool isComplete;
    }
}
