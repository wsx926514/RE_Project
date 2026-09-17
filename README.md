# Crane Physics Rig

The project now contains only a reusable Unity Physics 2D rig script. It does
not create GameObjects and contains no camera, rendering, input, UI, scoring,
timer, audio, particles, or gameplay flow.

## Scene objects to create

Create these objects yourself and assign them to `CranePhysicsRig`:

1. Root anchor: `Rigidbody2D`
2. First link: `Rigidbody2D`, `BoxCollider2D`, and `HingeJoint2D`
3. Second link: `Rigidbody2D`, `BoxCollider2D`, and `HingeJoint2D`
4. Optional first-joint and end-mass child colliders
5. Optional boom-pivot transform and `PhysicsMaterial2D` assets

Attach `CranePhysicsRig` to any manager object. Both links use local +X from
their hinge toward their free end. A mass collider may be placed on the same
link object or on a child without its own `Rigidbody2D`.

`startSimulatingOnAwake` releases the links automatically. A future UI can call
`SetConfiguration`, `SetBoomAngle`, `SetLinkLengths`, `SetEndMassDensity`,
`StartSimulation`, and `ResetSimulation`.

Link lengths only have a small non-zero minimum and no upper limit. Once
simulation starts, Unity Physics 2D controls all motion and collision response.
