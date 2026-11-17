/*
 * GeometricPresets
 * ----------------------------------------------------------------------------
 * Ready-made non-polyhedron shapes for VertexHeightPolyhedron, chosen with
 * "GeometricPreset = ..." instead of "faces = ...".
 *
 *   Cylinder    flat caps, round or N-sided
 *   Hemisphere  a dome on a flat base
 *   Macaroni    a bent tube (an elbow), from straight up to a half circle
 *   Ellipsoid   three independent radii (cigars, eggs, flattened rocks), with optional craggy ends
 *
 * The one rule behind all of them: KSP builds a planet's surface by moving every vertex
 * along a straight line out from the center of the body, so each direction from the center
 * can have only ONE surface point. Every shape here is therefore "star-shaped" about the
 * center. That is why a macaroni can't be hollow, and why it can only bend so far: bend a
 * thin tube too much and parts of it hide behind other parts as seen from the center. The
 * bend is limited automatically so that never happens (see MacaroniShape).
 *
 * Each shape is described by a "gauge": a value that is 1 on the surface, and grows in
 * proportion to distance from the center. The surface is then at radius 1 / gauge along any
 * direction. Several gauges are combined with a generalized maximum (an intersection) or
 * minimum (a union). The exponent p of that combination is what "smoothing" controls: p = 0
 * means a plain max/min (sharp edges), a finite p rounds the edges. This is the same idea
 * VertexHeightPolyhedron already uses for its flat faces.
 *
 * This file has no KSP or Unity dependencies.
 */

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PolyhedronPQS
{
    /// <summary>Which ready-made shape to use. None = use "faces" (a regular polyhedron).</summary>
    public enum GeometricPreset
    {
        None,
        Cylinder,
        Hemisphere,
        Macaroni,
        Ellipsoid
    }

    /// <summary>What the PQS mod needs to know about any shape it can build.</summary>
    public interface IShape
    {
        /// <summary>Distance from the center to the surface along the unit direction (x, y, z).</summary>
        double UnitRadius(double x, double y, double z, double p);

        /// <summary>Outward unit surface normal along the unit direction (x, y, z).</summary>
        void Normal(double x, double y, double z, double p, out double ox, out double oy, out double oz);

        /// <summary>Largest distance from the center to the surface.</summary>
        double MaxRadius(double p);

        /// <summary>Smallest distance from the center to the surface.</summary>
        double MinRadius(double p);

        /// <summary>A distance the surface never exceeds, for quick "is this point near the body" checks.</summary>
        double Bound(double p);
    }

    // ------------------------------------------------------------------ settings (one set per sub-node)

    /// <summary>A settings class that can set its values by name from text (see PresetText).</summary>
    public interface IPresetSettings
    {
        /// <summary>
        /// Sets one value by name (letter case does not matter). Returns false, with a message, when the
        /// name is not one of this shape's settings or the text is not a usable number.
        /// </summary>
        bool Set(string name, string text, out string problem);
    }

    /// <summary>Cylinder { ... }</summary>
    // Serializable: KSP copies each planet (Unity Instantiate) when a scene loads, and Unity only
    // copies fields it can serialize. Without this the copy would fall back to the default settings.
    [Serializable]
    public sealed class CylinderSettings : IPresetSettings
    {
        /// <summary>Half the distance between the two caps, as a multiple of the cylinder's radius. 1 = as tall as it is wide.</summary>
        public double verticalRadius = 1.0;

        /// <summary>Number of sides around the cylinder. 0 = perfectly round, 3 to 64 = a prism with that many sides.</summary>
        public int steps = 0;

        public bool Set(string name, string text, out string problem)
        {
            switch (PresetText.Key(name))
            {
                case "verticalradius": return PresetText.ToDouble(name, text, ref verticalRadius, out problem);
                case "steps": return PresetText.ToInt(name, text, ref steps, out problem);
            }
            return PresetText.Unknown(name, "verticalRadius, steps", out problem);
        }
    }

    /// <summary>Hemisphere { ... }</summary>
    // Serializable: KSP copies each planet (Unity Instantiate) when a scene loads, and Unity only
    // copies fields it can serialize. Without this the copy would fall back to the default settings.
    [Serializable]
    public sealed class HemisphereSettings : IPresetSettings
    {
        /// <summary>Height of the dome above its center, as a multiple of its width (radius). 1 = a true hemisphere shape.</summary>
        public double verticalRadius = 1.0;

        /// <summary>How far below the body's center the flat base sits, as a multiple of the radius. Small = nearly a perfect hemisphere.</summary>
        public double baseDepth = 0.1;

        /// <summary>Facets around the dome. 0 = smooth, 3 to 64 = faceted with that many sides.</summary>
        public int steps = 0;

        public bool Set(string name, string text, out string problem)
        {
            switch (PresetText.Key(name))
            {
                case "verticalradius": return PresetText.ToDouble(name, text, ref verticalRadius, out problem);
                case "basedepth": return PresetText.ToDouble(name, text, ref baseDepth, out problem);
                case "steps": return PresetText.ToInt(name, text, ref steps, out problem);
            }
            return PresetText.Unknown(name, "verticalRadius, baseDepth, steps", out problem);
        }
    }

    /// <summary>Macaroni { ... }</summary>
    // Serializable: KSP copies each planet (Unity Instantiate) when a scene loads, and Unity only
    // copies fields it can serialize. Without this the copy would fall back to the default settings.
    [Serializable]
    public sealed class MacaroniSettings : IPresetSettings
    {
        /// <summary>How far the tube bends, in degrees. 0 = straight, 90 = a quarter circle (elbow), up to 180.</summary>
        public double angle = 90.0;

        /// <summary>Length of the tube along its middle, as a multiple of the tube's radius.</summary>
        public double length = 6.0;

        /// <summary>Height of the tube compared with its width. 1 = round, below 1 = flattened.</summary>
        public double verticalRadius = 1.0;

        /// <summary>Sides around the tube. 0 = round, 3 to 64 = that many flat sides.</summary>
        public int steps = 0;

        /// <summary>false = ends cut flat like real macaroni, true = rounded ends.</summary>
        public bool roundEnds = false;

        public bool Set(string name, string text, out string problem)
        {
            switch (PresetText.Key(name))
            {
                case "angle": return PresetText.ToDouble(name, text, ref angle, out problem);
                case "length": return PresetText.ToDouble(name, text, ref length, out problem);
                case "verticalradius": return PresetText.ToDouble(name, text, ref verticalRadius, out problem);
                case "steps": return PresetText.ToInt(name, text, ref steps, out problem);
                case "roundends": return PresetText.ToBool(name, text, ref roundEnds, out problem);
            }
            return PresetText.Unknown(name, "angle, length, verticalRadius, steps, roundEnds", out problem);
        }
    }

    /// <summary>Ellipsoid { ... }</summary>
    // Serializable: KSP copies each planet (Unity Instantiate) when a scene loads, and Unity only
    // copies fields it can serialize. Without this the copy would fall back to the default settings.
    [Serializable]
    public sealed class EllipsoidSettings : IPresetSettings
    {
        /// <summary>Radius along X (an equator direction). Only the ratios matter: "fit" scales the result to the body radius.</summary>
        public double radiusX = 1.0;

        /// <summary>Radius along Y (the polar / spin axis).</summary>
        public double radiusY = 1.0;

        /// <summary>Radius along Z (the other equator direction).</summary>
        public double radiusZ = 1.0;

        /// <summary>
        /// Craggy relief at the two ends of the LONGEST axis, as a fraction of the local radius (0 = smooth,
        /// 0.05 = +/-5 %). It is faded out where the surface faces sideways, so long flanks never terrace.
        /// </summary>
        public double endNoise = 0.0;

        /// <summary>Size of the end relief: higher = smaller, more numerous lumps. Default 6.</summary>
        public double endNoiseFrequency = 6.0;

        /// <summary>Where along the long axis the end relief starts, 0 (middle) to 1 (tip). Default 0.35.</summary>
        public double endNoiseStart = 0.35;

        /// <summary>Changes the random pattern of the end relief.</summary>
        public int seed = 0;

        public bool Set(string name, string text, out string problem)
        {
            switch (PresetText.Key(name))
            {
                case "radiusx": return PresetText.ToDouble(name, text, ref radiusX, out problem);
                case "radiusy": return PresetText.ToDouble(name, text, ref radiusY, out problem);
                case "radiusz": return PresetText.ToDouble(name, text, ref radiusZ, out problem);
                case "endnoise": return PresetText.ToDouble(name, text, ref endNoise, out problem);
                case "endnoisefrequency": return PresetText.ToDouble(name, text, ref endNoiseFrequency, out problem);
                case "endnoisestart": return PresetText.ToDouble(name, text, ref endNoiseStart, out problem);
                case "seed": return PresetText.ToInt(name, text, ref seed, out problem);
            }
            return PresetText.Unknown(name, "radiusX, radiusY, radiusZ, endNoise, endNoiseFrequency, endNoiseStart, seed", out problem);
        }
    }

    /// <summary>
    /// Reads setting values from the text of a config line. A config file is meant to have one
    /// "name = value" per line, but the text is handled more kindly than that: several pairs on one
    /// line ("ringRadius = 0.7  tubeRadius = 0.3") are all picked up, a trailing "}" or "// comment"
    /// is ignored, and a value that is not a number is reported instead of making the whole body fail to load.
    /// </summary>
    public static class PresetText
    {
        /// <summary>
        /// 'raw' is everything the config parser handed over for 'name'. The first thing in it is the value
        /// of 'name'; anything after that of the form "other = value" sets 'other'. Returns what went wrong,
        /// one message per problem (empty when all is well).
        /// </summary>
        public static List<string> Apply(IPresetSettings settings, string name, string raw)
        {
            List<string> problems = new List<string>();
            if (raw == null)
            {
                return problems;
            }

            // drop a comment and any braces that ended up on the line
            int comment = raw.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
            {
                raw = raw.Substring(0, comment);
            }
            raw = raw.Replace("{", " ").Replace("}", " ").Trim();

            // the value of 'name' is the text up to the first space or the first "other ="
            int cut = raw.Length;
            for (int i = 0; i < raw.Length; i++)
            {
                if (char.IsWhiteSpace(raw[i]) || raw[i] == '=')
                {
                    cut = i;
                    break;
                }
            }
            string first = raw.Substring(0, cut);
            string rest = raw.Substring(cut).Trim();

            string problem;
            if (!settings.Set(name, first, out problem))
            {
                problems.Add(problem);
            }

            // the rest: name = value pairs, separated by spaces (or commas / semicolons)
            if (rest.Length > 0)
            {
                string[] tokens = rest.Replace("=", " = ").Replace(",", " ").Replace(";", " ")
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                int t = 0;
                while (t < tokens.Length)
                {
                    if (t + 2 < tokens.Length && tokens[t + 1] == "=")
                    {
                        if (!settings.Set(tokens[t], tokens[t + 2], out problem))
                        {
                            problems.Add(problem);
                        }
                        t += 3;
                    }
                    else
                    {
                        problems.Add("could not understand \"" + tokens[t] + "\" after the value of " + name
                                     + " (put each setting on its own line: name = value)");
                        t++;
                    }
                }
            }
            return problems;
        }

        /// <summary>Lower-case name with no spaces or underscores, so "Hole_Depth" and "holedepth" match.</summary>
        public static string Key(string name)
        {
            return name == null ? "" : name.Trim().Replace("_", "").ToLowerInvariant();
        }

        public static bool ToDouble(string name, string text, ref double target, out string problem)
        {
            double v;
            if (!TryNumber(text, out v))
            {
                problem = name + " = \"" + text + "\" is not a number";
                return false;
            }
            target = v;
            problem = null;
            return true;
        }

        public static bool ToInt(string name, string text, ref int target, out string problem)
        {
            double v;
            if (!TryNumber(text, out v) || Math.Abs(v - Math.Round(v)) > 1e-9 || Math.Abs(v) > 1e6)
            {
                problem = name + " = \"" + text + "\" is not a whole number";
                return false;
            }
            target = (int)Math.Round(v);
            problem = null;
            return true;
        }

        public static bool ToBool(string name, string text, ref bool target, out string problem)
        {
            string k = Key(text);
            if (k == "true" || k == "yes" || k == "1" || k == "on")
            {
                target = true;
                problem = null;
                return true;
            }
            if (k == "false" || k == "no" || k == "0" || k == "off")
            {
                target = false;
                problem = null;
                return true;
            }
            problem = name + " = \"" + text + "\" is not true or false";
            return false;
        }

        public static bool Unknown(string name, string known, out string problem)
        {
            problem = "\"" + name + "\" is not a setting of this shape (its settings are " + known + ")";
            return false;
        }

        // Plain numbers like 0.7, .7, -2, 1e-3, and also 0,7 and 0.7f
        private static bool TryNumber(string text, out double value)
        {
            value = 0.0;
            if (text == null)
            {
                return false;
            }
            text = text.Trim();
            if (text.Length > 1 && (text[text.Length - 1] == 'f' || text[text.Length - 1] == 'F'))
            {
                text = text.Substring(0, text.Length - 1);
            }
            if (text.IndexOf('.') < 0 && text.IndexOf(',') >= 0)
            {
                text = text.Replace(',', '.');
            }
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>Reads a preset name in any letter case. Returns false when it is not one of the presets.</summary>
        public static bool TryPreset(string text, out GeometricPreset preset)
        {
            preset = GeometricPreset.None;
            if (text == null)
            {
                return false;
            }
            string key = Key(text);
            if (key.Length == 0 || key == "none" || key == "off")
            {
                return true;
            }
            foreach (GeometricPreset candidate in Enum.GetValues(typeof(GeometricPreset)))
            {
                if (candidate != GeometricPreset.None && Key(candidate.ToString()) == key)
                {
                    preset = candidate;
                    return true;
                }
            }
            return false;
        }
    }

    // ------------------------------------------------------------------ shared machinery

    /// <summary>
    /// Base for the preset shapes. A shape only has to say what its radius is along a direction;
    /// the surface normal and the extreme radii are worked out from that.
    /// </summary>
    public abstract class PresetShape : IShape
    {
        public const int MaxSteps = 64;

        /// <summary>Number of sides around the vertical axis, 0 = round.</summary>
        protected readonly int steps;

        // cos / sin of each side's outward direction (angles 2 pi k / steps)
        private readonly double[] sideCos;
        private readonly double[] sideSin;

        /// <summary>1 / cos(pi / steps): how much farther a corner is than the middle of a side. 1 for round.</summary>
        protected readonly double cornerFactor;

        protected PresetShape(int steps)
        {
            if (steps >= 3 && steps <= MaxSteps)
            {
                this.steps = steps;
                sideCos = new double[steps];
                sideSin = new double[steps];
                for (int k = 0; k < steps; k++)
                {
                    double a = 2.0 * Math.PI * k / steps;
                    sideCos[k] = Math.Cos(a);
                    sideSin[k] = Math.Sin(a);
                }
                cornerFactor = 1.0 / Math.Cos(Math.PI / steps);
            }
            else
            {
                this.steps = 0;
                cornerFactor = 1.0;
            }
        }

        public abstract double UnitRadius(double x, double y, double z, double p);
        public abstract double Bound(double p);

        /// <summary>
        /// Gauge of the shape's cross-section in the plane (a, b): 1 on the outline of a circle of
        /// radius 1, or of a polygon whose sides are all 1 away from the middle.
        /// With p > 0 the corners of the polygon are rounded.
        /// </summary>
        protected double Rho(double a, double b, double p)
        {
            if (steps == 0)
            {
                return Math.Sqrt(a * a + b * b);
            }

            double m = double.NegativeInfinity;
            for (int k = 0; k < steps; k++)
            {
                double d = a * sideCos[k] + b * sideSin[k];
                if (d > m)
                {
                    m = d;
                }
            }
            if (!(m > 1e-12))
            {
                return 0.0;
            }
            if (p <= 0.0)
            {
                return m;
            }

            double sum = 0.0;
            for (int k = 0; k < steps; k++)
            {
                double d = a * sideCos[k] + b * sideSin[k];
                if (d > 0.0)
                {
                    sum += Math.Pow(d / m, p);
                }
            }
            return m * Math.Pow(sum, 1.0 / p);
        }

        /// <summary>Generalized maximum of two non-negative gauges (an intersection). p = 0 is a plain max.</summary>
        protected static double Lp(double p, double a, double b)
        {
            double m = Math.Max(a, b);
            if (!(m > 1e-12))
            {
                return 0.0;
            }
            if (p <= 0.0)
            {
                return m;
            }
            double s = 0.0;
            if (a > 0.0)
            {
                s += Math.Pow(a / m, p);
            }
            if (b > 0.0)
            {
                s += Math.Pow(b / m, p);
            }
            return m * Math.Pow(s, 1.0 / p);
        }

        // ---------------------------------------------------------------- normals

        /// <summary>
        /// Surface normal from the radius function alone: for a surface P = r(u) u, the normal is
        /// proportional to r u - (the slope of r along the sphere). The slope is measured by stepping
        /// a tiny angle either way along two perpendicular directions.
        /// </summary>
        public virtual void Normal(double x, double y, double z, double p,
            out double ox, out double oy, out double oz)
        {
            NumericNormal(this, x, y, z, p, out ox, out oy, out oz);
        }

        public static void NumericNormal(IShape shape, double x, double y, double z, double p,
            out double ox, out double oy, out double oz)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            if (!(len > 1e-12))
            {
                ox = 0.0;
                oy = 1.0;
                oz = 0.0;
                return;
            }
            x /= len;
            y /= len;
            z /= len;

            // two directions perpendicular to (x, y, z)
            double hx = Math.Abs(x) < 0.9 ? 1.0 : 0.0;
            double hy = Math.Abs(x) < 0.9 ? 0.0 : 1.0;
            double e1x = y * 0.0 - z * hy;
            double e1y = z * hx - x * 0.0;
            double e1z = x * hy - y * hx;
            double e1len = Math.Sqrt(e1x * e1x + e1y * e1y + e1z * e1z);
            e1x /= e1len;
            e1y /= e1len;
            e1z /= e1len;
            double e2x = y * e1z - z * e1y;
            double e2y = z * e1x - x * e1z;
            double e2z = x * e1y - y * e1x;

            const double step = 1e-5;
            double c = Math.Cos(step);
            double s = Math.Sin(step);

            double r0 = shape.UnitRadius(x, y, z, p);
            double d1 = (shape.UnitRadius(x * c + e1x * s, y * c + e1y * s, z * c + e1z * s, p)
                       - shape.UnitRadius(x * c - e1x * s, y * c - e1y * s, z * c - e1z * s, p)) / (2.0 * step);
            double d2 = (shape.UnitRadius(x * c + e2x * s, y * c + e2y * s, z * c + e2z * s, p)
                       - shape.UnitRadius(x * c - e2x * s, y * c - e2y * s, z * c - e2z * s, p)) / (2.0 * step);

            double nx = r0 * x - d1 * e1x - d2 * e2x;
            double ny = r0 * y - d1 * e1y - d2 * e2y;
            double nz = r0 * z - d1 * e1z - d2 * e2z;
            double nlen = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (!(nlen > 1e-12))
            {
                ox = x;
                oy = y;
                oz = z;
                return;
            }
            ox = nx / nlen;
            oy = ny / nlen;
            oz = nz / nlen;
        }

        // ---------------------------------------------------------------- extremes

        public double MaxRadius(double p)
        {
            return Extreme(p, true);
        }

        public double MinRadius(double p)
        {
            return Extreme(p, false);
        }

        // Directions worth starting a search from: the axes, the diagonals, and (for faceted
        // shapes) the middle and the corner of every side.
        /// <summary>Extra directions a shape knows are worth checking for its highest and lowest points.</summary>
        protected virtual void AddSeeds(List<double[]> list)
        {
        }

        private List<double[]> Seeds()
        {
            List<double[]> list = new List<double[]>();
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        if (i == 0 && j == 0 && k == 0)
                        {
                            continue;
                        }
                        double l = Math.Sqrt(i * i + j * j + k * k);
                        list.Add(new[] { i / l, j / l, k / l });
                    }
                }
            }
            for (int k = 0; k < steps; k++)
            {
                double a = 2.0 * Math.PI * k / steps;
                double b = a + Math.PI / steps;
                list.Add(new[] { Math.Cos(a), 0.0, Math.Sin(a) });
                list.Add(new[] { Math.Cos(b), 0.0, Math.Sin(b) });
            }
            AddSeeds(list);
            return list;
        }

        // Searches for the largest (or smallest) radius: from the seeds, and from the best spots
        // of a coarse sampling of the whole sphere. Runs once, when the mod is set up.
        private double Extreme(double p, bool wantMax)
        {
            // work with sign * radius so that "bigger is better" in both cases
            double sign = wantMax ? 1.0 : -1.0;
            double best = double.NegativeInfinity;

            foreach (double[] s in Seeds())
            {
                best = Math.Max(best, Refine(s[0], s[1], s[2], p, sign));
            }

            const int samples = 2000;
            const int keep = 8;
            double[] topV = new double[keep];
            double[] topX = new double[keep];
            double[] topY = new double[keep];
            double[] topZ = new double[keep];
            for (int i = 0; i < keep; i++)
            {
                topV[i] = double.NegativeInfinity;
            }

            double golden = Math.PI * (3.0 - Math.Sqrt(5.0));
            for (int i = 0; i < samples; i++)
            {
                double y = 1.0 - 2.0 * (i + 0.5) / samples;
                double rr = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
                double t = i * golden;
                double x = rr * Math.Cos(t);
                double z = rr * Math.Sin(t);
                double v = sign * UnitRadius(x, y, z, p);

                int worst = 0;
                for (int k = 1; k < keep; k++)
                {
                    if (topV[k] < topV[worst])
                    {
                        worst = k;
                    }
                }
                if (v > topV[worst])
                {
                    topV[worst] = v;
                    topX[worst] = x;
                    topY[worst] = y;
                    topZ[worst] = z;
                }
            }

            for (int k = 0; k < keep; k++)
            {
                if (topV[k] > double.NegativeInfinity)
                {
                    best = Math.Max(best, Refine(topX[k], topY[k], topZ[k], p, sign));
                }
            }

            return sign * best;
        }

        // Zooming grid search around a direction: tries a 5 x 5 patch, moves to the best point,
        // halves the spacing, repeats. Returns the best sign * radius found.
        private double Refine(double x, double y, double z, double p, double sign)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            x /= len;
            y /= len;
            z /= len;
            double best = sign * UnitRadius(x, y, z, p);

            for (double h = 0.05; h > 1e-9; h *= 0.5)
            {
                double hx = Math.Abs(x) < 0.9 ? 1.0 : 0.0;
                double hy = Math.Abs(x) < 0.9 ? 0.0 : 1.0;
                double e1x = y * 0.0 - z * hy;
                double e1y = z * hx - x * 0.0;
                double e1z = x * hy - y * hx;
                double e1len = Math.Sqrt(e1x * e1x + e1y * e1y + e1z * e1z);
                e1x /= e1len;
                e1y /= e1len;
                e1z /= e1len;
                double e2x = y * e1z - z * e1y;
                double e2y = z * e1x - x * e1z;
                double e2z = x * e1y - y * e1x;

                double bx = x;
                double by = y;
                double bz = z;
                for (int a = -2; a <= 2; a++)
                {
                    for (int b = -2; b <= 2; b++)
                    {
                        if (a == 0 && b == 0)
                        {
                            continue;
                        }
                        double px = x + h * (a * e1x + b * e2x);
                        double py = y + h * (a * e1y + b * e2y);
                        double pz = z + h * (a * e1z + b * e2z);
                        double pl = Math.Sqrt(px * px + py * py + pz * pz);
                        px /= pl;
                        py /= pl;
                        pz /= pl;
                        double v = sign * UnitRadius(px, py, pz, p);
                        if (v > best)
                        {
                            best = v;
                            bx = px;
                            by = py;
                            bz = pz;
                        }
                    }
                }
                x = bx;
                y = by;
                z = bz;
            }
            return best;
        }
    }

    // ------------------------------------------------------------------ the shapes

    /// <summary>Flat caps joined by a round or N-sided wall. The axis is Y.</summary>
    public sealed class CylinderShape : PresetShape
    {
        private readonly double h;

        public CylinderShape(double verticalRadius, int steps) : base(steps)
        {
            h = verticalRadius;
        }

        public override double UnitRadius(double x, double y, double z, double p)
        {
            double f = Lp(p, Rho(x, z, p), Math.Abs(y) / h);
            return f > 1e-12 ? 1.0 / f : Bound(p);
        }

        // the rim: a corner of the wall meeting a cap
        public override double Bound(double p)
        {
            return Math.Sqrt(cornerFactor * cornerFactor + h * h);
        }
    }

    /// <summary>A dome (an ellipsoid, if it is not 1 high) cut off by a flat base below the center. The axis is Y.</summary>
    public sealed class HemisphereShape : PresetShape
    {
        private readonly double v;
        private readonly double b;

        public HemisphereShape(double verticalRadius, double baseDepth, int steps) : base(steps)
        {
            v = verticalRadius;
            b = baseDepth;
        }

        public override double UnitRadius(double x, double y, double z, double p)
        {
            double rho = Rho(x, z, p);
            double yv = y / v;
            double dome = Math.Sqrt(rho * rho + yv * yv);
            double bas = y < 0.0 ? -y / b : 0.0;
            double f = Lp(p, dome, bas);
            return f > 1e-12 ? 1.0 / f : Bound(p);
        }

        public override double Bound(double p)
        {
            return Math.Max(cornerFactor, v);
        }
    }

    /// <summary>
    /// A bent tube ("elbow macaroni"). The tube's radius is 1; its middle line is an arc of the given
    /// length bent through the given angle, lying flat in the XZ plane (the body's equator), with the
    /// body's center at the arc's center of mass so the whole tube is seen from the center.
    ///
    /// The surface is described by a signed distance (negative inside the tube, the distance to the
    /// surface outside). The radius along a direction is found by starting beyond the shape and
    /// stepping inward by that distance (it can never step through the surface), then pinning the
    /// crossing down exactly. Taking the outermost crossing keeps the body one solid piece.
    /// </summary>
    public sealed class MacaroniShape : PresetShape
    {
        private readonly double theta;      // bend, radians
        private readonly double squash;     // tube height compared with its width
        private readonly bool roundEnds;
        private readonly bool straight;

        private readonly double ring;       // radius of the bend (middle line)
        private readonly double shift;      // the bend's center sits at x = -shift
        private readonly double sinHalf;
        private readonly double cosHalf;
        private readonly double half;       // half the length, for a straight tube

        private readonly double outer;      // a distance beyond every point of the tube
        private readonly double lipschitz;  // safe step factor for the distance
        private readonly double minStep;

        public MacaroniShape(double length, double angleDegrees, double verticalRadius, int steps, bool roundEnds)
            : base(steps)
        {
            theta = angleDegrees * Math.PI / 180.0;
            squash = verticalRadius;
            this.roundEnds = roundEnds;
            straight = theta < 1e-3;
            half = 0.5 * length;

            if (!straight)
            {
                double h = 0.5 * theta;
                ring = length / theta;
                shift = ring * Math.Sin(h) / h;
                sinHalf = Math.Sin(h);
                cosHalf = Math.Cos(h);
            }

            // farthest point of the middle line, plus the tube around it
            double far = 0.0;
            for (int i = 0; i <= 200; i++)
            {
                double cx, cz;
                Center(-1.0 + 2.0 * i / 200, out cx, out cz);
                far = Math.Max(far, Math.Sqrt(cx * cx + cz * cz));
            }
            outer = far + cornerFactor * Math.Max(1.0, squash) * 1.6 + (roundEnds ? 1.0 : 0.0) + 0.1;

            lipschitz = 0.8 * Math.Min(1.0, squash);
            minStep = 1e-4 * Math.Min(1.0, squash);
        }

        /// <summary>Point on the middle line, s from -1 (one end) to 1 (the other).</summary>
        private void Center(double s, out double cx, out double cz)
        {
            if (straight)
            {
                cx = 0.0;
                cz = s * half;
                return;
            }
            double phi = s * 0.5 * theta;
            cx = ring * Math.Cos(phi) - shift;
            cz = ring * Math.Sin(phi);
        }

        // Signed distance (in tube radii; exact up to the vertical squash and smoothing).
        private double Distance(double x, double y, double z, double p)
        {
            double yv = y / squash;
            double radial;
            double past;   // how far past the nearer end plane, negative between the ends
            if (straight)
            {
                radial = x;
                past = Math.Abs(z) - half;
            }
            else
            {
                double qx = x + shift;
                double qz = z;
                radial = Math.Sqrt(qx * qx + qz * qz) - ring;
                past = -qx * sinHalf + Math.Abs(qz) * cosHalf;
            }

            double across = Rho(radial, yv, p);
            if (roundEnds)
            {
                if (past <= 0.0)
                {
                    return across - 1.0;
                }
                return Math.Sqrt(across * across + past * past) - 1.0;
            }

            double a = across - 1.0;
            double b = past;
            double k = p > 0.0 ? Math.Min(0.5, 1.0 / p) : 0.0;
            if (k <= 0.0)
            {
                return Math.Max(a, b);
            }
            double hk = Math.Max(k - Math.Abs(a - b), 0.0) / k;
            return Math.Max(a, b) + hk * hk * k * 0.25;
        }

        public override double UnitRadius(double x, double y, double z, double p)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            if (!(len > 1e-12))
            {
                return outer;
            }
            double ux = x / len;
            double uy = y / len;
            double uz = z / len;

            // step inward from outside the shape
            double t = outer;
            double tOut = outer;
            double d = Distance(ux * t, uy * t, uz * t, p);
            int guard = 0;
            while (d > 1e-12 && guard < 2000)
            {
                tOut = t;
                t -= Math.Max(d * lipschitz, minStep);
                if (t <= 0.0)
                {
                    return tOut / len;   // cannot happen while the center is inside the tube
                }
                d = Distance(ux * t, uy * t, uz * t, p);
                guard++;
            }
            if (d > 0.0 || tOut <= t)
            {
                return t / len;
            }

            // the crossing is between t (inside) and tOut (outside): pin it down
            double a = t;
            double fa = d;
            double b = tOut;
            double fb = Distance(ux * b, uy * b, uz * b, p);
            int side = 0;
            for (int i = 0; i < 80 && b - a > 1e-13 * outer; i++)
            {
                double m = (a * fb - b * fa) / (fb - fa);
                if (!(m > a && m < b))
                {
                    m = 0.5 * (a + b);
                }
                double fm = Distance(ux * m, uy * m, uz * m, p);
                if (fm > 0.0)
                {
                    b = m;
                    fb = fm;
                    if (side == 1)
                    {
                        fa *= 0.5;
                    }
                    side = 1;
                }
                else
                {
                    a = m;
                    fa = fm;
                    if (side == -1)
                    {
                        fb *= 0.5;
                    }
                    side = -1;
                }
            }
            return 0.5 * (a + b) / len;
        }

        public override double Bound(double p)
        {
            return outer;
        }

        // the rims of the two ends and the outside of the bend: where the highest points are
        protected override void AddSeeds(List<double[]> list)
        {
            for (int e = -1; e <= 1; e++)
            {
                double cx, cz;
                Center(e, out cx, out cz);
                // outward direction across the tube (away from the bend's center) at this point
                double ox, oz;
                if (straight)
                {
                    ox = 1.0;
                    oz = 0.0;
                }
                else
                {
                    double qx = cx + shift;
                    double ql = Math.Sqrt(qx * qx + cz * cz);
                    ox = qx / ql;
                    oz = cz / ql;
                }
                for (int k = 0; k < 24; k++)
                {
                    double a = 2.0 * Math.PI * k / 24;
                    double px = cx + ox * Math.Cos(a) * cornerFactor;
                    double py = squash * Math.Sin(a) * cornerFactor;
                    double pz = cz + oz * Math.Cos(a) * cornerFactor;
                    double l = Math.Sqrt(px * px + py * py + pz * pz);
                    if (l > 1e-9)
                    {
                        list.Add(new[] { px / l, py / l, pz / l });
                    }
                }
            }
        }

        /// <summary>
        /// The largest bend (degrees, up to 180) that keeps a tube of this length and height fully
        /// visible from the body's center: the center stays inside the tube, and the straight line from
        /// the center to any point of the middle line stays well inside the tube.
        /// </summary>
        public static double MaxAngle(double length, double verticalRadius)
        {
            if (Visible(length, Math.PI, verticalRadius))
            {
                return 180.0;
            }
            double lo = 0.0;
            double hi = Math.PI;
            for (int i = 0; i < 40; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (Visible(length, mid, verticalRadius))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }
            return lo * 180.0 / Math.PI;
        }

        private static bool Visible(double length, double theta, double verticalRadius)
        {
            if (theta < 1e-3)
            {
                return true;
            }
            double margin = Math.Min(1.0, verticalRadius);
            double h = 0.5 * theta;
            double ring = length / theta;
            double shift = ring * Math.Sin(h) / h;
            if (ring - shift > 0.5 * margin)
            {
                return false;   // the center would be outside the tube
            }
            for (int i = 1; i <= 24; i++)
            {
                double phi = h * i / 24;
                double cx = ring * Math.Cos(phi) - shift;
                double cz = ring * Math.Sin(phi);
                for (int j = 1; j < 16; j++)
                {
                    double f = j / 16.0;
                    double qx = cx * f + shift;
                    double qz = cz * f;
                    double ang = Math.Atan2(qz, qx);
                    double dist;
                    if (Math.Abs(ang) <= h)
                    {
                        dist = Math.Abs(Math.Sqrt(qx * qx + qz * qz) - ring);
                    }
                    else
                    {
                        double ex = ring * Math.Cos(h) - qx;
                        double ez = ring * Math.Sin(h) * Math.Sign(qz) - qz;
                        dist = Math.Sqrt(ex * ex + ez * ez);
                    }
                    if (dist > 0.7 * margin)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    // ------------------------------------------------------------------ uniformity

    /// <summary>
    /// Applies only part of a shape: the surface is moved that fraction of the way from the plain
    /// sphere (radius 'baseRadius', in the shape's units) to the shape. 1 = the full shape.
    /// </summary>
    public sealed class BlendedShape : IShape
    {
        private readonly IShape inner;
        private readonly double amount;
        private readonly double baseRadius;
        private readonly double innerMax;
        private readonly double innerMin;
        private readonly double innerBound;

        public BlendedShape(IShape inner, double amount, double baseRadius,
            double innerMax, double innerMin, double innerBound)
        {
            this.inner = inner;
            this.amount = amount;
            this.baseRadius = baseRadius;
            this.innerMax = innerMax;
            this.innerMin = innerMin;
            this.innerBound = innerBound;
        }

        public double UnitRadius(double x, double y, double z, double p)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            if (!(len > 1e-12))
            {
                return baseRadius;
            }
            double r = inner.UnitRadius(x / len, y / len, z / len, p);
            return (baseRadius + amount * (r - baseRadius)) / len;
        }

        public void Normal(double x, double y, double z, double p, out double ox, out double oy, out double oz)
        {
            PresetShape.NumericNormal(this, x, y, z, p, out ox, out oy, out oz);
        }

        public double MaxRadius(double p)
        {
            return baseRadius + amount * (innerMax - baseRadius);
        }

        public double MinRadius(double p)
        {
            return baseRadius + amount * (innerMin - baseRadius);
        }

        public double Bound(double p)
        {
            return Math.Max(baseRadius, baseRadius + amount * (innerBound - baseRadius));
        }
    }

    // ------------------------------------------------------------------ orientation

    /// <summary>Turns any shape by Euler angles in degrees (Unity order: Z, then X, then Y).</summary>
    public sealed class RotatedShape : IShape
    {
        private readonly IShape inner;
        private readonly double rx;
        private readonly double ry;
        private readonly double rz;

        public RotatedShape(IShape inner, double rotX, double rotY, double rotZ)
        {
            this.inner = inner;
            rx = rotX;
            ry = rotY;
            rz = rotZ;
        }

        public double UnitRadius(double x, double y, double z, double p)
        {
            double lx, ly, lz;
            ToLocal(x, y, z, out lx, out ly, out lz);
            return inner.UnitRadius(lx, ly, lz, p);
        }

        public void Normal(double x, double y, double z, double p, out double ox, out double oy, out double oz)
        {
            double lx, ly, lz;
            ToLocal(x, y, z, out lx, out ly, out lz);
            double nx, ny, nz;
            inner.Normal(lx, ly, lz, p, out nx, out ny, out nz);
            ToWorld(nx, ny, nz, out ox, out oy, out oz);
        }

        public double MaxRadius(double p)
        {
            return inner.MaxRadius(p);
        }

        public double MinRadius(double p)
        {
            return inner.MinRadius(p);
        }

        public double Bound(double p)
        {
            return inner.Bound(p);
        }

        // world -> shape: undo Y, then X, then Z
        private void ToLocal(double x, double y, double z, out double ox, out double oy, out double oz)
        {
            Turn(ref x, ref y, ref z, 1, -ry);
            Turn(ref x, ref y, ref z, 0, -rx);
            Turn(ref x, ref y, ref z, 2, -rz);
            ox = x;
            oy = y;
            oz = z;
        }

        // shape -> world: Z, then X, then Y
        private void ToWorld(double x, double y, double z, out double ox, out double oy, out double oz)
        {
            Turn(ref x, ref y, ref z, 2, rz);
            Turn(ref x, ref y, ref z, 0, rx);
            Turn(ref x, ref y, ref z, 1, ry);
            ox = x;
            oy = y;
            oz = z;
        }

        // Rotation of (x, y, z) about the X (0), Y (1) or Z (2) axis; same formulas as VertexHeightPolyhedron.
        private static void Turn(ref double x, ref double y, ref double z, int axis, double degrees)
        {
            if (degrees == 0.0)
            {
                return;
            }
            double c = Math.Cos(degrees * Math.PI / 180.0);
            double s = Math.Sin(degrees * Math.PI / 180.0);
            double t;
            if (axis == 2)
            {
                t = x * c - y * s;
                y = x * s + y * c;
                x = t;
            }
            else if (axis == 0)
            {
                t = y * c - z * s;
                z = y * s + z * c;
                y = t;
            }
            else
            {
                t = x * c + z * s;
                z = -x * s + z * c;
                x = t;
            }
        }
    }

    /// <summary>
    /// An ellipsoid with radii a (X), b (Y, the polar axis) and c (Z). Optional smooth fractal relief is
    /// added at the two ends of the longest axis, faded out where the surface faces sideways (radial
    /// height on a sideways-facing slope would only smear along the surface into terraces).
    /// The gravity normal is the smooth ellipsoid's, so the relief never makes gravity jumpy.
    /// </summary>
    public sealed class EllipsoidShape : PresetShape
    {
        private readonly double a, b, c;
        private readonly double amp, freq, start;
        private readonly int longAxis;      // 0 = X, 1 = Y, 2 = Z
        private readonly double longR;
        private readonly int[] perm = new int[512];

        public EllipsoidShape(double radiusX, double radiusY, double radiusZ,
                              double endNoise, double endNoiseFrequency, double endNoiseStart, int seed) : base(0)
        {
            a = radiusX;
            b = radiusY;
            c = radiusZ;
            amp = endNoise;
            freq = endNoiseFrequency;
            start = endNoiseStart;
            longAxis = (a >= b && a >= c) ? 0 : (b >= c ? 1 : 2);
            longR = Math.Max(a, Math.Max(b, c));

            // permutation table for the gradient noise
            int[] p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            Random rng = new Random(seed * 7919 + 17);
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = p[i]; p[i] = p[j]; p[j] = t;
            }
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
        }

        private double Smooth(double x, double y, double z)
        {
            double g = (x / a) * (x / a) + (y / b) * (y / b) + (z / c) * (z / c);
            return g > 1e-24 ? 1.0 / Math.Sqrt(g) : longR;
        }

        public override double UnitRadius(double x, double y, double z, double p)
        {
            double r = Smooth(x, y, z);
            if (amp <= 0.0)
            {
                return r;
            }

            // position along the long axis, 0 at the middle to 1 at the tip
            double along = longAxis == 0 ? x : (longAxis == 1 ? y : z);
            double t = Math.Abs(along * r) / longR;
            double w = (t - start) / (1.0 - start);
            if (w <= 0.0)
            {
                return r;
            }
            if (w > 1.0) w = 1.0;
            w = w * w * (3.0 - 2.0 * w);

            // how directly the smooth surface faces outward here (1 = straight out, 0 = edge-on)
            double nx = x / (a * a), ny = y / (b * b), nz = z / (c * c);
            double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            double facing = nl > 1e-24 ? Math.Abs(nx * x + ny * y + nz * z) / nl : 1.0;
            facing = Math.Pow(Math.Min(facing, 1.0), 1.5);

            // noise sampled on the surface itself (scaled to the long radius) so lumps have an even size
            double sx = x * r / longR * freq, sy = y * r / longR * freq, sz = z * r / longR * freq;
            double n = 0.0, amplitude = 1.0, total = 0.0, f = 1.0;
            for (int o = 0; o < 4; o++)
            {
                n += amplitude * Gradient(sx * f, sy * f, sz * f);
                total += amplitude;
                amplitude *= 0.5;
                f *= 2.0;
            }
            n /= total;

            return r * (1.0 + amp * w * facing * n * 2.0);
        }

        public override void Normal(double x, double y, double z, double p,
            out double ox, out double oy, out double oz)
        {
            // smooth ellipsoid normal: gradient of (x/a)^2 + (y/b)^2 + (z/c)^2 at the surface point
            double r = Smooth(x, y, z);
            double px = x * r, py = y * r, pz = z * r;
            double nx = px / (a * a), ny = py / (b * b), nz = pz / (c * c);
            double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (!(nl > 1e-24))
            {
                ox = x; oy = y; oz = z;
                return;
            }
            ox = nx / nl;
            oy = ny / nl;
            oz = nz / nl;
        }

        public override double Bound(double p)
        {
            return longR * (1.0 + amp * 2.0);
        }

        protected override void AddSeeds(List<double[]> list)
        {
            list.Add(new[] { 1.0, 0.0, 0.0 });
            list.Add(new[] { -1.0, 0.0, 0.0 });
            list.Add(new[] { 0.0, 1.0, 0.0 });
            list.Add(new[] { 0.0, -1.0, 0.0 });
            list.Add(new[] { 0.0, 0.0, 1.0 });
            list.Add(new[] { 0.0, 0.0, -1.0 });
        }

        // ---- improved Perlin gradient noise, roughly -1..1
        private static double Fade(double t) { return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }
        private static double Lerp(double t, double a, double b) { return a + t * (b - a); }
        private static double Grad(int hash, double x, double y, double z)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
        private double Gradient(double x, double y, double z)
        {
            double fx = Math.Floor(x), fy = Math.Floor(y), fz = Math.Floor(z);
            int X = (int)fx & 255, Y = (int)fy & 255, Z = (int)fz & 255;
            x -= fx; y -= fy; z -= fz;
            double u = Fade(x), v = Fade(y), w = Fade(z);
            int A = perm[X] + Y, AA = perm[A] + Z, AB = perm[A + 1] + Z;
            int B = perm[X + 1] + Y, BA = perm[B] + Z, BB = perm[B + 1] + Z;
            return Lerp(w, Lerp(v, Lerp(u, Grad(perm[AA], x, y, z), Grad(perm[BA], x - 1, y, z)),
                                   Lerp(u, Grad(perm[AB], x, y - 1, z), Grad(perm[BB], x - 1, y - 1, z))),
                           Lerp(v, Lerp(u, Grad(perm[AA + 1], x, y, z - 1), Grad(perm[BA + 1], x - 1, y, z - 1)),
                                   Lerp(u, Grad(perm[AB + 1], x, y - 1, z - 1), Grad(perm[BB + 1], x - 1, y - 1, z - 1))));
        }
    }

    // ------------------------------------------------------------------ building a preset from its settings

    public static class PresetFactory
    {
        /// <summary>
        /// Builds the chosen preset. Values outside their useful range are clamped and mentioned in
        /// 'notes'. Returns null (the body stays a normal sphere) only for GeometricPreset.None.
        /// </summary>
        public static IShape Create(GeometricPreset preset,
            CylinderSettings cylinder, HemisphereSettings hemisphere, MacaroniSettings macaroni,
            EllipsoidSettings ellipsoid,
            double rotX, double rotY, double rotZ, out string notes)
        {
            List<string> list = new List<string>();
            IShape shape = null;

            switch (preset)
            {
                case GeometricPreset.Cylinder:
                {
                    CylinderSettings s = cylinder ?? new CylinderSettings();
                    shape = new CylinderShape(
                        Clamp(s.verticalRadius, 0.02, 50.0, 1.0, "Cylinder.verticalRadius", list),
                        CleanSteps(s.steps, "Cylinder.steps", list));
                    break;
                }
                case GeometricPreset.Hemisphere:
                {
                    HemisphereSettings s = hemisphere ?? new HemisphereSettings();
                    shape = new HemisphereShape(
                        Clamp(s.verticalRadius, 0.05, 50.0, 1.0, "Hemisphere.verticalRadius", list),
                        Clamp(s.baseDepth, 0.01, 50.0, 0.1, "Hemisphere.baseDepth", list),
                        CleanSteps(s.steps, "Hemisphere.steps", list));
                    break;
                }
                case GeometricPreset.Macaroni:
                {
                    MacaroniSettings s = macaroni ?? new MacaroniSettings();
                    double len = Clamp(s.length, 0.5, 40.0, 6.0, "Macaroni.length", list);
                    double vr = Clamp(s.verticalRadius, 0.2, 5.0, 1.0, "Macaroni.verticalRadius", list);
                    double ang = Clamp(s.angle, 0.0, 180.0, 90.0, "Macaroni.angle", list);
                    double most = MacaroniShape.MaxAngle(len, vr);
                    if (ang > most)
                    {
                        list.Add("Macaroni.angle = " + ang + " bends a tube of length " + len
                                 + " too far for it to stay visible from the body's center. Using "
                                 + Math.Floor(most * 10.0) / 10.0 + " (a shorter or thicker tube can bend further)");
                        ang = most;
                    }
                    shape = new MacaroniShape(len, ang, vr, CleanSteps(s.steps, "Macaroni.steps", list), s.roundEnds);
                    break;
                }
                case GeometricPreset.Ellipsoid:
                {
                    EllipsoidSettings s = ellipsoid ?? new EllipsoidSettings();
                    shape = new EllipsoidShape(
                        Clamp(s.radiusX, 0.02, 100.0, 1.0, "Ellipsoid.radiusX", list),
                        Clamp(s.radiusY, 0.02, 100.0, 1.0, "Ellipsoid.radiusY", list),
                        Clamp(s.radiusZ, 0.02, 100.0, 1.0, "Ellipsoid.radiusZ", list),
                        Clamp(s.endNoise, 0.0, 0.5, 0.0, "Ellipsoid.endNoise", list),
                        Clamp(s.endNoiseFrequency, 0.1, 200.0, 6.0, "Ellipsoid.endNoiseFrequency", list),
                        Clamp(s.endNoiseStart, 0.0, 0.95, 0.35, "Ellipsoid.endNoiseStart", list),
                        s.seed);
                    break;
                }
            }

            if (shape != null && (rotX != 0.0 || rotY != 0.0 || rotZ != 0.0))
            {
                shape = new RotatedShape(shape, rotX, rotY, rotZ);
            }

            notes = list.Count > 0 ? string.Join("; ", list.ToArray()) : null;
            return shape;
        }

        private static double Clamp(double value, double min, double max, double fallback,
            string name, List<string> notes)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                notes.Add(name + " is not a number, using " + fallback);
                return fallback;
            }
            if (value < min)
            {
                notes.Add(name + " = " + value + " is below the minimum of " + min + ", using " + min);
                return min;
            }
            if (value > max)
            {
                notes.Add(name + " = " + value + " is above the maximum of " + max + ", using " + max);
                return max;
            }
            return value;
        }

        private static int CleanSteps(int steps, string name, List<string> notes)
        {
            if (steps == 0)
            {
                return 0;
            }
            if (steps < 3)
            {
                notes.Add(name + " = " + steps + " cannot make a closed shape (use 0 for round, or 3 to "
                          + PresetShape.MaxSteps + "). Using round");
                return 0;
            }
            if (steps > PresetShape.MaxSteps)
            {
                notes.Add(name + " = " + steps + " is above the limit of " + PresetShape.MaxSteps + ". Using round");
                return 0;
            }
            return steps;
        }
    }
}
