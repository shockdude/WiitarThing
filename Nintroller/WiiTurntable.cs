using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;

namespace NintrollerLib
{
    public struct WiiTurntable : INintrollerState
    {
#if DEBUG
        private bool _debugViewActive;
        public bool DebugViewActive
        {
            get
            {
                return _debugViewActive;
            }
            set
            {
                _debugViewActive = value;
            }
        }
#endif

        public Wiimote wiimote;
        public Joystick Joy, JoyTableLR, JoyDialCrossfade;
        public Trigger Crossfader, Dial;
        public bool RG, RR, RB, LG, LR, LB;
        public Trigger RButtons, LButtons;
        public bool Euphoria;
        public bool Plus, Minus;

#if DEBUG
        public byte[] DebugLastData;
#endif

#if DEBUG
        private bool DebugButton_Dump;
#endif

        public WiiTurntable(Wiimote wm)
        {
            this = new WiiTurntable();
            wiimote = wm;

#if DEBUG
            DebugLastData = new byte[] { 0 };
#endif
        }

        public bool Start
        {
            get { return Plus; }
            set { Plus = value; }
        }

        public bool Select
        {
            get { return Minus; }
            set { Minus = value; }
        }

        public void Update(byte[] data)
        {
#if DEBUG
            //DebugLastData = new byte[data.Length];

            //for (int i = 0; i < data.Length; i++)
            //{
            //    DebugLastData[i] = data[i];
            //}

            DebugLastData = data;
#endif

            int offset = 0;
            switch ((InputReport)data[0])
            {
                case InputReport.BtnsExt:
                case InputReport.BtnsExtB:
                    offset = 3;
                    break;
                case InputReport.BtnsAccExt:
                    offset = 6;
                    break;
                case InputReport.BtnsIRExt:
                    offset = 13;
                    break;
                case InputReport.BtnsAccIRExt:
                    offset = 16;
                    break;
                case InputReport.ExtOnly:
                    offset = 1;
                    break;
                default:
                    return;
            }

            if (offset > 0)
            {
                byte rawLButtons, rawRButtons, rawLTable, rawRTable, rawDial, rawCrossfader;
                // Joystick
                Joy.rawX = (byte)(data[offset] & 0x3F);
                Joy.rawY = (byte)(data[offset + 1] & 0x3F);
                Joy.Normalize();

                // Buttons
                rawLButtons = (byte)(7 - (
                    ((data[offset + 5] & 0x80) >> 5) |
                    ((data[offset + 4] & 0x20) >> 4) |
                    ((data[offset + 5] & 0x08) >> 3)));
                rawRButtons = (byte)(7 - (
                    (data[offset + 5] & 0x04) |
                    (data[offset + 4] & 0x02) |
                    ((data[offset + 5] & 0x20) >> 5)));
                LG = (rawLButtons & 0x01) != 0;
                LR = (rawLButtons & 0x02) != 0;
                LB = (rawLButtons & 0x04) != 0;
                RG = (rawRButtons & 0x01) != 0;
                RR = (rawRButtons & 0x02) != 0;
                RB = (rawRButtons & 0x04) != 0;
                Euphoria = (data[offset + 5] & 0x10) == 0;
                Plus = (data[offset + 4] & 0x04) == 0;
                Minus = (data[offset + 4] & 0x10) == 0;

                // Button X360 triggers
                RButtons.rawValue = rawRButtons;
                RButtons.Normalize();
                LButtons.rawValue = rawLButtons;
                LButtons.Normalize();

                // Turntables
                // Wii range is 0-32 per direction.
                // 360 range is 0-128 per direction out of 0-32768
                // for 360 range, add 8160 to each rawTable value
                rawLTable = (byte)(((
                    ((data[offset + 4] & 0x01) << 5) |
                    (data[offset + 3] & 0x1f)
                    ) + 32) % 64);
                rawRTable = (byte)(((
                    ((data[offset + 2] & 0x01) << 5) |
                    ((data[offset] & 0xc0) >> 3) |
                    ((data[offset + 1] & 0xc0) >> 5) |
                    ((data[offset + 2] & 0x80) >> 7)
                    ) + 32) % 64);
                JoyTableLR.rawX = rawLTable;
                JoyTableLR.rawY = rawRTable;
                JoyTableLR.Normalize();

                // Dial & Crossfader Joystick
                rawDial = (byte)(
                    ((data[offset + 2] & 0x60) >> 2) |
                    ((data[offset + 3] & 0xe0) >> 5)
                    );
                rawCrossfader = (byte)((data[offset + 2] & 0x1e) >> 1);
                JoyDialCrossfade.rawX = rawDial;
                JoyDialCrossfade.rawY = rawCrossfader;
                JoyDialCrossfade.Normalize();

                // Dial Trigger
                Dial.rawValue = rawDial;
                Dial.Normalize();

                // Crossfader Trigger
                Crossfader.rawValue = rawCrossfader;
                Crossfader.Normalize();
            }

            wiimote.Update(data);

#if DEBUG
            if (offset > 0)
            {
                if (wiimote.buttons.Home)
                {
                    if (!DebugButton_Dump)
                    {
                        DebugButton_Dump = true;

                        //var sb = new StringBuilder();

                        //sb.AppendLine("Wii Guitar data packet dump:");

                        //for (int i = 0; i < data.Length; i++)
                        //{
                        //    sb.Append(data[i].ToString("X2") + " ");
                        //}

                        //MessageBox.Show(sb.ToString(), "DEBUG: WII GUITAR DUMP", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        DebugViewActive = true;
                    }
                }
                else
                {
                    DebugButton_Dump = false;
                }


            }
#endif
        }

