﻿//using CommNet;
using RemoteTech.Common;
using RemoteTech.Common.RemoteTechCommNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RemoteTech.Transmitter
{
    /// <summary>
    /// ModuleRTDataTransmitter class based on KSP's ModuleDataTransmitter, whose purpose is to convey a player visual
    /// indications on the progress of a data transmission while delaying the internal delivery of data.
    /// This class is to re-base RemoteTech's own antennas to the stock antenna's functionality, and to create even more
    /// complex antennas.
    /// </summary>

    [KSPModule("Data Transmitter (RT)")]
    public class ModuleRTDataTransmitter : ModuleDataTransmitter
    {
        [KSPField]
        public double telemetryConsumption = 1.0; // resource consumption when an antenna is activated (RemoteTech function)

        [KSPField]
        public double transmitConsumption = 5.0;

        [KSPField]
        public double bandwidth = 0.25;

        [KSPField(isPersistant = true)]
        public bool antennaEnabled = false;

        [KSPField(isPersistant = true)]
        public bool antennaDeployed = false;

        [KSPField]
        public bool invertedDeployment = false; // for the scenario of negative scalar of modules (?)

        [KSPField]
        public bool allowToggle = false;

        [KSPField]
        public string transmitterGUIName = string.Empty;

        [KSPField]
        public string transmitterName = string.Empty;

        [KSPField]
        public string antennaModuleNames = string.Empty;

        [KSPField(guiName = "Partial transmit", guiActive = true), UI_Toggle(disabledText = "Stop & Return", enabledText = "Leave Incompl", scene = UI_Scene.Flight)]
        public bool incompleteAllowed = false;

        [KSPField]
        public double CosAngle = -1.0; //by default, disabled until third-party mod enables it via MM

        [KSPField]
        public RemoteTechAntennaType customAntennaType = RemoteTechAntennaType.OMNI;

        [KSPField]
        public Guid AntennaTarget = Guid.Empty;

        public string[] antennaModules;
        public bool isDeployable = false;
        protected bool inFlight = false;
        private string toggleOnText = "Turn Antenna On";
        private string toggleOffText = "Turn Antenna Off";
        protected double basePower;

        protected List<Multiplier> powerMultipliers;
        protected double totalPowerMult = 1;
        protected List<Multiplier> bandwidthMultipliers;
        protected double totalBandwidthMult = 1;
        protected List<Multiplier> consumptionMultipliers;
        protected double totalConsumptionMult = 1;

        // science transmission variables
        protected float showProgressInterval = 2f;
        protected float timeElapsed = 0f;

        /// <summary>
        /// Return the transmission-data rate of a specific antenna
        /// </summary>
        public override float DataRate
        {
            get
            {
                return (float)(bandwidth * totalBandwidthMult);
            }
        }

        /// <summary>
        /// Return the resource cost per Mit of a specific antenna
        /// </summary>
        public override double DataResourceCost
        {
            get
            {
                return (transmitConsumption * totalConsumptionMult) / (bandwidth * totalBandwidthMult);
            }
        }

        /// <summary>
        /// Whether this antenna is usable for any function
        /// </summary>
        protected bool UsableAntenna
        {
            get
            {
                return antennaEnabled && antennaDeployed;
            }
        }

        /// <summary>
        /// Whether this antenna is capable to communicate with any other vessel
        /// </summary>
        public override bool CanComm()
        {
            return UsableAntenna && base.CanComm();
        }

        /// <summary>
        /// Check if an unloaded vessel has at least one antenna
        /// </summary>
        /// <param name="mSnap">Saved part data of an unloaded vessel</param>
        public override bool CanCommUnloaded(ProtoPartModuleSnapshot mSnap)
        {
            if (mSnap == null)
            {
                return base.CanCommUnloaded(mSnap);
            }
            if (mSnap.moduleValues.HasValue("antennaEnabled") && mSnap.moduleValues.HasValue("antennaDeployed"))
            {
                bool enabled, deployed;
                var success = bool.TryParse(mSnap.moduleValues.GetValue("antennaEnabled"), out enabled);
                success = success & bool.TryParse(mSnap.moduleValues.GetValue("antennaDeployed"), out deployed);
                return success && enabled && deployed && base.CanCommUnloaded(mSnap);
            }
            else
            {
                return base.CanCommUnloaded(mSnap);
            }
        }

        /// <summary>
        /// Whether this antenna is able to transmit
        /// </summary>
        /// <returns></returns>
        public override bool CanTransmit()
        {
            return UsableAntenna && base.CanTransmit();
        }

        /// <summary>
        /// Read the module node from the config file of a specific antenna into the class's variables
        /// </summary>
        /// <param name="node">Config node of ModuleRTDataTransmitter</param>
        public override void OnLoad(ConfigNode node)
        {
            Logging.Debug("[ModuleRTDataTransmitter] OnLoad()");

            base.OnLoad(node);

            // generate our values if they're missing in cfg (eg. module replacement MM with no content change)
            if (!node.HasValue("transmitConsumptionRate"))
            {
                transmitConsumption = Math.Max(Math.Round((packetResourceCost / packetInterval), 3), 0.001);
            }
            if (!node.HasValue("telemetryConsumptionRate"))
            {
                telemetryConsumption = Math.Max(Math.Round(transmitConsumption, 3), 0.001);
            }
            if (!node.HasValue("transmitDataRate"))
            {
                bandwidth = Math.Max(Math.Round((packetSize / packetInterval), 3), 0.001);
            }
            // this assumes stock is setting resource consumption rate to 1.0
            for (var i = 0; i < resHandler.inputResources.Count; i++)
            {
                resHandler.inputResources[i].rate *= telemetryConsumption;
            }

            antennaModules = antennaModuleNames.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            isDeployable = (antennaModules.Length > 0) || (DeployFxModuleIndices != null && DeployFxModuleIndices.Length > 0);
            basePower = antennaPower;
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        /// <summary>
        /// Execute a set of conditional actions at the flight/launch start
        /// </summary>
        /// <param name="state">Enum-type state of the active vessel</param>
        public override void OnStart(StartState state)
        {
            Logging.Debug("[ModuleRTDataTransmitter] OnStart()");
            base.OnStart(state);
            inFlight = state > StartState.Editor;

            if (antennaModules.Length > 0)
            {
                deployFxModules.Clear();
                for (var i = 0; i < antennaModules.Length; i++)
                {
                    var modules = part.Modules.GetModules<ModuleRTDeployableAntenna>();
                    var found = false;
                    for (var j = 0; j < modules.Count; j++)
                    {
                        if (modules[j].antennaModuleName.Trim() == antennaModules[i].Trim())
                        {
                            deployFxModules.Add(modules[j]);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        Logging.Info("[ModuleRTDataTransmitter] No ModuleRTDeployableAntenna module is found.");
                    }
                }
            }
            if (!isDeployable || allowToggle)
            {
                Events["ToggleAntenna"].active = true;
                Events["ToggleAntenna"].guiActive = true;
                Events["ToggleAntenna"].guiActiveEditor = true;
                //Events["EnableAntenna"].active = true;
                //Events["DisableAntenna"].active = true;
            }
            else
            {
                Actions["ToggleAntennaAction"].active = false;
                Actions["EnableAntennaAction"].active = false;
                Actions["DisableAntennaAction"].active = false;
            }
            if (!isDeployable)
            {
                antennaDeployed = true;
            }
            if (customAntennaType == RemoteTechAntennaType.INTERNAL)
            {
                Fields["incompleteAllowed"].guiActive = false;
            }
            if (transmitterGUIName.Length > 0)
            {
                var name = " (" + transmitterGUIName + ")";

                Events["ToggleAntenna"].guiName += name;
                //Events["EnableAntenna"].guiName += name;
                //Events["DisableAntenna"].guiName += name;
                Events["StartTransmission"].guiName += name;
                Events["StopTransmission"].guiName += name;

                Actions["ToggleAntennaAction"].guiName += name;
                Actions["EnableAntennaAction"].guiName += name;
                Actions["DisableAntennaAction"].guiName += name;
                Actions["StartTransmissionAction"].guiName += name;

                Fields["statusText"].guiName += name;
                Fields["powerText"].guiName += name;
                Fields["incompleteAllowed"].guiName += name;

                toggleOnText = "Turn " + transmitterGUIName + " On";
                toggleOffText = "Turn " + transmitterGUIName + " Off";
            }
            Events["TransmitIncompleteToggle"].active = false;
            Events["TransmitIncompleteToggle"].guiActive = false;
            Events["TransmitIncompleteToggle"].guiActiveEditor = false;

            Fields["statusText"].guiActive = true;
            Fields["statusText"].guiActiveEditor = true;

            UpdateContextMenu();
        }

        /// <summary>
        /// Return the description of a specific antenna (stock or RT) to KSP, which precomputes some part data during the Squad-monkey loading screen
        /// </summary>
        /// <returns></returns>
        public override string GetInfo()
        {
            Debug.Log("[ModuleRTDataTransmitter] GetInfo()");
            var text = new StringBuilder();
            text.Append("<b>Antenna Type: </b>");
            text.AppendLine(customAntennaType.displayDescription());
            text.Append("<b>Antenna Power Rating: </b>");
            text.AppendLine(powerText);

            //text.Append("to lvl1 DSN: ");
            //text.AppendLine(KSPUtil.PrintSI(CommNetScenario.RangeModel.GetMaximumRange(antennaPower, GameVariables.Instance.GetDSNRange(0f)), "m", 3, false));
            //text.Append("to lvl2 DSN: ");
            //text.AppendLine(KSPUtil.PrintSI(CommNetScenario.RangeModel.GetMaximumRange(antennaPower, GameVariables.Instance.GetDSNRange(0.5f)), "m", 3, false));
            //text.Append("to lvl3 DSN: ");
            //text.AppendLine(KSPUtil.PrintSI(CommNetScenario.RangeModel.GetMaximumRange(antennaPower, GameVariables.Instance.GetDSNRange(1f)), "m", 3, false));

            if (customAntennaType != RemoteTechAntennaType.INTERNAL)
            {
                //text.AppendLine();
                text.Append("<b>Bandwidth: </b>");
                text.AppendLine(bandwidth.ToString("###0.### Mits/s"));
            }

            //text.AppendLine();
            text.Append("<b><color=orange>Active antenna requires:");
            var tmpText = resHandler.PrintModuleResources(telemetryConsumption);
            var index = tmpText.IndexOf(":");
            text.AppendLine(tmpText.Substring(index + 1, tmpText.Length - index - Environment.NewLine.Length));

            if (customAntennaType != RemoteTechAntennaType.INTERNAL)
            {
                text.Append("<b><color=orange>Science transmission requires:");
                tmpText = resHandler.PrintModuleResources(transmitConsumption);
                index = tmpText.IndexOf(":");
                text.AppendLine(tmpText.Substring(index + 1, tmpText.Length - index - Environment.NewLine.Length));
            }
            else
            {
                text.Append("<b><color=orange>Cannot transmit Science</color></b>");
            }
            if (antennaEnabled)
            {
                text.Append("<b><color=#a0a0a0ff>Enabled by default</color></b>");
            }
            else
            {
                text.Append("<b><color=#a0a0a0ff>Disabled by default</color></b>");
            }
            return text.ToString();
        }

        /// <summary>
        /// Unity Engine invokes this method once, zero or several times per frame. Useful for regular
        /// and uniform interval of calculations regardless of player's framerate.
        /// Generally used for physics and other game mechanics.
        /// </summary>
        public void FixedUpdate()
        {
            CheckDeployed();
            ProcessPower();
        }

        /// <summary>
        /// Unity Engine invokes this method exactly once per frame.
        /// Genereally used for GUI and similar. Not bound to "ingame" time in any way.
        /// </summary>
        public void Update()
        {
            if (busy)
            {
                timeElapsed += TimeWarp.deltaTime;
            }
            if (incompleteAllowed != xmitIncomplete)
            {
                TransmitIncompleteToggle();
            }
        }

        /// <summary>
        /// Display a looped sequence of data packets over time to a player
        /// In RT, this sends data as a "stream" instead of chunks, moving small ammounts every fixed update.
        /// </summary>
        /// <param name="transmitInterval"></param>
        /// <param name="dataPacketSize"></param>
        /// <param name="callback"></param>
        /// <param name="sendData"></param>
        /// <returns></returns>
        protected override IEnumerator transmitQueuedData(float transmitInterval, float dataPacketSize, Callback callback = null, bool sendData = true)
        {
            // TODO: There's some weirdness when trying to transmit science in sandbox mode
            // It would be simple to just skip it and don't transmit anything, but it might be better
            // to "simulate" the transmission and consume resources, etc.

            Debug.Log("[ModuleRTDataTransmitter] transmitQueuedData()");
            busy = true;
            timeElapsed = 0f;
            Events["StopTransmission"].active = true;
            Events["StartTransmission"].active = false;

            while (transmissionQueue.Count > 0 && !xmitAborted)
            {
                var dataThrough = 0.0f;
                var progress = 0.0f;

                var scienceData = transmissionQueue[0];
                var dataAmount = (float)scienceData.dataAmount;

                scienceData.triggered = true;

                ScreenMessages.PostScreenMessage(string.Format("[{0}]: Starting Transmission of {1}.", part.partInfo.title, scienceData.title), 4f, ScreenMessageStyle.UPPER_LEFT);

                var subject = ResearchAndDevelopment.GetSubjectByID(scienceData.subjectID);
                if (subject == null)
                {
                    AbortTransmission(string.Format("[{0}]: Unable to identify science subjectID:{1}!", part.partInfo.title, scienceData.subjectID));
                    yield return null;
                }
                if (ResearchAndDevelopment.Instance != null)
                {
                    commStream = new RnDCommsStream(subject, scienceData.dataAmount, 1.0f,
                        scienceData.baseTransmitValue * scienceData.transmitBonus,
                        xmitIncomplete, ResearchAndDevelopment.Instance);
                }
                else
                {
                    AbortTransmission("Could not find Research and Development facility!");
                    yield return null;
                }
                statusText = "Transmitting";
                while (dataThrough < dataAmount && !xmitAborted && CanTransmit())
                {
                    yield return new WaitForFixedUpdate();
                    var pushData = (float)(TimeWarp.fixedDeltaTime * bandwidth * totalBandwidthMult);
                    commStream.StreamData(pushData, vessel.protoVessel);
                    dataThrough += pushData;
                    progress = dataThrough / dataAmount;
                    statusText = string.Format("Transmitting ({0:P0})", progress);
                    if (timeElapsed >= showProgressInterval)
                    {
                        progressMessage.message = string.Format("[{0}]: Transmission progress: {1:P0}", part.partInfo.title, progress);
                        ScreenMessages.PostScreenMessage(progressMessage);
                        timeElapsed -= showProgressInterval;
                    }
                }
                if (!CanTransmit())
                {
                    AbortTransmission("Connection lost!");
                    yield return null;
                }
                if (dataThrough < dataAmount && dataThrough > 0 && xmitIncomplete)
                {
                    GameEvents.OnTriggeredDataTransmission.Fire(scienceData, vessel, false);
                    transmissionQueue.RemoveAt(0);
                    ScreenMessages.PostScreenMessage(string.Format("[{0}]: <color=orange>Partial</color> transmission of {1} completed.", part.partInfo.title, scienceData.title), 4f, ScreenMessageStyle.UPPER_LEFT);
                }
                if (dataThrough >= dataAmount)
                {
                    GameEvents.OnTriggeredDataTransmission.Fire(scienceData, vessel, false);
                    transmissionQueue.RemoveAt(0);
                    ScreenMessages.PostScreenMessage(string.Format("[{0}]: Transmission of {1} completed.", part.partInfo.title, scienceData.title), 4f, ScreenMessageStyle.UPPER_LEFT);
                }
            }
            if (xmitAborted && transmissionQueue.Count > 0)
            {
                foreach (var data in transmissionQueue)
                {
                    ReturnDataToContainer(data);
                }
                ScreenMessages.PostScreenMessage(string.Format("[{0}]: Unsent data returned.", part.partInfo.title), 4f, ScreenMessageStyle.UPPER_LEFT);
            }
            timeElapsed = 0f;
            Events["StopTransmission"].active = false;
            busy = false;
            UpdateContextMenu();
            xmitAborted = false;

            if (callback != null)
            {
                callback.Invoke();
            }
        }

        [KSPAction(guiName = "Toggle antenna", actionGroup = KSPActionGroup.None)]
        public virtual void ToggleAntennaAction(KSPActionParam param)
        {
            SetAntennaState(!antennaEnabled);
        }

        [KSPEvent(guiName = "Toggle antenna")]
        public void ToggleAntenna()
        {
            SetAntennaState(!antennaEnabled);
        }

        [KSPAction(guiName = "Enable antenna", actionGroup = KSPActionGroup.None)]
        public void EnableAntennaAction(KSPActionParam param)
        {
            SetAntennaState(true);
        }

        //[KSPEvent(guiName = "Enable antenna")]
        //public void EnableAntenna()
        //{
        //    SetAntennaState(true);
        //}

        [KSPAction(guiName = "Disable antenna", actionGroup = KSPActionGroup.None)]
        public void DisableAntennaAction(KSPActionParam param)
        {
            SetAntennaState(false);
        }

        //[KSPEvent(guiName = "Disable antenna")]
        //public void DisableAntenna()
        //{
        //    SetAntennaState(false);
        //}

        public void SetPowerMultiplier(string name, double value)
        {
            totalPowerMult = 1;
            var found = false;
            for (var i= 0; i < powerMultipliers.Count; i++)
            {
                if (powerMultipliers[i].Name == name)
                {
                    found = true;
                    powerMultipliers[i].Value = value;
                }
                totalPowerMult *= powerMultipliers[i].Value;
            }
            if (!found)
            {
                powerMultipliers.Add(new Multiplier(name, value));
                totalPowerMult *= value;
            }
            antennaPower = basePower * totalPowerMult;
        }

        public void SetBandwidthMultiplier(string name, double value)
        {
            totalBandwidthMult = 1;
            var found = false;
            for (var i = 0; i < bandwidthMultipliers.Count; i++)
            {
                if (bandwidthMultipliers[i].Name == name)
                {
                    found = true;
                    bandwidthMultipliers[i].Value = value;
                }
                totalBandwidthMult *= bandwidthMultipliers[i].Value;
            }
            if (!found)
            {
                bandwidthMultipliers.Add(new Multiplier(name, value));
                totalBandwidthMult *= value;
            }
        }

        public void SetConsumptionMultiplier(string name, double value)
        {
            totalConsumptionMult = 1;
            var found = false;
            for (var i = 0; i < consumptionMultipliers.Count; i++)
            {
                if (consumptionMultipliers[i].Name == name)
                {
                    found = true;
                    consumptionMultipliers[i].Value = value;
                }
                totalConsumptionMult *= consumptionMultipliers[i].Value;
            }
            if (!found)
            {
                consumptionMultipliers.Add(new Multiplier(name, value));
                totalConsumptionMult *= value;
            }
        }

        public void RemovePowerMultiplier(string name)
        {
            totalPowerMult = 1;
            for( var i = powerMultipliers.Count - 1; i>=0; i--)
            {
                if (powerMultipliers[i].Name == name)
                {
                    powerMultipliers.RemoveAt(i);
                }
                else
                {
                    totalPowerMult *= powerMultipliers[i].Value;
                }
            }
            antennaPower = basePower * totalPowerMult;
        }

        public void RemoveBandwidthMultiplier(string name)
        {
            totalBandwidthMult = 1;
            for (var i = bandwidthMultipliers.Count - 1; i >= 0; i--)
            {
                if (bandwidthMultipliers[i].Name == name)
                {
                    bandwidthMultipliers.RemoveAt(i);
                }
                else
                {
                    totalBandwidthMult *= bandwidthMultipliers[i].Value;
                }
            }
        }

        public void RemoveConsumptionMultiplier(string name)
        {
            totalConsumptionMult = 1;
            for (var i = consumptionMultipliers.Count - 1; i >= 0; i--)
            {
                if (consumptionMultipliers[i].Name == name)
                {
                    consumptionMultipliers.RemoveAt(i);
                }
                else
                {
                    totalConsumptionMult *= consumptionMultipliers[i].Value;
                }
            }
        }

        /// <summary>
        /// Toggle the part action(s) of a specific antenna (RT function)
        /// </summary>
        /// <param name="state"></param>
        private void SetAntennaState(bool state)
        {
            if (state != antennaEnabled)
            {
                antennaEnabled = state;
                UpdateContextMenu();
            }
        }

        /// <summary>
        /// Check and consume the vessel's resource for this antenna activated or active transmission of data.
        /// </summary>
        private void ProcessPower()
        {
            if (inFlight)
            {
                if (UsableAntenna)
                {
                    var resErrorMsg = "";
                    var resAvailable = 1.0d;
                    if (busy)
                    {
                        resAvailable = resHandler.UpdateModuleResourceInputs(ref resErrorMsg, (transmitConsumption / telemetryConsumption) * totalConsumptionMult, 0.99, true, false, true);
                        if (resAvailable < 0.99)
                        {
                            AbortTransmission(resErrorMsg);
                        }
                    }
                    if (!busy || resAvailable < 0.99)
                    {
                        resAvailable = resHandler.UpdateModuleResourceInputs(ref resErrorMsg, totalConsumptionMult, 0.99, true, false, true);
                        if (resAvailable < 0.99)
                        {
                            SetAntennaState(false);
                            errorMessage.message = string.Format("[{0}]: Antenna shutting down, {1}", part.partInfo.title, resErrorMsg);
                            ScreenMessages.PostScreenMessage(errorMessage);
                        }
                    }
                }
                else if (busy)
                {
                    AbortTransmission("Antenna disabled!");
                }
            }
        }

        /// <summary>
        /// Check and update antenna status from fxmodules' state
        /// </summary>
        private void CheckDeployed()
        {
            if (isDeployable)
            {
                bool deployed;
                if (invertedDeployment)
                {
                    deployed = (GetModulesScalarMin(deployFxModules) < 0.1);
                }
                else
                {
                    deployed = (GetModulesScalarMin(deployFxModules) > 0.9);
                }
                if (antennaDeployed != deployed)
                {
                    antennaDeployed = deployed;
                    if (!allowToggle)
                    {
                        SetAntennaState(deployed);
                    }
                    UpdateContextMenu();
                }
            }
        }

        /// <summary>
        /// Function to keep the relevant antenna information in context menu updated at all times
        /// </summary>
        private void UpdateContextMenu()
        {
            if (!busy)
            {
                if (inFlight && customAntennaType != RemoteTechAntennaType.INTERNAL)
                {
                    Events["StartTransmission"].active = UsableAntenna;
                    Events["StartTransmission"].guiActive = UsableAntenna;
                    Events["StartTransmission"].guiActiveEditor = UsableAntenna;
                }
                if (isDeployable)
                {
                    if (antennaDeployed && antennaEnabled)
                    {
                        statusText = "Ready";
                    }
                    if (antennaDeployed && !antennaEnabled)
                    {
                        statusText = "Deployed, Off";
                    }
                    if (antennaEnabled && !antennaDeployed)
                    {
                        statusText = "Not deployed, On";
                    }
                    if (!antennaEnabled && !antennaDeployed)
                    {
                        statusText = "Not deployed, Off";
                    }
                }
                else
                {
                    if (antennaEnabled)
                    {
                        statusText = "Ready";
                        //Events["ToggleAntenna"].guiName = toggleOffText;
                    }
                    else
                    {
                        statusText = "Off";
                        //Events["ToggleAntenna"].guiName = toggleOnText;
                    }
                }
            }
        }
    }
}
