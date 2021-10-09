﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NintrollerLib;
using System.Xml.Serialization;
using System.IO;
using Shared.Windows;

namespace WiinUSoft
{
    public enum DeviceState
    {
        None = 0,
        Discovered,
        Connected_XInput,
        Connected_VJoy
    }

    public delegate void ConnectStateChange(DeviceControl sender, DeviceState oldState, DeviceState newState);
    public delegate void ConnectionLost(DeviceControl sender);

    public partial class DeviceControl : UserControl
    {
        #region Members

        // private members
        private string devicePath;
        private Nintroller device;
        private DeviceState state;
        private IR     previousIR;
        private bool  snapIRpointer = false;
        private float rumbleAmount      = 0;
        private int   rumbleStepCount   = 0;
        private int   rumbleStepPeriod  = 10;
        private float rumbleSlowMult    = 0.5f;
        
        // internally public members
        internal Holders.Holder holder;
        internal Property       properties;
        internal int            targetXDevice = 0;
        internal bool           lowBatteryFired = false;
        internal bool           identifying = false;
        internal string         dName = "";
        internal System.Threading.Timer updateTimer;

        // constance
        internal const int UPDATE_SPEED = 25;

        // events
        public event ConnectStateChange OnConnectStateChange;
        public event ConnectionLost OnConnectionLost;

        #endregion

        #region Properties

        internal Nintroller Device
        {
            get { return device; }
            set
            {
                if (device != null)
                {
                    device.ExtensionChange -= device_ExtensionChange;
                    device.StateUpdate -= device_StateChange;
                    device.LowBattery -= device_LowBattery;

#if DEBUG
                    Device.StateUpdate -= Debug_Device_StateUpdate;
#endif
                }

                device = value;

                if (device != null)
                {
                    device.ExtensionChange += device_ExtensionChange;
                    device.StateUpdate += device_StateChange;
                    device.LowBattery += device_LowBattery;

#if DEBUG
                    Device.StateUpdate += Debug_Device_StateUpdate;
#endif
                }
            }
        }

        internal ControllerType DeviceType { get; private set; }

        internal string DevicePath
        {
            get { return devicePath; }
            private set { devicePath = value; }
        }

        internal bool Connected
        {
            get
            {
                if (device == null)
                    return false;

                return device.Connected;
            }
        }

        internal DeviceState ConnectionState
        {
            get
            {
                return state;
            }

            set
            {
                if (value != state)
                {
                    DeviceState previous = state;
                    SetState(value);

                    if (OnConnectStateChange != null)
                    {
                        OnConnectStateChange(this, previous, value);
                    }
                }
            }
        }

        #endregion

        public DeviceControl()
        {
            InitializeComponent();
        }

        public DeviceControl(Nintroller nintroller, string path)
            : this()
        {
            Device = nintroller;
            devicePath = path;

            Device.Disconnected += device_Disconnected;
        }

#if DEBUG
        private Windows.DebugDataWindow DebugDataWindowInstance = null;

        private void Debug_Device_StateUpdate(object sender, NintrollerStateEventArgs e)
        {
            if (e.state.DebugViewActive)
            {
                e.state.DebugViewActive = false;
                DebugViewActivate();
            }
        }

        private void DebugViewActivate()
        {
            if (DebugDataWindowInstance == null || !DebugDataWindowInstance.IsVisible)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    DebugDataWindowInstance = new Windows.DebugDataWindow();
                    DebugDataWindowInstance.nintroller = Device;
                    DebugDataWindowInstance.RegisterNintrollerUpdate();
                    DebugDataWindowInstance.Show();

                }), System.Windows.Threading.DispatcherPriority.ContextIdle);


            }
        }
