using System.Collections;
using UnityEngine;

namespace Waffle.Revenant.States
{
    public class ProjectileAttackState : PassiveState
    {
        private Coroutine CurrentAttack;
        public bool AttackDone;
        public GameObject DecoProjectile;

        public ProjectileAttackState(Revenant revenant) : base(revenant)
        {
        }

        public override void Begin()
        {
            Revenant.StartCoroutine(DoStuff());
        }

        public IEnumerator DoStuff()
        {
            CurrentAttack = Revenant.StartCoroutine(Revenant.RangedAttack());
            yield return CurrentAttack;

            End();
        }

        public override void End()
        {
            Revenant.StopCoroutine(CurrentAttack);
            Object.Destroy(DecoProjectile);
            base.End();
        }

        public override void Update()
        {
            if (AttackDone)
            {
                base.Update();
            }
        }
    }
}
