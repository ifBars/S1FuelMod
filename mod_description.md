# S1FuelMod - Realistic Fuel System

[![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.0-brightgreen.svg?style=flat-square)](https://melonwiki.xyz/#/)
[![Schedule I](https://img.shields.io/badge/Schedule%20I-Mono%20%7C%20IL2CPP-blue.svg?style=flat-square)]()
[![Version](https://img.shields.io/badge/Version-1.0.0-blueviolet.svg?style=flat-square)](https://github.com/ifBars/S1FuelMod/releases)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-black.svg?style=flat-square&logo=github)](https://github.com/ifBars/S1FuelMod)

**Transform Schedule I with a comprehensive fuel system that adds depth, strategy, and immersion to every drive.**

---

## 🚗 What This Mod Does

S1FuelMod adds realistic fuel consumption to all land vehicles in Schedule I. Vehicles now require fuel to operate, with consumption rates tied to throttle input and vehicle speed. When fuel runs low, engines sputter and eventually shut down, forcing players to manage their fuel economy and visit gas stations.

## ✨ Key Features

- **Realistic Fuel Consumption**: Vehicles consume fuel based on throttle and speed (6L/min full throttle, 0.5L/min idle)
- **Per-Vehicle Fuel Tanks**: Different capacities for each vehicle type (Shitbox: 30L, Dinkler: 55L, etc.)
- **Interactive Gas Stations**: Refuel at the two gas stations in the game world
- **Dynamic Fuel Pricing**: Prices vary by day and scale with player tier level
- **Real-time Fuel Gauge**: HUD display with color-coded warnings for low fuel
- **Engine Performance**: Stuttering and shutdown mechanics when fuel is depleted
- **Multiplayer Sync**: Fuel states synchronize across multiplayer sessions
- **Persistent Fuel Levels**: Fuel amounts save and load with your game

## 🎮 Gameplay Impact

- **Strategic Planning**: Plan routes and manage fuel economy
- **Economic Depth**: Fuel costs add another money management layer
- **Risk Management**: Running out of fuel can leave you stranded
- **Multiplayer Coordination**: Team up to share fuel resources

## 🛠️ Installation

### Requirements
- **[MelonLoader](https://melonwiki.xyz/#/)** for Schedule I

### Installation Steps
1. Ensure **[MelonLoader](https://melonwiki.xyz/#/)** is installed for Schedule I
2. Download the latest release
3. Choose the appropriate version:
   - **IL2CPP**: `S1FuelMod_Il2cpp.dll` (main branch)
   - **Mono**: `S1FuelMod_Mono.dll` (alternate branch)
4. Place the DLL in your `Mods` folder
5. Launch the game - fuel system activates automatically

## ⚙️ Configuration

All settings are configurable through **MelonPreferences**:
- Toggle fuel system on/off
- Adjust global consumption multiplier
- Customize fuel capacities per vehicle type
- Enable/disable dynamic pricing
- Toggle fuel gauge display
- Enable debug logging

Access settings in `UserData/MelonPreferences.cfg` under the `[S1FuelMod]` section.

## 🎯 Perfect For

- Players who want more realistic vehicle mechanics
- Multiplayer sessions requiring resource management
- Roleplay scenarios involving logistics and planning
- Anyone seeking additional depth to the driving experience

## 📋 Support & Development

- **📂 GitHub Repository**: [https://github.com/ifBars/S1FuelMod](https://github.com/ifBars/S1FuelMod)
- **📝 Development Roadmap**: [Trello Board](https://trello.com/b/GXaIpuIN)
- **🐛 Bug Reports**: [GitHub Issues](https://github.com/ifBars/S1FuelMod/issues)
- **💬 Feedback**: Open an issue on GitHub or check the Trello board

## 👥 Credits

- **SirTidez** - Gameplay tuning, capacity logic, dynamic pricing, UI/UX polish
- **ifBars** - Initial architecture, fuel stations, persistence, networking

---

**Drive smart, refuel often - your engine will thank you!** 🚗⛽
