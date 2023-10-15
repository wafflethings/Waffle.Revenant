using System.Collections;
using UnityEngine;

namespace Waffle.Revenant.States
{
    public class StompState : PassiveState
    {
        public bool AttackDone = false;
        public bool Bounced = false;
        private Coroutine _currentAttack;
        private float _height;

        public StompState(Revenant revenant, float height) : base(revenant)
        {
            _height = height;

            if (_height == 0)
            {
                _height = 25;
            }
        }

        public override void Begin()
        {
            Revenant.StartCoroutine(DoStuff());
        }

        public IEnumerator DoStuff()
        {
            JumpscareCanvas.Instance.FlashImage(Revenant);
            yield return new WaitForSeconds(Random.Range(0.2f, 0.4f));

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

            Revenant.transform.position = NewMovement.Instance.transform.position + addedDirection + Vector3.up * _height;

            Vector3 stompToPosition = NewMovement.Instance.transform.position;
            if (Physics.Raycast(Revenant.transform.position, NewMovement.Instance.transform.position - Revenant.transform.position, out RaycastHit hit, 1000, LayerMaskDefaults.Get(LMD.Environment))) 
            {
                stompToPosition = hit.point;
            }

            _currentAttack = Revenant.StartCoroutine(Revenant.StompAttack(stompToPosition));
            yield return _currentAttack;

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