        public float GetValue(string input)
        {
            throw new NotImplementedException();
        }

        public void SetCalibration(Calibrations.CalibrationPreset preset)
        {
            wiimote.SetCalibration(preset);

            // only default calibration is supported right now
            SetCalibration(Calibrations.Defaults.WiiTurntableDefault);

            /*
            switch (preset)
            {
                case Calibrations.CalibrationPreset.Default:
                    SetCalibration(Calibrations.Defaults.WiiTurntableDefault);
                    break;

                case Calibrations.CalibrationPreset.Modest:
                    SetCalibration(Calibrations.Moderate.WiiTurntableModest);
                    break;

                case Calibrations.CalibrationPreset.Extra:
                    SetCalibration(Calibrations.Extras.WiiTurntableExtra);
                    break;

                case Calibrations.CalibrationPreset.Minimum:
                    SetCalibration(Calibrations.Minimum.WiiTurntableMinimal);
                    break;

                case Calibrations.CalibrationPreset.None:
                    SetCalibration(Calibrations.None.WiiTurntableRaw);
                    break;
            }
            */
        }

        public void SetCalibration(INintrollerState from)
        {
            if (from.CalibrationEmpty)
            {
                // don't apply empty calibrations
                return;
            }

            if (from.GetType() == typeof(WiiTurntable))
            {
                Joy.Calibrate(((WiiTurntable)from).Joy);
                JoyTableLR.Calibrate(((WiiTurntable)from).JoyTableLR);
                JoyDialCrossfade.Calibrate(((WiiTurntable)from).JoyDialCrossfade);
                LButtons.Calibrate(((WiiTurntable)from).LButtons);
                RButtons.Calibrate(((WiiTurntable)from).RButtons);
                Dial.Calibrate(((WiiTurntable)from).Dial);
                Crossfader.Calibrate(((WiiTurntable)from).Crossfader);
            }
            else if (from.GetType() == typeof(Wiimote))
            {
                wiimote.SetCalibration(from);
            }
        }

