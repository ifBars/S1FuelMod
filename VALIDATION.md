# S1FuelMod 1.3.4 validation

The release was checked against Schedule I 0.4.6f13 and 0.4.7f6, separately for Mono and IL2CPP.

| Game | Runtime | Loader | Release build | Loaded-save probe |
|---|---|---|---|---|
| 0.4.6f13 Alternate | Mono | 0.7.3 | Pass | Pass |
| 0.4.6f13 | IL2CPP | 0.7.0 | Pass | Pass |
| 0.4.7f6 Alternate | Mono | 0.7.3 | Pass | Pass |
| 0.4.7f6 | IL2CPP | 0.7.3 | Pass | Pass |

Each probe used an isolated install with only S1FuelMod and a temporary validation mod, plus a disposable copy of a completed 0.4.6 save. It reached normal gameplay with 31 vehicles and checked:

- An owned vehicle has a fuel component.
- Consuming 2 L from 10 L leaves 8 L; adding 3 L leaves 11 L.
- An empty tank reports out of fuel.
- Vehicle save serialization includes fuel data.
- Fuel station components exist.
- Equipping the native gasoline item attaches the gas-can component.
- The owned vehicle has its gas-can interaction component.

The original 1.3.3 source failed both Mono builds with three missing hotbar-equipment member errors and one missing audio-volume member error. Both original IL2CPP builds compiled; an original IL2CPP runtime baseline also passed the basic fuel probe.

The existing IL2CPP registration diagnostic (`Assembly S1FuelMod-IL2CPP.dll is not registered in il2cpp`) remains. Both tested IL2CPP loader versions continued registering the components and passed the gameplay checks. This release does not claim to fix that diagnostic or compatibility with other mods.

These are single-player component and integration probes, not a full driving, pump-payment, save/reload, or multiplayer regression test. Network initialization used the local test Steam emulator; real Steam multiplayer was not retested. Local game assemblies, generated wrappers, saves, screenshots, and temporary probes are excluded from source and packages.

Packaging validates assembly version and permits only the selected S1FuelMod DLL in each ZIP. Companion JSON files contain archive, DLL, and reference hashes. Existing compiler warnings are not treated as a clean warning baseline.
