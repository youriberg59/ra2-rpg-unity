using System;
using UnityEngine;

namespace RA2RPG.RA2
{
    [Serializable]
    public struct RA2SequenceRange
    {
        public int Start;
        public int Frames;
        public int FacingStride;
        public bool IsValid => Start >= 0 && Frames > 0;
    }

    public sealed class RA2InfantryAnimationData : MonoBehaviour
    {
        public string SequenceId;
        public RA2SequenceRange Ready;
        public RA2SequenceRange Walk;
        public RA2SequenceRange FireUp;
        public RA2SequenceRange FireProne;
        public RA2SequenceRange Die1;
        public RA2SequenceRange Die2;
        public RA2SequenceRange Idle1;
        public RA2SequenceRange Idle2;
    }
}
