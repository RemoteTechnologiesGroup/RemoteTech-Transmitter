using System;
using System.Collections.Generic;
using UnityEngine;

namespace RemoteTech.Transmitter.ShannonHartley
{
    public class RadioBand
    {
        [Persistent] public short Index;
        [Persistent] public string Name;
        [Persistent] public Color MarkColor;
        [Persistent] public short StartFrequencyGHz;
        [Persistent] public short EndFrequencyGHz;
        [Persistent] public short BandwidthGHz;
        [Persistent] public double AtmosphericLossMultiplier;
        [Persistent] public string Remark;

        /// <summary>
        /// Empty constructor for ConfigNode.LoadObjectFromConfig
        /// </summary>
        public RadioBand() { }

        public RadioBand(short index, string name, Color mcolor, short startFreq, short endFreq, short bandwidth, double almult, string remark)
        {
            this.Index = index;
            this.Name = name;
            this.MarkColor = mcolor;
            this.StartFrequencyGHz = startFreq;
            this.EndFrequencyGHz = endFreq;
            this.BandwidthGHz = bandwidth;
            this.AtmosphericLossMultiplier = almult;
            this.Remark = remark;
        }

        public static bool Validate(List<RadioBand> bands)
        {
            throw new NotImplementedException();
        }
    }
}
