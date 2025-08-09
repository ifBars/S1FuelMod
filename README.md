# S1FuelMod

A comprehensive fuel system mod for *Schedule I*, adding realistic fuel consumption, HUD integration, and persistence. Built with extensibility and configurability in mind.

---

##  Features

### **Implemented**
- Fuel consumption tied to throttle input and vehicle speed  
- HUD fuel gauge display  
- Engine stuttering and shutdown when fuel is depleted  
- Fuel levels persist across game sessions  

### **Planned Enhancements**
- Vehicle-specific fuel economy profiles  
- In-game fuel costs and economic integration  
- Multiplayer synchronization of fuel states  

---

##  Installation

1. Install **MelonLoader** for *Schedule I*.
2. Download the latest **S1FuelMod.dll**.
3. Place the DLL into the `Mods` folder within your *Schedule I* directory.
4. Launch the game — fuel mechanics should activate for all land vehicles (Joyride Compatible).

---

##  Configuration

All settings are available via the **MelonPreferences** system under the `S1FuelMod` category:

| Key                        | Default | Description                                        |
|----------------------------|---------|----------------------------------------------------|
| `EnableFuelSystem`         | `true`  | Toggle the fuel system on or off                   |
| `FuelConsumptionMultiplier`| `1.0`   | Global modifier for fuel consumption (0.5 = half, 2.0 = double) |
| `DefaultFuelCapacity`      | `50.0`  | Default fuel tank capacity (liters)                |
| `ShowFuelGauge`            | `true`  | Enable the on‑screen fuel gauge UI                 |
| `EnableDebugLogging`       | `false` | Toggle detailed debug logs                         |

---

##  Development Setup

### Prerequisites
- Visual Studio 2019/2022 or JetBrains Rider  
- .NET Framework 4.7.2+  
- A working installation of *Schedule I* (Mono and/or Il2Cpp)

### Get Started
```bash
git clone https://github.com/ifBars/S1FuelMod.git
cd S1FuelMod
```

### Configure Game Path
Update the `<GamePath>` in `S1FuelMod.csproj` for your target build:

```xml
<PropertyGroup Condition="'$(Configuration)'=='Mono'">
  <GamePath>C:\Path\To\ScheduleI_Mono</GamePath>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)'=='Il2cpp'">
  <GamePath>C:\Path\To\ScheduleI_Il2cpp</GamePath>
</PropertyGroup>
```

### Build Commands
```bash
# For development/testing (Mono build)
dotnet build --configuration Mono

# For production release (Il2cpp build)
dotnet build --configuration Il2cpp
```

### Auto‑Deploy (Optional)
By default, the build process will:
1. Terminate any running *Schedule I* instance  
2. Copy the DLL into the `Mods` folder  
3. Launch the game automatically  

To disable, comment out or remove the `<Target Name="PostBuild">` block in the `.csproj`.

### Manual Deploy
If auto-deploy is disabled:
- **Mono build**: copy `bin\Mono\S1FuelMod_Mono.dll` to `[GamePath]\Mods\`
- **Il2cpp build**: copy `bin\Il2cpp\S1FuelMod_Il2cpp.dll` to `[GamePath]\Mods\`

---

Consider working on these open or planned features:
- **Low‑Fuel Behavior**: Improve stutter logic with a state machine
- **Refueling Mechanics**: Portable canisters
- **Multiplayer Sync**: Design a server-authoritative fuel replication system
- **UI Polish**: Implement range estimates, flashing alerts, and HUD customization

See `CHANGELOG.md` for version history and recent changes.

---

Enjoy enhanced immersion with S1FuelMod! Drive smart, refuel often — your engine (and community) will appreciate it.