#endif

        public void RefreshState()
        {
            if (state != DeviceState.Connected_XInput)
                ConnectionState = DeviceState.Discovered;

            // Load Properties
            properties = UserPrefs.Instance.GetDevicePref(devicePath);
            if (properties != null)
            {
                SetName(string.IsNullOrWhiteSpace(properties.name) ? device.Type.ToString() : properties.name);
                ApplyCalibration(properties.calPref, properties.calString ?? "");
                snapIRpointer = properties.pointerMode != Property.PointerOffScreenMode.Center;
                if (!string.IsNullOrEmpty(properties.lastIcon))
                {
                    icon.Source = (ImageSource)Application.Current.Resources[properties.lastIcon];
                }
            }
            else
            {
                properties = new Property(devicePath);
                UpdateIcon(device.Type);
                SetName(device.Type.ToString());
            }
        }

        public void SetName(string newName)
        {
            dName = newName;
            labelName.Content = new TextBlock() { Text = newName };
        }

        public void Detatch()
        {
            device?.StopReading();
            holder?.Close();
            lowBatteryFired = false;
            ConnectionState = DeviceState.Discovered;
            Dispatcher.BeginInvoke
            (
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => statusGradient.Color = (Color)FindResource("AntemBlue")
            ));
        }

        public void SetState(DeviceState newState)
        {
            state = newState;
            if (updateTimer != null)
            {
                updateTimer.Dispose();
                updateTimer = null;
            }

            switch (newState)
            {
                case DeviceState.None:
                    btnIdentify.IsEnabled   = false;
                    btnProperties.IsEnabled = false;
                    btnXinput.IsEnabled     = false;
                    //btnVjoy.IsEnabled     = false;
                    //btnConfig.IsEnabled     = false;
                    btnDetatch.IsEnabled    = false;
                   // btnConfig.Visibility    = Visibility.Hidden;
                    btnDetatch.Visibility   = Visibility.Hidden;
                    btnDebugView.Visibility = Visibility.Hidden;
                    break;

                case DeviceState.Discovered:
                    btnIdentify.IsEnabled   = true;
                    btnProperties.IsEnabled = true;
                    btnXinput.IsEnabled     = true;
                    //btnVjoy.IsEnabled     = true;
                    //btnConfig.IsEnabled     = false;
                    btnDetatch.IsEnabled    = false;
                    //btnConfig.Visibility    = Visibility.Hidden;
                    btnDetatch.Visibility   = Visibility.Hidden;
                    btnDebugView.Visibility = Visibility.Hidden;
                    break;

                case DeviceState.Connected_XInput:
                    btnIdentify.IsEnabled   = true;
                    btnProperties.IsEnabled = true;
                    btnXinput.IsEnabled     = false;
                    //btnVjoy.IsEnabled     = false;
                    //btnConfig.IsEnabled     = true;
                    btnDetatch.IsEnabled    = true;
                    //btnConfig.Visibility    = Visibility.Visible;
                    btnDetatch.Visibility   = Visibility.Visible;

#if DEBUG
                    btnDebugView.Visibility = Visibility.Visible;
#else
                    btnDebugView.Visibility = Visibility.Hidden;
#endif

                    var xHolder = new Holders.XInputHolder(device.Type);
                    LoadProfile(properties.profile, xHolder);
                    xHolder.ConnectXInput(targetXDevice);
                    holder = xHolder;
                    device.SetPlayerLED(targetXDevice);
                    updateTimer = new System.Threading.Timer(HolderUpdate, device, 1000, UPDATE_SPEED);
                    break;

                //case DeviceState.Connected_VJoy:
                //    btnIdentify.IsEnabled = true;
                //    btnProperties.IsEnabled = true;
                //    btnXinput.IsEnabled = false;
                //    btnVjoy.IsEnabled = false;
                //    btnConfig.IsEnabled = true;
                //    btnDetatch.IsEnabled = true;
                //    btnConfig.Visibility = System.Windows.Visibility.Visible;
                //    btnDetatch.Visibility = System.Windows.Visibility.Visible;

                //    // Instantiate VJoy Holder (not for 1st release)
                //    break;
            }
        }

        void device_ExtensionChange(object sender, NintrollerExtensionEventArgs e)
        {
            DeviceType = e.controllerType;

            if (holder != null)
            {
                holder.AddMapping(DeviceType);
            }

            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, 
                new Action(() =>
                {
                    UpdateIcon(DeviceType);
                    int playerNum = targetXDevice;
                    Detatch();
                    AssignToXinputPlayer(playerNum);
                    UpdateIcon(DeviceType);
                }
            ));
        }

        void device_LowBattery(object sender, LowBatteryEventArgs e)
        {
            SetBatteryStatus(e.batteryLevel == BatteryStatus.Low || e.batteryLevel == BatteryStatus.VeryLow);
        }

        void device_StateChange(object sender, NintrollerStateEventArgs e)
        {
            // Makes the timer wait
            if (updateTimer != null) updateTimer.Change(1000, UPDATE_SPEED);

            if (holder == null)
            {
                return;
            }

//            float intensity = 0;
//            if (holder.Values.TryGetValue(Inputs.Flags.RUMBLE, out intensity))
//            {
//                rumbleAmount = (int)intensity;
                RumbleStep();
//            }

            holder.ClearAllValues();

            switch (e.controllerType)
            {
                // TODO: Motion Plus Reading (not for 1st release)
                // TODO: Balance Board Reading (not for 1st release)
                // TODO: Musical Extension readings (not for 1st release)
                case ControllerType.ProController:
#region Pro Controller
                    ProController pro = (ProController)e.state;

                    holder.SetValue(Inputs.ProController.A, pro.A);
                    holder.SetValue(Inputs.ProController.B, pro.B);
                    holder.SetValue(Inputs.ProController.X, pro.X);
                    holder.SetValue(Inputs.ProController.Y, pro.Y);

                    holder.SetValue(Inputs.ProController.UP, pro.Up);
                    holder.SetValue(Inputs.ProController.DOWN, pro.Down);
                    holder.SetValue(Inputs.ProController.LEFT, pro.Left);
                    holder.SetValue(Inputs.ProController.RIGHT, pro.Right);

                    holder.SetValue(Inputs.ProController.L, pro.L);
                    holder.SetValue(Inputs.ProController.R, pro.R);
                    holder.SetValue(Inputs.ProController.ZL, pro.ZL);
                    holder.SetValue(Inputs.ProController.ZR, pro.ZR);

                    holder.SetValue(Inputs.ProController.START, pro.Plus);
                    holder.SetValue(Inputs.ProController.SELECT, pro.Minus);
                    holder.SetValue(Inputs.ProController.HOME, pro.Home);
                    holder.SetValue(Inputs.ProController.LS, pro.LStick);
                    holder.SetValue(Inputs.ProController.RS, pro.RStick);

                    holder.SetValue(Inputs.ProController.LRIGHT, pro.LJoy.X > 0 ? pro.LJoy.X : 0f);
                    holder.SetValue(Inputs.ProController.LLEFT,  pro.LJoy.X < 0 ? pro.LJoy.X * -1 : 0f);
                    holder.SetValue(Inputs.ProController.LUP,    pro.LJoy.Y > 0 ? pro.LJoy.Y : 0f);
                    holder.SetValue(Inputs.ProController.LDOWN,  pro.LJoy.Y < 0 ? pro.LJoy.Y * -1 : 0f);

                    holder.SetValue(Inputs.ProController.RRIGHT, pro.RJoy.X > 0 ? pro.RJoy.X : 0f);
                    holder.SetValue(Inputs.ProController.RLEFT,  pro.RJoy.X < 0 ? pro.RJoy.X * -1 : 0f);
                    holder.SetValue(Inputs.ProController.RUP,    pro.RJoy.Y > 0 ? pro.RJoy.Y : 0f);
                    holder.SetValue(Inputs.ProController.RDOWN,  pro.RJoy.Y < 0 ? pro.RJoy.Y * -1 : 0f);
#endregion
                    break;

                case ControllerType.Wiimote:
                    Wiimote wm = (Wiimote)e.state;
                    SetWiimoteInputs(wm);
                    break;

                case ControllerType.Nunchuk:
                case ControllerType.NunchukB:
#region Nunchuk
                    Nunchuk nun = (Nunchuk)e.state;

                    SetWiimoteInputs(nun.wiimote);

                    holder.SetValue(Inputs.Nunchuk.C, nun.C);
                    holder.SetValue(Inputs.Nunchuk.Z, nun.Z);

                    holder.SetValue(Inputs.Nunchuk.RIGHT, nun.joystick.X > 0 ? nun.joystick.X : 0f);
                    holder.SetValue(Inputs.Nunchuk.LEFT,  nun.joystick.X < 0 ? nun.joystick.X * -1 : 0f);
                    holder.SetValue(Inputs.Nunchuk.UP,    nun.joystick.Y > 0 ? nun.joystick.Y : 0f);
                    holder.SetValue(Inputs.Nunchuk.DOWN,  nun.joystick.Y < 0 ? nun.joystick.Y * -1 : 0f);

                    //TODO: Nunchuk Accelerometer (not for 1st release)
                    holder.SetValue(Inputs.Nunchuk.TILT_RIGHT, nun.accelerometer.X > 0 ? nun.accelerometer.X : 0f);
                    holder.SetValue(Inputs.Nunchuk.TILT_LEFT, nun.accelerometer.X < 0 ? nun.accelerometer.X * -1 : 0f);
                    holder.SetValue(Inputs.Nunchuk.TILT_UP, nun.accelerometer.Y > 0 ? nun.accelerometer.Y : 0f);
                    holder.SetValue(Inputs.Nunchuk.TILT_DOWN, nun.accelerometer.Y < 0 ? nun.accelerometer.Y * -1 : 0f);

                    holder.SetValue(Inputs.Nunchuk.ACC_SHAKE_X, nun.accelerometer.X > 1.15f);
                    holder.SetValue(Inputs.Nunchuk.ACC_SHAKE_Y, nun.accelerometer.Y > 1.15f);
                    holder.SetValue(Inputs.Nunchuk.ACC_SHAKE_Z, nun.accelerometer.Z > 1.15f);
#endregion
                    break;

                case ControllerType.ClassicController:
#region Classic Controller
                    ClassicController cc = (ClassicController)e.state;

                    SetWiimoteInputs(cc.wiimote);

                    holder.SetValue(Inputs.ClassicController.A, cc.A);
                    holder.SetValue(Inputs.ClassicController.B, cc.B);
                    holder.SetValue(Inputs.ClassicController.X, cc.X);
                    holder.SetValue(Inputs.ClassicController.Y, cc.Y);

                    holder.SetValue(Inputs.ClassicController.UP, cc.Up);
                    holder.SetValue(Inputs.ClassicController.DOWN, cc.Down);
                    holder.SetValue(Inputs.ClassicController.LEFT, cc.Left);
                    holder.SetValue(Inputs.ClassicController.RIGHT, cc.Right);

                    holder.SetValue(Inputs.ClassicController.L, cc.L.value > 0);
                    holder.SetValue(Inputs.ClassicController.R, cc.R.value > 0);
                    holder.SetValue(Inputs.ClassicController.ZL, cc.ZL);
                    holder.SetValue(Inputs.ClassicController.ZR, cc.ZR);

                    holder.SetValue(Inputs.ClassicController.START, cc.Start);
                    holder.SetValue(Inputs.ClassicController.SELECT, cc.Select);
                    holder.SetValue(Inputs.ClassicController.HOME, cc.Home);

                    holder.SetValue(Inputs.ClassicController.LFULL, cc.LFull);
                    holder.SetValue(Inputs.ClassicController.RFULL, cc.RFull);
                    holder.SetValue(Inputs.ClassicController.LT, cc.L.value > 0.1f ? cc.L.value : 0f);
                    holder.SetValue(Inputs.ClassicController.RT, cc.R.value > 0.1f ? cc.R.value : 0f);

                    holder.SetValue(Inputs.ClassicController.LRIGHT, cc.LJoy.X > 0 ? cc.LJoy.X : 0f);
                    holder.SetValue(Inputs.ClassicController.LLEFT, cc.LJoy.X < 0 ? cc.LJoy.X * -1 : 0f);
                    holder.SetValue(Inputs.ClassicController.LUP, cc.LJoy.Y > 0 ? cc.LJoy.Y : 0f);
                    holder.SetValue(Inputs.ClassicController.LDOWN, cc.LJoy.Y < 0 ? cc.LJoy.Y * -1 : 0f);

                    holder.SetValue(Inputs.ClassicController.RRIGHT, cc.RJoy.X > 0 ? cc.RJoy.X : 0f);
                    holder.SetValue(Inputs.ClassicController.RLEFT, cc.RJoy.X < 0 ? cc.RJoy.X * -1 : 0f);
                    holder.SetValue(Inputs.ClassicController.RUP, cc.RJoy.Y > 0 ? cc.RJoy.Y : 0f);
                    holder.SetValue(Inputs.ClassicController.RDOWN, cc.RJoy.Y < 0 ? cc.RJoy.Y * -1 : 0f);
#endregion
                    break;

                case ControllerType.ClassicControllerPro:
#region Classic Controller Pro
                    ClassicControllerPro ccp = (ClassicControllerPro)e.state;

                    SetWiimoteInputs(ccp.wiimote);

                    holder.SetValue(Inputs.ClassicControllerPro.A, ccp.A);
                    holder.SetValue(Inputs.ClassicControllerPro.B, ccp.B);
                    holder.SetValue(Inputs.ClassicControllerPro.X, ccp.X);
                    holder.SetValue(Inputs.ClassicControllerPro.Y, ccp.Y);

                    holder.SetValue(Inputs.ClassicControllerPro.UP, ccp.Up);
                    holder.SetValue(Inputs.ClassicControllerPro.DOWN, ccp.Down);
                    holder.SetValue(Inputs.ClassicControllerPro.LEFT, ccp.Left);
                    holder.SetValue(Inputs.ClassicControllerPro.RIGHT, ccp.Right);

                    holder.SetValue(Inputs.ClassicControllerPro.L, ccp.L);
                    holder.SetValue(Inputs.ClassicControllerPro.R, ccp.R);
                    holder.SetValue(Inputs.ClassicControllerPro.ZL, ccp.ZL);
                    holder.SetValue(Inputs.ClassicControllerPro.ZR, ccp.ZR);

                    holder.SetValue(Inputs.ClassicControllerPro.START, ccp.Start);
                    holder.SetValue(Inputs.ClassicControllerPro.SELECT, ccp.Select);
                    holder.SetValue(Inputs.ClassicControllerPro.HOME, ccp.Home);

                    holder.SetValue(Inputs.ClassicControllerPro.LRIGHT, ccp.LJoy.X > 0 ? ccp.LJoy.X : 0f);
                    holder.SetValue(Inputs.ClassicControllerPro.LLEFT, ccp.LJoy.X < 0 ? ccp.LJoy.X * -1 : 0f);
                    holder.SetValue(Inputs.ClassicControllerPro.LUP, ccp.LJoy.Y > 0 ? ccp.LJoy.Y : 0f);
                    holder.SetValue(Inputs.ClassicControllerPro.LDOWN, ccp.LJoy.Y < 0 ? ccp.LJoy.Y * -1 : 0f);

                    holder.SetValue(Inputs.ClassicControllerPro.RRIGHT, ccp.RJoy.X > 0 ? ccp.RJoy.X : 0f);
                    holder.SetValue(Inputs.ClassicControllerPro.RLEFT, ccp.RJoy.X < 0 ? ccp.RJoy.X * -1 : 0f);
                    holder.SetValue(Inputs.ClassicControllerPro.RUP, ccp.RJoy.Y > 0 ? ccp.RJoy.Y : 0f);
                    holder.SetValue(Inputs.ClassicControllerPro.RDOWN, ccp.RJoy.Y < 0 ? ccp.RJoy.Y * -1 : 0f);
#endregion
                    break;

                case ControllerType.Guitar:
#region Wii Guitar
                    WiiGuitar wgt = (WiiGuitar)e.state;

                    //SetWiimoteInputs(wgt.wiimote);

                    holder.SetValue(Inputs.WiiGuitar.G, wgt.G);
                    holder.SetValue(Inputs.WiiGuitar.R, wgt.R);
                    holder.SetValue(Inputs.WiiGuitar.Y, wgt.Y);
                    holder.SetValue(Inputs.WiiGuitar.B, wgt.B);
                    holder.SetValue(Inputs.WiiGuitar.O, wgt.O);

                    holder.SetValue(Inputs.WiiGuitar.UP, wgt.Up);
                    holder.SetValue(Inputs.WiiGuitar.DOWN, wgt.Down);
                    holder.SetValue(Inputs.WiiGuitar.LEFT, wgt.Left);
                    holder.SetValue(Inputs.WiiGuitar.RIGHT, wgt.Right);

                    holder.SetValue(Inputs.WiiGuitar.WHAMMYHIGH, wgt.WhammyHigh);
                    holder.SetValue(Inputs.WiiGuitar.WHAMMYLOW, wgt.WhammyLow);

                    holder.SetValue(Inputs.WiiGuitar.TILTHIGH, wgt.TiltHigh);
                    holder.SetValue(Inputs.WiiGuitar.TILTLOW, wgt.TiltLow);

                    holder.SetValue(Inputs.WiiGuitar.START, wgt.Start);
                    holder.SetValue(Inputs.WiiGuitar.SELECT, wgt.Select);
#endregion
                    break;

                case ControllerType.Drums:
#region Wii Drums
                    WiiDrums wdr = (WiiDrums)e.state;

                    holder.SetValue(Inputs.WiiDrums.G, wdr.G);
                    holder.SetValue(Inputs.WiiDrums.R, wdr.R);
                    holder.SetValue(Inputs.WiiDrums.Y, wdr.Y);
                    holder.SetValue(Inputs.WiiDrums.B, wdr.B);
                    holder.SetValue(Inputs.WiiDrums.O, wdr.O);
                    holder.SetValue(Inputs.WiiDrums.BASS, wdr.Bass);

                    holder.SetValue(Inputs.WiiDrums.UP, wdr.Up);
                    holder.SetValue(Inputs.WiiDrums.DOWN, wdr.Down);
                    holder.SetValue(Inputs.WiiDrums.LEFT, wdr.Left);
                    holder.SetValue(Inputs.WiiDrums.RIGHT, wdr.Right);

                    holder.SetValue(Inputs.WiiDrums.START, wdr.Start);
                    holder.SetValue(Inputs.WiiDrums.SELECT, wdr.Select);
#endregion
                break;

                case ControllerType.Turntable:
#region Wii Turntable
                    WiiTurntable wtb = (WiiTurntable)e.state;

                    SetWiimoteInputs(wtb.wiimote);

                    // analog
                    holder.SetValue(Inputs.WiiTurntable.LUP, wtb.Joy.Y > 0 ? wtb.Joy.Y : 0f);
                    holder.SetValue(Inputs.WiiTurntable.LDOWN, wtb.Joy.Y < 0 ? -wtb.Joy.Y : 0f);
                    holder.SetValue(Inputs.WiiTurntable.LLEFT, wtb.Joy.X < 0 ? -wtb.Joy.X : 0f);
                    holder.SetValue(Inputs.WiiTurntable.LRIGHT, wtb.Joy.X > 0 ? wtb.Joy.X : 0f);

                    // digital
                    holder.SetValue(Inputs.WiiTurntable.UP, wtb.Joy.Y > 0.5f ? 1f : 0f);
                    holder.SetValue(Inputs.WiiTurntable.DOWN, wtb.Joy.Y < -0.5f ? -1f : 0f);
                    holder.SetValue(Inputs.WiiTurntable.LEFT, wtb.Joy.X < -0.5f ? -1f : 0f);
                    holder.SetValue(Inputs.WiiTurntable.RIGHT, wtb.Joy.X > 0.5f ? 1f : 0f);

                    holder.SetValue(Inputs.WiiTurntable.LTABLECLKWISE, wtb.JoyTableLR.X > 0 ? wtb.JoyTableLR.X : 0f);
                    holder.SetValue(Inputs.WiiTurntable.LTABLECTRCLKWISE, wtb.JoyTableLR.X < 0 ? -wtb.JoyTableLR.X : 0f);

                    holder.SetValue(Inputs.WiiTurntable.RTABLECLKWISE, wtb.JoyTableLR.Y > 0 ? wtb.JoyTableLR.Y : 0f);
                    holder.SetValue(Inputs.WiiTurntable.RTABLECTRCLKWISE, wtb.JoyTableLR.Y < 0 ? -wtb.JoyTableLR.Y : 0f);

                    holder.SetValue(Inputs.WiiTurntable.RG, wtb.RG);
                    holder.SetValue(Inputs.WiiTurntable.RR, wtb.RR);
                    holder.SetValue(Inputs.WiiTurntable.RB, wtb.RB);
                    holder.SetValue(Inputs.WiiTurntable.RBUTTONS, wtb.RButtons.value);

                    holder.SetValue(Inputs.WiiTurntable.LG, wtb.LG);
                    holder.SetValue(Inputs.WiiTurntable.LR, wtb.LR);
                    holder.SetValue(Inputs.WiiTurntable.LB, wtb.LB);
                    holder.SetValue(Inputs.WiiTurntable.LBUTTONS, wtb.LButtons.value);

                    holder.SetValue(Inputs.WiiTurntable.DIALCLKWISE, wtb.JoyDialCrossfade.X > 0 ? wtb.JoyDialCrossfade.X : 0f);
                    holder.SetValue(Inputs.WiiTurntable.DIALCTRCLKWISE, wtb.JoyDialCrossfade.X < 0 ? -wtb.JoyDialCrossfade.X : 0f);
                    holder.SetValue(Inputs.WiiTurntable.DIALT, wtb.Dial.value);

                    holder.SetValue(Inputs.WiiTurntable.CROSSFADERLEFT, wtb.JoyDialCrossfade.Y < 0 ? -wtb.JoyDialCrossfade.Y : 0f);
                    holder.SetValue(Inputs.WiiTurntable.CROSSFADERRIGHT, wtb.JoyDialCrossfade.Y > 0 ? wtb.JoyDialCrossfade.Y : 0f);
                    holder.SetValue(Inputs.WiiTurntable.CROSSFADERT, wtb.Crossfader.value);

                    holder.SetValue(Inputs.WiiTurntable.EUPHORIA, wtb.Euphoria);
                    holder.SetValue(Inputs.WiiTurntable.SELECT, wtb.Select);
                    holder.SetValue(Inputs.WiiTurntable.START, wtb.Start);
#endregion
                    break;
            }
            
            holder.Update();

            // Resumes the timer in case this method is not called withing 100ms
            if (updateTimer != null) updateTimer.Change(100, UPDATE_SPEED);
        }

        private void device_Disconnected(object sender, DisconnectedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    Detatch();
                    OnConnectionLost?.Invoke(this);
                    MainWindow.Instance.ShowBalloon("Connection Lost", "Failed to communicate with controller. It may no longer be connected.", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Error);
                }
            ));
        }

        private void SetWiimoteInputs(Wiimote wm)
        {
            wm.irSensor.Normalize();

            holder.SetValue(Inputs.Wiimote.A, wm.buttons.A);
            holder.SetValue(Inputs.Wiimote.B, wm.buttons.B);
            holder.SetValue(Inputs.Wiimote.ONE, wm.buttons.One);
            holder.SetValue(Inputs.Wiimote.TWO, wm.buttons.Two);

            holder.SetValue(Inputs.Wiimote.UP, wm.buttons.Up);
            holder.SetValue(Inputs.Wiimote.DOWN, wm.buttons.Down);
            holder.SetValue(Inputs.Wiimote.LEFT, wm.buttons.Left);
            holder.SetValue(Inputs.Wiimote.RIGHT, wm.buttons.Right);

            holder.SetValue(Inputs.Wiimote.MINUS, wm.buttons.Minus);
            holder.SetValue(Inputs.Wiimote.PLUS, wm.buttons.Plus);
            holder.SetValue(Inputs.Wiimote.HOME, wm.buttons.Home);

            holder.SetValue(Inputs.Wiimote.TILT_RIGHT, wm.accelerometer.X > 0 ? wm.accelerometer.X : 0);
            holder.SetValue(Inputs.Wiimote.TILT_LEFT, wm.accelerometer.X < 0 ? wm.accelerometer.X : 0);
            holder.SetValue(Inputs.Wiimote.TILT_UP, wm.accelerometer.Y > 0 ? wm.accelerometer.Y : 0);
            holder.SetValue(Inputs.Wiimote.TILT_DOWN, wm.accelerometer.Y < 0 ? wm.accelerometer.Y : 0);

            holder.SetValue(Inputs.Wiimote.ACC_SHAKE_X, wm.accelerometer.X > 1.15);
            holder.SetValue(Inputs.Wiimote.ACC_SHAKE_Y, wm.accelerometer.Y > 1.15);
            holder.SetValue(Inputs.Wiimote.ACC_SHAKE_Z, wm.accelerometer.Z > 1.15);

            if (snapIRpointer && !wm.irSensor.point1.visible && !wm.irSensor.point2.visible)
            {
                if (properties.pointerMode == Property.PointerOffScreenMode.SnapX ||
                    properties.pointerMode == Property.PointerOffScreenMode.SnapXY)
                {
                    wm.irSensor.X = previousIR.X;
                }

                if (properties.pointerMode == Property.PointerOffScreenMode.SnapY ||
                    properties.pointerMode == Property.PointerOffScreenMode.SnapXY)
                {
                    wm.irSensor.Y = previousIR.Y;
                }
            }

            holder.SetValue(Inputs.Wiimote.IR_RIGHT, wm.irSensor.X > 0 ? wm.irSensor.X : 0);
            holder.SetValue(Inputs.Wiimote.IR_LEFT, wm.irSensor.X < 0 ? wm.irSensor.X : 0);
            holder.SetValue(Inputs.Wiimote.IR_UP, wm.irSensor.Y > 0 ? wm.irSensor.Y : 0);
            holder.SetValue(Inputs.Wiimote.IR_DOWN, wm.irSensor.Y < 0 ? wm.irSensor.Y : 0);

            previousIR = wm.irSensor;
        }

        private void HolderUpdate(object holderState)
        {
            if (holder == null) return;

            holder.Update();

//            float intensity = 0;
//            if (holder.Values.TryGetValue(Inputs.Flags.RUMBLE, out intensity))
//            {
//                rumbleAmount = (int)intensity;
                RumbleStep();
//            }

            SetBatteryStatus(device.BatteryLevel == BatteryStatus.Low);
        }

        void RumbleStep()
        {
            if (identifying) return;

            bool currentRumbleState = device.RumbleEnabled;

            // disable rumble for turntables
            if (!properties.useRumble || device.Type == ControllerType.Turntable)
            {
                if (currentRumbleState) device.RumbleEnabled = false;
                return;
            }

            rumbleAmount = holder.RumbleAmount;

            float dutyCycle = 0;
            float modifier = properties.rumbleIntensity * 0.5f;

            if (rumbleAmount < 256)
            {
                dutyCycle = rumbleSlowMult * (float)rumbleAmount / 256f;
            }
            else
            {
                dutyCycle = (float)rumbleAmount / 65535f;
            }

            int stopStep = (int)Math.Round(modifier * dutyCycle * rumbleStepPeriod);

            if (rumbleStepCount < stopStep)
            {
                if (!currentRumbleState) device.RumbleEnabled = true;
            }
            else
            {
                if (currentRumbleState) device.RumbleEnabled = false;
            }

            rumbleStepCount += 1;

            if (rumbleStepCount >= rumbleStepPeriod)
            {
                rumbleStepCount = 0;
            }
        }

        static System.Threading.Tasks.Task Delay(int milliseconds)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<object>();
            new System.Threading.Timer(_ => tcs.SetResult(null)).Change(milliseconds, -1);
            return tcs.Task;
        }

        private void SetBatteryStatus(bool isLow)
        {
            if (isLow && !lowBatteryFired)
            {
                Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                        {
                            statusGradient.Color = (Color)FindResource("LowBattery");
                            if (MainWindow.Instance.trayIcon.Visibility == Visibility.Visible)
                            {
                                lowBatteryFired = true;
                                MainWindow.Instance.ShowBalloon
                                (
                                    "Battery Low",
                                    dName + (!dName.Equals(device.Type.ToString()) ? " (" + device.Type.ToString() + ") " : " ")
                                    + "is running low on battery life.",
                                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Warning,
                                    System.Media.SystemSounds.Hand
                                );
                            }
                        }
                ));
            }
            else if (!isLow && lowBatteryFired)
            {
                statusGradient = (GradientStop)FindResource("AntemBlue");
                lowBatteryFired = false;
            }
        }

        private void LoadProfile(string profilePath, Holders.Holder h)
        {
            Profile loadedProfile = null;

            if (!string.IsNullOrWhiteSpace(profilePath) && File.Exists(profilePath))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Profile));

                    using (FileStream stream = File.OpenRead(profilePath))
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        loadedProfile = serializer.Deserialize(reader) as Profile;
                        reader.Close();
                        stream.Close();
                    }
                }
                catch { }
            }

            if (loadedProfile == null)
            {
                loadedProfile = UserPrefs.Instance.defaultProfile;
            }

            if (loadedProfile != null)
            {
                for (int i = 0; i < Math.Min(loadedProfile.controllerMapKeys.Count, loadedProfile.controllerMapValues.Count); i++)
                {
                    h.SetMapping(loadedProfile.controllerMapKeys[i], loadedProfile.controllerMapValues[i]);
                    CheckIR(loadedProfile.controllerMapKeys[i]);
                }
            }
        }

        private void UpdateIcon(ControllerType cType)
        {
            switch (cType)
            {
                case ControllerType.ProController:
                    icon.Source = (ImageSource)Application.Current.Resources["ProIcon"];
                    UserPrefs.Instance.UpdateDeviceIcon(devicePath, "ProIcon");
                    break;
                case ControllerType.ClassicControllerPro:
                    icon.Source = (ImageSource)Application.Current.Resources["CCPIcon"];
                    UserPrefs.Instance.UpdateDeviceIcon(devicePath, "CCPIcon");
                    break;
                case ControllerType.ClassicController:
                    icon.Source = (ImageSource)Application.Current.Resources["CCIcon"];
                    UserPrefs.Instance.UpdateDeviceIcon(devicePath, "CCIcon");
                    break;
                case ControllerType.Nunchuk:
                case ControllerType.NunchukB:
                    icon.Source = (ImageSource)Application.Current.Resources["WNIcon"];
                    UserPrefs.Instance.UpdateDeviceIcon(devicePath, "WNIcon");
                    break;

                case ControllerType.Guitar:
                    icon.Source = (ImageSource)Application.Current.Resources["WGTIcon"];
                    UserPrefs.Instance.UpdateDeviceIcon(devicePath, "WGTIcon");
                    break;

                case ControllerType.Drums:
                    icon.Source = (ImageSource)Application.Current.Resources["WDRIcon"];
                    UserPrefs.Instance.UpdateDeviceIcon(devicePath, "WDRIcon");
                    break;

                case ControllerType.Turntable:
                    icon.Source = (ImageSource)Application.Current.Resources["WTBIcon"];
                    UserPrefs.Instance.UpdateDeviceIcon(devicePath, "WTBIcon");
                    break;

                default:
                    icon.Source = (ImageSource)Application.Current.Resources["WIcon"];
                    UserPrefs.Instance.UpdateDeviceIcon(devicePath, "WIcon");
                    break;
            }
        }

        private void ApplyCalibration(Property.CalibrationPreference calPref, string calString)
        {
            // Load calibration settings
            switch (calPref)
            {
                case Property.CalibrationPreference.Default:
                    device.SetCalibration(Calibrations.CalibrationPreset.Default);
                    break;

                case Property.CalibrationPreference.More:
                    device.SetCalibration(Calibrations.CalibrationPreset.Modest);
                    break;

                case Property.CalibrationPreference.Extra:
                    device.SetCalibration(Calibrations.CalibrationPreset.Extra);
                    break;

                case Property.CalibrationPreference.Minimal:
                    device.SetCalibration(Calibrations.CalibrationPreset.Minimum);
                    break;

                case Property.CalibrationPreference.Raw:
                    device.SetCalibration(Calibrations.CalibrationPreset.None);
                    break;

                case Property.CalibrationPreference.Custom:
                    CalibrationStorage calStor = new CalibrationStorage(calString);
                    device.SetCalibration(calStor.ProCalibration);
                    device.SetCalibration(calStor.NunchukCalibration);
                    device.SetCalibration(calStor.ClassicCalibration);
                    device.SetCalibration(calStor.ClassicProCalibration);
                    device.SetCalibration(calStor.WiimoteCalibration);
                    break;
            }
        }

        private void CheckIR(string assignment)
        {
            if (assignment.StartsWith("wIR") && device != null && device.IRMode == IRCamMode.Off)
            {
                if (device.Type == ControllerType.Wiimote ||
                    device.Type == ControllerType.Nunchuk ||
                    device.Type == ControllerType.NunchukB)
                {
                    device.IRMode = IRCamMode.Basic;
                }
            }
        }

