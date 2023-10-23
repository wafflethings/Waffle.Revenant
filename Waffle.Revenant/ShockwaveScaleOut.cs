using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Waffle.Revenant
{
    public class ShockwaveScaleOut : MonoBehaviour
    {
        private PhysicalShockwave _shockwave;
        private MeshRenderer _visual;
        private SpriteRenderer _sprite;

        public void Start()
        {
            if (!TryGetComponent(out _shockwave) || !transform.Find("Visual").TryGetComponent(out _visual) || !transform.Find("Shockwave").TryGetComponent(out _sprite))
            {
                Destroy(this);
            }
        }

        public void Update()
        {
            if (transform.localScale.x >= _shockwave.maxSize * 0.75f)
            {
                Color col = _visual.material.GetColor("_Color");
                col = Vector4.MoveTowards(col, new Color(col.r, col.g, col.b, 0), Time.deltaTime * 10);
                _visual.material.SetColor("_Color", col);

                Color spr = _sprite.color;
                _sprite.color = Vector4.MoveTowards(spr, new Color(spr.r, spr.g, spr.b, 0), Time.deltaTime * 5);
            }
        }
    }
}
