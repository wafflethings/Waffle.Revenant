using System.Collections;
using UnityEngine;

namespace Waffle.Revenant.States
{
    public class StompState : PassiveState
    {
        public bool AttackDone = false;
        public bool Bounced = false;
        public bool ShouldCombo = false;
        private Coroutine _currentAttack;
        private Vector3 _startPos;
        private float _startAnimSpeed;

        public StompState(Revenant revenant, Vector3 startPos) : base(revenant)
        {
            _startPos = startPos;
        }

        public override void Begin()
        {
            Revenant.StartCoroutine(DoStuff());
        }

        public IEnumerator DoStuff()
        {
            JumpscareCanvas.Instance.FlashImage(Revenant);
            yield return new WaitForSeconds(Random.Range(0.2f, 0.4f) / Revenant.SpeedMultiplier);

            float mult = 5;
            Vector3 addedDirection = new();

            switch (Random.Range(0, 3))
            {
                case 0:
                    addedDirection = NewMovement.Instance.transform.forward * mult;
                    break;
                case 1:
                    addedDirection = -NewMovement.Instance.transform.forward * mult;
                    break;
                case 2:
                    addedDirection = -NewMovement.Instance.transform.right * mult;
                    break;
                case 3:
                    addedDirection = NewMovement.Instance.transform.right * mult;
                    break;
            }

            if (Physics.Raycast(Revenant.transform.position, addedDirection, mult, LayerMaskDefaults.Get(LMD.Environment)))
            {
                //if it's in a wall, it will only go to the directly above the player (which is already checked to be good in CheckForStomp)
                addedDirection = Vector3.zero;
            }

            Revenant.transform.position = _startPos + addedDirection - Vector3.up * 5;

            Vector3 stompToPosition = NewMovement.Instance.transform.position;
            if (Physics.Raycast(Revenant.transform.position, NewMovement.Instance.transform.position - Revenant.transform.position, out RaycastHit hit, 1000, LayerMaskDefaults.Get(LMD.Environment))) 
            {
                stompToPosition = hit.point;
            }

            _startAnimSpeed = Revenant.Machine.anim.speed;
            _currentAttack = Revenant.StartCoroutine(Revenant.StompAttack(stompToPosition));
            yield return _currentAttack;

            Revenant.ResetRotation = true;

            float rand = Random.value;
            if (!Revenant.Enraged)
            {
                if (rand < 0.75f)
                {
                    yield return new WaitForSeconds(0.5f / Revenant.SpeedMultiplier);
                    ShouldCombo = true;
                }
                else
                {
                    yield return new WaitForSeconds(0.5f / Revenant.SpeedMultiplier);
                    yield return Revenant.StartCoroutine(Revenant.GoInvisible(0, true));
                }
            }
            else
            {
                ShouldCombo = true;
            }

            Revenant.ResetRotation = false;
            Revenant.ResetXRotation();
            End();
        }

        public IEnumerator StartSpiralling()
        {
            Revenant.StopCoroutine(_currentAttack);
            Revenant.Machine.anim.speed = _startAnimSpeed;
            Revenant.Machine.anim.SetBool("Stomp First Frame", false);

            Vector3 pos = CameraController.Instance.transform.position + CameraController.Instance.transform.forward * 100;
            bool didHit = false;

            if (Physics.Raycast(CameraController.Instance.transform.position, CameraController.Instance.transform.forward, out RaycastHit hit, 10000, LayerMaskDefaults.Get(LMD.Environment)))
            {
                pos = hit.point;
                didHit = true;
            }

            yield return Revenant.StartCoroutine(Revenant.StompFall(pos, 1, false, true, didHit));

            if (didHit)
            {
                Revenant.Machine.eid.hitter = "punch";
                Revenant.Machine.eid.DeliverDamage(Revenant.gameObject, Vector3.zero, Vector3.zero, 4, false);
                Object.Instantiate(Revenant.Hole, Revenant.transform.position + Revenant.transform.up * 2, Revenant.transform.rotation);
            }

            yield return new WaitForSeconds(2.5f / Revenant.SpeedMultiplier);
            yield return Revenant.GoInvisible(0, false);

            Revenant.ResetRotation = false;
            Revenant.ResetXRotation();
            End();
        }

        public override void End()
        {
            Revenant.StopCoroutine(_currentAttack);
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
