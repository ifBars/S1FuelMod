# S1FuelMod

A comprehensive fuel system mod for Schedule I that adds realistic fuel consumption and refueling mechanics to land vehicles.

## Features

### Current Implementation (v1.0.0)
- **Fuel System**: All vehicles now consume fuel based on throttle input and speed
- **Fuel Gauge UI**: Real-time fuel level display when driving vehicles
- **Fuel Warnings**: Visual and audio warnings for low fuel and critical fuel levels
- **Engine Effects**: Vehicles stop working when out of fuel, with engine stuttering at very low fuel
- **Debug Controls**: Developer tools for testing and debugging fuel systems

### Planned Features
- **Fuel Stations**: Dedicated refueling locations around the map
- **Fuel Economy**: Different consumption rates for different vehicle types
- **Fuel Costs**: Economic integration with the game's money system
- **Multiplayer Sync**: Fuel levels synchronized across multiplayer sessions
- **Save/Load**: Fuel levels persist across game sessions
- **Fuel Types**: Different fuel grades with varying performance characteristics

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) for Schedule I
2. Download the latest release of S1FuelMod
3. Place `S1FuelMod.dll` in the `Mods` folder in your Schedule I directory
4. Launch the game

## Configuration

The mod creates a configuration file at `UserData/S1FuelMod.cfg` with the following options:

- **EnableFuelSystem** (default: true): Enable or disable the entire fuel system
- **FuelConsumptionMultiplier** (default: 1.0): Adjust fuel consumption rate (0.5 = half consumption, 2.0 = double consumption)
- **DefaultFuelCapacity** (default: 50.0): Default fuel tank capacity in liters
- **ShowFuelGauge** (default: true): Show fuel gauge UI when driving
- **EnableDebugLogging** (default: false): Enable detailed debug logging

## Debug Controls

- **F9**: Toggle fuel system debug information
- **F10**: Refill all vehicles to full capacity
- **F11**: Drain 10L of fuel from all vehicles

## Technical Details

### Fuel Consumption
- Base consumption: 6L/hour at full throttle
- Idle consumption: 0.5L/hour when engine is running
- Consumption scales with throttle input and vehicle speed
- High-speed driving (>50 km/h) increases consumption

### Warning Thresholds
- Low fuel warning: 20% of tank capacity
- Critical fuel warning: 5% of tank capacity
- Engine cutoff: 0L (empty tank)
- Engine stuttering: Below 2L

### UI Elements
- Fuel gauge positioned in top-left corner of screen
- Color-coded fuel level (green → yellow → red)
- Percentage and liter display
- Pulsing warning icon for critical fuel levels

## Development

### Project Structure
```
S1FuelMod/
├── Core.cs                     # Main mod entry point
├── Utils/
│   ├── Constants.cs           # Configuration constants
│   └── ModLogger.cs           # Logging utilities
├── Systems/
│   ├── VehicleFuelSystem.cs   # Individual vehicle fuel management
│   └── FuelSystemManager.cs   # Global fuel system management
├── UI/
│   ├── FuelUIManager.cs       # UI system management
│   └── FuelGaugeUI.cs         # Fuel gauge implementation
└── Integrations/
    └── HarmonyPatches.cs      # Game integration patches
```

### Dependencies
- MelonLoader
- HarmonyLib (included with MelonLoader)
- UnityEngine (game dependency)

### Building
1. Reference the Schedule I game assemblies
2. Reference MelonLoader assemblies
3. Build as a .NET Framework library

## Compatibility

- **Schedule I Version**: Latest version
- **MelonLoader Version**: 0.6.1+
- **Other Mods**: Should be compatible with most mods

## Known Issues

- Fuel levels do not yet persist across game sessions (save/load not implemented)
- Multiplayer fuel synchronization not yet implemented
- No fuel stations available yet (manual refueling only via debug commands)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## Changelog

### v1.0.0 (Current)
- Initial release
- Basic fuel system implementation
- Fuel gauge UI
- Debug controls
- Harmony integration with vehicle systems

## License

This mod is released under the MIT License. See LICENSE file for details.

## Support

For bug reports, feature requests, or general support, please create an issue on the project repository.
