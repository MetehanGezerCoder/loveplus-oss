# Proximity experience

Distance is a backend projection from both users' latest valid shared locations. Mobile never submits a trusted distance or proximity label.

Default configured thresholds:

| Level | Distance |
|---|---:|
| `SamePlace` | up to 100 m |
| `VeryClose` | 100–500 m |
| `Nearby` | 500 m–2 km |
| `SameArea` | 2–10 km |
| `Far` | over 10 km |
| `Unavailable` | missing, stale or private sample |

The `Realtime:Proximity` configuration owns these boundaries. For `SamePlace`, the server adds both GPS accuracy radii to the calculated distance. If that uncertainty crosses 100 m, wording is downgraded to `VeryClose`; the UI also labels approximate distance.

If either person disables location sharing, the partner DTO returns no exact distance and no prior coordinate. Stale or absent samples return an explicit `DistanceAvailability` reason. The map can show named markers and a line when configured; without a Maps API key it renders a safe empty state while telemetry/proximity continue.

No visit history, historical distance, “bugün yaklaştınız” score or shared-place memory is generated in v0.4.0. Those require explicit consent and retention design in the Memories phase.
