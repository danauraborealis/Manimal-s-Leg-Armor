using System;
using UnityEngine;

namespace Manimal.LegArmor.Patches
{
    // attached at runtime to a VLG-managed corpse-loot slot. each LateUpdate,
    // applies a Y offset on top of the VLG-computed natural position. detects
    // reflow (search reveal, drag/drop) by noticing when current Y diverges
    // from what we last wrote, then updates the baseline.
    //
    // BUT: ignores the drift if it looks like vanilla just undid our offset
    // (current ~= lastWritten - offset). that pattern means VLG is re-running
    // every frame and pulling us back to natural; without this check we'd
    // keep treating each undo as a new baseline and re-add the offset on top,
    // compounding the position off-screen.
    public class CorpseSlotOffsetter : MonoBehaviour
    {
        public Func<float> OffsetFn;

        private RectTransform _rt;
        private float _lastWrittenY = float.NaN;
        private float _baselineY = float.NaN;

        private void Awake()
        {
            _rt = transform as RectTransform;
        }

        private void LateUpdate()
        {
            if (_rt == null || OffsetFn == null) return;

            var currentY = _rt.anchoredPosition.y;
            var offset = OffsetFn();

            var driftIsUndo = !float.IsNaN(_lastWrittenY)
                && Mathf.Abs(currentY - (_lastWrittenY - offset)) < 1f;

            if (float.IsNaN(_lastWrittenY) || (!driftIsUndo && Mathf.Abs(currentY - _lastWrittenY) > 0.1f))
                _baselineY = currentY;

            var targetY = _baselineY + offset;
            if (Mathf.Abs(targetY - currentY) > 0.1f)
            {
                _rt.anchoredPosition = new Vector2(_rt.anchoredPosition.x, targetY);
                _lastWrittenY = targetY;
            }
            else
            {
                _lastWrittenY = currentY;
            }
        }
    }
}
