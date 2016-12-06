using CommNet;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RemoteTech.Transmitter
{
    // TODO: 
    //   - add KSPEvents for enabling and disabling antenna and others from ModuleRTAntenna
    //   - figure out how to deal with deployable antennas
    //   - fix antenna coming out disabled from the editor - DONE
    
    // This attribute has no effect, sadly
    //[KSPModule("Data Transmitter (RT)")]
    public class ModuleRTDataTransmitter: ModuleDataTransmitter
    {
        // TODO: move this to settings?
        private static readonly double stockToRTTelemetryConsumptionFactor = 0.1;
        private static readonly double stockToRTTransmitConsumptionFactor = 0.25;
        private static readonly double stockToRTTransmitDataRateFactor = 0.25;

        [KSPField(isPersistant = true)]
        public double telemetryConsumptionRate = 0.1;

        [KSPField(isPersistant = true)]
        public double transmitConsumptionRate = 5.0;

        [KSPField(isPersistant = true)]
        public double transmitDataRate = 0.5;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Antenna"),
            UI_Toggle(disabledText = "Disabled", enabledText ="Enabled", invertButton = true)]
        public bool antennaEnabled = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName ="Partial science"),
            UI_Toggle(disabledText = "Disabled", enabledText = "Enabled")]
        public new bool xmitIncomplete;

        private float notifyEverySecs = 2f;
        private float timeElapsed = 0f;
        private bool shouldConsume = false;

        public override float DataRate
        {
            get
            {
                return (float)transmitDataRate;
            }
        }

        public override double DataResourceCost
        {
            get
            {
                return transmitConsumptionRate;
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!node.HasValue("transmitConsumptionRate"))
            {
                transmitConsumptionRate = packetResourceCost / packetInterval * stockToRTTransmitConsumptionFactor;
            }
            if (!node.HasValue("telemetryConsumptionRate"))
            {
                telemetryConsumptionRate = transmitConsumptionRate * stockToRTTelemetryConsumptionFactor;
            }
            if (!node.HasValue("transmitDataRate"))
            {
                transmitDataRate = packetSize / packetInterval * stockToRTTransmitDataRateFactor;
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!(state==StartState.None || state == StartState.Editor))
            {
                shouldConsume = true;
            }
        }

        [KSPEvent(guiName = "Toggle Transmit Incomplete", guiActive = true)]
        public override void TransmitIncompleteToggle()
        {
            base.TransmitIncompleteToggle();
        }

        public override bool CanTransmit()
        {
            return antennaEnabled && base.CanTransmit();
        }

        public override string GetInfo()
        {
            var text = new StringBuilder();
            text.Append("<b>Antenna Type: </b>");
            text.AppendLine(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(antennaType.ToString().ToLowerInvariant()));
            text.Append("<b>Antenna Power Rating: </b>");
            text.AppendLine(powerText);
            text.Append("to lvl1 DSN: ");
            text.AppendLine(KSPUtil.PrintSI(CommNetScenario.RangeModel.GetMaximumRange(antennaPower, GameVariables.Instance.GetDSNRange(0f)), "m", 3, false));
            text.Append("to lvl2 DSN: ");
            text.AppendLine(KSPUtil.PrintSI(CommNetScenario.RangeModel.GetMaximumRange(antennaPower, GameVariables.Instance.GetDSNRange(0.5f)), "m", 3, false));
            text.Append("to lvl3 DSN: ");
            text.AppendLine(KSPUtil.PrintSI(CommNetScenario.RangeModel.GetMaximumRange(antennaPower, GameVariables.Instance.GetDSNRange(1f)), "m", 3, false));
            if (antennaType != AntennaType.INTERNAL)
            {
                text.AppendLine();
                text.Append("<b>Bandwidth: </b>");
                text.AppendLine(transmitDataRate.ToString("###0.### Mits/s"));
            }

            text.AppendLine();
            text.Append("<b><color=orange>Active antenna requires:");
            var tmpText = resHandler.PrintModuleResources(telemetryConsumptionRate);
            var index = tmpText.IndexOf(":");
            text.AppendLine(tmpText.Substring(index + 1));
            if (antennaType != AntennaType.INTERNAL)
            {
                text.Append("<b><color=orange>Science transmission requires:");
                tmpText = resHandler.PrintModuleResources(transmitConsumptionRate);
                index = tmpText.IndexOf(":");
                text.AppendLine(tmpText.Substring(index + 1));
            }
            else
            {
                text.Append("<b><color=orange>Cannot transmit Science</color></b>");
            }
            // TODO: What is this meant to cover?
            if (!this.moduleIsEnabled)
            {
                // just a guess
                text.Append("<b><color=red>Antenna permanently DISABLED</color></b>");
            }
            return text.ToString();
        }

        public override bool CanComm()
        {
            return antennaEnabled && base.CanComm();
        }

        public override bool CanCommUnloaded(ProtoPartModuleSnapshot mSnap)
        {
            if (mSnap == null)
            {
                return base.CanCommUnloaded(mSnap);
            }
            if (mSnap.moduleValues.HasValue("antennaEnabled"))
            {
                return mSnap.moduleValues.GetValue("antennaEnabled").ToLower() == "true" && base.CanCommUnloaded(mSnap);
            }
            else
            {
                return base.CanCommUnloaded(mSnap);
            }
        }

        public void FixedUpdate()
        {
            if (shouldConsume)
            {
                if (antennaEnabled)
                {
                    var resErrorMsg = "";
                    var aborting = false;
                    var resAvailable = 0.0d;
                    if (busy)
                    {
                        resAvailable = resHandler.UpdateModuleResourceInputs(ref resErrorMsg, transmitConsumptionRate, 0.99, true, false, true);
                        Debug.Log(string.Format("[ModuleModDT] (transmit) resAvailable={0}", resAvailable));
                        if (resAvailable < 0.99)
                        {
                            AbortTransmission(resErrorMsg);
                            aborting = true;
                        }
                    }
                    if (!busy || aborting)
                    {
                        resAvailable = resHandler.UpdateModuleResourceInputs(ref resErrorMsg, telemetryConsumptionRate, 0.99, true, false, true);
                        Debug.Log(string.Format("[ModuleModDT] (telemetry) resAvailable={0}", resAvailable));
                        if (resAvailable < 0.99)
                        {
                            antennaEnabled = false;
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

        public void Update()
        {
            if (busy)
            {
                timeElapsed += Time.deltaTime;
            }
        }

        protected override IEnumerator transmitQueuedData(float transmitInterval, float dataPacketSize, Callback callback = null, bool sendData = true)
        { 
            busy = true;
            Events["StopTransmission"].active = true;

            while (transmissionQueue.Any() && !xmitAborted)
            {
                var dataThrough = 0.0f;
                var progress = 0.0f;

                var scienceData = transmissionQueue[0];
                var dataAmount = (float)scienceData.dataAmount;

                scienceData.triggered = true;

                statusMessage.message = string.Format("[{0}]: Starting Transmission of {1}", part.partInfo.title, scienceData.title);
                ScreenMessages.PostScreenMessage(statusMessage);

                var subject = ResearchAndDevelopment.GetSubjectByID(scienceData.subjectID);
                if (subject == null)
                {
                    AbortTransmission(string.Format("Unable to identify science subjectID:{0}!", scienceData.subjectID));
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
                }
                statusText = "Transmitting";
                while (dataThrough < dataAmount && !xmitAborted)
                {
                    yield return new WaitForFixedUpdate();
                    var pushData = (float)((double)TimeWarp.fixedDeltaTime*(double)transmitDataRate);
                    commStream.StreamData(pushData, vessel.protoVessel);
                    dataThrough += pushData;
                    progress = dataThrough / dataAmount;
                    statusText = string.Format("Transmitting ({0:P0})", progress);
                    if (timeElapsed >= notifyEverySecs)
                    {
                        progressMessage.message = string.Format("[{0}]: Transmission progress: {1:P0}", part.partInfo.title, progress);
                        ScreenMessages.PostScreenMessage(progressMessage);
                        timeElapsed -= notifyEverySecs;
                    }
                }
                if (dataThrough < dataAmount && dataThrough>0 && xmitIncomplete)
                {
                    statusMessage.message = string.Format("[{0}]: <color=orange>Partial</color> transmission of {1} completed", part.partInfo.title, scienceData.title);
                    GameEvents.OnTriggeredDataTransmission.Fire(scienceData, vessel, false);
                    transmissionQueue.RemoveAt(0);
                }
                if (dataThrough >= dataAmount)
                {
                    GameEvents.OnTriggeredDataTransmission.Fire(scienceData, vessel, false);
                    transmissionQueue.RemoveAt(0);
                    statusMessage.message = string.Format("[{0}]: Transmission of {1} completed", part.partInfo.title, scienceData.title);
                    ScreenMessages.PostScreenMessage(statusMessage);
                }
            }

            if (xmitAborted && transmissionQueue.Any())
            {
                statusMessage.message = string.Format("[{0}]: Returning unsent data.", part.partInfo.title);
                ScreenMessages.PostScreenMessage(statusMessage);
                foreach (var data in transmissionQueue)
                {
                    ReturnDataToContainer(data);
                }
            }
            timeElapsed = 0f;
            Events["StopTransmission"].active = false;
            busy = false;
            statusText = "Idle";
            xmitAborted = false;

            if (callback != null)
            {
                callback.Invoke();
            }

        }
    }
}
