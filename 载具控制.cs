Program()
{
    try { InitDatas(); ReadDatas(); } catch (Exception) { StartReady = false; }
    Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;
}
void Main(string argument, UpdateType updateSource)
{
    try
    {
        switch (updateSource)
        {
            case UpdateType.IGC:
            case UpdateType.Mod:
            case UpdateType.Script:
            case UpdateType.Terminal:
            case UpdateType.Trigger:
                if (argument.Contains("HoverMode")) HoverMode = !HoverMode;
                if (argument.Contains("HasWings")) HasWings = !HasWings;
                if (argument.Contains("EnabledCuriser")) EnabledCuriser = !EnabledCuriser;
                if (argument.Contains("EnabledThrusters")) EnabledThrusters = !EnabledThrusters;
                if (argument.Contains("EnabledGyros")) EnabledGyros = !EnabledGyros;
                if (argument.Contains("LoadConfig")) ReadDatas();
                if (argument.Contains("SaveConfig")) WriteDatas();
                break;
            case UpdateType.Update1: PoseCtrl(); ThrustControl(); WheelControl(); AutoCloseDoorController.Running(GridTerminalSystem); break;
            case UpdateType.Update10: if (!StartReady) { InitDatas(); ReadDatas(); } break;
            case UpdateType.Update100: break;
            case UpdateType.Once: break;
            default: break;
        }
        RunningDataShow();
    }
    catch (Exception e) { StartReady = false; Echo($"Error:{e.Message}"); Echo($"Error:{e.StackTrace}"); Echo($"Error:{e.Source}"); count = 0; }
}
#region InternalFunctions
private void RunningDataShow()
{
    skip = (++skip) % gap;
    if (skip == 0)
    {
        Echo($"Universe Controller Running:[{RunningSignal[(count++) % RunningSignal.Length]}]"); Echo($"Current Controller Role:[{Role}]");
        if (Role == ControllerRole.Aeroplane || Role == ControllerRole.VTOL) Echo($"Wings Mode:{HasWings}"); if (Role == ControllerRole.VTOL || Role == ControllerRole.SpaceShip) Echo($"Hover Mode:{HoverMode}");
        if (Role == ControllerRole.Aeroplane || Role == ControllerRole.VTOL || Role == ControllerRole.SpaceShip) Echo($"Curiser Mode:{EnabledCuriser}");
        Echo($"Thrusters Enabled:{EnabledThrusters}"); Echo($"Gyros Enabled:{EnabledGyros}");
    }
}
private void InitDatas()
{
    OnModeChange();
    Controller = Utils.GetT(GridTerminalSystem, (IMyShipController block) => block.IsMainCockpit || block.IsUnderControl);
    if (Utils.IsNull(Controller)) { StartReady = false; return; }
    ThrustControllerSystem.UpdateBlocks(GridTerminalSystem, Controller);
    GyroControllerSystem.UpdateBlocks(GridTerminalSystem, Controller);
    WheelsController.UpdateBlocks(GridTerminalSystem, Controller);
    WheelsController.TrackVehicle = Role == ControllerRole.TrackVehicle;
    StartReady = true;
}
private void OnModeChange() { _Target_Sealevel = sealevel = Utils.GetSealevel(Controller) ?? 0; }
private void PoseCtrl()
{
    Vector3 Rotation;
    bool EnabledGyros_Inner = EnabledGyros;
    if (Role == ControllerRole.HoverVehicle || Role == ControllerRole.TrackVehicle || Role == ControllerRole.WheelVehicle || Role == ControllerRole.SeaShip || Role == ControllerRole.Submarine)
    {
        Rotation = Utils.ProcessRotation_GroundVehicle(Controller, RotationCtrlLines, ref ForwardDirection, InitAngularDampener, AngularDampeners, MaxReactions_AngleV, DisabledRotation, ForwardDirectionOverride, PlaneNormalOverride) ?? new Vector3(Controller?.RotationIndicator ?? Vector2.Zero, Controller?.RollIndicator ?? 0);
    }
    else
    {
        Rotation = Utils.ProcessRotation(_EnabledCuriser, Controller, RotationCtrlLines, ref ForwardDirection, InitAngularDampener, AngularDampeners, ForwardOrUp, PoseMode, MaximumSpeed, MaxReactions_AngleV, Need2CtrlSignal, LocationSensetive, SafetyStage, IgnoreForwardVelocity, Refer2Velocity, DisabledRotation, ForwardDirectionOverride, PlaneNormalOverride) ?? new Vector3(Controller?.RotationIndicator ?? Vector2.Zero, Controller?.RollIndicator ?? 0);
    }
    GyroControllerSystem.SetEnabled(EnabledGyros_Inner);
    GyroControllerSystem.GyrosOverride(Rotation);
}
private void ThrustControl()
{
    if (Role == ControllerRole.TrackVehicle || Role == ControllerRole.WheelVehicle || Role == ControllerRole.HoverVehicle)
    {
        ThrustControllerSystem.SetupMode(false, true, (!EnabledThrusters), MaximumSpeed);
        ThrustControllerSystem.Running(ThrustsControlLine, 0, true);
    }
    else
    {
        Vector3 Ctrl = ThrustsControlLine;
        bool CtrlOrCruise = HoverMode || (Ctrl != Vector3.Zero);
        UpdateTargetSealevel();
        target_speed = MathHelper.Clamp(HandBrake ? 0 : (Ctrl != Vector3.Zero) ? ForwardOrUp ? LinearVelocity.Dot(Forward) : 0 : target_speed, 0, MaximumSpeed);
        ThrustControllerSystem.SetupMode((!ForwardOrUp), EnabledAllDirection, (!EnabledThrusters), CtrlOrCruise ? MaximumSpeed : target_speed);
        ThrustControllerSystem.Running(CtrlOrCruise ? Ctrl : Vector3.Forward, diffsealevel, Dampener);
    }
}
private void WheelControl()
{
    bool NoWheelCtrl = !(Role == ControllerRole.TrackVehicle || Role == ControllerRole.WheelVehicle);
    WheelsController.TrackVehicle = Role == ControllerRole.TrackVehicle;
    WheelsController.ForwardIndicator = NoWheelCtrl ? 0 : Controller?.MoveIndicator.Z ?? 0;
    WheelsController.TurnIndicator = NoWheelCtrl ? 0 : Controller?.MoveIndicator.X ?? 0;
    WheelsController.Running();
}
private double sealevel, _Target_Sealevel;
private float target_speed = 0, diffsealevel = 0;
private void UpdateTargetSealevel() { if (IgnoreLevel) diffsealevel = 0; else { sealevel = Utils.GetSealevel(Controller) ?? 0; if (!KeepLevel) _Target_Sealevel = sealevel; diffsealevel = (float)(_Target_Sealevel - sealevel) * MultAttitude; } }
private static float SetInRange_AngularDampeners(float data) => MathHelper.Clamp(data, 0.1f, 10f);
private void ReadDatas()
{
    MyConfigs.CustomDataConfigRead_INI(Me, Configs);
    if (Utils.IsNullCollection(Configs) || !Configs.ContainsKey(VehicleControllerConfigID)) { WriteDatas(); return; }
    var data = Configs[VehicleControllerConfigID];
    foreach (var configitem in data)
    {
        switch (configitem.Key)
        {
            case "HasWings": HasWings = MyConfigs.ParseBool(configitem.Value); break;
            case "EnabledCuriser": EnabledCuriser = MyConfigs.ParseBool(configitem.Value); break;
            case "HoverMode": HoverMode = MyConfigs.ParseBool(configitem.Value); break;
            case "AngularDampeners_Roll": AngularDampeners_Roll = MyConfigs.ParseFloat(configitem.Value); break;
            case "AngularDampeners_Pitch": AngularDampeners_Pitch = MyConfigs.ParseFloat(configitem.Value); break;
            case "AngularDampeners_Yaw": AngularDampeners_Yaw = MyConfigs.ParseFloat(configitem.Value); break;
            case "SafetyStage": SafetyStage = MyConfigs.ParseFloat(configitem.Value); break;
            case "MaxReactions_AngleV": MaxReactions_AngleV = MyConfigs.ParseFloat(configitem.Value); break;
            case "LocationSensetive": LocationSensetive = MyConfigs.ParseFloat(configitem.Value); break;
            case "MaxiumFlightSpeed": MaxiumFlightSpeed = MyConfigs.ParseFloat(configitem.Value); break;
            case "MaxiumHoverSpeed": MaxiumHoverSpeed = MyConfigs.ParseFloat(configitem.Value); break;
            case "MaximumCruiseSpeed": WheelsController.MaximumSpeed = MaximumCruiseSpeed = MyConfigs.ParseFloat(configitem.Value); break;
            case "Role": Role = (ControllerRole)Enum.Parse(typeof(ControllerRole), configitem.Value); break;
            case "MultAttitude": MultAttitude = MyConfigs.ParseFloat(configitem.Value); break;
            default: break;
        }
    }
}
private void WriteDatas()
{
    MyConfigs.CustomDataConfigRead_INI(Me, Configs);
    if (Utils.IsNullCollection(Configs) || !Configs.ContainsKey(VehicleControllerConfigID))
    {
        Configs.Add(VehicleControllerConfigID, new Dictionary<string, string>());
        InitParameters();
    }
    var data = Configs[VehicleControllerConfigID];
    MyConfigs.ModifyProperty(data, "HasWings", HasWings.ToString());
    MyConfigs.ModifyProperty(data, "EnabledCuriser", EnabledCuriser.ToString());
    MyConfigs.ModifyProperty(data, "HoverMode", HoverMode.ToString());
    MyConfigs.ModifyProperty(data, "AngularDampeners_Roll", AngularDampeners_Roll.ToString());
    MyConfigs.ModifyProperty(data, "AngularDampeners_Pitch", AngularDampeners_Pitch.ToString());
    MyConfigs.ModifyProperty(data, "AngularDampeners_Yaw", AngularDampeners_Yaw.ToString());
    MyConfigs.ModifyProperty(data, "SafetyStage", SafetyStage.ToString());
    MyConfigs.ModifyProperty(data, "MaxReactions_AngleV", MaxReactions_AngleV.ToString());
    MyConfigs.ModifyProperty(data, "LocationSensetive", LocationSensetive.ToString());
    MyConfigs.ModifyProperty(data, "MaxiumFlightSpeed", MaxiumFlightSpeed.ToString());
    MyConfigs.ModifyProperty(data, "MaxiumHoverSpeed", MaxiumHoverSpeed.ToString());
    MyConfigs.ModifyProperty(data, "MaximumCruiseSpeed", MaximumCruiseSpeed.ToString());
    MyConfigs.ModifyProperty(data, "Role", Role.ToString());
    MyConfigs.ModifyProperty(data, "MultAttitude", MultAttitude.ToString());
    Me.CustomName = @"Programmable Block Vehicle Controller";
    Me.CustomData = MyConfigs.CustomDataConfigSave_INI(Configs);
}
private void InitParameters() { HasWings = true; AngularDampeners_Roll = 5; AngularDampeners_Pitch = 5; AngularDampeners_Yaw = 7; SafetyStage = 3; MaxReactions_AngleV = 40; LocationSensetive = 10; EnabledCuriser = false; HoverMode = true; MaxiumFlightSpeed = 1000; MaxiumHoverSpeed = 30; MaximumCruiseSpeed = 80; }
#endregion
#region RunningProcess
private int count = 0; private string[] RunningSignal { get; } = { "- ", "\\", "| ", "/" }; private int skip = 0; private const int gap = 10;
#endregion
#region OuterControlLines
public bool HasWings { get; set; } = false;
public bool HoverMode
{
    get { switch (Role) { case ControllerRole.Helicopter: return true; case ControllerRole.VTOL: case ControllerRole.SpaceShip: return !ForwardOrUp; default: return false; } }
    set { switch (Role) { case ControllerRole.VTOL: case ControllerRole.SpaceShip: ForwardOrUp = !value; if (!ForwardOrUp) { OnModeChange(); diffsealevel = (float)(_Target_Sealevel - sealevel) * MultAttitude; } else target_speed = LinearVelocity.Length(); break; default: ForwardOrUp = true; break; } }
}
public float MaxiumFlightSpeed { get { return _MaxiumFlightSpeed; } set { _MaxiumFlightSpeed = MathHelper.Clamp(value, 30, float.MaxValue); } }
public float MaxiumHoverSpeed { get { return _MaxiumHoverSpeed; } set { _MaxiumHoverSpeed = MathHelper.Clamp(value, 5, 100); } }
public float MaximumCruiseSpeed { get { return _MaxiumSpeed * 3.6f; } set { _MaxiumSpeed = MathHelper.Clamp(Math.Abs(value / 3.6f), -360f, 360f); } }
public bool EnabledCuriser
{
    get { switch (Role) { case ControllerRole.Aeroplane: case ControllerRole.VTOL: case ControllerRole.SpaceShip: return _EnabledCuriser; case ControllerRole.SeaShip: case ControllerRole.Submarine: return true; default: return false; } }
    set { switch (Role) { case ControllerRole.Aeroplane: case ControllerRole.VTOL: case ControllerRole.SpaceShip: _EnabledCuriser = value; break; default: break; } }
}
public Vector3? ForwardDirectionOverride { get; set; } = null;
public Vector3? PlaneNormalOverride { get; set; } = null;
public float LocationSensetive
{
    get { switch (Role) { case ControllerRole.SeaShip: case ControllerRole.Submarine: case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: return 1; default: return _LocationSensetive; } }
    set { switch (Role) { case ControllerRole.SeaShip: case ControllerRole.Submarine: case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: _LocationSensetive = 1; break; default: _LocationSensetive = MathHelper.Clamp(value, 0.5f, 4f); break; } }
}
public float MaxReactions_AngleV
{
    get { switch (Role) { case ControllerRole.SeaShip: case ControllerRole.Submarine: case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: return 1; default: return _MaxReactions_AngleV; } }
    set { switch (Role) { case ControllerRole.SeaShip: case ControllerRole.Submarine: case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: _MaxReactions_AngleV = 1; break; default: _MaxReactions_AngleV = MathHelper.Clamp(value, 1f, 90f); break; } }
}
public float SafetyStage
{
    get { switch (Role) { case ControllerRole.SeaShip: case ControllerRole.Submarine: case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: return 1; default: return _SafetyStage; } }
    set { switch (Role) { case ControllerRole.SeaShip: case ControllerRole.Submarine: case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: _SafetyStage = 1; break; default: _SafetyStage = MathHelper.Clamp(value, SafetyStageMin, SafetyStageMax); break; } }
}
public bool EnabledThrusters { get; set; } = true;
public bool EnabledGyros { get; set; } = true;
public float AngularDampeners_Roll { get { AngularDampeners.Z = SetInRange_AngularDampeners(AngularDampeners.Z); return AngularDampeners.Z; } set { AngularDampeners.Z = SetInRange_AngularDampeners(value); } }
public float AngularDampeners_Yaw { get { AngularDampeners.Y = SetInRange_AngularDampeners(AngularDampeners.Y); return AngularDampeners.Y; } set { AngularDampeners.Y = SetInRange_AngularDampeners(value); } }
public float AngularDampeners_Pitch { get { AngularDampeners.X = SetInRange_AngularDampeners(AngularDampeners.X); return AngularDampeners.X; } set { AngularDampeners.X = SetInRange_AngularDampeners(value); } }
#endregion
#region PrivateSignals
private bool ForwardOrUp
{
    get { switch (Role) { case ControllerRole.VTOL: case ControllerRole.SpaceShip: return _ForwardOrUp; case ControllerRole.Aeroplane: return true; default: return false; } }
    set { switch (Role) { case ControllerRole.VTOL: case ControllerRole.SpaceShip: _ForwardOrUp = value; break; default: _ForwardOrUp = false; break; } }
}
private Vector3 ThrustsControlLine
{
    get
    {
        if (HandBrake) return Vector3.Zero;
        switch (Role)
        {
            case ControllerRole.Aeroplane: return (Controller?.MoveIndicator ?? Vector3.Zero) * Vector3.Backward;
            case ControllerRole.Helicopter: return (Controller?.MoveIndicator ?? Vector3.Zero) * Vector3.Up;
            case ControllerRole.VTOL: return (Controller?.MoveIndicator ?? Vector3.Zero) * (HandBrake ? Vector3.Zero : EnabledAllDirection ? Vector3.One : ForwardOrUp ? Vector3.Backward : Vector3.Up);
            case ControllerRole.SpaceShip: return (Controller?.MoveIndicator ?? Vector3.Zero);
            case ControllerRole.SeaShip: return new Vector3(0, 0, (Controller?.MoveIndicator.Z ?? 0));
            case ControllerRole.Submarine: return new Vector3(0, (Controller?.MoveIndicator.Y ?? 0), (Controller?.MoveIndicator.Z ?? 0));
            case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: return Vector3.Backward * (Controller?.MoveIndicator ?? Vector3.Zero);
            default: return Vector3.Zero;
        }
    }
}
private Vector4 RotationCtrlLines
{
    get
    {
        if (HandBrake) return Vector4.Zero;
        switch (Role)
        {
            case ControllerRole.Aeroplane: case ControllerRole.SpaceShip: return new Vector4(0, 0, Controller?.RotationIndicator.X ?? 0, Controller?.RotationIndicator.Y ?? 0);
            case ControllerRole.Helicopter: return new Vector4(Controller?.MoveIndicator.Z ?? 0, Controller?.MoveIndicator.X ?? 0, 0, Controller?.RollIndicator ?? 0);
            case ControllerRole.VTOL: return (HasWings && (!ForwardOrUp) && (Gravity != Vector3.Zero)) ? (new Vector4(Controller?.MoveIndicator.Z ?? 0, Controller?.MoveIndicator.X ?? 0, 0, Controller?.RollIndicator ?? 0)) : new Vector4(0, 0, Controller?.RotationIndicator.X ?? 0, Controller?.RotationIndicator.Y ?? 0);
            case ControllerRole.SeaShip: case ControllerRole.Submarine: case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: return new Vector4(0, 0, 0, Controller?.MoveIndicator.X ?? 0);
            default: return Vector4.Zero;
        }
    }
}
private bool KeepLevel { get { switch (Role) { case ControllerRole.Helicopter: return (Controller?.MoveIndicator.Y ?? 0) == 0; case ControllerRole.VTOL: case ControllerRole.SpaceShip: return (!ForwardOrUp) && (Controller?.MoveIndicator.Y ?? 0) == 0; default: return false; } } }
private bool IgnoreLevel { get { switch (Role) { case ControllerRole.Aeroplane: return true; case ControllerRole.Helicopter: return Gravity == Vector3.Zero; case ControllerRole.VTOL: case ControllerRole.SpaceShip: return ForwardOrUp || Gravity == Vector3.Zero; default: return false; } } }
private bool DisabledRotation
{
    get
    {
        switch (Role)
        {
            case ControllerRole.VTOL:
            case ControllerRole.SpaceShip:
                return false;
            case ControllerRole.Aeroplane:
            case ControllerRole.Helicopter:
            case ControllerRole.SeaShip:
            case ControllerRole.Submarine:
            case ControllerRole.TrackVehicle:
            case ControllerRole.WheelVehicle:
            case ControllerRole.HoverVehicle:
                return Gravity == Vector3.Zero;
            default: return true;
        }
    }
}
private bool EnabledAllDirection
{
    get
    {
        switch (Role)
        {
            case ControllerRole.Helicopter:
                return HandBrake || Gravity == Vector3.Zero;
            case ControllerRole.Aeroplane:
            case ControllerRole.VTOL:
                return HandBrake || Gravity == Vector3.Zero || (!HasWings);
            default: return true;
        }
    }
}
private Vector3 InitAngularDampener
{
    get
    {
        if (Role == ControllerRole.HoverVehicle || Role == ControllerRole.TrackVehicle || Role == ControllerRole.WheelVehicle)
            return new Vector3(60, 70, 60);
        return new Vector3(70, 30, 10);
    }
}
private float MaximumSpeed { get { switch (Role) { case ControllerRole.Aeroplane: return _MaxiumFlightSpeed; case ControllerRole.Helicopter: return _MaxiumHoverSpeed; case ControllerRole.VTOL: case ControllerRole.SpaceShip: return ForwardOrUp ? _MaxiumFlightSpeed : _MaxiumHoverSpeed; case ControllerRole.SeaShip: case ControllerRole.Submarine: case ControllerRole.TrackVehicle: case ControllerRole.WheelVehicle: case ControllerRole.HoverVehicle: return _MaxiumSpeed; default: return 100; } } }
private IMyShipController Controller { get { return _Controller; } set { _Controller = value; } }
private Vector3 Gravity => Controller?.GetNaturalGravity() ?? Vector3.Zero;
private Vector3 LinearVelocity => Controller?.GetShipVelocities().LinearVelocity ?? Vector3.Zero;
private Vector3D Forward => Controller?.WorldMatrix.Forward ?? Vector3D.Forward;
private bool Dampener => Controller?.DampenersOverride ?? true;
private bool HandBrake => Controller?.HandBrake ?? true;
private Vector3 ProjectLinnerVelocity_CockpitForward { get { return Utils.ProjectOnPlane(LinearVelocity, Controller?.WorldMatrix.Forward ?? Vector3.Forward); } }
private bool PoseMode { get { switch (Role) { case ControllerRole.Helicopter: return true; case ControllerRole.VTOL: return HasWings ? (!ForwardOrUp) : (_EnabledCuriser && (Gravity != Vector3.Zero)); case ControllerRole.SpaceShip: return _EnabledCuriser && (Gravity != Vector3.Zero); default: return false; } } }
private bool IgnoreForwardVelocity { get { switch (Role) { case ControllerRole.Helicopter: return false; case ControllerRole.VTOL: return ForwardOrUp || Gravity == Vector3.Zero; default: return true; } } }
private bool Need2CtrlSignal { get { switch (Role) { case ControllerRole.Helicopter: return true; case ControllerRole.VTOL: return !(ForwardOrUp || Gravity == Vector3.Zero); default: return false; } } }
private bool Refer2Velocity { get { switch (Role) { case ControllerRole.Aeroplane: case ControllerRole.Helicopter: return true; case ControllerRole.VTOL: return (HasWings && (Gravity != Vector3.Zero)) || Refer2Velocity_SpaceShip; case ControllerRole.SpaceShip: return Refer2Velocity_SpaceShip; default: return false; } } }
private bool Refer2Velocity_SpaceShip => (ProjectLinnerVelocity_CockpitForward.LengthSquared() >= _MaxiumHoverSpeed * _MaxiumHoverSpeed) && (Gravity == Vector3.Zero || ForwardOrUp);
#endregion
#region Variables
private float MultAttitude = 25f;
private float _MaxiumHoverSpeed = DefaultSpeed;
private float _MaxiumFlightSpeed = DefaultSpeed;
private float _MaxiumSpeed = DefaultSpeed;
private bool _EnabledCuriser = false;
private bool _ForwardOrUp = false;
private float _SafetyStage = 1f;
private float _LocationSensetive = 1f;
private float _MaxReactions_AngleV = 1f;
private Vector3 AngularDampeners = Vector3.One;
private Vector3 ForwardDirection;
private IMyShipController _Controller;
private bool StartReady = false;
private ControllerRole Role = ControllerRole.None;
private const float DefaultSpeed = 100;
public const float SafetyStageMin = 0f;
public const float SafetyStageMax = 9f;
private MyThrusterController ThrustControllerSystem { get; } = new MyThrusterController();
private MyGyrosController GyroControllerSystem { get; } = new MyGyrosController();
private MyWheelsController WheelsController { get; } = new MyWheelsController();
private Dictionary<string, Dictionary<string, string>> Configs { get; } = new Dictionary<string, Dictionary<string, string>>();
private MyAutoCloseDoorController AutoCloseDoorController { get; } = new MyAutoCloseDoorController();
#endregion
#region APIs
internal class MyWheelsController
{
    public MyWheelsController() { }
    public void UpdateBlocks(IMyGridTerminalSystem GridTerminalSystem, IMyShipController ShipController)
    {
        this.ShipController = null;
        if (Utils.IsNull(ShipController) || Utils.IsNull(GridTerminalSystem)) return;
        Motors_Hover = Utils.GetTs(GridTerminalSystem, (IMyTerminalBlock thrust) => thrust.BlockDefinition.SubtypeId.Contains(HoverEngineNM));
        var Group = GridTerminalSystem.GetBlockGroupWithName(WheelsGroupNM);
        SWheels = Utils.GetTs<IMyMotorSuspension>(Group);
        MWheels = Utils.GetTs<IMyMotorStator>(Group);
        this.ShipController = ShipController;
        if (Utils.IsNullCollection(Motors_Hover)) HoverDevices = false;
        else { HoverDevices = true; return; }
        if (NullWheels) return;
        Wheels = Init4GetAction(GridTerminalSystem);
    }
    internal ControllerRole ControlMode => NullWheels ? ControllerRole.None : HoverDevices ? ControllerRole.HoverVehicle : TrackVehicle ? ControllerRole.TrackVehicle : ControllerRole.WheelVehicle;
    public void Running()
    {
        if (NullWheels) return;
        Wheels?.Invoke();
    }
    private List<IMyMotorSuspension> SWheels;
    private List<IMyMotorStator> MWheels;
    private List<IMyTerminalBlock> Motors_Hover;
    private IMyShipController ShipController;
    public bool TrackVehicle { get; set; } = true;
    public float MaxiumRpm { get; set; } = 90f;
    public float DiffRpmPercentage { get; set; } = 1f;
    public float Friction { get; set; } = 100f;
    public float TurnFaction { get; set; } = 25f;
    public float MaximumSpeed { get; set; } = 20f;
    public float ForwardIndicator { get; set; }
    public float TurnIndicator { get; set; }
    public bool NullWheels => Utils.IsNull(ShipController) || (NullSWheel && NullMWheel);
    public bool NullSWheel => Utils.IsNullCollection(SWheels);
    public bool NullMWheel => Utils.IsNullCollection(MWheels);
    public bool HoverDevices { get; private set; } = false;
    private Vector3 LinearVelocity => ShipController?.GetShipVelocities().LinearVelocity ?? Vector3.Zero;
    private Action Init4GetAction(IMyGridTerminalSystem GridTerminalSystem)
    {
        Action Wheels = () => { };
        if (HoverDevices) { HoverDevices = true; return Wheels + LoadIndicateLights(GridTerminalSystem); }
        bool CanRunning = false;
        {
            var action_wheels = LoadSuspends();
            if (action_wheels != null)
                Wheels += action_wheels;
            CanRunning = CanRunning || (action_wheels != null);
        }
        {
            var action_wheels = LoadMotorWheels();
            if (action_wheels != null)
                Wheels += action_wheels;
            CanRunning = CanRunning || (action_wheels != null);
        }
        if (!CanRunning) return Wheels;
        return (Wheels + LoadIndicateLights(GridTerminalSystem));
    }
    private Action LoadIndicateLights(IMyGridTerminalSystem GridTerminalSystem)
    {
        Action UtilsCtrl = () => { };
        var brakelights = Utils.GetTs(GridTerminalSystem, (IMyInteriorLight lightblock) => lightblock.CustomName.Contains(BrakeNM));
        foreach (var item in brakelights) { UtilsCtrl += () => item.Enabled = ForwardIndicator == 0; }
        var backlights = Utils.GetTs(GridTerminalSystem, (IMyInteriorLight lightblock) => lightblock.CustomName.Contains(BackwardNM));
        foreach (var item in backlights) { UtilsCtrl += () => item.Enabled = ForwardIndicator > 0; }
        return UtilsCtrl;
    }
    private Action LoadSuspends()
    {
        if (Utils.IsNull(ShipController) || Utils.IsNullCollection(SWheels)) return null;
        Action Wheels = () => { };
        foreach (var Motor in SWheels)
        {
            Wheels += () =>
            {
                var sign = Math.Sign(ShipController.WorldMatrix.Right.Dot(Motor.WorldMatrix.Up));
                bool EnTrO = (TrackVehicle || (LinearVelocity.LengthSquared() < 4f));
                float PropulsionOverride = (EnTrO ? DiffTurns(sign) : 0) + (ForwardIndicator * sign);
                Motor.Brake = PropulsionOverride == 0;
                Motor.InvertSteer = false;
                Motor.SetValue<float>(Motor.GetProperty(MotorOverrideId).Id, Math.Sign(PropulsionOverride));
                Motor.Power = Math.Abs(PropulsionOverride);
                Motor.Steering = !TrackVehicle;
                Motor.Friction = MathHelper.Clamp((TurnIndicator != 0) ? (TrackVehicle ? (TurnFaction / Vector3.DistanceSquared(Motor.GetPosition(), ShipController.CubeGrid.GetPosition())) : Friction) : Friction, 0, Friction);
                if (Motor.Steering && EnTrO && TurnIndicator != 0)
                    Motor.SetValue<float>(Motor.GetProperty(SteerOverrideId).Id, Math.Sign(ShipController.WorldMatrix.Left.Dot(Motor.WorldMatrix.Up)) * (Motor.CustomName.Contains("Rear") ? -1 : 1));
                else
                    Motor.SetValue<float>(Motor.GetProperty(SteerOverrideId).Id, 0);
            };
        }
        return Wheels;
    }
    private Action LoadMotorWheels()
    {
        Action Wheels = () => { };
        if (Utils.IsNull(ShipController) || Utils.IsNullCollection(MWheels)) return Wheels;
        foreach (var Motor in MWheels)
        {
            Wheels += () =>
            {
                var sign = Math.Sign(ShipController.WorldMatrix.Right.Dot(Motor.WorldMatrix.Up));
                Motor.TargetVelocityRPM = -DiffTurns(sign) * MaxiumRpm;
            };
        }
        return Wheels;
    }
    private float DiffTurns(int sign)
    {
        Vector2 Indicator = new Vector2(Math.Max(Math.Sign(MaximumSpeed - LinearVelocity.Length()), 0) * ForwardIndicator * sign, TurnIndicator * DiffRpmPercentage);
        if (Indicator != Vector2.Zero)
            Indicator = Vector2.Normalize(Indicator);
        return Vector2.Dot(Vector2.One, Indicator);
    }
    private Action Wheels = () => { };
}
internal class MyThrusterController
{
    public float MaxSpeedLimit { get; set; } = 1000f;
    public float MiniValue { get { return _MiniValue; } set { _MiniValue = MathHelper.Clamp(value, 0, 1); } }
    public MyThrusterController() { }
    public void UpdateBlocks(IMyGridTerminalSystem GridTerminalSystem, IMyShipController ShipController)
    {
        if (Utils.IsNull(ShipController) || Utils.IsNull(GridTerminalSystem)) return;
        this.ShipController = ShipController;
        thrusts = Utils.GetTs(GridTerminalSystem, (IMyThrust thrust) => Utils.ExceptKeywords(thrust) && thrust.CubeGrid == this.ShipController.CubeGrid);
        if (Utils.IsNullCollection(thrusts)) return;
        _MiniValue = MiniValueC;
        StatisticU = (ref float force) => { }; StatisticD = (ref float force) => { }; StatisticL = (ref float force) => { };
        StatisticR = (ref float force) => { }; StatisticF = (ref float force) => { }; StatisticB = (ref float force) => { };
        ApplyPercentageU = (float percentage) => { }; ApplyPercentageD = (float percentage) => { }; ApplyPercentageL = (float percentage) => { };
        ApplyPercentageR = (float percentage) => { }; ApplyPercentageF = (float percentage) => { }; ApplyPercentageB = (float percentage) => { };
        EnabledU = (bool enabled) => { }; EnabledD = (bool enabled) => { }; EnabledL = (bool enabled) => { };
        EnabledR = (bool enabled) => { }; EnabledF = (bool enabled) => { }; EnabledB = (bool enabled) => { };
        foreach (var thrust in thrusts)
        {
            if (thrust.WorldMatrix.Backward.Dot(ShipController.WorldMatrix.Forward) > DirectionGate)
            {
                StatisticF += (ref float force) => { if (thrust == null) return; force += thrust.MaxEffectiveThrust; };
                ApplyPercentageF += (float percentage) => { if (thrust == null) return; thrust.ThrustOverridePercentage = MathHelper.Clamp(percentage, MiniValue, 1); };
                EnabledF += (bool enabled) => { thrust.Enabled = enabled; };
            }
            else if (thrust.WorldMatrix.Backward.Dot(ShipController.WorldMatrix.Backward) > DirectionGate)
            {
                StatisticB += (ref float force) => { if (thrust == null) return; force += thrust.MaxEffectiveThrust; };
                ApplyPercentageB += (float percentage) => { if (thrust == null) return; thrust.ThrustOverridePercentage = MathHelper.Clamp(percentage, MiniValue, 1); };
                EnabledB += (bool enabled) => { thrust.Enabled = enabled; };
            }
            else if (thrust.WorldMatrix.Backward.Dot(ShipController.WorldMatrix.Up) > DirectionGate)
            {
                StatisticU += (ref float force) => { if (thrust == null) return; force += thrust.MaxEffectiveThrust; };
                ApplyPercentageU += (float percentage) => { if (thrust == null) return; thrust.ThrustOverridePercentage = MathHelper.Clamp(percentage, MiniValue, 1); };
                EnabledU += (bool enabled) => { thrust.Enabled = enabled; };
            }
            else if (thrust.WorldMatrix.Backward.Dot(ShipController.WorldMatrix.Down) > DirectionGate)
            {
                StatisticD += (ref float force) => { if (thrust == null) return; force += thrust.MaxEffectiveThrust; };
                ApplyPercentageD += (float percentage) => { if (thrust == null) return; thrust.ThrustOverridePercentage = MathHelper.Clamp(percentage, MiniValue, 1); };
                EnabledD += (bool enabled) => { thrust.Enabled = enabled; };
            }
            else if (thrust.WorldMatrix.Backward.Dot(ShipController.WorldMatrix.Left) > DirectionGate)
            {
                StatisticL += (ref float force) => { if (thrust == null) return; force += thrust.MaxEffectiveThrust; };
                ApplyPercentageL += (float percentage) => { if (thrust == null) return; thrust.ThrustOverridePercentage = MathHelper.Clamp(percentage, MiniValue, 1); };
                EnabledL += (bool enabled) => { thrust.Enabled = enabled; };
            }
            else if (thrust.WorldMatrix.Backward.Dot(ShipController.WorldMatrix.Right) > DirectionGate)
            {
                StatisticR += (ref float force) => { if (thrust == null) return; force += thrust.MaxEffectiveThrust; };
                ApplyPercentageR += (float percentage) => { if (thrust == null) return; thrust.ThrustOverridePercentage = MathHelper.Clamp(percentage, MiniValue, 1); };
                EnabledR += (bool enabled) => { thrust.Enabled = enabled; };
            }
        }
    }
    public void SetupMode(bool UpOrForward, bool EnableAll, bool DisableAll, float MaximumSpeed)
    {
        if (NullThrust) return;
        if (DisableAll) { EnabledU(false); EnabledF(false); EnabledD(false); EnabledL(false); EnabledR(false); EnabledB(false); return; }
        EnabledU(EnableAll || UpOrForward); EnabledF(EnableAll || (!UpOrForward)); EnabledD(EnableAll); EnabledL(EnableAll); EnabledR(EnableAll); EnabledB(EnableAll); MaxSpeedLimit = MaximumSpeed;
        MiniValue = DisableAll ? 0 : MiniValueC;
    }
    public void Running(Vector3 MoveIndicate, float SealevelDiff = 0, bool EnabledDampener = true)
    {
        if (NullThrust) return;
        if (ShipController == null) { foreach (var thrust in thrusts) thrust.ThrustOverridePercentage = 0; return; }
        var velocity = (EnabledDampener ? (LinearVelocity - MaxSpeedLimit * Vector3.TransformNormal(MoveIndicate, ShipController.WorldMatrix)) : Vector3.Zero);
        var ReferValue = (velocity * Math.Max(1, Gravity.Length()) + ((Gravity == Vector3.Zero) ? Vector3.Zero : (Gravity * (1 + SealevelDiff) / GetMultipy()))) * ShipMass;
        Percentage6Direction(ReferValue, velocity);
    }
    private float GetMultipy()
    {
        if (Gravity == Vector3.Zero) return 1;
        var value = Math.Abs(Vector3.Normalize(Gravity).Dot(ShipController.WorldMatrix.Down));
        if (value == 0) return 1;
        return MathHelper.Clamp(1 / value, 1, 20f);
    }
    public void SetAll(bool Enabled)
    {
        EnabledU(Enabled);
        EnabledD(Enabled);
        EnabledL(Enabled);
        EnabledR(Enabled);
        EnabledF(Enabled);
        EnabledB(Enabled);
        ApplyPercentageU(0);
        ApplyPercentageD(0);
        ApplyPercentageL(0);
        ApplyPercentageR(0);
        ApplyPercentageF(0);
        ApplyPercentageB(0);
    }
    private float[] StatisticThrustForce6Direction()
    {
        float[] Force6Direction = new float[6];
        StatisticU(ref Force6Direction[(int)Base6Directions.Direction.Up]);
        StatisticD(ref Force6Direction[(int)Base6Directions.Direction.Down]);
        StatisticL(ref Force6Direction[(int)Base6Directions.Direction.Left]);
        StatisticR(ref Force6Direction[(int)Base6Directions.Direction.Right]);
        StatisticF(ref Force6Direction[(int)Base6Directions.Direction.Forward]);
        StatisticB(ref Force6Direction[(int)Base6Directions.Direction.Backward]);
        return Force6Direction;
    }
    private float[] RequiredForce6Direction(Vector3 Force)
    {
        float[] Force6Direction = new float[6];
        Force6Direction[(int)Base6Directions.Direction.Forward] = Force.Dot(ShipController.WorldMatrix.Backward);
        Force6Direction[(int)Base6Directions.Direction.Backward] = Force.Dot(ShipController.WorldMatrix.Forward);
        Force6Direction[(int)Base6Directions.Direction.Up] = Force.Dot(ShipController.WorldMatrix.Down);
        Force6Direction[(int)Base6Directions.Direction.Down] = Force.Dot(ShipController.WorldMatrix.Up);
        Force6Direction[(int)Base6Directions.Direction.Left] = Force.Dot(ShipController.WorldMatrix.Right);
        Force6Direction[(int)Base6Directions.Direction.Right] = Force.Dot(ShipController.WorldMatrix.Left);
        return Force6Direction;
    }
    private bool[] VelocityOverGate(Vector3 velocity)
    {
        bool[] Force6Direction = new bool[6];
        Force6Direction[(int)Base6Directions.Direction.Forward] = velocity.Dot(ShipController.WorldMatrix.Backward) > VelocityGate;
        Force6Direction[(int)Base6Directions.Direction.Backward] = velocity.Dot(ShipController.WorldMatrix.Forward) > VelocityGate;
        Force6Direction[(int)Base6Directions.Direction.Up] = velocity.Dot(ShipController.WorldMatrix.Down) > VelocityGate;
        Force6Direction[(int)Base6Directions.Direction.Down] = velocity.Dot(ShipController.WorldMatrix.Up) > VelocityGate;
        Force6Direction[(int)Base6Directions.Direction.Left] = velocity.Dot(ShipController.WorldMatrix.Right) > VelocityGate;
        Force6Direction[(int)Base6Directions.Direction.Right] = velocity.Dot(ShipController.WorldMatrix.Left) > VelocityGate;
        return Force6Direction;
    }
    private void Percentage6Direction(Vector3 Force, Vector3 OV)
    {
        float[] TF = StatisticThrustForce6Direction();
        float[] RF = RequiredForce6Direction(Force);
        bool[] OF = VelocityOverGate(OV);
        float[] Percentage = new float[6];
        for (int index = 0; index < 6; index++)
            Percentage[index] = MathHelper.Clamp((TF[index] != 0) ? OF[index] ? 1 : (RF[index] / TF[index]) : 0, 0, 1);
        ApplyPercentageU(Percentage[(int)Base6Directions.Direction.Up]);
        ApplyPercentageD(Percentage[(int)Base6Directions.Direction.Down]);
        ApplyPercentageL(Percentage[(int)Base6Directions.Direction.Left]);
        ApplyPercentageR(Percentage[(int)Base6Directions.Direction.Right]);
        ApplyPercentageF(Percentage[(int)Base6Directions.Direction.Forward]);
        ApplyPercentageB(Percentage[(int)Base6Directions.Direction.Backward]);
    }
    private bool NullThrust { get { return Utils.IsNullCollection(thrusts); } }
    private Vector3 LinearVelocity => ShipController?.GetShipVelocities().LinearVelocity ?? Vector3.Zero;
    private Vector3 Gravity => ShipController?.GetNaturalGravity() ?? Vector3.Zero;
    private float ShipMass => ShipController?.CalculateShipMass().TotalMass ?? 1;
    private const double DirectionGate = 0.95;
    private const float MiniValueC = 1e-6f;
    private const float VelocityGate = 50f;
    private float _MiniValue;
    private List<IMyThrust> thrusts;
    private IMyShipController ShipController;
    #region DoingActions
    private MyActionRef<float> StatisticU;
    private MyActionRef<float> StatisticD;
    private MyActionRef<float> StatisticL;
    private MyActionRef<float> StatisticR;
    private MyActionRef<float> StatisticF;
    private MyActionRef<float> StatisticB;
    private Action<float> ApplyPercentageU;
    private Action<float> ApplyPercentageD;
    private Action<float> ApplyPercentageL;
    private Action<float> ApplyPercentageR;
    private Action<float> ApplyPercentageF;
    private Action<float> ApplyPercentageB;
    private Action<bool> EnabledU;
    private Action<bool> EnabledD;
    private Action<bool> EnabledL;
    private Action<bool> EnabledR;
    private Action<bool> EnabledF;
    private Action<bool> EnabledB;
    #endregion
}
internal class MyGyrosController
{
    public Vector3 PowerScale3Axis { get; set; } = Vector3.One;
    public MyGyrosController() { }
    public void UpdateBlocks(IMyGridTerminalSystem GridTerminalSystem, IMyShipController ShipController)
    {
        if (Utils.IsNull(ShipController) || Utils.IsNull(GridTerminalSystem)) return;
        this.ShipController = ShipController;
        gyros = Utils.GetTs(GridTerminalSystem, (IMyGyro gyro) => Utils.ExceptKeywords(gyro) && gyro.CubeGrid == ShipController.CubeGrid);
        if (Utils.IsNullCollection(gyros)) return;
    }
    public void GyrosOverride(Vector3? RotationIndicate)
    {
        if (Utils.IsNullCollection(gyros)) return;
        foreach (var gyro in gyros)
        {
            gyro.GyroOverride = RotationIndicate.HasValue && (ShipController != null);
            if (ShipController == null) gyro.Roll = gyro.Yaw = gyro.Pitch = 0;
        }
        if (!RotationIndicate.HasValue) return;
        Matrix matrix_Main = Utils.GetWorldMatrix(ShipController);
        foreach (var gyro in gyros)
        {
            var result = Vector3.TransformNormal(RotationIndicate.Value * PowerScale3Axis, matrix_Main * Matrix.Transpose(Utils.GetWorldMatrix(gyro)));
            gyro.Roll = result.Z; gyro.Yaw = result.Y; gyro.Pitch = result.X;
        }
    }
    public void SetPowerPercentage(float power)
    {
        if (gyros == null || gyros.Count < 1) return;
        foreach (var gyro in gyros)
            gyro.GyroPower = power;
    }
    public void SetEnabled(bool Enabled)
    {
        if (gyros == null || gyros.Count < 1) return;
        foreach (var gyro in gyros)
            gyro.Enabled = Enabled;
    }
    public void SetOverride(bool Enabled)
    {
        if (gyros == null || gyros.Count < 1) return;
        foreach (var gyro in gyros)
            gyro.GyroOverride = Enabled;
    }
    private List<IMyGyro> gyros;
    private IMyShipController ShipController;
}
internal static class MyConfigs
{
    public static Dictionary<string, Dictionary<string, string>> CustomDataConfigRead_INI(string CustomData)
    {
        var lines = CustomData.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines == null || lines.Length < 1) return null;
        Dictionary<string, Dictionary<string, string>> Configs = new Dictionary<string, Dictionary<string, string>>();
        string current_block_name = "";
        for (int index = 0; index < lines.Length; index++)
        {
            var line = RemoveStartEndEmpty(RemoveIniComment(lines[index]));
            if (IsNewBlockStart(line))
            {
                current_block_name = NewBlockName(line);
                Configs.Add(current_block_name, new Dictionary<string, string>());
                continue;
            }
            if (!Configs.ContainsKey(current_block_name))
                continue;
            GetProperty(Configs[current_block_name], line);
        }
        return Configs;
    }
    public static void CustomDataConfigRead_INI(IMyTerminalBlock ShipController, Dictionary<string, Dictionary<string, string>> Configs)
    {
        if (ShipController == null || Configs == null) return;
        var lines = ShipController.CustomData.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines == null || lines.Length < 1) return;
        string current_block_name = "";
        for (int index = 0; index < lines.Length; index++)
        {
            var line = RemoveStartEndEmpty(RemoveIniComment(lines[index]));
            if (IsNewBlockStart(line))
            {
                current_block_name = NewBlockName(line);
                if (!Configs.ContainsKey(current_block_name))
                    Configs.Add(current_block_name, new Dictionary<string, string>());
                continue;
            }
            if (!Configs.ContainsKey(current_block_name))
                continue;
            GetProperty(Configs[current_block_name], line);
        }
    }
    public static void ModifyProperty(Dictionary<string, string> Properties, string key, string value)
    {
        if (Properties == null) return;
        if (Properties.ContainsKey(key))
            Properties.Remove(key);
        Properties.Add(key, value);
    }
    public static string CustomDataConfigSave_INI(Dictionary<string, Dictionary<string, string>> ConfigTree)
    {
        if (ConfigTree == null || ConfigTree.Count < 1) return "";
        StringBuilder _str = new StringBuilder();
        _str.Clear();
        foreach (var ConfigBlock in ConfigTree)
        {
            _str.Append($"[{ConfigBlock.Key}]\n\r");
            foreach (var ConfigItem in ConfigBlock.Value)
                _str.Append($"{ConfigItem.Key}={ConfigItem.Value}\n\r");
        }
        return _str.ToString();
    }
    #region BasicFunctions
    public static int ParseInt(string str) { int value; if (!int.TryParse(str, out value)) value = 0; return value; }
    public static float ParseFloat(string str) { float value; if (!float.TryParse(str, out value)) value = 0; return value; }
    public static double ParseDouble(string str) { double value; if (!double.TryParse(str, out value)) value = 0; return value; }
    public static bool ParseBool(string str) { bool value = false; if (str == "yes" || str == "true") value = true; else if (str == "no" || str == "false") value = false; return value; }
    public static string RemoveIniComment(string line) { var com_start_index = line.IndexOf(';'); if (com_start_index < 0) return line; return line.Remove(com_start_index); }
    public static string RemoveStartEndEmpty(string str) => str.TrimStart(' ', '\t').TrimEnd(' ', '\t');
    public static bool IsNewBlockStart(string str) => str.StartsWith("[") && str.EndsWith("]");
    public static string NewBlockName(string str) => str.TrimStart('[').TrimEnd(']');
    public static void GetProperty(Dictionary<string, string> Properties, string line)
    {
        if (Properties == null) return;
        var key_value = line.Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
        if (key_value == null || key_value.Length < 1) return;
        for (int index = 0; index < key_value.Length; index++)
            key_value[index] = RemoveStartEndEmpty(key_value[index]);
        if (key_value.Length == 1)
        {
            ModifyProperty(Properties, key_value[0], "");
            return;
        }
        ModifyProperty(Properties, key_value[0], key_value[1]);
    }
    #endregion
}
internal static class Utils
{
    public static double? GetSealevel(IMyShipController Controller) { double value; if (IsNull(Controller) || (!Controller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out value))) return null; return value; }
    public static Vector3? ProcessRotation(bool _EnabledCuriser, IMyShipController ShipController, Vector4 RotationCtrlLines, ref Vector3 ForwardDirection, Vector3? InitAngularDampener = null, Vector3? AngularDampeners = null, bool ForwardOrUp = false, bool PoseMode = false, float MaximumSpeedLimited = 100f, float MaxReactions_AngleV = 1f, bool Need2CtrlSignal = true, float LocationSensetive = 1f, float SafetyStage = 1f, bool IgnoreForwardVelocity = true, bool Refer2Velocity = true, bool DisabledRotation = true, Vector3? ForwardDirectionOverride = null, Vector3? PlaneNormalOverride = null)
    {
        if (IsNull(ShipController) || DisabledRotation) return null;
        Vector3? current_gravity = ShipController?.GetNaturalGravity();
        Vector3? ReferNormal;
        if (PlaneNormalOverride.HasValue && PlaneNormalOverride.Value != Vector3.Zero)
        {
            ReferNormal = PlaneNormalOverride;
        }
        else
        {
            Vector3? current_velocity_linear = Refer2Velocity ? ((Vector3?)(ProjectLinnerVelocity_CockpitForward(ShipController, Refer2Velocity, IgnoreForwardVelocity)
                - ((Need2CtrlSignal ? (Vector3.ClampToSphere((-ShipController.WorldMatrix.Forward * RotationCtrlLines.X + ShipController.WorldMatrix.Right * RotationCtrlLines.Y), 1) * MaximumSpeedLimited) : Vector3.Zero)))) : null;
            if (!current_gravity.HasValue)
                ReferNormal = current_velocity_linear;
            else if (!current_velocity_linear.HasValue)
                ReferNormal = current_gravity;
            else
                ReferNormal = Vector3.ClampToSphere(current_velocity_linear.Value * LocationSensetive + Dampener(current_gravity.Value) * SafetyStage, 1f);
        }
        if (IsNull(ReferNormal)) { return null; }
        Vector3 Direciton;
        if (!IsNull(ForwardDirectionOverride))
        {
            Direciton = ForwardDirectionOverride.Value + RotationCtrlLines.W * ShipController.WorldMatrix.Right - RotationCtrlLines.Z * ShipController.WorldMatrix.Up;
        }
        else
        {
            if (RotationCtrlLines.W != 0 || RotationCtrlLines.Z != 0)
                ForwardDirection = ShipController.WorldMatrix.Forward;
            if (_EnabledCuriser && ForwardOrUp && (current_gravity != null))
            {
                ForwardDirection = ProjectOnPlane(ForwardDirection, current_gravity.Value);
                if (ForwardDirection == Vector3.Zero)
                    ForwardDirection = ProjectOnPlane(ShipController.WorldMatrix.Down, current_gravity.Value);
            }
            if (ForwardDirection != Vector3.Zero)
                ForwardDirection = ScaleVectorTimes(Vector3.Normalize(ForwardDirection));
            Direciton = ForwardDirection + RotationCtrlLines.W * ShipController.WorldMatrix.Right - RotationCtrlLines.Z * ShipController.WorldMatrix.Up;
        }
        return (ProcessDampeners(ShipController, InitAngularDampener, AngularDampeners) + (new Vector3(
            Dampener(PoseMode && (ReferNormal.Value != Vector3.Zero) ? Calc_Direction_Vector(ReferNormal.Value, ShipController.WorldMatrix.Backward) : Calc_Direction_Vector(Direciton, ShipController.WorldMatrix.Down)),
            Dampener(SetupAngle(Calc_Direction_Vector(Direciton, ShipController.WorldMatrix.Right), Calc_Direction_Vector(Direciton, ShipController.WorldMatrix.Forward))),
            (ReferNormal.Value != Vector3.Zero) ? Dampener(SetupAngle(Calc_Direction_Vector(ReferNormal.Value, ShipController.WorldMatrix.Left), Calc_Direction_Vector(ReferNormal.Value, ShipController.WorldMatrix.Down))) : 0
            ) * MaxReactions_AngleV));
    }
    public static Vector3? ProcessRotation_GroundVehicle(IMyShipController ShipController, Vector4 RotationCtrlLines, ref Vector3 ForwardDirection, Vector3? InitAngularDampener = null, Vector3? AngularDampeners = null, float MaxReactions_AngleV = 1f, bool DisabledRotation = true, Vector3? ForwardDirectionOverride = null, Vector3? PlaneNormalOverride = null)
    {
        if (IsNull(ShipController) || DisabledRotation) return null;
        Vector3 ReferNormal = PlaneNormalOverride ?? ShipController?.GetNaturalGravity() ?? Vector3.Zero;
        if (Vector3.IsZero(ReferNormal)) return null;
        Vector3 Direciton = (ForwardDirectionOverride ?? ShipController.WorldMatrix.Forward) + RotationCtrlLines.W * ShipController.WorldMatrix.Right;
        ForwardDirection = ProjectOnPlane(Direciton, ReferNormal);
        return ProcessDampeners(ShipController, InitAngularDampener, AngularDampeners) +
            new Vector3(Calc_Direction_Vector(ReferNormal, ShipController.WorldMatrix.Backward),
            Dampener(SetupAngle(Calc_Direction_Vector(Direciton, ShipController.WorldMatrix.Right), Calc_Direction_Vector(Direciton, ShipController.WorldMatrix.Forward))) * 1800000f,
            Dampener(SetupAngle(Calc_Direction_Vector(ReferNormal, ShipController.WorldMatrix.Left), Calc_Direction_Vector(ReferNormal, ShipController.WorldMatrix.Down)))) * MaxReactions_AngleV;
    }
    public static float SetupAngle(float current_angular_local, float current_angular_add) { if (Math.Abs(current_angular_local) < 0.005f && current_angular_add < 0f) return current_angular_add; return current_angular_local; }
    public static float Calc_Direction_Vector(Vector3 vector, Vector3 direction) => Vector3.Normalize(direction).Dot(vector);
    public static Vector3 ScaleVectorTimes(Vector3 vector, float Times = 10f) => vector * Times;
    public static Vector3 ProjectLinnerVelocity_CockpitForward(IMyShipController ShipController, bool EnableToGet = true, bool IgnoreForwardVelocity = false) { if (ShipController == null) return Vector3.Zero; var LinearVelocity = EnableToGet ? ShipController.GetShipVelocities().LinearVelocity : Vector3D.Zero; if (IgnoreForwardVelocity) return ProjectOnPlane(LinearVelocity, ShipController.WorldMatrix.Forward); else return LinearVelocity; }
    public static Vector3 ProcessDampeners(IMyShipController ShipController, Vector3? InitAngularDampener = null, Vector3? AngularDampeners = null)
    {
        if (ShipController == null) return Vector3.Zero;
        var temp = Vector3.TransformNormal(ShipController.GetShipVelocities().AngularVelocity, Matrix.Transpose(ShipController.WorldMatrix));
        var a_temp = Vector3.Abs(temp);
        var _InitAngularDampener = InitAngularDampener ?? (new Vector3(70, 50, 10));
        return Vector3.Clamp(a_temp * temp * _InitAngularDampener / 4, -_InitAngularDampener, _InitAngularDampener) * (AngularDampeners ?? Vector3.One);
    }
    public static Vector3 ProjectOnPlane(Vector3 direction, Vector3 planeNormal) => Vector3.ProjectOnPlane(ref direction, ref planeNormal);
    public static float Dampener(float value) => value * Math.Abs(value);
    public static Vector3 Dampener(Vector3 value) => value * Math.Abs(value.Length());
    public static bool IsNull(Vector3? Value) => Value == null || Value.Value == Vector3.Zero;
    public static bool IsNull(Vector3D? Value) => Value == null || Value.Value == Vector3D.Zero;
    public static bool IsNullCollection<T>(ICollection<T> Value) => Value == null || Value.Count < 1;
    public static bool IsNull<T>(T Value) where T : class => Value == null;
    public static T GetT<T>(IMyGridTerminalSystem gridTerminalSystem, Func<T, bool> requst = null) where T : class { List<T> Items = GetTs<T>(gridTerminalSystem, requst); if (IsNullCollection(Items)) return null; else return Items.First(); }
    public static List<T> GetTs<T>(IMyGridTerminalSystem gridTerminalSystem, Func<T, bool> requst = null) where T : class { if (gridTerminalSystem == null) return null; List<T> Items = new List<T>(); gridTerminalSystem.GetBlocksOfType<T>(Items, requst); return Items; }
    public static T GetT<T>(IMyBlockGroup blockGroup, Func<T, bool> requst = null) where T : class
    {
        List<T> Items = GetTs(blockGroup, requst);
        if (Items.Count > 0)
            return Items[0];
        else
            return null;
    }
    public static List<T> GetTs<T>(IMyBlockGroup blockGroup, Func<T, bool> requst = null) where T : class
    {
        if (blockGroup == null) return null;
        List<T> Items = new List<T>();
        blockGroup.GetBlocksOfType<T>(Items, requst);
        return Items;
    }
    public static Matrix GetWorldMatrix(IMyTerminalBlock ShipController) { Matrix me_matrix; ShipController.Orientation.GetMatrix(out me_matrix); return me_matrix; }
    public static bool ExceptKeywords(IMyTerminalBlock block) { foreach (var item in BlackList_ShipController) { if (block.BlockDefinition.SubtypeId.Contains(item)) return false; } return true; }
    private static readonly string[] BlackList_ShipController = new string[] { "Hover", "Torpedo", "Torp", "Payload", "Missile", "At_Hybrid_Main_Thruster_Large", "At_Hybrid_Main_Thruster_Small", };
}
delegate void MyActionRef<T>(ref T value);
internal class MyAutoCloseDoorController
{
    private List<MyAutoCloseDoorTimmer> Timers { get; } = new List<MyAutoCloseDoorTimmer>();
    public void UpdateBlocks(IMyGridTerminalSystem GridTerminalSystem) { var doors_group = GridTerminalSystem.GetBlockGroupWithName(ACDoorsGroupNM); if (doors_group == null) return; var doors = Utils.GetTs<IMyDoor>(doors_group); foreach (var door in doors) { Timers.Add(new MyAutoCloseDoorTimmer(door)); } }
    public void Running(IMyGridTerminalSystem GridTerminalSystem) { try { if (Timers.Count == 0) UpdateBlocks(GridTerminalSystem); else { foreach (var Timer in Timers) { Timer.Running(); } } } catch (Exception) { Timers.Clear(); } }
}
internal class MyAutoCloseDoorTimmer
{
    public MyAutoCloseDoorTimmer(IMyDoor Door) { this.Door = Door; }
    public void Running()
    {
        if (Door == null) return;
        switch (Door.Status)
        {
            case DoorStatus.Opening: Count = Gap; return;
            case DoorStatus.Open: if (Count > 0) Count--; else Door.CloseDoor(); return;
            default: break;
        }
    }
    private readonly IMyDoor Door;
    private const int Gap = 20;
    private int Count;
}
#endregion
#region ConstValues
internal enum ControllerRole { None, Aeroplane, Helicopter, VTOL, SpaceShip, SeaShip, Submarine, TrackVehicle, WheelVehicle, HoverVehicle }
internal const string HoverEngineNM = "Hover";
internal const string WheelsGroupNM = @"Wheels";
internal const string ACDoorsGroupNM = @"ACDoors";
internal const string BrakeNM = @"Brake";
internal const string BackwardNM = @"Backward";
internal const string MotorOverrideId = @"Propulsion override";
internal const string SteerOverrideId = @"Steer override";
internal const string VehicleControllerConfigID = @"VehicleController";
#endregion