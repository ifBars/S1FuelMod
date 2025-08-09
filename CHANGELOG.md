# Changelog
All notable changes to this project will be documented in this file.

## [Unreleased]

## 2025-08-09
### Added
- Vehicle-type field to simplify switching logic.
- Configurable fuel capacity per vehicle type.
- Conditional fuel capacity based on vehicle type.
- FuelGauge UI shown while refueling.
- Config options to enable/disable dynamic pricing and pricing by tier.

### Changed
- Updated refuel rate to feel more realistic.
- Lowered default fuel capacity to 18L.
- Pricing now reflects player level and can change dynamically by day.
- Appended price-per-liter to hover text.
- Logging: switched a frequent log from `Info` to `Debug`.

### Fixed
- Distance gating for refueling now requires the vehicle to be near the pump (not just the player).
- `localPlayerIsDriver` check corrected.
- Prevented `OnDestroy()` from trying to hide an already-destroyed UI element.

### Removed
- Unused dependencies.

### Documentation
- README refreshed to match the current project.

### Chore
- Synced branch state (“Update to match main branch”).
- Initial repo setup (project files, `.gitattributes`, `.gitignore`).