        public void SetCalibration(string calibrationString)
        {
            if (calibrationString.Count(c => c == '0') > 5)
            {
                // don't set empty calibrations
                return;
            }

            string[] components = calibrationString.Split(new char[] { ':' });

            foreach (string component in components)
            {
                if (component.StartsWith("joy"))
                {
                    string[] joyConfig = component.Split(new char[] { '|' });

                    for (int j = 1; j < joyConfig.Length; j++)
                    {
                        int value = 0;
                        if (int.TryParse(joyConfig[j], out value))
                        {
                            switch (j)
                            {
                                case 1: Joy.centerX = value; break;
                                case 2: Joy.minX = value; break;
                                case 3: Joy.maxX = value; break;
                                case 4: Joy.deadX = value; break;
                                case 5: Joy.centerY = value; break;
                                case 6: Joy.minY = value; break;
                                case 7: Joy.maxY = value; break;
                                case 8: Joy.deadY = value; break;
                                default: break;
                            }
                        }
                    }
                }
                else if (component.StartsWith("jtb"))
                {
                    string[] joyConfig = component.Split(new char[] { '|' });

                    for (int j = 1; j < joyConfig.Length; j++)
                    {
                        int value = 0;
                        if (int.TryParse(joyConfig[j], out value))
                        {
                            switch (j)
                            {
                                case 1: JoyTableLR.centerX = value; break;
                                case 2: JoyTableLR.minX = value; break;
                                case 3: JoyTableLR.maxX = value; break;
                                case 4: JoyTableLR.deadX = value; break;
                                case 5: JoyTableLR.centerY = value; break;
                                case 6: JoyTableLR.minY = value; break;
                                case 7: JoyTableLR.maxY = value; break;
                                case 8: JoyTableLR.deadY = value; break;
                                default: break;
                            }
                        }
                    }
                }
                else if (component.StartsWith("jdc"))
                {
                    string[] joyConfig = component.Split(new char[] { '|' });

                    for (int j = 1; j < joyConfig.Length; j++)
                    {
                        int value = 0;
                        if (int.TryParse(joyConfig[j], out value))
                        {
                            switch (j)
                            {
                                case 1: JoyDialCrossfade.centerX = value; break;
                                case 2: JoyDialCrossfade.minX = value; break;
                                case 3: JoyDialCrossfade.maxX = value; break;
                                case 4: JoyDialCrossfade.deadX = value; break;
                                case 5: JoyDialCrossfade.centerY = value; break;
                                case 6: JoyDialCrossfade.minY = value; break;
                                case 7: JoyDialCrossfade.maxY = value; break;
                                case 8: JoyDialCrossfade.deadY = value; break;
                                default: break;
                            }
                        }
                    }
                }
                else if (component.StartsWith("lb"))
                {
                    string[] lButtonsConfig = component.Split(new char[] { '|' });

                    for (int t = 1; t < lButtonsConfig.Length; t++)
                    {
                        int value = 0;
                        if (int.TryParse(lButtonsConfig[t], out value))
                        {
                            switch (t)
                            {
                                case 1: LButtons.min = value; break;
                                case 2: LButtons.max = value; break;
                                default: break;
                            }
                        }
                    }
                }
                else if (component.StartsWith("rb"))
                {
                    string[] rButtonsConfig = component.Split(new char[] { '|' });

                    for (int t = 1; t < rButtonsConfig.Length; t++)
                    {
                        int value = 0;
                        if (int.TryParse(rButtonsConfig[t], out value))
                        {
                            switch (t)
                            {
                                case 1: RButtons.min = value; break;
                                case 2: RButtons.max = value; break;
                                default: break;
                            }
                        }
                    }
                }
                else if (component.StartsWith("cf"))
                {
                    string[] crossfaderConfig = component.Split(new char[] { '|' });

                    for (int t = 1; t < crossfaderConfig.Length; t++)
                    {
                        int value = 0;
                        if (int.TryParse(crossfaderConfig[t], out value))
                        {
                            switch (t)
                            {
                                case 1: Crossfader.min = value; break;
                                case 2: Crossfader.max = value; break;
                                default: break;
                            }
                        }
                    }
                }
                else if (component.StartsWith("di"))
                {
                    string[] dialConfig = component.Split(new char[] { '|' });

                    for (int t = 1; t < dialConfig.Length; t++)
                    {
                        int value = 0;
                        if (int.TryParse(dialConfig[t], out value))
                        {
                            switch (t)
                            {
                                case 1: Dial.min = value; break;
                                case 2: Dial.max = value; break;
                                default: break;
                            }
                        }
                    }
                }
            }
        }