#region UI Events
        private void btnXinput_Click(object sender, RoutedEventArgs e)
        {
            if (btnXinput.ContextMenu != null)
            {
                XOption1.IsEnabled = Holders.XInputHolder.availabe[0];
                XOption2.IsEnabled = Holders.XInputHolder.availabe[1];
                XOption3.IsEnabled = Holders.XInputHolder.availabe[2];
                XOption4.IsEnabled = Holders.XInputHolder.availabe[3];

                btnXinput.ContextMenu.PlacementTarget = btnXinput;
                btnXinput.ContextMenu.IsOpen = true;
            }
        }

        private void AssignToXinputPlayer(int player)
        {
            device.BeginReading();
            device.GetStatus();

            targetXDevice = player;
            ConnectionState = DeviceState.Connected_XInput;

            RefreshState();
        }

        private void XOption_Click(object sender, RoutedEventArgs e)
        {
            if (Device.Type != ControllerType.ProController)
                MessageBox.Show("Press 1+2 on the Wii remote and press OK to continue.", "Connect Wii Remote", MessageBoxButton.OK, MessageBoxImage.Information);

            if ((device.DataStream as WinBtStream).OpenConnection() && device.DataStream.CanRead)
            {
                int tmp = 0;
                if (int.TryParse(((MenuItem)sender).Name.Replace("XOption", ""), out tmp))
                {
                    AssignToXinputPlayer(tmp);
                }
            }

            
        }

        private void typeOption_Click(object sender, RoutedEventArgs e)
        {
            switch(icon.ContextMenu.Items.IndexOf(sender))
            {
                case 0:
                    device.ForceControllerType(ControllerType.Unknown);
                    break;

                case 1:
                    device.ForceControllerType(ControllerType.ProController);
                    break;

                case 2:
                    device.ForceControllerType(ControllerType.Wiimote);
                    break;

                case 3:
                    device.ForceControllerType(ControllerType.Nunchuk);
                    break;

                case 4:
                    device.ForceControllerType(ControllerType.ClassicController);
                    break;

                case 5:
                    device.ForceControllerType(ControllerType.ClassicControllerPro);
                    break;

                case 6:
                    device.ForceControllerType(ControllerType.Guitar);
                    break;

                case 7:
                    device.ForceControllerType(ControllerType.Turntable);
                    break;

                default:
                    break;
            }

            RefreshState();
        }

        private void btnDetatch_Click(object sender, RoutedEventArgs e)
        {
            Detatch();
        }

        private void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            var config = new ConfigWindow(holder.Mappings, device.Type);
            config.ShowDialog();
            if (config.result)
            {
                foreach (KeyValuePair<string, string> pair in config.map)
                {
                    holder.SetMapping(pair.Key, pair.Value);
                    CheckIR(pair.Key);
                }
            }
        }

        private void btnIdentify_Click(object sender, RoutedEventArgs e)
        {
            bool wasConnected = Connected;

            if (wasConnected || ((device.DataStream as WinBtStream).OpenConnection() && device.DataStream.CanRead))
            {
                if (!wasConnected)
                    device.BeginReading();

                identifying = true;
                device.RumbleEnabled = true;
                Delay(500).ContinueWith(o =>
                {
                    identifying = false;
                    device.RumbleEnabled = false;
                    if (!wasConnected) device.StopReading();
                });

                // light show
                device.SetPlayerLED(1);
                const int LIGHTSHOW_LENGTH = 400;
                Delay((LIGHTSHOW_LENGTH / 7) * 1).ContinueWith(o => device.SetPlayerLED(2));
                Delay((LIGHTSHOW_LENGTH / 7) * 2).ContinueWith(o => device.SetPlayerLED(3));
                Delay((LIGHTSHOW_LENGTH / 7) * 3).ContinueWith(o => device.SetPlayerLED(4));
                Delay((LIGHTSHOW_LENGTH / 7) * 4).ContinueWith(o => device.SetPlayerLED(3));
                Delay((LIGHTSHOW_LENGTH / 7) * 5).ContinueWith(o => device.SetPlayerLED(2));
                Delay((LIGHTSHOW_LENGTH / 7) * 6).ContinueWith(o => device.SetPlayerLED(1));
                if (targetXDevice != 0)
                    Delay(LIGHTSHOW_LENGTH).ContinueWith(o => device.SetPlayerLED(targetXDevice));
            }
        }

        //private void btnVjoy_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    if (btnVjoy_image == null)
        //        return;

        //    if ((bool)e.NewValue)
        //    {
        //        btnVjoy_image.Opacity = 1.0;
        //    }
        //    else
        //    {
        //        btnVjoy_image.Opacity = 0.5;
        //    }
        //}

        private void btnXinput_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                btnXinput.Visibility = Visibility.Visible;
            }
            else
            {
                btnXinput.Visibility = Visibility.Collapsed;
            }
        }

        private void btnProperties_Click(object sender, RoutedEventArgs e)
        {
            PropWindow win = new PropWindow(properties, device.Type.ToString());
            win.ShowDialog();

            if (win.customCalibrate)
            {
                CalibrateWindow cb = new CalibrateWindow(device);
                cb.ShowDialog();

                if (cb.doSave)
                {
                    win.props.calString = cb.Calibration.ToString();
                    win.ShowDialog();
                }
                else
                {
                    win.Show();
                }
            }

            if (win.doSave)
            {
                ApplyCalibration(win.props.calPref, win.props.calString);
                properties = new Property(win.props);
                snapIRpointer = properties.pointerMode != Property.PointerOffScreenMode.Center;
                SetName(properties.name);
                UserPrefs.Instance.AddDevicePref(properties);
                UserPrefs.SavePrefs();
            }
        }
#endregion

        private void btnDebugView_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            DebugViewActivate();
#endif
        }
    }
}