using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Waffle.Revenant
{
    public class CallOnOtherObjectsDestroyed : MonoBehaviour
    {
        public GameObject[] DependantObjects;
        public UltrakillEvent Event;

        public void Update()
        {
            bool hasOnlyNull = true;

            foreach (GameObject gameObject in DependantObjects)
            {
                hasOnlyNull = hasOnlyNull && gameObject == null;
            }

            if (hasOnlyNull)
            {
                Event.Invoke();
            }
        }
    }
}
