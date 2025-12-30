using System.IO;
using System.Collections;
using BepInEx;
using BepInEx.Logging;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Diagnostics.Tracing;
using UnityEngine.PlayerLoop;

namespace ConsoleCommands;

[BepInPlugin("nl.lunar.modding", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Techtonica.exe")]
//[BepInDependency("Tobey.UnityAudio", BepInDependency.DependencyFlags.SoftDependency)]


//TODO: SUGGESTION - "would it be possible to make the console key configurable (set another key to open/close)?"
//TODO: SUGGESTION - Mining charge radius command
//TODO: Open game settings menu command
public class ConsoleCommands : BaseUnityPlugin
{
	public static new ManualLogSource Logger;
	private string InputText;
	public List<string> InputHistory = new List<string>();
	public List<string> OutputHistory = new List<string>();
	public int InputIndex = -1; // -1 for not looking through history
	public bool bIsEnabled;
	public const int MaxTotalHistory = 16;
	public static bool bHasScanOverride;
	public static float ScanOverrideMultiplier;
	public const bool DebugTheShitOutOfEverything=false; // :bangbang:

	private GUIStyle ConsoleTextFieldStyle;
	private void Awake()
	{
		Logger = base.Logger;
		Harmony.CreateAndPatchAll(typeof(OpenSesamePatch));
		Harmony.CreateAndPatchAll(typeof(ScannerPatch));
		Harmony.CreateAndPatchAll(typeof(InstaMolePatch));
		Harmony.CreateAndPatchAll(typeof(AccumulatorPatch));
		Logger.LogInfo("Thanks for downloading and using Techtonica Console Commands!\nThe mod has just finished initializing.");
		Logger.LogWarning("The mod is still in development, bugs and issues may occur.");
		this.gameObject.hideFlags = HideFlags.HideAndDontSave;
		InitializeWarps();

		// GUIStyles
		Texture2D consoleBackground;
		consoleBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false); 
    	consoleBackground.SetPixel(0, 0, new Color(0.086f, 0.086f, 0.149f, 1));
    	consoleBackground.Apply(); // not sure if this is necessary

		ConsoleTextFieldStyle = new GUIStyle(GUIStyle.none);
		ConsoleTextFieldStyle.normal.background = consoleBackground;
		ConsoleTextFieldStyle.fontSize = 20;
		ConsoleTextFieldStyle.normal.textColor = Color.white;
	}

 void OnGUI() 
	{
		if(!bIsEnabled) return;
		
		// GUI.backgroundColor = new Color(0.086f, 0.086f, 0.149f, 1);
		// GUI.Box(new Rect (0,Screen.height - 30,Screen.width,30), ""); 
		Input.eatKeyPressOnTextFieldFocus = false;

		GUI.SetNextControlName("Console");
        InputText = GUI.TextField(new Rect (0,Screen.height - 30,Screen.width,30), InputText, ConsoleTextFieldStyle);
		GUI.FocusControl("Console"); // set focus on textfield
		// Output background
		GUI.Box(new Rect(0,Screen.height - (25*(InputHistory.Count+OutputHistory.Count)+30),(int)Screen.width/2,25*(InputHistory.Count+OutputHistory.Count)), "");
		// Output
		GUI.skin.label.fontSize=20;
		GUI.Label(new Rect(0,Screen.height - (25*(InputHistory.Count+OutputHistory.Count)+30),Screen.width/2,25*(InputHistory.Count+OutputHistory.Count)), GetHistory());
    }

	void Update()
    {
		if(Player.instance == null) return; // aka: if in menu do nothing
		if(bIsNoclipping){
			Player.instance.transform.position = Player.instance.camController.camParent.position;
			Player.instance.transform.rotation = Player.instance.camController.camParent.rotation;
		}
		if(Bindings.Count > 0 && !bIsEnabled)	HandleKeyBinds();
		if(Input.GetKeyDown(KeyCode.Slash)) ToggleConsole();

		if(!bIsEnabled) return;

        //Detect when the Return key is pressed down
        if(Input.GetKeyDown(KeyCode.Return) && InputText != "" && HandleCommand(InputText))
        {
			UpdateHistory(InputText, false);
			InputText=""; // clear input text so that a new command can be inputted without clearing the previous manually
			InputIndex=-1;
        }

		if(Input.GetKeyDown(KeyCode.PageUp))
		{
			if(InputIndex+1 < InputHistory.Count)
			{
				InputIndex++;
				if(InputIndex >= InputHistory.Count) InputIndex=InputHistory.Count-1;
				InputText=InputHistory[InputIndex];
			}
		}

		if(Input.GetKeyDown(KeyCode.PageDown))
		{
			if(InputIndex-1 >= -1)
			{
				InputIndex--;
				if(InputIndex >= 0) InputText=InputHistory[InputIndex];
				else InputText="";
			}
		}

		if(DebugTheShitOutOfEverything) Debug.Log(InputIndex.ToString());
	}

	void HandleKeyBinds()
	{
		for(int i = 0; i < Bindings.Count; i++)
		{
			if(Input.GetKeyDown(Bindings[i].key))
			{
				if(Bindings[i].args != null) Bindings[i].command.Invoke(this, Bindings[i].args.ToArray<string>());
				else Bindings[i].command.Invoke(this, null);
			}
		}
	}

	void ToggleConsole()
	{
		bIsEnabled = !bIsEnabled;
		InputHandler.instance.uiInputBlocked = bIsEnabled;
	}
	public void UpdateHistory(string TextToAdd, bool bIsOutput) // * i hate this function dearly.
	{
		if(!bIsOutput)
		{
			if(InputHistory.Count <= (int)MaxTotalHistory/2) InputHistory.Add(TextToAdd);
			else
			{
				InputHistory.RemoveAt(0);
				InputHistory.Add(TextToAdd);
			}
		}
		else
		{
			if(OutputHistory.Count <= (int)MaxTotalHistory/2) OutputHistory.Add(TextToAdd);
			else
			{
				OutputHistory.RemoveAt(0);
				OutputHistory.Add(TextToAdd);
			}
		}
	}
	public bool HandleCommand(string UserInput)
	{
		if(UserInput == null) return false; // This seems to happen in rare cases. Better safe than sorry!
		string CommandName = UserInput.ToLower().Split(' ')[0];
		List<string> args = UserInput.Split(' ').ToList<string>();
		args.RemoveAt(0);
		MethodInfo m = GetType().GetMethod(CommandName);
		if(m != null) m.Invoke(this, args.ToArray());
		else {
			UpdateHistory(UserInput, false);
			DetermineAndLogError(m, UserInput, args);
			return false;
		}
		return true;
	}

	void DetermineAndLogError(MethodInfo theMethod, string UserInput, List<string> args)
	{
		if(theMethod == null) LogCommandError("Command '"+UserInput.Split(' ')[0]+"' doesn't exist! Are you sure you typed it correctly?", true);
		else if(args.Count != theMethod.GetParameters().Length) LogCommandError("Missing or obsolete arguments! Expected "+theMethod.GetParameters().Length.ToString()+" arguments, got "+args.Count.ToString()+".", true);

		else LogCommandError("We don't exactly know what went wrong with your command! Please check for mistakes and try again.", true);
	}

	void LogCommandError(string StringToLog, bool bShouldAppearInHistory)
	{
		Logger.LogError(StringToLog);
		UIManager.instance.systemLog.FlashMessage("[ERROR] "+StringToLog);
		if(bShouldAppearInHistory) UpdateHistory(StringToLog, true);
	}

	void LogCommandOutput(string StringToLog, bool bShouldAppearInHistory) // // TODO: make all commands output something at the end (Done!)
	{
		Logger.LogInfo(StringToLog);
		//SystemMessageInfo m_info = new SystemMessageInfo("[OUTPUT] "+StringToLog);
		//UIManager.instance.systemLog.uiSettings[0].width = StringToLog.Length*4+8; //WHY
		//UIManager.instance.systemLog.ShowMessage(m_info);
		if(bShouldAppearInHistory) UpdateHistory(StringToLog, true);
	}
 	public void give(string item, string amount) // // TODO: add support for all resourceinfos, not only the ones in player's inventory.
	{
		var ResourceTypes = GameDefines.instance.resources;
		if(!int.TryParse(amount, out int count))
		{ 
			LogCommandError("The amount you provided, '"+amount+"', doesn't seem to be a number! Are you sure you typed it correctly?", true);
			return;
		}
		ResourceInfo result = new ResourceInfo();
		for(var i = 0; i < ResourceTypes.Count; i++)
		{
			if(item.ToLower() == ResourceTypes[i].displayName.ToLower().Replace(" ", ""))
			{
				Player.instance.inventory.AddResources(ResourceTypes[i], count);
				result = ResourceTypes[i];
				break;
			}
			if(i == ResourceTypes.Count-1) 
			{
				LogCommandError("The item ('"+item+"') you provided doesn't seem to be correct! Try another name.", true);
				return;
			}
		}
		LogCommandOutput(amount+" of "+result.displayName+" has been given to player.", true);
	}

	public void echo(string logstring, string logtype)
	{
		switch(logtype.ToLower())
		{
			case "info":
				Logger.LogInfo(logstring);
				break;
			case "warning":
				Logger.LogWarning(logstring);
				break;
			case "error":
				Logger.LogError(logstring);
				break;
			case "fatal":
				Logger.LogFatal(logstring);
				break;
			case "message":
				Logger.LogMessage(logstring);
				break;
			default:
				LogCommandError("Unrecognized log type! Choose from: info, warning, error, fatal, or message", true);
				break;
		}
		LogCommandOutput("Logged '"+logstring+"'.", true);
	}

	// * Done!
	// // TODO: redo this with new Output/Input history system
	
	public string GetHistory()
	{
		string s = "";
		bool switchbool = false;
		for(int i = 0; i < OutputHistory.Count+InputHistory.Count; i++)
		{
			if(switchbool) s += OutputHistory[(int)i/2]+Environment.NewLine;
			else s += InputHistory[(int)i/2]+Environment.NewLine;
			switchbool = !switchbool;
		}
		return s;
	}

	public void setplayerparams(string paramtype, string value)
	{
		if(!float.TryParse(value, out float v))
		{
			LogCommandError("Unrecognized value '"+value+"'! Are you sure you typed it correctly?", true);
			return;
		}
		switch(paramtype.ToLower())
		{
			case "maxrunspeed":
				PlayerFirstPersonController.instance.maxRunSpeed = v;
				break;
			case "maxwalkspeed":
				PlayerFirstPersonController.instance.maxWalkSpeed = v;
				break;
			case "maxflyspeed":
				PlayerFirstPersonController.instance.maxFlySpeed = v;
				break;
			case "jumpspeed":
				PlayerFirstPersonController.instance.jumpSpeed = v;
				break;
			case "scanspeed":
				bHasScanOverride = true;
				ScanOverrideMultiplier = 1/v;
				break;
			case "gravity":
				PlayerFirstPersonController.instance.gravity = v;
				break;
			case "maxflyheight":
				Player.instance.equipment.hoverPack._stiltHeight = v;
				break;
			case "railrunnerspeed":
				Player.instance.equipment.railRunner._hookSpeed = v;
				break;
			default:
				LogCommandError("Unrecognized type '"+paramtype+"'! Are you sure you typed it correctly?\nPossible options are: maxrunspeed, maxwalkspeed, maxflyspeed, maxjumpvelocity, scanspeed, gravity, maxjumpheight", true);
				return;
		}
		LogCommandOutput("Player param '"+paramtype+"' set with value '"+value+"'. ", true);
	}

	public void weightless()
	{
		Player.instance.cheats.disableEncumbrance = !Player.instance.cheats.disableEncumbrance;
		if(Player.instance.cheats.disableEncumbrance) LogCommandOutput("Enabled weightlessness.", true);
		else LogCommandOutput("Disabled weightlessness.", true);
	}

	public void echolocation()
	{
		echo(PlayerFirstPersonController.instance.transform.position.ToString(), "info");
	}
	
	public void tp(string X, string Y, string Z)
	{
		if((float.TryParse(X, out float ix) || X == "~") && (float.TryParse(Y, out float iy) || Y == "~") && (float.TryParse(Z, out float iz) || Z == "~"))
		{
			if(X == "~") ix = PlayerFirstPersonController.instance.transform.position.x;
			else if(Y == "~") iy = PlayerFirstPersonController.instance.transform.position.y;
			else if(Z == "~") iz = PlayerFirstPersonController.instance.transform.position.z;
			PlayerFirstPersonController.instance.transform.position = new Vector3(ix, iy, iz);
			LogCommandOutput("Teleported player to "+X+", "+Y+", "+Z+".", true);
		}
		else LogCommandError("Your three vector components dont seem to be valid! ('"+X+"', '"+Y+"', '"+Z+"')", true);
	}

	public struct WarpData
	{
		public string name;
		public Vector3 loc;

		public WarpData(string name, Vector3 loc)
		{
			this.name = name;
			this.loc = loc;
		}
 	}
	public List<WarpData> warps = new List<WarpData>();

	private int FindWarpInList(string warpname)
	{
		InitializeWarps(); // better safe than sorry
		for(int i = 0; i > warps.Count; i++)
		{
			if(warps[i].name == warpname)
			{
				return i;
			}
		}
		return -1;
	}
	public void warp(string name)
	{
		switch(name.ToLower())
		{
			case "victor":
				Player.instance.transform.position = new Vector3((float)138.00, (float)12.30, (float)-116.00);
				break;
			case "lima":
				Player.instance.transform.position = new Vector3((float)85.00, (float)-2.84, (float)-330.00);
				break;
			case "xray":
				Player.instance.transform.position = new Vector3((float)-307.57, (float)92.95, (float)20.11);
				break;
			case "freight":
				Player.instance.transform.position = new Vector3((float)-153.08, (float)36.30, (float)188.50);
				break;
			case "waterfall":
				Player.instance.transform.position = new Vector3((float)-265.880, (float)-17.850, (float)-131.330);
				break;
			default:
				if(FindWarpInList(name) != -1)
				{
					Player.instance.transform.position = warps[FindWarpInList(name)].loc;
					break;
				}
				else { LogCommandError("Your warp, '"+name+"', doesn't seem to exist! Check info.txt and warps.txt for all possible warps.", true); return;} 
		}
		LogCommandOutput("Teleported to "+name+"!", true);
	}
	public string WarpTXTPath
	{
		get
		{
			return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("console_commands.dll", string.Empty)+@"\warps.txt";
		}
	}
	public void setwarp(string name)
	{
		if(!File.Exists(WarpTXTPath)) File.WriteAllText(WarpTXTPath, "");
		Debug.Log(WarpTXTPath);

		string filetext = File.ReadAllText(WarpTXTPath);
		if(filetext.Contains(name)) {
			LogCommandError("That warpname is already in use. Pick another.", true);
			return;
		}
		File.AppendAllText(WarpTXTPath, "NAME "+name+Environment.NewLine+"LOC "+Player.instance.transform.position.ToString()+"\n");
		warps.Add(new WarpData(name, Player.instance.transform.position));
		LogCommandOutput("Saved new warp '"+name+"' at "+Player.instance.transform.position.ToString(), true);
		// Format: 
		// NAME defaultwarp 
		// LOC 0|0|0
	}

	private void InitializeWarps()
	{
		if(!File.Exists(WarpTXTPath)) File.WriteAllText(WarpTXTPath, "");
		string filetext = File.ReadAllText(WarpTXTPath);
		string[] lines = filetext.Split(Environment.NewLine);
		WarpData w = new WarpData();
		for(int i = 0; i > lines.Length; i++)
		{
			if(lines[i].Contains("NAME ")) 
				w.name = lines[i].Replace("NAME ", string.Empty);
			else if(lines[i].Contains("LOC ")) 
			{
				w.loc = stringToVec(lines[i].Replace("LOC ", string.Empty));
				warps.AddItem(w);
			}
		}
	}
	public void delwarp(string name)
	{

		if(!File.Exists(WarpTXTPath)) {
			LogCommandError("warps txt not available", true);
			return;
		}
		string filetext = File.ReadAllText(WarpTXTPath);
		List<string> lines = filetext.Split(Environment.NewLine).ToList<string>();
		for(int i = 0; i > lines.Count; i++)
		{
			if(lines[i].Contains("NAME "+name))
			{
				lines.RemoveAt(i);
				lines.RemoveAt(i); // do it twice because of the two lines used to save a warp
				File.WriteAllText(string.Join(Environment.NewLine, lines), WarpTXTPath);
			}
		}
		LogCommandOutput("Deleted warp '"+name+"'.", true);
	}

	public Vector3 stringToVec(string s) // Thanks random person from the unity forums! Your work is much appreciated
	{
    	string[] temp = s.Substring (1, s.Length-2).Split (',');
    	return new Vector3 (float.Parse(temp[0]), float.Parse(temp[1]), float.Parse(temp[2]));
	}

	public void unlock(string name, string DrawPower)
	{
		if(!bool.TryParse(DrawPower, out bool b))
		{ 
			LogCommandError("The bool you provided, '"+DrawPower+"', doesn't seem to be valid!", true);
			return;
		}
		if(name.ToLower() == "all") UnlockAll(b);
		var unlocks = GameDefines.instance.unlocks;
		for(var i = 0; i < unlocks.Count; i++)
		{
			if(LocsUtility.TranslateStringFromHash(unlocks[i].displayNameHash, null) == null) continue;
			if(name.ToLower() == LocsUtility.TranslateStringFromHash(unlocks[i].displayNameHash, null).ToLower().Replace(" ", ""))
			{
				ResearchTechNoReq(unlocks[i].uniqueId, b);
				if(!b) LogCommandOutput("Unlocked tech "+unlocks[i].displayName+" without drawing power.", true);
				else LogCommandOutput("Unlocked tech "+unlocks[i].displayName+".", true);
				break;
			}
			if(i == unlocks.Count-1) LogCommandError("The name ('"+name+"') you provided doesn't seem to be correct! Try another name.", true);
		}
	}

	private void UnlockAll(bool DrawPower)
	{
		var unlocks = GameDefines.instance.unlocks;
		for(var i = 0; i < unlocks.Count; i++)
		{
			ResearchTechNoReq(unlocks[i].uniqueId, DrawPower);
		}
		LogCommandOutput("Unlocked all tech!", true);
	}
	
	private void ResearchTechNoReq(int unlockId, bool b)
	{
		var action = new UnlockTechAction
		{
			info = new UnlockTechInfo
			{
				unlockID = unlockId,
				drawPower = b
			}
		};
		NetworkMessageRelay.instance.SendNetworkAction(action);
	}

	public void opensesame()
	{
		// ResourceGateInstance rgi;
		if(!GetClosestDoor(8, out uint doorid, out ResourceGateInstance rgi))
		{
			LogCommandError("You're not looking at a door!", true);
			return;
		}
		DoorToOpen = rgi;
		AddRequiredResources(rgi);
		OpenDoor(doorid, rgi); // this won't work as further in network code the resources are checked again, so we must set the required resources before we do this.
		rgi.ProcessUpgrade();
		rgi.interactionState = 2;
		LogCommandOutput("Opened door "+rgi.myConfig.displayName+".", true);
	}

	public static ResourceGateInstance DoorToOpen;
	private bool GetClosestDoor(float MaxDist, out uint id, out ResourceGateInstance rgi)
	{
		var doors = MachineManager.instance.GetMachineList<ResourceGateInstance, ResourceGateDefinition>(MachineTypeEnum.ResourceGate);
        float closestDoor = float.MaxValue;
        ResourceGateInstance gate = doors.myArray[0];
        foreach (var door in doors.myArray)
        {
            var center = door.gridInfo.Center;
            var distanceToDoor = center.Distance(Player.instance.cam.transform.position);
            if(distanceToDoor < closestDoor)
            {
                Logger.LogInfo($" Found closer door {door.gridInfo.myRef.instanceId} is {distanceToDoor} away.");
                closestDoor = distanceToDoor;
                gate = door;
            }
            else
            {
                Logger.LogInfo($" Found further door {door.gridInfo.myRef.instanceId} is {distanceToDoor} away.");
            }
        }
		rgi = gate;
		id = gate.gridInfo.myRef.instanceId;
		if(closestDoor > MaxDist) return false;
		else return true;
	}
	private void OpenDoor(uint id, ResourceGateInstance rgi)
	{
		if (rgi.CheckForRequiredResources())
		{
			CompleteResourceGateAction action = new CompleteResourceGateAction
			{
				info = new CompleteResourceGateInfo
				{
					machineId = rgi.gridInfo.myRef.instanceId,
					unlockLevel = 1
				}
			};
			NetworkMessageRelay.instance.SendNetworkAction(action);
			Player.instance.audio.productionTerminalTierUpgrade.PlayRandomClip(true);
			return;
		}
		Player.instance.audio.error.PlayRandomClip(true);
	}

	private void AddRequiredResources(ResourceGateInstance rgi)
	{
		for(var i = 0; i < rgi.resourcesRequired.Length; i++)
		{
			// rgi.AddResources(rgi.resourcesRequired[i].resType.uniqueId, out int remainder, rgi.resourcesRequired[i].quantity);
			rgi.GetInputInventory().AddResourcesToSlot(rgi.resourcesRequired[i].resType.uniqueId, i, out int remainder, rgi.resourcesRequired[i].quantity, true);
		}
	}

	public static bool bShouldInstaMine;
	public void instamole() // * functionality handled in InstaMolePatch.cs
	{
		bShouldInstaMine = !bShouldInstaMine;
		if(bShouldInstaMine) LogCommandOutput("Enabled instamine.", true);
		else LogCommandOutput("Disabled instamine.", true);
	}

	public void gamespeed(string value) // * Built in simspeed variable in PlayerCheats.cs
	{
		if(!float.TryParse(value, out float v))
		{
			LogCommandError("The float parameter you provided, '"+value+"', doesn't seem to be valid!", true);
			return;
		}
		Player.instance.cheats.simSpeed = v;
		LogCommandOutput("Correctly set game simulation speed to "+value+"!", true);
	}

	public void cammode(string value)
	{
		if(!ParseCamMode(value, out PlayerCheats.FreeCameraMode fcm))
		{
			LogCommandError("The camera mode you inputted, '"+value+"', doesn't seem to be valid!", true);
			return;
		}
		Player.instance.cheats.freeCameraMode = fcm;
		LogCommandOutput("New camera mode set: "+value+".", true);
	}
	

	// ! This can be replaced with (PlayerCheats.FreeCameraMode) Enum.Parse(typeof(PlayerCheats.FreeCameraMode), string)!
	public bool ParseCamMode(string value, out PlayerCheats.FreeCameraMode fcm)
	{
		switch(value.ToLower())
		{
			case "normal":
				fcm =  PlayerCheats.FreeCameraMode.Normal;
				return true;
			case "free":
				fcm =  PlayerCheats.FreeCameraMode.Free;
				return true;
			case "scriptedanimation":
				fcm = PlayerCheats.FreeCameraMode.ScriptedAnimation;
				return true;
			default:
				fcm = PlayerCheats.FreeCameraMode.Normal;
				return false;
		}
	}

	public static bool bShouldFillAccumulators;

	public void fillaccumulators()
	{
		bShouldFillAccumulators = true;
	}

	public void camtp()
	{
		if(Player.instance.cheats.freeCameraMode == PlayerCheats.FreeCameraMode.Free)
		{
			Player.instance.transform.position = Player.instance.cam.transform.position;
			LogCommandOutput("Succesfully teleported to the freecam position!", true);
		}
		else LogCommandError("You're not in free camera mode!", true);
	}

	public bool bIsNoclipping;
	public void noclip()
	{
		if(!bIsNoclipping)
		{
			Player.instance.cheats.freeCameraMode = PlayerCheats.FreeCameraMode.Free;
			bIsNoclipping = true;
			LogCommandOutput("Noclip has been enabled!", true);
		}
		else
		{
			Player.instance.cheats.freeCameraMode = PlayerCheats.FreeCameraMode.Normal;
			bIsNoclipping = false;
			LogCommandOutput("Noclip has been disabled!", true);
		}
	}

	struct CommandKeyBindData
	{
		public KeyCode key;
		public MethodInfo command;
		public List<string> args;
	}

	private List<CommandKeyBindData> Bindings = new List<CommandKeyBindData>();

	// command formatting: CommandName{arg1,arg2,arg3,etc.}
	public void bind(string key, string command) // bind a hotkey to a command
	{
		// System.Enum.Parse(typeof(KeyCode), key)

		if(Enum.IsDefined(typeof(KeyCode), key)) // is key valid
		{
			KeyCode keybind;
			CommandKeyBindData BindingData = new CommandKeyBindData();
			List<string> args = null;
			string commandname;
			keybind = (KeyCode) Enum.Parse(typeof(KeyCode), key);
			if(command.Contains("{") || command.Contains("}"))
			{
				commandname = command.Substring(0, command.IndexOf('{'));
				args = command.Substring(command.IndexOf('{'), command.IndexOf('}')-command.IndexOf('{')).Replace(",", " ").Replace("}", "").Replace("{", "").Split(" ").ToList<string>();
				Debug.Log(args[0]);
			}
			else commandname = command;
			if(GetType().GetMethod(commandname) == null)
			{
				LogCommandError("The command you inputted, "+commandname+", doesn't exist!", true);
				return;
			}
			if(Bindings.Find(BindingData => BindingData.key == keybind).command == null) // check if the key has already been bound to something (true if not)
			{
				BindingData.key = keybind;
				BindingData.command = GetType().GetMethod(commandname.ToLower());
				if(command.Contains("{") || command.Contains("}")) BindingData.args = args;
				Bindings.Add(BindingData);
				LogCommandOutput("Bound "+key+" to '"+command+"'!", true);
			}
			else
			{
				LogCommandError("This key has already been bound to another command! Use 'unbind' to unbind keys!", true);
				return;
			}
		}
		else LogCommandError("The key you provided, '"+key+"', isn't valid!", true);
	}

	public void unbind(string key) // unbind a hotkey
	{
		if(!Enum.IsDefined(typeof(KeyCode), key))
		{
			LogCommandError("The key you provided, '"+key+"', isn't valid!", true);
			return;
		}
		KeyCode keybind = (KeyCode) Enum.Parse(typeof(KeyCode), key);
		if(Bindings.Find(BindingData => BindingData.key == keybind).command != null)
		{
			Bindings.Remove(Bindings.Find(BindingData => BindingData.key == keybind));
			LogCommandOutput("Key '"+keybind.ToString()+"' has been unbound!", true);
		}
		else LogCommandError("That key isn't bound to anything!", true);
	}

	public void setsize(string value, string bSyncParams)
	{
		if(!float.TryParse(value, out float f))
		{
			LogCommandError("The float you provided isn't valid!", true);
			return;
		}
		if(f <= 0)
		{
			LogCommandError("To prevent game-breaking bugs, scales below 0 are not accepted.", true);
			return;
		}
		if(!bool.TryParse(bSyncParams, out bool b))
		{
			LogCommandError("The bool you provided isn't valid!", true);
			return;
		}
		Player.instance.transform.localScale = new Vector3(f, f, f);
		Player.instance.cam.transform.localScale = new Vector3(f, f, f);
		if(b) ScalePlayerParamsToNewSize(f);
		LogCommandOutput("Set scale to "+f.ToString()+"!", true);
	}
	void ScalePlayerParamsToNewSize(float newsize)
	{
		PlayerFirstPersonController.instance.maxWalkSpeed = 5f * newsize;
		PlayerFirstPersonController.instance.maxRunSpeed = 8f * newsize;
		PlayerFirstPersonController.instance.peakMinHeight = 1f * newsize;
		PlayerFirstPersonController.instance.peakMaxHeight = 2f * newsize;
		PlayerFirstPersonController.instance.inAirDuration = 2f * newsize;
		PlayerFirstPersonController.instance.gravity = 20f * newsize;
		PlayerFirstPersonController.instance.maxFallSpeed = -15f * newsize;
		Player.instance.equipment.hoverPack._stiltHeight = 3f * newsize;
		Player.instance.equipment.hoverPack._raiseSpeed = 5f * newsize;
	}

	public void clear(string item, string amount)
	{
		ResourceInfo result = new ResourceInfo();
		for(int i = 0; i < GameDefines.instance.resources.Count; i++)
		{
			if(GameDefines.instance.resources[i].displayName.Replace(" ", "").ToLower() == item.ToLower())
			{
				result = GameDefines.instance.resources[i];
				break;
			}
			if(i == GameDefines.instance.resources.Count-1) LogCommandError("The item you provided doesn't seem to exist!", true);
		}
		if(int.TryParse(amount, out int a))
		{
			Player.instance.inventory.TryRemoveResources(result, a);
			LogCommandOutput("Removed "+a.ToString()+" of "+result.displayName+" from player's inventory.", true);
		}
		else if(amount.ToLower() == "all")
		{
			Player.instance.inventory.TryRemoveResources(result, Player.instance.inventory.GetResourceCount(result.uniqueId));
			LogCommandOutput("Removed all of "+result.displayName+" from player's inventory.", true);
		}
		else LogCommandError("The amount you provided doesn't seem to be valid!", true);
	}
	public void setmoledimensions(string valuex, string valuey, string valuez)
	{
		if(int.TryParse(valuex, out int x) && int.TryParse(valuey, out int y) && int.TryParse(valuez, out int z))
		{
			TerrainManipulator tm = Player.instance.equipment.GetAllEquipment<TerrainManipulator>()[0];
			tm.tunnelMode._currentDimensions.x = x;
			tm.tunnelMode._currentDimensions.y = y;
			tm.tunnelMode._currentDimensions.z = z;
			tm.flattenMode._currentDimensions.x = x;
			tm.flattenMode._currentDimensions.y = y;
			tm.flattenMode._currentDimensions.z = z;
			LogCommandOutput("Set dimension to "+x.ToString()+", "+y.ToString()+", "+z.ToString()+"!", true);
		}
		else LogCommandError("The integer(s) you provided do(es) not seem to be valid!", true);
	}

	// public void skipdialogue()
	// {
	// 	SaveState.instance.queuedDialogue = new List<int>(); // no more dialogue fuck you
	// 	SaveState.instance.mainReceiverQueuedMessages = new List<int>();
	// }

	public void gamesettings()
	{

	}
}
