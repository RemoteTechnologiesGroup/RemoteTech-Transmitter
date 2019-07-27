namespace RemoteTech.Transmitter.ShannonHartley
{
    public class CelestialBody
    {
        [Persistent] public string Name;
        [Persistent] public double Temperature;
        [Persistent] public double Emissivity;

        /// <summary>
        /// Empty constructor for ConfigNode.LoadObjectFromConfig
        /// </summary>
        public CelestialBody() { }

        public CelestialBody(string name, double temp, double emiss)
        {
            this.Name = name;
            this.Temperature = temp;
            this.Emissivity = emiss;
        }

    }
}
