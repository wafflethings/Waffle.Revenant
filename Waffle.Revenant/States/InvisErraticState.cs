using System.Collections;
using UnityEngine;

namespace Waffle.Revenant.States
{
    public class InvisErraticState : RevenantState
    {
        public float TimeBeforeInvis;
        public float Countdown;

        public InvisErraticState(Revenant revenant) : base(revenant)
        {
        }

        public override void Begin()
        {
            Revenant.StartCoroutine(Revenant.GoInvisible(TimeBeforeInvis, true));
            Revenant.StartCoroutine(EndInTime());
        }

        public IEnumerator EndInTime()
        {
            yield return new WaitForSeconds(Random.Range(1f, 2f));
            End();
        }
    }
}
