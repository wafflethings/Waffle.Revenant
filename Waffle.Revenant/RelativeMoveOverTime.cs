using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Waffle.Revenant
{
    public class RelativeMoveOverTime : MonoBehaviour
    {
        public float Speed;
        public Vector3 TargetPos;

        public void Update()
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, TargetPos, Speed * Time.deltaTime);
        }
    }
}
