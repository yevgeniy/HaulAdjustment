using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PickUpAndHaul
{
    public class InMapTargeter : BaseTargeter
    {
        private Action<LocalTargetInfo> action;
        private TargetingParameters targetParams;
        private Map map;

        public static InMapTargeter Instance { get; private set; }

        public override bool IsTargeting => action != null;

        public static void BeginTargeting(TargetingParameters targetParams, Action<LocalTargetInfo> action, Thing master, Action actionWhenFinished = null, Texture2D mouseAttachment = null)
        {
            Instance.action = action;
            Instance.targetParams = targetParams;
            Instance.thing = master;
            Instance.actionWhenFinished = actionWhenFinished;
            Instance.mouseAttachment = mouseAttachment;
            Instance.map = master.Map;
        }

        public override void StopTargeting()
        {
            if (actionWhenFinished != null)
            {
                Action action = actionWhenFinished;
                actionWhenFinished = null;
                action();
            }
            action = null;
        }

        public override void ProcessInputEvents()
        {
            ConfirmStillValid();
            if (IsTargeting)
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    Event.current.Use();
                    if (action != null)
                    {
                        
                        LocalTargetInfo obj = CurrentTargetUnderMouse();
                        if (obj.Cell.InBounds(map))
                        {
                            action(obj);
                            SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        }
                        else
                        {
                            SoundDefOf.ClickReject.PlayOneShotOnCamera(null);
                        }
                    }

                }
                if ((Event.current.type == EventType.MouseDown && Event.current.button == 1) || KeyBindingDefOf.Cancel.KeyDownEvent)
                {
                    StopTargeting();
                    SoundDefOf.CancelMode.PlayOneShotOnCamera(null);
                    Event.current.Use();
                }
            }
        }

        public override void TargeterOnGUI()
        {
            if (action != null)
            {
                Texture2D icon = mouseAttachment ?? TexCommand.Attack;
                if (icon)
                {
                    GenUI.DrawMouseAttachment(icon);
                }
            }
        }

        public override void TargeterUpdate()
        {
            if (IsTargeting)
            {
                LocalTargetInfo target = CurrentTargetUnderMouse();
                SimpleColor lineColor = SimpleColor.Red;
                if (CurrentTargetUnderMouse() is { } obj && obj.Cell.InBounds(map))
                {
                    lineColor = SimpleColor.White;
                }
                Vector3 linePos = UI.MouseMapPosition();
                if (target.IsValid)
                {
                    GenDraw.DrawTargetHighlight(target);
                    linePos = target.CenterVector3;
                }
                GenDraw.DrawLineBetween(thing.DrawPos, linePos, lineColor);
            }
        }

        private void ConfirmStillValid()
        {
            if (thing is null || thing.Map != Find.CurrentMap || thing.Destroyed || !Find.Selector.IsSelected(thing))
            {
                StopTargeting();
            }
        }

        protected override LocalTargetInfo CurrentTargetUnderMouse()
        {
            if (!IsTargeting)
            {
                return LocalTargetInfo.Invalid;
            }
            return GenUI.TargetsAtMouse(targetParams).FirstOrFallback(LocalTargetInfo.Invalid);
        }



        public override void PostInit()
        {
            Instance = this;
        }
    }

    public abstract class BaseTargeter
    {
        protected Thing thing;
        protected Action actionWhenFinished;
        protected Texture2D mouseAttachment;

        public abstract bool IsTargeting { get; }

        public abstract void StopTargeting();

        public abstract void ProcessInputEvents();

        public abstract void TargeterOnGUI();

        public abstract void TargeterUpdate();

        protected virtual LocalTargetInfo CurrentTargetUnderMouse()
        {
            if (!IsTargeting)
            {
                return LocalTargetInfo.Invalid;
            }
            LocalTargetInfo target = Verse.UI.MouseCell();
            return target;
        }

        public virtual void PostInit()
        {
        }
    }

    [StaticConstructorOnStartup]
    public static class Targeters
    {
        private static readonly List<BaseTargeter> targeters = new List<BaseTargeter>();
        //private static readonly List<BaseWorldTargeter> worldTargeters = new List<BaseWorldTargeter>();

        public static BaseTargeter CurrentTargeter { get; private set; }
        //public static BaseWorldTargeter CurrentWorldTargeter { get; private set; }

        static Targeters()
        {
            foreach (Type type in typeof(BaseTargeter).InstantiableDescendantsAndSelf())
            {
                BaseTargeter targeter = (BaseTargeter)Activator.CreateInstance(type, null);
                targeters.Add(targeter);
                targeter.PostInit();
            }
            //foreach (Type type in typeof(BaseWorldTargeter).InstantiableDescendantsAndSelf())
            //{
            //    BaseWorldTargeter targeter = (BaseWorldTargeter)Activator.CreateInstance(type, null);
            //    worldTargeters.Add(targeter);
            //    targeter.PostInit();
            //}
        }

        /* ------ Map Targeters ------ */
        internal static void StopAllTargeters()
        {
            foreach (BaseTargeter targeter in targeters)
            {
                if (targeter.IsTargeting)
                {
                    targeter.StopTargeting();
                }
            }
        }

        internal static void OnGUITargeters()
        {
            foreach (BaseTargeter targeter in targeters)
            {
                if (targeter.IsTargeting)
                {
                    targeter.TargeterOnGUI();
                }
            }
        }

        internal static void UpdateTargeters()
        {
            foreach (BaseTargeter targeter in targeters)
            {
                if (targeter.IsTargeting)
                {
                    targeter.TargeterUpdate();
                }
            }
        }

        internal static void ProcessTargeterInputEvents()
        {
            foreach (BaseTargeter targeter in targeters)
            {
                if (targeter.IsTargeting)
                {
                    targeter.ProcessInputEvents();
                }
            }
        }
        /* --------------------------- */

        /* ----- World Targeters ----- */
        //internal static void StopAllWorldTargeters()
        //{
        //    foreach (BaseWorldTargeter targeter in worldTargeters)
        //    {
        //        if (targeter.IsTargeting)
        //        {
        //            targeter.StopTargeting();
        //        }
        //    }
        //}

        //internal static void OnGUIWorldTargeters()
        //{
        //    foreach (BaseWorldTargeter targeter in worldTargeters)
        //    {
        //        if (targeter.IsTargeting)
        //        {
        //            targeter.TargeterOnGUI();
        //        }
        //    }
        //}

        //internal static void UpdateWorldTargeters()
        //{
        //    foreach (BaseWorldTargeter targeter in worldTargeters)
        //    {
        //        if (targeter.IsTargeting)
        //        {
        //            targeter.TargeterUpdate();
        //        }
        //    }
        //}

        //internal static void ProcessWorldTargeterInputEvents()
        //{
        //    foreach (BaseWorldTargeter targeter in worldTargeters)
        //    {
        //        if (targeter.IsTargeting)
        //        {
        //            targeter.ProcessInputEvents();
        //        }
        //    }
        //}
        /* --------------------------- */
    }

}
