using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteTech.Transmitter
{
    public class RemoteTechTransmitterParams : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomStringParameterUI("", autoPersistance = false, lines = 3)]
        public string description = "To use stock antennas, delete 'RemoteTech-Transmitter' from RemoteTech's main folder";
        
        [GameParameters.CustomFloatParameterUI("Consumption Multiplier: ", toolTip = "If set to a value other than 1, the power consumption of all antennas will be increased or decreased by this factor.\nDoes not affect energy consumption for science transmissions.", minValue = 0f, maxValue = 2f, stepCount = 10, displayFormat = "F2")]
        public float ConsumptionMultiplier = 1f;

        [GameParameters.CustomFloatParameterUI("Multiple Antenna Multiplier", toolTip = "Multiple omnidirectional antennas on the same craft work together.\nThe default value of 0 means this is disabled.\nThe largest value of 1.0 sums the range of all omnidirectional antennas to provide a greater effective range.\nThe effective range scales linearly and this option works with both the Standard and Root range models.", minValue = 0f, maxValue = 1f, stepCount = 10, displayFormat = "F2")]
        public float MultipleAntennaMultiplier = 0f;


        public override string DisplaySection
        {
            get
            {
                return "RemoteTech";
            }
        }

        public override GameParameters.GameMode GameMode
        {
            get
            {
                return GameParameters.GameMode.ANY;
            }
        }

        public override bool HasPresets
        {
            get
            {
                return false;
            }
        }

        public override string Section
        {
            get
            {
                return "RemoteTech";
            }
        }

        public override int SectionOrder
        {
            get
            {
                return 2;
            }
        }

        public override string Title
        {
            get
            {
                return "Advanced Antennas";
            }
        }
    }
}
