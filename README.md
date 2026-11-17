# PolyhedronPQS

A [Kopernicus](https://github.com/Kopernicus/Kopernicus) PQS mod for Kerbal Space Program 1.12.x that turns a planet or moon into a **polyhedron** (a cube, a prism, a dodecahedron, up to 30 faces) or into a ready-made **irregular shape**: a cylinder, a hemisphere, a bent "macaroni" tube, or an ellipsoid.

It works much like Kopernicus's oblate mods, but gravity points along the *surface* of the shape instead of at the core, so standing on a flat face feels like standing on flat ground, and the free camera keeps "up" lined up with that gravity.

- **Download:** [SpaceDock](https://spacedock.info/mod/4793/PolyhedronPQS) (CKAN listing: see [Install](#install))
- **Shape reference (every preset and setting):** [docs/PRESETS.md](docs/PRESETS.md)
- **Source:** [Source/](Source)

## Dependencies

| Mod | Why | Link |
|---|---|---|
| Kopernicus | provides the PQS mod and config system this plugs into | [github.com/Kopernicus/Kopernicus](https://github.com/Kopernicus/Kopernicus) |
| HarmonyKSP (Harmony 2) | patches gravity, the camera and terrain visibility | [github.com/KSPModdingLibs/HarmonyKSP](https://github.com/KSPModdingLibs/HarmonyKSP) |
| ModuleManager | required by Kopernicus, and used to patch bodies | [github.com/sarbian/ModuleManager](https://github.com/sarbian/ModuleManager) |

Kopernicus has its own dependencies; install it following its instructions (CKAN installs everything for you).

## Install

1. Install the dependencies above.
2. Download PolyhedronPQS from [SpaceDock](https://spacedock.info/mod/4793/PolyhedronPQS) and put the `PolyhedronPQS` folder in `GameData`. The plugin is `GameData/PolyhedronPQS/Plugins/PolyhedronPQS.dll`.
3. Add a `VertexHeightPolyhedron` node to the PQS mods of a Kopernicus body (below).
4. After changing any shape setting, **delete that body's Kopernicus mesh cache** (`GameData/<your pack>/Cache/<Body>.bin`) so the scaled-space mesh is rebuilt to match the ground.

CKAN: PolyhedronPQS has a [NetKAN entry](https://github.com/KSP-CKAN/NetKAN/blob/master/NetKAN/PolyhedronPQS.netkan) pointing at the SpaceDock release, so it can be installed with CKAN along with its dependencies.

## Quick start

A body that is a cube:

```
@Kopernicus:AFTER[KOPERNICUS]
{
    @Body[MyBody]
    {
        @PQS
        {
            @Mods
            {
                VertexHeightPolyhedron
                {
                    faces = 6
                    smoothing = 0.05
                    surfaceGravity = true
                }
            }
        }
    }
}
```

A cigar-shaped body (an ellipsoid three times as long as it is wide):

```
VertexHeightPolyhedron
{
    GeometricPreset = Ellipsoid
    Ellipsoid
    {
        radiusX = 3
        radiusY = 1
        radiusZ = 1
    }
    surfaceGravity = true
}
```

Write **one value per line**, like everywhere else in a KSP config. The mod is forgiving: names are not case sensitive, and a typo or a word where a number should be is a warning in `KSP.log` (search for `PolyhedronPQS`), not a body that fails to load.

## Shapes

Set either `faces` (a polyhedron) **or** `GeometricPreset` (an irregular shape). When `GeometricPreset` is set, `faces` and `layout` are ignored.

| Shape | Set with | In one line | Details |
|---|---|---|---|
| Polyhedron | `faces = 4..30` | flat faces, from tetrahedron and cube to 30 faces | [below](#polyhedron-faces-layout-fit) |
| Cylinder | `GeometricPreset = Cylinder` | flat caps joined by a round or N-sided wall | [PRESETS.md](docs/PRESETS.md#cylinder) |
| Hemisphere | `GeometricPreset = Hemisphere` | a dome on a flat base | [PRESETS.md](docs/PRESETS.md#hemisphere) |
| Macaroni | `GeometricPreset = Macaroni` | a bent tube, from straight to a half circle | [PRESETS.md](docs/PRESETS.md#macaroni) |
| Ellipsoid | `GeometricPreset = Ellipsoid` | three independent radii; optional craggy ends | [PRESETS.md](docs/PRESETS.md#ellipsoid) |

Each preset has its own settings node of the same name inside `VertexHeightPolyhedron { }` (`Cylinder { }`, `Hemisphere { }`, `Macaroni { }`, `Ellipsoid { }`). Every value in them is optional.

### Polyhedron: faces, layout, fit

| Setting | Values | Default | Meaning |
|---|---|---|---|
| `faces` | 4 to 30 | 0 | number of faces. 0, below 4 or above 30 leaves the body a normal sphere (with a warning) |
| `layout` | `Auto`, `Prism`, `Spread` | `Auto` | `Auto`: tetrahedron (4), cube (6), octahedron (8), dodecahedron (12), icosahedron (20), otherwise a prism (2 caps + faces - 2 sides). `Prism`: always a prism. `Spread`: faces spread evenly over the sphere (gem or golf-ball look) |
| `fit` | `Circumradius`, `Inradius` | `Circumradius` | `Circumradius`: the corners touch the body radius (the solid sits inside the sphere). `Inradius`: the face centers touch it (corners poke out) |

Counts that cannot form a closed solid with the chosen layout (for example 4 faces with `Prism`) fall back to a normal sphere.

## Options for every shape

| Setting | Default | Meaning |
|---|---|---|
| `GeometricPreset` | `None` | `None`, `Cylinder`, `Hemisphere`, `Macaroni` or `Ellipsoid` |
| `uniformity` | 5 | 0 to 5: how much of the shape is applied. 5 = the full shape, 2.5 = every point halfway between a sphere and the shape, 0 = the mod does nothing (including gravity) |
| `smoothing` | 0 | 0 to 0.5: rounds edges and corners. Up to 0.002 counts as sharp; about 0.05 softens corners slightly, 0.15 clearly rounds them. (The ellipsoid is already smooth, so this does not change it) |
| `fit` | `Circumradius` | which part of the shape touches the body radius (see above). For presets, the farthest point is used with `Circumradius` and the nearest with `Inradius` |
| `rotationX`, `rotationY`, `rotationZ` | 0 | orientation in degrees (Unity Euler order). By default a shape's axis is the body's spin axis |
| `surfaceGravity` | `false` | gravity follows the surface normal, equally strong everywhere on the surface |
| `gravitySmoothing` | 0.1 | how gradually the gravity direction turns across an edge (0 = abrupt) |
| `gravityFadeHeight` | 0.25 | how high above the surface (as a fraction of the distance from the center to the surface) surface gravity fades into normal gravity |
| `surfaceGravityScale` | 1 | multiplier on the body's surface gravity used in surface-gravity mode |
| `alignCamera` | `true` | with surface gravity on, the free camera's "up" follows the gravity direction |

## What it does in game

- **Terrain.** The mod adds height to every vertex so the body's surface follows the shape. It is additive, so heightmaps and noise stack on top of it.
- **Surface gravity.** With `surfaceGravity = true` gravity points along the shape's surface normal and has the same strength (the body's surface gravity times `surfaceGravityScale`) everywhere on the surface. It falls off as 1/d² above the surface and fades into stock gravity over `gravityFadeHeight`. Inside the body's nominal radius the pull toward the center is held at the strength it has there, so low spots (the flat side of a hemisphere, the inside of a macaroni's bend) do not produce crushing gravity. Orbit Drift Compensation is switched off while the active vessel is inside a surface-gravity zone and restored afterwards.
- **Camera.** In free camera mode the vertical axis follows local gravity so the ground does not look tilted near edges and corners.
- **Terrain visibility.** KSP decides which terrain quads to draw from a horizon computed for a ball of the body's radius. On these shapes the camera is often below that radius, which makes terrain flicker or vanish; the mod keeps every quad in view for its bodies while that is the case.

## Limits worth knowing

- **Every shape is star-shaped about the body's center.** KSP builds a surface by moving each vertex along a straight line out from the center, so each direction has exactly one surface point. A real hollow torus is impossible, and the macaroni's bend is limited so no part of the tube hides behind another as seen from the center (see [PRESETS.md](docs/PRESETS.md#macaroni)).
- **Textures stretch where the surface runs almost straight out from the center** (steep walls near the ends of a macaroni or a long ellipsoid). Equirectangular color, height and normal maps need to be pre-warped, or you accept some stretching; raising the body's PQS `maxLevel` adds mesh detail there.
- Out-of-range values are clamped, and each clamp is reported in `KSP.log`.

## Building from source

The source is in [Source/](Source) (C# 7.3, .NET Framework 4.8):

```
dotnet build Source/PolyhedronPQS.csproj -c Release -p:KSPRoot="/path/to/Kerbal Space Program"
```

It references `Assembly-CSharp`, `UnityEngine`, `Kopernicus.dll`, `Kopernicus.Parser.dll` and `0Harmony.dll` from your KSP install (override locations with `-p:KSPManaged=`, `-p:KopernicusPlugins=`, `-p:HarmonyDll=`) and copies the result to `GameData/PolyhedronPQS/Plugins`. Keep the project folder outside `GameData`, or KSP will load the `bin` and `obj` copies as duplicates.

`GeometricPresets.cs` has no KSP or Unity dependencies, so the shape math can be reused and tested on its own.

## Credits and license

Built on Kopernicus and HarmonyKSP.

Licensed under CC0 1.0 (see [LICENSE](LICENSE)), matching the SpaceDock listing.
