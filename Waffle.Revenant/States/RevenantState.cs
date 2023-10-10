using System;
using System.Collections.Generic;
using System.Text;

namespace Waffle.Revenant.States
{
    public class RevenantState
    {
        public Revenant Revenant;
        public bool Complete = false;

        public RevenantState(Revenant revenant)
        {
            Revenant = revenant;
            Begin();
        }

        public virtual void Begin()
        {

        }

        public virtual void End()
        {
            Complete = true;
        }

        public virtual void Update()
        {

        }
    }

    public static class RevenantStateExtensions
    {
        public static bool GetState<T>(this RevenantState state, out T castState) where T : RevenantState
        {
            if (state.GetType() == typeof(T))
            {
                castState = state as T;
                return true;
            }

            castState = null;
            return false;
        }
    }
}
