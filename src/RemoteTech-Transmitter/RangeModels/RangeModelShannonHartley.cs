using CommNet;
using RemoteTech.Common.RemoteTechCommNet;
using System;

namespace RemoteTech.RangeModel
{
    public class RangeModelShannonHartley : IRangeModel
    {
        public double GetMaximumRange(double aPower, double bPower)
        {
            throw new NotImplementedException();
        }

        public double GetNormalizedRange(double aPower, double bPower, double distance)
        {
            throw new NotImplementedException();
        }

        public bool InRange(double aPower, double bPower, double sqrDistance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// bits per second
        /// </summary>
        public static ulong CalculateDataRate(RemoteTechCommNetVessel v1, RemoteTechCommNetVessel v2)
        {
            throw new NotImplementedException();
        }
    }
}
