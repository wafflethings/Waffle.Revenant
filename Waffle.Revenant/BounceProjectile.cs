using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Waffle.Revenant
{
    public class BounceProjectile : MonoBehaviour
    {
        public int AmountOfBounces;
        public GameObject KeepNonRotated;
        private int _timesBounced;

        public bool HasBouncesLeft()
        {
            return _timesBounced++ <= AmountOfBounces;
        }
    }
}
