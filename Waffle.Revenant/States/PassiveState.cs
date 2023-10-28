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
            CheckDirection();
            Revenant.StartCoroutine(EndInTime());
        }

        public IEnumerator EndInTime()
        {
            yield return new WaitForSeconds(Random.Range(1f, 3f) / Revenant.SpeedMultiplier);
            End();
        }

        public override void End()
        {
            Revenant.Machine.anim.SetBool("Float Left", false);
            Revenant.Machine.anim.SetBool("Float Right", false);

            base.End();
        }

        public override void Update()
        {
            CheckDirection();

            Quaternion quaternion = Quaternion.LookRotation(NewMovement.Instance.transform.position - Revenant.transform.position, Vector3.up);
            Revenant.transform.rotation = Quaternion.RotateTowards(Revenant.transform.rotation, quaternion, Time.deltaTime * (10f * Quaternion.Angle(quaternion, Revenant.transform.rotation) + 2f) * Revenant.SpeedMultiplier);
            Revenant.Machine.rb.MovePosition(Revenant.transform.position + Revenant.transform.right * (GoingRight ? 1 : -1) * 5f * Time.deltaTime * Revenant.SpeedMultiplier);
        }

        public void CheckDirection()
        {
            if (Physics.Raycast(Revenant.transform.position, (GoingRight ? -1 : 1) * Revenant.transform.right, out RaycastHit hit, 2f, LayerMaskDefaults.Get(LMD.Environment)))
            {
                GoingRight = !GoingRight;
            }

            Revenant.Machine.anim.SetBool("Float Left", !GoingRight);
            Revenant.Machine.anim.SetBool("Float Right", GoingRight);
        }
    }
}
