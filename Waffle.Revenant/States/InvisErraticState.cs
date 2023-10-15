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
            TeleportAroundPlayer(15);
            Revenant.InstantLookAtPlayer();
            Revenant.ForwardBoost = 40;
        }

        public IEnumerator EndInTime()
        {
            yield return new WaitForSeconds(Random.Range(0.5f, 2f));
            End();
        }

        public void TeleportAroundPlayer(float distance)
        {
            Vector3 spawnDir = Random.onUnitSphere * distance;
            spawnDir.y = 0;
            spawnDir.Normalize();

            Revenant.transform.position = NewMovement.Instance.transform.position + spawnDir;
        }
    }
}
