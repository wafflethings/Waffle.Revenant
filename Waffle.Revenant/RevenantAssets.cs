using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Waffle.Revenant
{
    public class RevenantAssets : MonoBehaviour
    {
        public static RevenantAssets Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new Exception("You have not placed the RevenantAssets singleton in your scene. Please place it.");
                }

                return _instance;
            }
        }

        private static RevenantAssets _instance;

        public GameObject TemplateJumpscareCanvas;
        public Material NoiseMaterial;

        public void Start()
        {
            _instance = this;
        }
    }
}
