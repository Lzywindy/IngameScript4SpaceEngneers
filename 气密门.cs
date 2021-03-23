private const string GroupAll="AirLocker";
private const string Group_Inner_All="Inner";
private const string Group_Outer_All="Outer";
private const string AtomsphereOxygonDetector_NM="Atomsphere Oxygon Detector";
private const string Inner_Dpd="Danger";
private const string Inner_Pd="Arrow";
private const string Outer_Dpd="Cross";
private const string Outer_Pd="Arrow";
private const string AutoCloseDoor_NM="AutoClose";
private static readonly string GroupAirlockerVent_NM=$"{GroupAll} Vent";
private static readonly string GroupInner=$"{GroupAll} {Group_Inner_All}";
private static readonly string GroupOuter=$"{GroupAll} {Group_Outer_All}";
private static string Cmd_TriggerRoomState="TriggerRoomState";
Program()
{
    oxygonManager = new OxygonManager(GridTerminalSystem);
    AirLockerSys.AtomsphereOxygonDetector = GridTerminalSystem.GetBlockWithName(AtomsphereOxygonDetector_NM) as IMyAirVent;
    if(AirLockerSys.AtomsphereOxygonDetector == null)
        throw new Exception("No Atomsphere Oxygon Detector Refered!");
    GetGroups();
    Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100;

}
void Main(string argument, UpdateType updateSource)
{
    if(updateSource.HasFlag(UpdateType.Terminal) || updateSource.HasFlag(UpdateType.Trigger))
        SetConsoleCommade(argument);
    else if(updateSource.HasFlag(UpdateType.Update10))
    {
        RoomAirManager();
        AutoCloseDoorRunner();
        oxygonManager.Running();
    }
}
void GetGroups()
{
    string[] GroupNames=Me.CustomData.Split(new string[]{"\n","\r",",",";","." },StringSplitOptions.RemoveEmptyEntries);
    if(GroupNames == null)
        throw new Exception("No Air Lock Room Need to be Managed!");
    foreach(var GroupName in GroupNames)
    {
        Get_AirLockerGroup(GroupName);
        AutoCloseDoor_GroupCreater(GroupName);
    }
}
void Get_AirLockerGroup(string GroupName)
{
    if(!GroupName.Contains($"-{GroupAll}"))
        return;
    GroupName = GroupName.Replace(" ", "");
    GroupName = GroupName.Replace("\t", "");
    GroupName = GroupName.Replace($"-{GroupAll}", "");
    var RoomEquipmentGroup = GridTerminalSystem.GetBlockGroupWithName(GroupName);
    if(RoomEquipmentGroup == null)
        throw new Exception("No Air Locker Group Refered!");
    var AirLocker=new AirLockerSys(RoomEquipmentGroup);
    AirLockers.Add(AirLocker);
    SetConsoleCommade += (string cmd) => { if(!cmd.StartsWith(AirLocker.Name)) return; string _cmd= cmd.TrimStart(AirLocker.Name.ToArray()); _cmd = _cmd.TrimStart(' '); AirLocker.SetConsoleCommade(_cmd); };
    RoomAirManager += () => { AirLocker.RoomAirManager(); };
}
void AutoCloseDoor_GroupCreater(string GroupName)
{
    if(!GroupName.Contains($"-{AutoCloseDoor_NM}"))
        return;
    GroupName = GroupName.Replace(" ", "");
    GroupName = GroupName.Replace("\t", "");
    GroupName = GroupName.Replace($"-{AutoCloseDoor_NM}", "");
    var RoomEquipmentGroup = GridTerminalSystem.GetBlockGroupWithName(GroupName);
    if(RoomEquipmentGroup == null)
        throw new Exception("No Auto Close Door Group Refered!");
    var doors=new List<IMyDoor>();
    RoomEquipmentGroup.GetBlocksOfType(doors);
    foreach(var door in doors)
    {
        var door_auto=new AutoCloseDoor(door);
        AutoCloseDoorRunner += () => { door_auto.Running(); };
        AutoClosers.Add(door_auto);
    }
}
delegate void ActionRef<T>(ref T value);
delegate void ActionRef<T1, T2>(T1 value1, ref T2 value2);
public enum AirLockState { Idle, Start, Pressure, Depressure, Init }
private readonly List<AirLockerSys> AirLockers=new List<AirLockerSys>();
private readonly List<AutoCloseDoor> AutoClosers=new List<AutoCloseDoor>();
private OxygonManager oxygonManager;
private Action<string> SetConsoleCommade=(string cmd)=>{ };
private Action RoomAirManager=()=>{ };
private Action AutoCloseDoorRunner=()=>{ };
class OxygonManager
{
    private float Oxygon_Total;
    private float Oxygon_Current;
    public OxygonManager(IMyGridTerminalSystem GridTerminalSystem)
    {
        List<IMyGasTank> OxygonTanks=new List<IMyGasTank>();
        GridTerminalSystem.GetBlocksOfType(OxygonTanks, (IMyGasTank tank) => { return ( tank.BlockDefinition.SubtypeId == "OxygenTankSmall" || tank.BlockDefinition.SubtypeId == "" ); });
        foreach(var tank in OxygonTanks)
        {
            Statistic_Oxygon_Total += (ref float Total) => { Total += tank.Capacity; };
            Statistic_Oxygon_Current += (ref float Total) => { Total += ( tank.Capacity * (float) tank.FilledRatio ); };
        }
        List<IMyGasGenerator> GasGenerators=new List<IMyGasGenerator>();
        GridTerminalSystem.GetBlocksOfType(GasGenerators);
        foreach(var GasGenerator in GasGenerators)
        {
            Enabled_H2O2Generator += (bool OnOff) => { GasGenerator.Enabled = OnOff; };
        }
    }
    public void Running()
    {
        Oxygon_Total = 0;
        Oxygon_Current = 0;
        Statistic_Oxygon_Total(ref Oxygon_Total);
        Statistic_Oxygon_Current(ref Oxygon_Current);
        Enabled_H2O2Generator(( Oxygon_Current / Oxygon_Total ) < 0.8f);

    }
    private ActionRef<float> Statistic_Oxygon_Total=(ref float Total)=>{ };
    private ActionRef<float> Statistic_Oxygon_Current=(ref float Total)=>{ };
    private Action<bool> Enabled_H2O2Generator=(bool OnOff)=>{ };

}
class AirLockerSys
{
    public AirLockerSys(IMyBlockGroup RoomEquipmentGroup)
    {
        Name = RoomEquipmentGroup.Name;
        List<IMyDoor> Doors=new List<IMyDoor>();
        RoomEquipmentGroup.GetBlocksOfType(Doors);
        foreach(var door in Doors)
        {
            DoorGroupAction += (string GroupName, bool Open) => { if(!door.CustomName.Contains(GroupName)) return; if(Open) door.OpenDoor(); else door.CloseDoor(); };
            DoorGroupActionTrigger += (string GroupName) => { if(!door.CustomName.Contains(GroupName)) return; door.ToggleDoor(); };
            DoorGroupLocker += (string GroupName, bool Lock) => { if(!door.CustomName.Contains(GroupName)) return; door.Enabled = !Lock; };
            if(door.CustomName.Contains(GroupInner))
            {
                Room_InnerDoorState_Open += (ref bool Open) => { Open = Open && ( door.Status == DoorStatus.Open ); };
                Room_InnerDoorState_Close += (ref bool Close) => { Close = Close && ( door.Status == DoorStatus.Closed ); };
            }
            else if(door.CustomName.Contains(GroupOuter))
            {
                Room_OuterDoorState_Open += (ref bool Open) => { Open = Open && ( door.Status == DoorStatus.Open ); };
                Room_OuterDoorState_Close += (ref bool Close) => { Close = Close && ( door.Status == DoorStatus.Closed ); };
            }
        }
        List<IMyAirVent> AirVentsLocker=new List<IMyAirVent>();
        RoomEquipmentGroup.GetBlocksOfType(AirVentsLocker, (IMyAirVent vent) => vent.CustomName.Contains(GroupAirlockerVent_NM));
        stateShow = new StateShowSys(RoomEquipmentGroup);
        foreach(var vent in AirVentsLocker)
        {
            Room_CanPressurize += (ref bool CanPressurize) => { CanPressurize = ( CanPressurize && vent.CanPressurize ); };
            Room_Pressurize += (bool Pressurize) => { vent.Depressurize = !Pressurize; };
            Room_AirVentOnOff += (bool OnOff) => { vent.Enabled = OnOff; };
            Room_Pressurized += (ref bool Pressurized) => { Pressurized = ( Pressurized && ( vent.GetOxygenLevel() > 0.9f ) ); };
            Room_Depressurized += (ref bool Depressurized) => { Depressurized = ( Depressurized && ( VRageMath.MathHelper.RoundOn2(vent.GetOxygenLevel()) <= 0.00f ) ); };
        }
        DoorGroupLocker(GroupAll, false);
        DoorGroupAction(GroupAll, false);
        RoomState = AirLockState.Init;
    }
    public void SetConsoleCommade(string cmd)
    {
        if(RoomState != AirLockState.Idle || CommadeLines.Count != 0) return;
        string[] cmdline=cmd.Split(Splites,StringSplitOptions.RemoveEmptyEntries);
        if(cmdline == null) return;
        CommadeLines.AddArray(cmdline);
    }
    public void RoomAirManager()
    {
        if(AtomsphereOxygonDetector.GetOxygenLevel() > 0.75f)
        {
            Room_AirVentOnOff(false);
            DoorGroupLocker(GroupAll, false);
            stateShow.SetImage_InnerLcds(Inner_Pd);
            stateShow.SetImage_OuterLcds(Outer_Pd);
            DoorManage();
        }
        else
        {
            bool Pressureable=true;
            bool Pressurized=true;
            bool Depressurized=true;
            bool open_outer=true;
            bool open_inner=true;
            bool close_outer=true;
            bool close_inner=true;
            Room_CanPressurize(ref Pressureable);
            Room_Pressurized(ref Pressurized);
            Room_Depressurized(ref Depressurized);
            Room_InnerDoorState_Open(ref open_inner);
            Room_OuterDoorState_Open(ref open_outer);
            Room_InnerDoorState_Close(ref close_inner);
            Room_OuterDoorState_Close(ref close_outer);
            Room_AirVentOnOff(true);
            if(Pressurized)
                stateShow.SetImage_InnerLcds(Inner_Pd);
            else
                stateShow.SetImage_InnerLcds(Inner_Dpd);
            if(Depressurized)
                stateShow.SetImage_OuterLcds(Outer_Pd);
            else
                stateShow.SetImage_OuterLcds(Outer_Dpd);
            if(RoomState == AirLockState.Init)
            {
                DoorGroupLocker(GroupAll, false);
                DoorGroupAction(GroupAll, false);
                Room_Pressurize(true);
                if(close_inner && close_outer)
                    RoomState = AirLockState.Idle;
                return;
            }
            switch(RoomState)
            {
                case AirLockState.Start:
                    if(!Pressurized)
                    {
                        DoorGroupLocker(GroupOuter, false);
                        DoorGroupAction(GroupOuter, false);
                        if(close_outer)
                        {
                            RoomState = AirLockState.Pressure;
                            DoorGroupLocker(GroupOuter, true);
                        }
                    }
                    else
                    {
                        DoorGroupLocker(GroupInner, false);
                        DoorGroupAction(GroupInner, false);
                        if(close_inner)
                        {
                            RoomState = AirLockState.Depressure;
                            DoorGroupLocker(GroupInner, true);
                        }
                    }
                    return;
                case AirLockState.Pressure:
                    Room_Pressurize(true);
                    if(Pressurized)
                        RoomState = AirLockState.Idle;
                    return;
                case AirLockState.Depressure:
                    Room_Pressurize(false);
                    if(Depressurized)
                        RoomState = AirLockState.Idle;
                    return;
                default:
                    DoorGroupLocker(GroupInner, !Pressurized);
                    DoorGroupLocker(GroupOuter, !Depressurized);
                    break;
            }
            DoorManage();
        }
    }
    private void DoorManage()
    {
        if(CommadeLines.Count <= 0) return;
        if(CommadeLines[0] == Cmd_TriggerRoomState)
        {
            RoomState = AirLockState.Start;
            CommadeLines.RemoveAt(0);
            return;
        }
        else
        {
            switch(CommadeLines[0])
            {
                case "Open": DoorGroupAction(CommadeLines[1], true); break;
                case "Close": DoorGroupAction(CommadeLines[1], false); break;
                case "Toggle": DoorGroupActionTrigger(CommadeLines[1]); break;
                case "Lock": DoorGroupLocker(CommadeLines[1], true); break;
                case "Unlock": DoorGroupLocker(CommadeLines[1], false); break;
                default: break;
            }
            CommadeLines.Clear();
            return;
        }
    }
    public AirLockState RoomState { get; private set; } = AirLockState.Idle;
    public string Name { get; private set; }
    public static IMyAirVent AtomsphereOxygonDetector;
    public readonly Action<string,bool> DoorGroupAction=(string GroupName,bool Open)=>{ };
    public readonly Action<string> DoorGroupActionTrigger=(string GroupName)=>{ };
    public readonly Action<string,bool> DoorGroupLocker=(string GroupName,bool Lock)=>{ };
    private readonly Action<bool> Room_Pressurize=(bool Pressurize)=>{ };
    private readonly Action<bool> Room_AirVentOnOff=(bool OnOff)=>{ };
    private readonly StateShowSys stateShow;
    private readonly ActionRef<bool> Room_InnerDoorState_Open=(ref bool Open)=>{ Open=(Open&&true); };
    private readonly ActionRef<bool> Room_OuterDoorState_Open=(ref bool Open)=>{ Open=(Open&&true); };
    private readonly ActionRef<bool> Room_InnerDoorState_Close=(ref bool Close)=>{ Close=(Close&&true); };
    private readonly ActionRef<bool> Room_OuterDoorState_Close=(ref bool Close)=>{ Close=(Close&&true); };
    private readonly ActionRef<bool> Room_CanPressurize=(ref bool CanPressurize)=>{CanPressurize=(CanPressurize&&true); };
    private readonly ActionRef<bool> Room_Pressurized=(ref bool Pressurized)=>{Pressurized=(Pressurized&&true); };
    private readonly ActionRef<bool> Room_Depressurized=(ref bool Depressurized)=>{Depressurized=(Depressurized&&true); };
    private readonly List<string> CommadeLines=new List<string>();
}
class StateShowSys
{
    public StateShowSys(IMyBlockGroup RoomEquipmentGroup)
    {
        List<IMyButtonPanel> Buttons=new List<IMyButtonPanel>();
        RoomEquipmentGroup.GetBlocksOfType(Buttons);
        List<IMyTextPanel> TextPanels=new List<IMyTextPanel>();
        RoomEquipmentGroup.GetBlocksOfType(TextPanels);
        foreach(var Lcd in Buttons)
        {
            if(Lcd is IMyTextSurfaceProvider)
            {
                if(( Lcd as IMyTextSurfaceProvider ).SurfaceCount > 0)
                {
                    if(Lcd.CustomName.Contains(Group_Inner_All))
                        SetImage_InnerLcds += (string ID) =>
                        {
                            var surface = (Lcd as IMyTextSurfaceProvider).GetSurface(0);
                            surface.ClearImagesFromSelection();
                            surface.AddImageToSelection(ID);
                        };
                    else if(Lcd.CustomName.Contains(Group_Outer_All))
                        SetImage_OuterLcds += (string ID) =>
                        {
                            var surface = (Lcd as IMyTextSurfaceProvider).GetSurface(0);
                            surface.ClearImagesFromSelection();
                            surface.AddImageToSelection(ID);
                        };
                }
            }
        }
        foreach(var Lcd in TextPanels)
        {
            if(Lcd.CustomName.Contains(Group_Inner_All))
                SetImage_InnerLcds += (string ID) =>
                {
                    Lcd.ClearImagesFromSelection();
                    Lcd.AddImageToSelection(ID);
                };
            else if(Lcd.CustomName.Contains(Group_Outer_All))
                SetImage_OuterLcds += (string ID) =>
                {
                    Lcd.ClearImagesFromSelection();
                    Lcd.AddImageToSelection(ID);
                };
        }
    }
    public readonly Action<string> SetImage_InnerLcds=(string ID)=>{ };
    public readonly Action<string> SetImage_OuterLcds=(string ID)=>{ };
}
class AutoCloseDoor
{
    public readonly IMyDoor Door;
    private int Timer;
    private const int LastTime=30;
    public AutoCloseDoor(IMyDoor Door)
    {
        this.Door = Door;
        Timer = LastTime;
        Door.CloseDoor();
    }
    public void Running()
    {
        if(Door.Status == DoorStatus.Closed)
        {
            Timer = LastTime;
            return;
        }
        if(Timer > 0)
            Timer--;
        else
            Door.CloseDoor();
    }
}
private static readonly string[] Splites=new string[]{" ","\t" };