        public string GetCalibrationString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("-wtb");
            sb.Append(":joy");
                sb.Append("|"); sb.Append(Joy.centerX);
                sb.Append("|"); sb.Append(Joy.minX);
                sb.Append("|"); sb.Append(Joy.maxX);
                sb.Append("|"); sb.Append(Joy.deadX);
                sb.Append("|"); sb.Append(Joy.centerY);
                sb.Append("|"); sb.Append(Joy.minY);
                sb.Append("|"); sb.Append(Joy.maxY);
                sb.Append("|"); sb.Append(Joy.deadY);
            sb.Append(":jtb");
                sb.Append("|"); sb.Append(JoyTableLR.centerX);
                sb.Append("|"); sb.Append(JoyTableLR.minX);
                sb.Append("|"); sb.Append(JoyTableLR.maxX);
                sb.Append("|"); sb.Append(JoyTableLR.deadX);
                sb.Append("|"); sb.Append(JoyTableLR.centerY);
                sb.Append("|"); sb.Append(JoyTableLR.minY);
                sb.Append("|"); sb.Append(JoyTableLR.maxY);
                sb.Append("|"); sb.Append(JoyTableLR.deadY);
            sb.Append(":jdc");
                sb.Append("|"); sb.Append(JoyDialCrossfade.centerX);
                sb.Append("|"); sb.Append(JoyDialCrossfade.minX);
                sb.Append("|"); sb.Append(JoyDialCrossfade.maxX);
                sb.Append("|"); sb.Append(JoyDialCrossfade.deadX);
                sb.Append("|"); sb.Append(JoyDialCrossfade.centerY);
                sb.Append("|"); sb.Append(JoyDialCrossfade.minY);
                sb.Append("|"); sb.Append(JoyDialCrossfade.maxY);
                sb.Append("|"); sb.Append(JoyDialCrossfade.deadY);
            sb.Append(":lb");
                sb.Append("|"); sb.Append(LButtons.min);
                sb.Append("|"); sb.Append(LButtons.max);
            sb.Append(":rb");
                sb.Append("|"); sb.Append(RButtons.min);
                sb.Append("|"); sb.Append(RButtons.max);
            sb.Append(":cf");
                sb.Append("|"); sb.Append(Crossfader.min);
                sb.Append("|"); sb.Append(Crossfader.max);
            sb.Append(":di");
                sb.Append("|"); sb.Append(Dial.min);
                sb.Append("|"); sb.Append(Dial.max);

            return sb.ToString();
        }

        public bool CalibrationEmpty
        {
            get
            {
                if (Joy.maxX == 0 && Joy.maxY == 0 && JoyTableLR.maxX == 0 && JoyTableLR.maxY == 0 && JoyDialCrossfade.maxX == 0 && JoyDialCrossfade.maxY == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }


        public IEnumerator<KeyValuePair<string, float>> GetEnumerator()
        {
            foreach (var input in wiimote)
            {
                yield return input;
            }

            Joy.Normalize();
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LX, Joy.X);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LY, Joy.Y);

            // analog
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LUP, Joy.Y > 0f ? Joy.Y : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LDOWN, Joy.Y < 0f ? -Joy.Y : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LLEFT, Joy.X < 0f ? -Joy.X : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LRIGHT, Joy.X > 0f ? Joy.X : 0.0f);

            // digital
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.UP, Joy.Y > 0.5f ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.DOWN, Joy.Y < -0.5f ? -1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LEFT, Joy.X < -0.5f ? -1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.RIGHT, Joy.X > 0.5f ? 1.0f : 0.0f);

            JoyTableLR.Normalize();
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LTABLECLKWISE, JoyTableLR.X > 0f ? JoyTableLR.X : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LTABLECTRCLKWISE, JoyTableLR.X < 0f ? -JoyTableLR.X : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LTABLE, JoyTableLR.X);

            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.RTABLECLKWISE, JoyTableLR.Y > 0f ? JoyTableLR.Y : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.RTABLECTRCLKWISE, JoyTableLR.Y < 0f ? -JoyTableLR.Y : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.RTABLE, JoyTableLR.Y);

            RButtons.Normalize();
            LButtons.Normalize();
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.RG, RG ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.RR, RR ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.RB, RB ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.RBUTTONS, RButtons.value);

            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LG, LG ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LR, LR ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LB, LB ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.LBUTTONS, LButtons.value);

            JoyDialCrossfade.Normalize();
            Dial.Normalize();
            Crossfader.Normalize();
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.DIALCLKWISE, JoyDialCrossfade.X > 0f ? JoyDialCrossfade.X : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.DIALCTRCLKWISE, JoyDialCrossfade.X < 0f ? -JoyDialCrossfade.X : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.DIAL, JoyDialCrossfade.X);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.DIALT, Dial.value);

            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.CROSSFADERLEFT, JoyDialCrossfade.Y < 0f ? -JoyDialCrossfade.Y : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.CROSSFADERRIGHT, JoyDialCrossfade.Y > 0f ? JoyDialCrossfade.Y : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.CROSSFADER, JoyDialCrossfade.Y);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.CROSSFADERT, Crossfader.value);

            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.EUPHORIA, Euphoria ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.START, Start ? 1.0f : 0.0f);
            yield return new KeyValuePair<string, float>(INPUT_NAMES.WII_TURNTABLE.SELECT, Select ? 1.0f : 0.0f);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
