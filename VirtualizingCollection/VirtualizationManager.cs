﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlphaChiTech.Virtualization.Actions;
using AlphaChiTech.VirtualizingCollection.Interfaces;

namespace AlphaChiTech.VirtualizingCollection
{
    public class VirtualizationManager
    {
        private object _ActionLock = new object();

        private List<IVirtualizationAction> _Actions = new List<IVirtualizationAction>();

        bool _Processing;

        private Action<Action> _UIThreadExcecuteAction;

        public static VirtualizationManager Instance { get; } = new VirtualizationManager();

        public static bool IsInitialized { get; private set; }

        public Action<Action> UIThreadExcecuteAction
        {
            get { return this._UIThreadExcecuteAction; }
            set
            {
                this._UIThreadExcecuteAction = value;
                IsInitialized = true;
            }
        }

        public void AddAction(IVirtualizationAction action)
        {
            lock (this._ActionLock)
            {
                this._Actions.Add(action);
            }
        }

        public void AddAction(Action action)
        {
            this.AddAction(new ActionVirtualizationWrapper(action));
        }

        public void ProcessActions()
        {
            if (this._Processing) return;

            this._Processing = true;

            List<IVirtualizationAction> lst;
            lock (this._ActionLock)
            {
                lst = this._Actions.ToList();
            }

            foreach (var action in lst)
            {
                bool bdo = true;

                if (action is IRepeatingVirtualizationAction)
                {
                    bdo = (action as IRepeatingVirtualizationAction).IsDueToRun();
                }

                if (bdo)
                {
                    switch (action.ThreadModel)
                    {
                        case VirtualActionThreadModelEnum.UseUIThread:
                            if (this.UIThreadExcecuteAction == null) // PLV
                                throw new Exception(
                                    "VirtualizationManager isn’t already initialized !  set the VirtualizationManager’s UIThreadExcecuteAction (VirtualizationManager.Instance.UIThreadExcecuteAction = a => Dispatcher.Invoke( a );)");
                            this.UIThreadExcecuteAction.Invoke(() => action.DoAction());
                            break;
                        case VirtualActionThreadModelEnum.Background:
                            Task.Run(() => action.DoAction());
                            break;
                    }

                    if (action is IRepeatingVirtualizationAction)
                    {
                        if (!(action as IRepeatingVirtualizationAction).KeepInActionsList())
                        {
                            lock (this._ActionLock)
                            {
                                this._Actions.Remove(action);
                            }
                        }
                    }
                    else
                    {
                        lock (this._ActionLock)
                        {
                            this._Actions.Remove(action);
                        }
                    }
                }
            }

            this._Processing = false;
        }

        public void RunOnUI(IVirtualizationAction action)
        {
            if (this.UIThreadExcecuteAction == null) // PLV
                throw new Exception(
                    "VirtualizationManager isn’t already initialized !  set the VirtualizationManager’s UIThreadExcecuteAction (VirtualizationManager.Instance.UIThreadExcecuteAction = a => Dispatcher.Invoke( a );)");
            this.UIThreadExcecuteAction.Invoke(() => action.DoAction());
        }

        public void RunOnUI(Action action)
        {
            this.RunOnUI(new ActionVirtualizationWrapper(action));
        }
    }
}