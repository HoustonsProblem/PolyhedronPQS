/*
 * PolyhedronGravity
 * ----------------------------------------------------------------------------
 * Hooks KSP so bodies that use VertexHeightPolyhedron with "surfaceGravity = true"
 * pull along the surface normal with the same strength everywhere on the surface,
 * instead of toward the center of the body, and (optionally) keeps the free camera's
 * "up" lined up with that gravity.
 *
 * Same approach as VertexHeightOblateAdvanced (MIT, by Lt_Duckweed): Harmony patches on
 * FlightGlobals.getGeeForceAtPosition and FlightCamera.GetCameraFoR. Anything that is
 * not on such a body, or is too far above it, falls straight through to stock behaviour.
 *
 * Every patch is installed on its own and guarded, so if a KSP method is not what is
 * expected the worst case is that feature stays off (see KSP.log, search "PolyhedronPQS").
 *
 * Requires HarmonyKSP (GameData/000_Harmony), which Kopernicus already needs.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PolyhedronPQS
{
    internal static class PolyhedronGravity
    {
        // body -> its polyhedron mod (null = looked up, there isn't one)
        private static readonly Dictionary<CelestialBody, PQSMod_VertexHeightPolyhedron> cache =
            new Dictionary<CelestialBody, PQSMod_VertexHeightPolyhedron>();

        private static bool errorLogged;

        internal static PQSMod_VertexHeightPolyhedron GetMod(CelestialBody body)
        {
            if (body == null || body.pqsController == null)
            {
                return null;
            }

            PQSMod_VertexHeightPolyhedron mod;
            if (cache.TryGetValue(body, out mod))
            {
                if ((object)mod == null)
                {
                    return null; // already checked: this body has no polyhedron mod
                }
                if (mod != null)
                {
                    return mod;  // still alive
                }
                // the object was destroyed (scene reload): look again
            }

            mod = body.pqsController.GetComponentInChildren<PQSMod_VertexHeightPolyhedron>(true);
            cache[body] = mod;
            return mod;
        }

        /// <summary>Surface gravity at a world position, or false to use stock gravity.</summary>
        internal static bool TryCompute(Vector3d pos, CelestialBody body, out Vector3d result)
        {
            result = new Vector3d(0.0, 0.0, 0.0);
            try
            {
                PQSMod_VertexHeightPolyhedron mod = GetMod(body);
                if (mod == null)
                {
                    return false;
                }
                return mod.TryGetSurfaceGravity(pos, body, out result);
            }
            catch (Exception e)
            {
                // Never let a problem here break stock gravity.
                result = new Vector3d(0.0, 0.0, 0.0);
                if (!errorLogged)
                {
                    errorLogged = true;
                    Debug.LogError("[PolyhedronPQS] surface gravity failed, using stock gravity: " + e);
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Prefix for FlightGlobals.getGeeForceAtPosition(Vector3d pos, CelestialBody mainBody).
    /// Harmony matches the parameter names below to the original's, as VertexHeightOblateAdvanced does.
    /// </summary>
    internal static class GeeForcePatch
    {
        private static bool Prefix(ref Vector3d __result, Vector3d pos, CelestialBody mainBody)
        {
            // the stock method defaults a missing body to the one the game is currently in
            CelestialBody body = mainBody != null ? mainBody : FlightGlobals.currentMainBody;

            Vector3d g;
            if (PolyhedronGravity.TryCompute(pos, body, out g))
            {
                __result = g;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Postfix for FlightCamera.GetCameraFoR. In FREE camera mode, inside a surface-gravity
    /// zone, the camera's vertical axis follows the local gravity direction instead of the
    /// direction away from the center, so the ground does not look tilted near edges and corners.
    /// The frame is built the same way stock FREE mode (and VertexHeightOblateAdvanced) does.
    /// </summary>
    internal static class CameraFoRPatch
    {
        private static bool errorLogged;

        private static void Postfix(ref Quaternion __result)
        {
            try
            {
                FlightCamera cam = FlightCamera.fetch;
                if (cam == null || cam.mode != FlightCamera.Modes.FREE)
                {
                    return;
                }

                Vessel vessel = FlightGlobals.ActiveVessel;
                if (vessel == null || vessel.mainBody == null)
                {
                    return;
                }

                PQSMod_VertexHeightPolyhedron mod = PolyhedronGravity.GetMod(vessel.mainBody);
                if (mod == null || !mod.alignCamera)
                {
                    return;
                }

                Vector3d g;
                if (!mod.TryGetSurfaceGravity(vessel.GetWorldPos3D(), vessel.mainBody, out g))
                {
                    return;
                }

                double mag = g.magnitude;
                if (!(mag > 1e-12))
                {
                    return;
                }

                // gravity points down, so up is the opposite way
                Vector3 up = (Vector3)(g * (-1.0 / mag));
                __result = Quaternion.LookRotation(
                    Quaternion.AngleAxis(90f, Vector3.Cross(Vector3.up, up)) * -up, up);
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Debug.LogError("[PolyhedronPQS] camera alignment failed, using the stock camera: " + e);
                }
            }
        }
    }

    /// <summary>
    /// Postfix for PQS.UpdateVisual. KSP decides which terrain quads to draw from a horizon worked out
    /// for a ball of the body's radius. When the camera's target is below that radius (easy on these
    /// shapes: with Circumradius fit most of the surface is below it) that formula breaks down: it
    /// takes the cosine of a number above pi/2, the "visible radius" swings between huge, negative and
    /// NaN from frame to frame, and the terrain flickers or vanishes. For bodies using this mod, while
    /// the target is below the radius, every quad is kept in view instead.
    /// </summary>
    internal static class PQSVisibilityPatch
    {
        // PQS -> does it carry an active VertexHeightPolyhedron
        private static readonly Dictionary<PQS, PQSMod_VertexHeightPolyhedron> mods =
            new Dictionary<PQS, PQSMod_VertexHeightPolyhedron>();

        private static bool errorLogged;

        private static void Postfix(PQS __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                bool belowRadius = __instance.visibleAltitude < 0.0;
                bool broken = !(__instance.visibleRadius > 0.0);
                if (!belowRadius && !broken)
                {
                    return;
                }

                PQSMod_VertexHeightPolyhedron mod;
                if (!mods.TryGetValue(__instance, out mod) || (object)mod != null && mod == null)
                {
                    mod = __instance.GetComponentInChildren<PQSMod_VertexHeightPolyhedron>(true);
                    mods[__instance] = mod;
                }
                if (mod == null || !mod.HasShape)
                {
                    return;
                }

                double r = __instance.radius;
                double rMin = Math.Min(__instance.radiusMin, r);
                // what the stock formula gives at the radius itself, and everything within reach visible
                __instance.horizonDistance = Math.Sqrt(Math.Max(0.0, r * r - rMin * rMin)) + __instance.halfChord;
                __instance.horizonAngle = 1.0 / Math.Cos(1.0);
                __instance.visibleRadius = double.MaxValue;
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Debug.LogError("[PolyhedronPQS] terrain visibility fix failed: " + e);
                }
            }
        }
    }

    /// <summary>Installs the patches once, when the game reaches the main menu.</summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class PolyhedronGravityInstaller : MonoBehaviour
    {
        private void Awake()
        {
            Harmony harmony;
            try
            {
                harmony = new Harmony("PolyhedronPQS.SurfaceGravity");
            }
            catch (Exception e)
            {
                Debug.LogError("[PolyhedronPQS] Harmony is not available, surface gravity is off: " + e);
                return;
            }

            PatchGravity(harmony);
            PatchCamera(harmony);
            PatchVisibility(harmony);
        }

        private static void PatchVisibility(Harmony harmony)
        {
            try
            {
                MethodInfo original = AccessTools.Method(typeof(PQS), "UpdateVisual");
                if (original == null)
                {
                    Debug.LogWarning("[PolyhedronPQS] PQS.UpdateVisual not found, terrain may flicker below the body radius");
                    return;
                }
                harmony.Patch(original, postfix: new HarmonyMethod(typeof(PQSVisibilityPatch), "Postfix"));
                Debug.Log("[PolyhedronPQS] terrain visibility patch installed");
            }
            catch (Exception e)
            {
                Debug.LogError("[PolyhedronPQS] could not install the terrain visibility patch: " + e);
            }
        }

        private static void PatchGravity(Harmony harmony)
        {
            try
            {
                MethodInfo original = AccessTools.Method(typeof(FlightGlobals), "getGeeForceAtPosition",
                    new Type[] { typeof(Vector3d), typeof(CelestialBody) });
                if (original == null)
                {
                    Debug.LogError("[PolyhedronPQS] FlightGlobals.getGeeForceAtPosition(Vector3d, CelestialBody) not found, surface gravity is off");
                    return;
                }
                harmony.Patch(original, prefix: new HarmonyMethod(typeof(GeeForcePatch), "Prefix"));
                Debug.Log("[PolyhedronPQS] surface gravity patch installed");
            }
            catch (Exception e)
            {
                Debug.LogError("[PolyhedronPQS] could not install the surface gravity patch, surface gravity is off: " + e);
            }
        }

        private static void PatchCamera(Harmony harmony)
        {
            try
            {
                int patched = 0;
                BindingFlags all = BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (MethodInfo method in typeof(FlightCamera).GetMethods(all))
                {
                    if (method.Name == "GetCameraFoR" && method.ReturnType == typeof(Quaternion))
                    {
                        harmony.Patch(method, postfix: new HarmonyMethod(typeof(CameraFoRPatch), "Postfix"));
                        patched++;
                    }
                }

                if (patched == 0)
                {
                    Debug.LogWarning("[PolyhedronPQS] FlightCamera.GetCameraFoR not found, the camera will not follow surface gravity");
                }
                else
                {
                    Debug.Log("[PolyhedronPQS] camera alignment patch installed (" + patched + ")");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PolyhedronPQS] could not install the camera patch, the camera will not follow surface gravity: " + e);
            }
        }
    }

    /// <summary>
    /// While the active vessel is inside a surface-gravity zone, turns off Orbit Drift
    /// Compensation (it assumes ordinary Keplerian gravity and would fight this one),
    /// and puts the player's setting back when the vessel leaves or the scene ends.
    /// VertexHeightOblateAdvanced does the same for its non-central gravity.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class PolyhedronGravityFlight : MonoBehaviour
    {
        private bool overridden;
        private bool savedSetting;
        private bool errorLogged;

        private void FixedUpdate()
        {
            try
            {
                bool inZone = false;
                Vessel vessel = FlightGlobals.ActiveVessel;
                if (vessel != null && vessel.mainBody != null)
                {
                    PQSMod_VertexHeightPolyhedron mod = PolyhedronGravity.GetMod(vessel.mainBody);
                    inZone = mod != null && mod.IsInGravityZone(vessel.GetWorldPos3D(), vessel.mainBody);
                }

                if (inZone && !overridden)
                {
                    savedSetting = GameSettings.ORBIT_DRIFT_COMPENSATION;
                    GameSettings.ORBIT_DRIFT_COMPENSATION = false;
                    overridden = true;
                }
                else if (!inZone && overridden)
                {
                    Restore();
                }
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Debug.LogError("[PolyhedronPQS] " + e);
                }
            }
        }

        private void OnDestroy()
        {
            Restore();
        }

        private void Restore()
        {
            if (overridden)
            {
                GameSettings.ORBIT_DRIFT_COMPENSATION = savedSetting;
                overridden = false;
            }
        }
    }
}
