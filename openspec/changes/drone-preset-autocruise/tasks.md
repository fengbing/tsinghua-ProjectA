## 1. Route authoring



- [x] 1.1 Add `DroneAutocruiseRoute` (or equivalent) with ordered waypoint list: `Transform[]` or children under a route root; validate non-null entries at edit/runtime.

- [x] 1.2 Optional: `OnDrawGizmos` / `DrawGizmosSelected` to draw polyline and arrival radii for designers.



## 2. Cruise controller (drone)



- [x] 2.1 Add `DroneAutocruiseController` on the drone: references `Rigidbody`, `PlaneController`, `DroneAutocruiseRoute`, `DroneGripper` (for hold check).

- [x] 2.2 Implement `FixedUpdate` (or coordinated phase): steer toward current waypoint with serialized `cruiseMaxSpeed`, `arrivalRadius`, smooth acceleration if needed.

- [x] 2.3 Advance waypoint index when within `arrivalRadius`; on last waypoint reached, call `EndCruise()`; optional Inspector `loopRoute` if product wants repeat.



## 3. Input and PlaneController integration



- [x] 3.1 Default `KeyCode.Y` on `GetKeyDown`; expose in Inspector; start only if route valid and **not** holding cargo (`DroneGripper.IsHolding` or project equivalent).

- [x] 3.2 On cruise start: `PlaneController.SetInputEnabled(false)`; on complete/cancel: `SetInputEnabled(true)` (respect tutorial: if tutorial already disabled input, do not force-enable—coordinate with `TutorialInputRestriction` if present).



## 4. QA and scenes



- [ ] 4.1 Place a sample route + waypoints in one gameplay scene; assign on drone prefab/instance; verify Y start/stop, path order, arrival at final point, block-while-holding.

- [x] 4.2 Document in code/tooltips: how to add waypoints, toggle cancel with Y, tuning speeds and radii.


