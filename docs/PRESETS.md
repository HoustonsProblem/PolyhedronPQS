# Shape and subnode reference

Everything inside a `VertexHeightPolyhedron { }` node. See the [README](../README.md) for installation and a general overview.

- [Common settings](#common-settings)
- [Polyhedron](#polyhedron)
- [Cylinder](#cylinder)
- [Hemisphere](#hemisphere)
- [Macaroni](#macaroni)
- [Ellipsoid](#ellipsoid)
- [Troubleshooting](#troubleshooting)

Conventions: names are not case sensitive; write one `name = value` per line; sizes are **relative** (only ratios matter, since `fit` scales the finished shape to the body's radius); out-of-range numbers are clamped and reported in `KSP.log` (search for `PolyhedronPQS`).

## Common settings

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `GeometricPreset` | `None` | `None`, `Cylinder`, `Hemisphere`, `Macaroni`, `Ellipsoid` | selects an irregular shape; overrides `faces` and `layout` |
| `faces` | 0 | 4 to 30 | polyhedron face count (used only when `GeometricPreset` is `None`) |
| `layout` | `Auto` | `Auto`, `Prism`, `Spread` | how a polyhedron's faces are arranged |
| `uniformity` | 5 | 0 to 5 | blend between a sphere (0) and the full shape (5) |
| `smoothing` | 0 | 0 to 0.5 | rounds edges/corners; 0.002 or less is sharp. No effect on the already smooth ellipsoid |
| `fit` | `Circumradius` | `Circumradius`, `Inradius` | `Circumradius` scales so the farthest point is at the body radius; `Inradius` so the nearest point is |
| `rotationX` / `rotationY` / `rotationZ` | 0 | degrees | orientation (Unity Euler order). Unrotated, a shape's long or main axis is the spin axis (Y) |
| `surfaceGravity` | `false` | | gravity follows the surface normal |
| `gravitySmoothing` | 0.1 | | how gradually gravity direction turns across edges |
| `gravityFadeHeight` | 0.25 | | fraction of the center-to-surface distance over which surface gravity fades into normal gravity |
| `surfaceGravityScale` | 1 | | multiplier on the surface gravity strength |
| `alignCamera` | `true` | | free camera "up" follows local gravity |

Notes:

- **uniformity** blends every direction's distance between the sphere and the shape, so 2.5 is half way. At 0 the mod is inert, including gravity.
- **Gravity** has the same strength everywhere on the surface (body surface gravity times `surfaceGravityScale`), falls off as 1/d² above it, and is capped inside the nominal radius so concave spots do not give crushing gravity.
- After changing any shape setting, delete the body's Kopernicus cache (`Cache/<Body>.bin`).

## Polyhedron

```
VertexHeightPolyhedron
{
    faces = 12
    layout = Auto
}
```

| Faces | `Auto` gives |
|---|---|
| 4 | tetrahedron |
| 6 | cube |
| 8 | octahedron |
| 12 | dodecahedron |
| 20 | icosahedron |
| other 5 to 30 | prism: two caps and (faces - 2) sides |

`Prism` always builds a prism; `Spread` spreads the faces evenly over the sphere. Counts that cannot close with the chosen layout fall back to a plain sphere and log a warning.

## Cylinder

Flat caps joined by a round or N-sided wall. The axis is the spin axis.

```
GeometricPreset = Cylinder
Cylinder
{
    verticalRadius = 1
    steps = 0
}
```

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `verticalRadius` | 1 | 0.02 to 50 | half the distance between the caps as a multiple of the cylinder's radius. 1 = as tall as wide; 0.2 = a flat coin; 3 = a tall tube |
| `steps` | 0 | 0 or 3 to 64 | sides around the wall. 0 = round; 6 = hexagonal prism. 1 or 2, or above 64, fall back to round |

## Hemisphere

A dome on a flat base.

```
GeometricPreset = Hemisphere
Hemisphere
{
    verticalRadius = 1
    baseDepth = 0.1
    steps = 0
}
```

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `verticalRadius` | 1 | 0.05 to 50 | dome height above the center as a multiple of its width. 1 = a true hemisphere; below 1 = a flattened dome |
| `baseDepth` | 0.1 | 0.01 to 50 | how far below the center the flat base lies, as a multiple of the radius. Small = a near-perfect hemisphere; larger = a deeper "bowl" body |
| `steps` | 0 | 0 or 3 to 64 | facets around the dome. 0 = smooth |

## Macaroni

A bent tube: straight (0 degrees) up to a half circle (180 degrees). Used by the Platus moon in the Polytrius pack.

```
GeometricPreset = Macaroni
Macaroni
{
    angle = 90
    length = 6
    verticalRadius = 1
    steps = 0
    roundEnds = false
}
```

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `angle` | 90 | 0 to 180 | how far the tube bends, in degrees (0 straight, 90 elbow, 180 half circle). Limited by the visibility rule below |
| `length` | 6 | 0.5 to 40 | length of the tube's middle line as a multiple of the tube radius |
| `verticalRadius` | 1 | 0.2 to 5 | tube height compared with its width. 1 = round; below 1 = flattened |
| `steps` | 0 | 0 or 3 to 64 | sides around the tube. 0 = round |
| `roundEnds` | `false` | `true` / `false` | `false` = ends cut flat like real macaroni; `true` = rounded ends |

**Why the bend is limited.** The surface is built by moving each vertex along a ray from the body's center, so every direction can have only one surface point. If a tube bends so far that one part hides behind another as seen from the center, that cannot be represented. The mod therefore computes the largest angle for which the center stays inside the tube and every point of the middle line stays visible from it. A request above that limit is reduced to it, with the value written to `KSP.log`. A shorter or thicker tube can bend further; a very long thin one only slightly.

## Ellipsoid

A smooth ellipsoid with three independent radii, plus optional craggy relief at the ends of its longest axis (for asteroid- or comet-like bodies).

```
GeometricPreset = Ellipsoid
Ellipsoid
{
    radiusX = 3
    radiusY = 1
    radiusZ = 1
    endNoise = 0.05
    endNoiseFrequency = 6
    endNoiseStart = 0.35
    seed = 0
}
```

| Setting | Default | Range | Meaning |
|---|---|---|---|
| `radiusX` | 1 | 0.02 to 100 | radius along X (one equator direction) |
| `radiusY` | 1 | 0.02 to 100 | radius along Y, the polar / spin axis |
| `radiusZ` | 1 | 0.02 to 100 | radius along Z (the other equator direction) |
| `endNoise` | 0 | 0 to 0.5 | craggy relief at the two ends of the **longest** axis, as a fraction of the local radius (0 = smooth, 0.05 = plus or minus 5%). Faded out where the surface faces sideways, so long flanks never terrace |
| `endNoiseFrequency` | 6 | 0.1 to 200 | size of the relief: higher = smaller, more numerous lumps |
| `endNoiseStart` | 0.35 | 0 to 0.95 | where along the long axis the relief begins: 0 = from the middle, 1 = only at the very tip |
| `seed` | 0 | any integer | changes the random pattern of the relief |

Only the **ratios** of the radii matter; `fit` scales the result so the largest radius reaches the body radius (`Circumradius`) or the smallest does (`Inradius`).

Examples (all with `GeometricPreset = Ellipsoid`):

| Look | Settings |
|---|---|
| Sphere (the default) | all radii 1 |
| Egg / cigar | `radiusY = 1.6`, `radiusX = 1`, `radiusZ = 1` |
| Flattened disc | `radiusY = 0.3`, `radiusX = 1`, `radiusZ = 1` |
| Potato asteroid | `radiusX = 1.6`, `radiusY = 1`, `radiusZ = 0.8`, `endNoise = 0.06` |
| Fully three-axis | `radiusX = 3`, `radiusY = 2`, `radiusZ = 1` |

`smoothing` has no effect on an ellipsoid (it is already smooth). Rotation works as for the other shapes; for a long ellipsoid whose axis should not be the spin axis, use `rotationX`/`rotationZ`.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Body is a plain sphere | no `GeometricPreset` and `faces` outside 4 to 30; or `uniformity = 0`. Check `KSP.log` for lines starting with `PolyhedronPQS` |
| Scaled-space (map view) mesh does not match the ground | stale cache: delete `Cache/<Body>.bin` |
| A setting seems ignored | typo in a name or a value that is not a number: it is warned about in `KSP.log`, not an error |
| Stretched textures on steep ends or inner bends | expected where the surface runs almost straight out from the center; raise PQS `maxLevel` for more mesh detail, or pre-warp the maps |
| Terrain flickers while below the nominal radius | the mod keeps all quads visible for its bodies; make sure `0Harmony` (HarmonyKSP) is installed so the patch applies |
