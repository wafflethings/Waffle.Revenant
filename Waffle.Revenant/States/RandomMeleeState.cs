using System.Collections;
using UnityEngine;

namespace Waffle.Revenant.States
{
    public class RandomMeleeState : PassiveState
    {
        public bool AttackDone = false;
        private Coroutine _currentAttack;
        private bool _fromCombo;

        public RandomMeleeState(Revenant revenant, bool originatesFromCombo = false) : base(revenant)
        {
            _fromCombo = originatesFromCombo;
        }

        public override void Begin()
        {
            Revenant.StartCoroutine(DoStuff());
        }

        public IEnumerator DoStuff()
        {
            yield return null; //not my greatest code, because Begin runs in the revstate cctor, _fromCombo is set after this runs

            if (!_fromCombo)
            {
                JumpscareCanvas.Instance.FlashImage(Revenant);
                yield return new WaitForSeconds(Random.Range(0.2f, 0.4f) / Revenant.SpeedMultiplier);

                float mult = 6;
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

                if (Physics.Raycast(NewMovement.Instance.transform.position, addedDirection, out RaycastHit hit, mult))
                {
                    Revenant.transform.position = hit.point;
                }
                else
                {
                    Revenant.transform.position = NewMovement.Instance.transform.position + addedDirection;
                }
            }

            Revenant.transform.position += Revenant.transform.forward * 2;
            IEnumerator chosenAttack = null;

            switch (Random.Range(0, 3))
            {
                case 0:
                    chosenAttack = Revenant.Melee1();
                    break;

                case 1:
                    chosenAttack = Revenant.Melee2();
                    break;

                case 2:
                    chosenAttack = Revenant.Melee3();
                    break;
            }

            Revenant.InstantLookAtPlayer();
            _currentAttack = Revenant.StartCoroutine(chosenAttack);
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
