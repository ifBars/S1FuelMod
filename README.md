# S1FuelMod

A comprehensive fuel system mod for Schedule I that adds realistic fuel consumption and refueling mechanics to land vehicles.

## Features

### Current Implementation (v1.0.0)
- **Fuel System**: All vehicles now consume fuel based on throttle input and speed
- **Fuel Gauge UI**: Real-time fuel level display when driving vehicles
- **Engine Effects**: Vehicles stop working when out of fuel, with engine stuttering at very low fuel (engine stuttering needs improvement)
- **Save/Load**: Fuel levels persist across game sessions

### Planned Features
- **Fuel Stations**: Dedicated refueling locations around the map
- **Fuel Economy**: Different consumption rates for different vehicle types
- **Fuel Costs**: Economic integration with the game's money system
- **Multiplayer Sync**: Fuel levels synchronized across multiplayer sessions


## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) for Schedule I
2. Download the latest release of S1FuelMod
3. Place `S1FuelMod.dll` in the `Mods` folder in your Schedule I directory
4. Launch the game

## Configuration

The mod uses MelonPreferences, you can find these configuration options under the "S1FuelMod" category:

- **EnableFuelSystem** (default: true): Enable or disable the entire fuel system
- **FuelConsumptionMultiplier** (default: 1.0): Adjust fuel consumption rate (0.5 = half consumption, 2.0 = double consumption)
- **DefaultFuelCapacity** (default: 50.0): Default fuel tank capacity in liters
- **ShowFuelGauge** (default: true): Show fuel gauge UI when driving
- **EnableDebugLogging** (default: false): Enable detailed debug logging

## Technical Details

### Fuel Consumption
- Base consumption: 6L/hour at full throttle
- Idle consumption: 0.5L/hour when engine is running
- Consumption scales with throttle input and vehicle speed
- High-speed driving (>50 km/h) increases consumption

## Development Setup

### Prerequisites
- Visual Studio 2019/2022 or JetBrains Rider
- .NET Framework 4.7.2 or later
- Schedule I game installed (both Mono and Il2cpp versions if testing both)

### (Main Branch) Project Configuration

#### 1. Clone the Repository
```bash
git clone https://github.com/ifBars/S1FuelMod.git
cd S1FuelMod
```

#### 2. Configure Game Paths
The project supports both Mono and Il2cpp builds. You need to update the game paths in `S1FuelMod.csproj`:

**For Mono Build (Development/Testing):**
- Find the `<PropertyGroup Condition="'$(Configuration)'=='Mono'">` section
- Update `<GamePath>` to point to your Schedule I Mono installation:
  ```xml
  <GamePath>C:\Your\Path\To\Schedule I_alternate</GamePath>
  ```

**For Il2cpp Build (Production):**
- Find the `<PropertyGroup Condition="'$(Configuration)'=='Il2cpp'">` section
- Update `<GamePath>` to point to your Schedule I Il2cpp installation:
  ```xml
  <GamePath>C:\Your\Path\To\Schedule I_public</GamePath>
  ```

#### 3. Verify MelonLoader Installation
Ensure MelonLoader is installed in your Schedule I directories:
- Mono: `[GamePath]\MelonLoader\net35\`
- Il2cpp: `[GamePath]\MelonLoader\net6\`

#### 4. Build Configurations
The project includes two build configurations:
- **Mono**: For development and testing (faster builds)
- **Il2cpp**: For production releases

To switch between configurations:
- In Visual Studio: Use the Configuration Manager dropdown
- In Rider: Use the build configuration selector
- Command line: `dotnet build --configuration Mono` or `dotnet build --configuration Il2cpp`

### Building and Testing

#### 1. Build the Mod
```bash
# For Mono (development)
dotnet build --configuration Mono

# For Il2cpp (production)
dotnet build --configuration Il2cpp
```

#### 2. Auto-Deploy (Optional)
The project includes a post-build script that automatically:
- Kills any running Schedule I process
- Copies the built DLL to the Mods folder
- Launches the game

To disable auto-deploy, comment out the `<Target Name="PostBuild">` section in the .csproj file.

#### 3. Manual Deployment
If auto-deploy is disabled, manually copy the built DLL:
- Mono: `bin\Mono\S1FuelMod_Mono.dll` → `[GamePath]\Mods\`
- Il2cpp: `bin\Il2cpp\S1FuelMod_Il2cpp.dll` → `[GamePath]\Mods\`

### Code Style Guidelines
- Follow C# coding conventions
- Use meaningful variable and method names
- Add comments for complex logic
- Keep methods focused and concise
- Test your changes before submitting