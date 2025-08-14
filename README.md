# S1FuelMod

A comprehensive fuel system mod for *Schedule I*, adding realistic fuel consumption, HUD integration, fuel stations, and multiplayer synchronization. Built with extensibility and configurability in mind.

---

## 🚗 Features

### **Core Fuel System**
- **Realistic Fuel Consumption**: Fuel consumption tied to throttle input and vehicle speed
- **Per-Vehicle Profiles**: Different fuel capacities and consumption rates for each vehicle type
- **Engine Performance**: Engine stuttering and shutdown when fuel is depleted
- **Persistence**: Fuel levels persist across game sessions with save/load integration

### **Fuel Stations**
- **Interactive Refueling**: Hold interaction to refuel vehicles at designated fuel stations
- **Dynamic Pricing**: Fuel prices vary by day and player tier level
- **Proximity System**: Both player and vehicle must be near the pump to refuel

### **User Interface**
- **Real-time Fuel Gauge**: HUD display showing current fuel level and capacity
- **Visual Warnings**: Color-coded alerts for low and critical fuel levels
- **Refueling Display**: Fuel gauge remains visible during refueling operations

### **Multiplayer Support**
- **Steam P2P Networking**: Synchronizes fuel states across multiplayer sessions
- **Host Authority**: Server-authoritative fuel replication system
- **Automatic Sync**: Real-time updates with heartbeat-based synchronization

### **Configuration & Debugging**
- **MelonPreferences Integration**: In-game mod configuration system
- **Extensive Logging**: Toggable detailed debug information for troubleshooting

---

## 🛠️ Installation

### Prerequisites
- **MelonLoader** installed for *Schedule I*
- Game version: Both Mono and IL2CPP builds supported

### Installation Steps
1. Download the latest **S1FuelMod.dll** for your game version
2. Place the DLL into the `Mods` folder within your *Schedule I* directory
3. Launch the game — fuel mechanics activate automatically for all land vehicles

### Version Compatibility
- **Mono Build**: `S1FuelMod_Mono.dll` for alternate branch
- **IL2CPP Build**: `S1FuelMod_Il2cpp.dll` for main branch

---

## ⚙️ Configuration

All settings are available via the **MelonPreferences** system under the `S1FuelMod` category:

### **Core Settings**
| Setting | Default | Description |
|---------|---------|-------------|
| `EnableFuelSystem` | `true` | Toggle the fuel system on/off |
| `FuelConsumptionMultiplier` | `1.0` | Global fuel consumption modifier (0.5 = half, 2.0 = double) |
| `ShowFuelGauge` | `true` | Enable the on-screen fuel gauge UI |
| `EnableDebugLogging` | `true` | Toggle detailed debug logs |

### **Fuel Capacities (Liters)**
| Vehicle | Default | Range |
|---------|---------|-------|
| `DefaultFuelCapacity` | `40.0` | 10-200L |
| `ShitboxFuelCapacity` | `30.0` | 10-200L |
| `VeeperFuelCapacity` | `40.0` | 10-200L |
| `BruiserFuelCapacity` | `40.0` | 10-200L |
| `DinklerFuelCapacity` | `55.0` | 10-200L |
| `HounddogFuelCapacity` | `35.0` | 10-200L |
| `CheetahFuelCapacity` | `35.0` | 10-200L |
| `HotboxFuelCapacity` | `50.0` | 10-200L |

### **Pricing & Economy**
| Setting | Default | Description |
|---------|---------|-------------|
| `EnableDynamicPricing` | `true` | Enable daily price variations |
| `EnablePricingOnTier` | `true` | Scale prices based on player tier |

---

## 🎮 Gameplay

### **Fuel Consumption**
- **Base Rate**: 6L per minute at full throttle
- **Idle Rate**: 0.5L per minute when stationary
- **Vehicle-Specific**: Each vehicle type has unique consumption characteristics

### **Refueling Mechanics**
- **Station Locations**: You can refuel at either of the two gas stations in the game
- **Interaction**: Hold the interaction key at a fuel pump with your car nearby to refuel
- **Cost**: Fuel prices scale with player tier and vary by day

### **Engine Behavior**
- **Low Fuel Warning**: Alert at 20% fuel remaining
- **Critical Warning**: Alert at 5% fuel remaining
- **Engine Sputter**: Performance degradation below 4L
- **Engine Cutoff**: Complete shutdown at 0L

---

## 🔧 Development Setup

### **Prerequisites**
- Visual Studio 2019/2022 or JetBrains Rider
- .NET Framework 4.7.2+
- A working installation of *Schedule I* (Mono and/or IL2CPP)

### **Get Started**
```bash
git clone https://github.com/ifBars/S1FuelMod.git
cd S1FuelMod
```

### **Configure Game Paths**
Update the paths in `build/paths.props` for your target installations:

```xml
<S1MonoDir>D:\SteamLibrary\steamapps\common\Schedule I_alternate</S1MonoDir>
<S1CPPDir>D:\SteamLibrary\steamapps\common\Schedule I_public</S1CPPDir>
```

### **Build Commands**
```bash
# For development/testing (Mono build)
dotnet build --configuration "Debug Mono"

# For production release (IL2CPP build)
dotnet build --configuration "Release IL2CPP"

# For debugging with UnityExplorer
dotnet build --configuration "Debug IL2CPP" /p:UnityExplorer=true
```

### **Auto-Deploy Features**
The build process automatically:
1. Terminates running *Schedule I* instances
2. Copies the DLL to the `Mods` folder
3. Launches the game with appropriate settings

---

## 🏗️ Architecture

### **Core Systems**
- **FuelSystemManager**: Central coordinator for all fuel-related operations
- **FuelStationManager**: Manages fuel station interactions and pricing
- **VehicleFuelSystem**: Individual vehicle fuel logic and state
- **FuelUIManager**: Handles HUD display and UI updates

### **Networking**
- **FuelNetworkManager**: Steam P2P networking for multiplayer sync
- **P2PMessages**: Network message serialization and handling
- **MiniMessageSerializer**: Compact message format for efficiency

### **Integration**
- **HarmonyPatches**: Game code modifications for fuel system integration
- **Persistence**: Save/load integration with game's vehicle data system

---

## 📋 Planned Features

### **Short Term**
- **Balance fuel consumption by vehicle type**: Fine-tune consumption rates per vehicle
- **Dynamic pricing refinement**: Optimize base price and daily variance targets
- **Usable Gas Can Item**: Allow gas can item to be used as a one time fuel source

### **Long Term**
- **Diesel fuel for trucks**: Separate fuel type system
- **NPC interactions**: Walk-out animations and voicelines at fuel stations
- **Advanced economy**: Fuel market simulation and supply/demand mechanics

---

## 🤝 Contributing

### **Current Contributors**
- **SirTidez**
- **ifBars**

---

## 🐛 Troubleshooting

### **Common Issues**
- **Mod not loading**: Ensure MelonLoader is properly installed
- **Fuel gauge not showing**: Check `ShowFuelGauge` setting in preferences
- **Multiplayer sync issues**: Verify that you are running the game through Steam

### **Debug Information**
Enable debug logging via `EnableDebugLogging` preference or press F6 to toggle debug output.

---

## 📄 License

This mod is provided as-is for educational and entertainment purposes. Use at your own risk.

---

**Enjoy enhanced immersion with S1FuelMod!** 🚗⛽

Drive smart, refuel often — your engine (and multiplayer partners) will appreciate it.