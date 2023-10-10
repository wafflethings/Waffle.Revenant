using System.Collections;
using UnityEngine;

namespace Waffle.Revenant.States
{
    public class PassiveState : RevenantState
    {
        public bool GoingRight;

        public PassiveState(Revenant revenant) : base(revenant)
        {
        }

        public override void Begin()
        {
            GoingRight = Random.value >= 0.5f;

            Revenant.Machine.anim.SetBool("Going Left", !GoingRight);
            Revenant.Machine.anim.SetBool("Going Right", GoingRight);

            Revenant.StartCoroutine(EndInTime());
        }

        public IEnumerator EndInTime()
        {
            yield return new WaitForSeconds(Random.Range(1f, 3f));
            End();
        }

        public override void End()
        {
            Revenant.Machine.anim.SetBool("Going Left", false);
            Revenant.Machine.anim.SetBool("Going Right", false);

            base.End();
        }

        public override void Update()
        {
            Quaternion quaternion = Quaternion.LookRotation(NewMovement.Instance.transform.position - Revenant.transform.position, Vector3.up);
            Revenant.transform.rotation = Quaternion.RotateTowards(Revenant.transform.rotation, quaternion, Time.deltaTime * (10f * Quaternion.Angle(quaternion, Revenant.transform.rotation) + 2f) * Revenant.Machine.eid.totalSpeedModifier);
            Revenant.Machine.rb.MovePosition(Revenant.transform.position + Revenant.transform.right * (GoingRight ? 1 : -1) * 5f * Time.deltaTime * Revenant.Machine.eid.totalSpeedModifier);
        }
    }
}
