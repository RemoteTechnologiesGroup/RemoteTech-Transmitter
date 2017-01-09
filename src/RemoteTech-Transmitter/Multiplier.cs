using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteTech.Transmitter
{
    public class Multiplier
    {
        public string Name
        {
            get;
            private set;
        }
        public double Value
        {
            get;
            set;
        }

        public Multiplier(string name, double value)
        {
            Name = name;
            Value = value;
        }
    }
}
