/*
 * VertexHeightPolyhedron
 * ----------------------------------------------------------------------------
 * A Kopernicus PQS mod that turns a body into a flat-faced polyhedron by
 * specifying how many faces it should have.
 *
 *   faces = 4  -> tetrahedron
 *   faces = 6  -> cube
 *   faces = 5  -> triangular prism
 *   ...
 *
 * How it works: every face is a plane tangent to the same inner sphere (an
 * "inradius" of 1). The distance from the body's center to the surface along
 * any direction u is then  1 / f(u)  where
 *
 *     f(u) = ( sum over faces of max(n_i . u, 0)^p ) ^ (1/p)
 *
 * and n_i are the face normals. With p = infinity this is just max(n_i . u),
 * which gives sharp edges. A finite p rounds the edges and corners (p = 2 is
 * roughly a sphere), and the same field gives a surface normal that changes
 * smoothly across an edge:  N ~ sum (n_i . u)^(p-1) * n_i.
 *
 * Irregular shapes: instead of "faces", GeometricPreset = Cylinder / Hemisphere / Macaroni / Ellipsoid picks a
 * ready-made shape (see GeometricPresets.cs). Each one has a settings node of its own, with a
 * "verticalRadius" and a "steps" value (and a few more for the macaroni). They work the
 * same way as the polyhedron: one function gives the distance from the center to the surface along any
 * direction, so smoothing, fit, rotation and surface gravity all apply to them as well.
 *
 * Options
 *   GeometricPreset    a ready-made irregular shape; "faces" is ignored when this is set
 *   uniformity         0 to 5: how much of the shape is applied (5 = all of it, 2.5 = halfway
 *                      between a sphere and the shape, 0 = the mod does nothing)
 *   smoothing          rounds the edges and corners of the mesh itself
 *   surfaceGravity     gravity points along the surface normal and has the same
 *                      strength everywhere on the surface (see PolyhedronGravity.cs)
 *   gravitySmoothing   how gradually the gravity direction rotates across an edge
 *   alignCamera        the free camera's "up" follows that gravity (see PolyhedronGravity.cs)
 *
 * Fallback to the normal spherical body (the mod does nothing) when:
 *   - faces is 0 / not set and no GeometricPreset is chosen
 *   - faces is below 4 (no closed polyhedron exists)
 *   - faces is above MaxFaces (30) - protects against heavy subdivision cost
 *   - the chosen layout cannot form a closed solid for that face count
 *
 * Targets Kopernicus for KSP 1.12.x. Written for C# 7.3.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Kopernicus.ConfigParser.Attributes;
using Kopernicus.ConfigParser.BuiltinTypeParsers;
using Kopernicus.ConfigParser.Enumerations;
using Kopernicus.ConfigParser.Interfaces;

namespace PolyhedronPQS
{
    /// <summary>How the face normals are arranged for a given face count.</summary>
    public enum PolyhedronLayout
    {
        /// <summary>Platonic solid for 4, 6, 8, 12, 20 faces; otherwise a prism.</summary>
        Auto,
        /// <summary>Always a prism: two caps plus (faces - 2) side faces. 6 faces = cube.</summary>
        Prism,
        /// <summary>Faces spread evenly over the sphere (Fibonacci lattice), gem / golf-ball look.</summary>
        Spread
    }

    /// <summary>Which part of the polyhedron touches the body's nominal radius.</summary>
    public enum PolyhedronFit
    {
        /// <summary>Corners touch the radius; the solid sits inside the sphere.</summary>
        Circumradius,
        /// <summary>Face centers touch the radius; corners poke out of the sphere.</summary>
        Inradius
    }

    /// <summary>
    /// Pure geometry (no KSP types). Faces are the planes n_i . x = 1 with unit
    /// normals n_i, so every face is tangent to the unit sphere.
    /// </summary>
    public sealed class PolyhedronShape : IShape
    {
        public const int MinFaces = 4;
        public const int MaxFaces = 30;

        /// <summary>Smoothing at or below this is treated as perfectly sharp.</summary>
        public const double SharpBelow = 0.002;

        /// <summary>Smoothing is clamped to this (p = 2).</summary>
        public const double MaxSmoothing = 0.5;

        public readonly int Faces;

        /// <summary>Distance to the farthest corner of the sharp solid, in units where the faces are 1 away.</summary>
        public readonly double CircumRadius;

        private readonly double[] nx;
        private readonly double[] ny;
        private readonly double[] nz;

        // unit directions from the center to each corner
        private readonly double[] cx;
        private readonly double[] cy;
        private readonly double[] cz;

        private PolyhedronShape(int faces, double[] nx, double[] ny, double[] nz,
            double circumRadius, List<double[]> corners)
        {
            Faces = faces;
            this.nx = nx;
            this.ny = ny;
            this.nz = nz;
            CircumRadius = circumRadius;

            cx = new double[corners.Count];
            cy = new double[corners.Count];
            cz = new double[corners.Count];
            for (int i = 0; i < corners.Count; i++)
            {
                cx[i] = corners[i][0];
                cy[i] = corners[i][1];
                cz[i] = corners[i][2];
            }
        }

        /// <summary>
        /// Turns a smoothing value into the exponent p used by the field.
        /// Returns 0 for "sharp" (a plain maximum). 0.1 gives p = 10, 0.5 gives p = 2.
        /// </summary>
        public static double ExponentFor(double smoothing)
        {
            if (!(smoothing > SharpBelow))
            {
                return 0.0;
            }
            if (smoothing > MaxSmoothing)
            {
                smoothing = MaxSmoothing;
            }
            return 1.0 / smoothing;
        }

        /// <summary>Returns null when the request cannot become a closed polyhedron.</summary>
        public static PolyhedronShape Create(int faces, PolyhedronLayout layout,
            double rotX, double rotY, double rotZ)
        {
            if (faces < MinFaces || faces > MaxFaces)
            {
                return null;
            }

            List<double[]> normals = BuildNormals(faces, layout);
            double[] x = new double[faces];
            double[] y = new double[faces];
            double[] z = new double[faces];
            for (int i = 0; i < faces; i++)
            {
                double[] v = Rotate(normals[i], rotX, rotY, rotZ);
                x[i] = v[0];
                y[i] = v[1];
                z[i] = v[2];
            }

            if (!IsBounded(x, y, z))
            {
                return null;
            }

            List<double[]> corners = new List<double[]>();
            double circum = ComputeCorners(x, y, z, corners);
            if (!(circum > 0.0) || double.IsInfinity(circum) || double.IsNaN(circum) || corners.Count == 0)
            {
                return null;
            }

            return new PolyhedronShape(faces, x, y, z, circum, corners);
        }

        /// <summary>
        /// The "distance" field: 1 on the surface, growing linearly with scale
        /// (Field(2x) = 2 Field(x)). p = 0 means sharp (plain maximum).
        /// </summary>
        public double Field(double x, double y, double z, double p)
        {
            double m = double.NegativeInfinity;
            for (int i = 0; i < nx.Length; i++)
            {
                double d = nx[i] * x + ny[i] * y + nz[i] * z;
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
            for (int i = 0; i < nx.Length; i++)
            {
                double d = nx[i] * x + ny[i] * y + nz[i] * z;
                if (d > 0.0)
                {
                    sum += Math.Pow(d / m, p);
                }
            }
            return m * Math.Pow(sum, 1.0 / p);
        }

        /// <summary>
        /// Distance from the center to the surface along the unit direction (x, y, z), in
        /// units where the faces of the sharp solid are 1 away.
        /// </summary>
        public double UnitRadius(double x, double y, double z, double p)
        {
            double f = Field(x, y, z, p);
            return f > 1e-12 ? 1.0 / f : CircumRadius;
        }

        /// <summary>
        /// Outward unit surface normal at direction (x, y, z). With p = 0 this is the normal
        /// of the face being looked at (jumps across an edge); a finite p blends the
        /// neighbouring faces, so it turns gradually across an edge.
        /// </summary>
        public void Normal(double x, double y, double z, double p,
            out double ox, out double oy, out double oz)
        {
            double m = double.NegativeInfinity;
            int best = -1;
            for (int i = 0; i < nx.Length; i++)
            {
                double d = nx[i] * x + ny[i] * y + nz[i] * z;
                if (d > m)
                {
                    m = d;
                    best = i;
                }
            }

            if (best < 0 || !(m > 1e-12))
            {
                ox = 0.0;
                oy = 1.0;
                oz = 0.0;
                return;
            }

            if (p <= 0.0)
            {
                ox = nx[best];
                oy = ny[best];
                oz = nz[best];
                return;
            }

            double sx = 0.0;
            double sy = 0.0;
            double sz = 0.0;
            double e = p - 1.0;
            for (int i = 0; i < nx.Length; i++)
            {
                double d = nx[i] * x + ny[i] * y + nz[i] * z;
                if (d > 0.0)
                {
                    double w = Math.Pow(d / m, e);
                    sx += w * nx[i];
                    sy += w * ny[i];
                    sz += w * nz[i];
                }
            }

            double len = Math.Sqrt(sx * sx + sy * sy + sz * sz);
            if (len < 1e-12)
            {
                ox = nx[best];
                oy = ny[best];
                oz = nz[best];
                return;
            }
            ox = sx / len;
            oy = sy / len;
            oz = sz / len;
        }

        /// <summary>Largest distance to the (smoothed) surface: at the corners, or just beside them once smoothing is applied.</summary>
        /// <summary>The sharp solid's farthest corner also bounds any smoothed version of it.</summary>
        public double Bound(double p)
        {
            return CircumRadius;
        }

        public double MaxRadius(double p)
        {
            return Extreme(p, true);
        }

        /// <summary>Smallest distance to the (smoothed) surface: at the face centers, or just beside them once smoothing is applied.</summary>
        public double MinRadius(double p)
        {
            return Extreme(p, false);
        }

        // For a sharp solid the extremes are exactly the corners / face centers. Smoothing can
        // nudge them slightly off (it is not symmetric for every shape), so search outward from
        // those spots and from a coarse sampling of the whole sphere. Runs once, when the mod is set up.
        private double Extreme(double p, bool wantMax)
        {
            double[] sx = wantMax ? cx : nx;
            double[] sy = wantMax ? cy : ny;
            double[] sz = wantMax ? cz : nz;

            if (p <= 0.0)
            {
                double e = wantMax ? 0.0 : double.PositiveInfinity;
                for (int i = 0; i < sx.Length; i++)
                {
                    double r = UnitRadius(sx[i], sy[i], sz[i], 0.0);
                    e = wantMax ? Math.Max(e, r) : Math.Min(e, r);
                }
                return e;
            }

            // work with sign * radius so that "bigger is better" in both cases
            double sign = wantMax ? 1.0 : -1.0;
            double best = double.NegativeInfinity;

            for (int i = 0; i < sx.Length; i++)
            {
                best = Math.Max(best, Refine(sx[i], sy[i], sz[i], p, sign));
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

        // ---------------------------------------------------------------- normals

        private static List<double[]> BuildNormals(int n, PolyhedronLayout layout)
        {
            if (layout == PolyhedronLayout.Auto)
            {
                List<double[]> platonic = PlatonicNormals(n);
                if (platonic != null)
                {
                    return platonic;
                }
                layout = PolyhedronLayout.Prism;
            }
            return layout == PolyhedronLayout.Spread ? SpreadNormals(n) : PrismNormals(n);
        }

        private static List<double[]> PlatonicNormals(int n)
        {
            switch (n)
            {
                case 4: return Tetrahedron();
                case 6: return Cube();
                case 8: return Octahedron();
                case 12: return Dodecahedron();
                case 20: return Icosahedron();
                default: return null;
            }
        }

        private static void AddNormalized(List<double[]> list, double x, double y, double z)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            list.Add(new[] { x / len, y / len, z / len });
        }

        // One face on the bottom, a corner at the top.
        private static List<double[]> Tetrahedron()
        {
            double side = Math.Sqrt(8.0) / 3.0;
            List<double[]> list = new List<double[]>();
            list.Add(new[] { 0.0, -1.0, 0.0 });
            for (int k = 0; k < 3; k++)
            {
                double a = 2.0 * Math.PI * k / 3.0;
                list.Add(new[] { side * Math.Cos(a), 1.0 / 3.0, side * Math.Sin(a) });
            }
            return list;
        }

        private static List<double[]> Cube()
        {
            List<double[]> list = new List<double[]>();
            list.Add(new[] { 1.0, 0.0, 0.0 });
            list.Add(new[] { -1.0, 0.0, 0.0 });
            list.Add(new[] { 0.0, 1.0, 0.0 });
            list.Add(new[] { 0.0, -1.0, 0.0 });
            list.Add(new[] { 0.0, 0.0, 1.0 });
            list.Add(new[] { 0.0, 0.0, -1.0 });
            return list;
        }

        private static List<double[]> Octahedron()
        {
            double[] signs = { -1.0, 1.0 };
            List<double[]> list = new List<double[]>();
            foreach (double sx in signs)
            {
                foreach (double sy in signs)
                {
                    foreach (double sz in signs)
                    {
                        AddNormalized(list, sx, sy, sz);
                    }
                }
            }
            return list;
        }

        // Face normals of a dodecahedron are the corners of an icosahedron.
        private static List<double[]> Dodecahedron()
        {
            double phi = (1.0 + Math.Sqrt(5.0)) / 2.0;
            double[] signs = { -1.0, 1.0 };
            List<double[]> list = new List<double[]>();
            foreach (double a in signs)
            {
                foreach (double b in signs)
                {
                    AddNormalized(list, 0.0, a, b * phi);
                    AddNormalized(list, a, b * phi, 0.0);
                    AddNormalized(list, b * phi, 0.0, a);
                }
            }
            return list;
        }

        // Face normals of an icosahedron are the corners of a dodecahedron.
        private static List<double[]> Icosahedron()
        {
            double phi = (1.0 + Math.Sqrt(5.0)) / 2.0;
            double inv = 1.0 / phi;
            double[] signs = { -1.0, 1.0 };
            List<double[]> list = new List<double[]>();
            foreach (double a in signs)
            {
                foreach (double b in signs)
                {
                    foreach (double c in signs)
                    {
                        AddNormalized(list, a, b, c);
                    }
                    AddNormalized(list, 0.0, a * inv, b * phi);
                    AddNormalized(list, a * inv, b * phi, 0.0);
                    AddNormalized(list, b * phi, 0.0, a * inv);
                }
            }
            return list;
        }

        // Two caps (+Y / -Y) and (n - 2) side faces around the Y axis.
        private static List<double[]> PrismNormals(int n)
        {
            int sides = n - 2;
            List<double[]> list = new List<double[]>();
            list.Add(new[] { 0.0, 1.0, 0.0 });
            list.Add(new[] { 0.0, -1.0, 0.0 });
            for (int k = 0; k < sides; k++)
            {
                double a = 2.0 * Math.PI * k / sides;
                list.Add(new[] { Math.Cos(a), 0.0, Math.Sin(a) });
            }
            return list;
        }

        private static List<double[]> SpreadNormals(int n)
        {
            double golden = Math.PI * (3.0 - Math.Sqrt(5.0));
            List<double[]> list = new List<double[]>();
            for (int i = 0; i < n; i++)
            {
                double y = 1.0 - 2.0 * (i + 0.5) / n;
                double r = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
                double t = i * golden;
                list.Add(new[] { r * Math.Cos(t), y, r * Math.Sin(t) });
            }
            return list;
        }

        // Euler angles in degrees, same order as Unity's Quaternion.Euler (Z, then X, then Y).
        private static double[] Rotate(double[] v, double rx, double ry, double rz)
        {
            const double toRad = Math.PI / 180.0;
            double x = v[0];
            double y = v[1];
            double z = v[2];
            double c, s, t;

            if (rz != 0.0)
            {
                c = Math.Cos(rz * toRad);
                s = Math.Sin(rz * toRad);
                t = x * c - y * s;
                y = x * s + y * c;
                x = t;
            }
            if (rx != 0.0)
            {
                c = Math.Cos(rx * toRad);
                s = Math.Sin(rx * toRad);
                t = y * c - z * s;
                z = y * s + z * c;
                y = t;
            }
            if (ry != 0.0)
            {
                c = Math.Cos(ry * toRad);
                s = Math.Sin(ry * toRad);
                t = x * c + z * s;
                z = -x * s + z * c;
                x = t;
            }
            return new[] { x, y, z };
        }

        // ------------------------------------------------------------- validation

        // The solid is closed only if no direction escapes every face. Escape
        // directions of a rank-3 face set always lie along n_i x n_j for some pair.
        private static bool IsBounded(double[] nx, double[] ny, double[] nz)
        {
            int n = nx.Length;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double dx = ny[i] * nz[j] - nz[i] * ny[j];
                    double dy = nz[i] * nx[j] - nx[i] * nz[j];
                    double dz = nx[i] * ny[j] - ny[i] * nx[j];
                    double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (len < 1e-9)
                    {
                        continue;
                    }
                    dx /= len;
                    dy /= len;
                    dz /= len;

                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        bool escapes = true;
                        for (int k = 0; k < n && escapes; k++)
                        {
                            if (sign * (nx[k] * dx + ny[k] * dy + nz[k] * dz) > 1e-9)
                            {
                                escapes = false;
                            }
                        }
                        if (escapes)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static void AddCorner(List<double[]> list, double x, double y, double z)
        {
            for (int i = 0; i < list.Count; i++)
            {
                double[] c = list[i];
                if (Math.Abs(c[0] - x) + Math.Abs(c[1] - y) + Math.Abs(c[2] - z) < 1e-6)
                {
                    return;
                }
            }
            list.Add(new[] { x, y, z });
        }

        // Every corner is where three face planes meet; keep the ones that are not
        // cut off by another face. Returns the farthest corner distance and fills
        // 'corners' with the unit direction to each corner.
        private static double ComputeCorners(double[] nx, double[] ny, double[] nz, List<double[]> corners)
        {
            int n = nx.Length;
            double best = 0.0;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        // cross(nj, nk), cross(nk, ni), cross(ni, nj)
                        double ax = ny[j] * nz[k] - nz[j] * ny[k];
                        double ay = nz[j] * nx[k] - nx[j] * nz[k];
                        double az = nx[j] * ny[k] - ny[j] * nx[k];
                        double bx = ny[k] * nz[i] - nz[k] * ny[i];
                        double by = nz[k] * nx[i] - nx[k] * nz[i];
                        double bz = nx[k] * ny[i] - ny[k] * nx[i];
                        double cx = ny[i] * nz[j] - nz[i] * ny[j];
                        double cy = nz[i] * nx[j] - nx[i] * nz[j];
                        double cz = nx[i] * ny[j] - ny[i] * nx[j];

                        double det = nx[i] * ax + ny[i] * ay + nz[i] * az;
                        if (Math.Abs(det) < 1e-9)
                        {
                            continue;
                        }

                        double px = (ax + bx + cx) / det;
                        double py = (ay + by + cy) / det;
                        double pz = (az + bz + cz) / det;

                        bool inside = true;
                        for (int l = 0; l < n && inside; l++)
                        {
                            if (nx[l] * px + ny[l] * py + nz[l] * pz > 1.0 + 1e-7)
                            {
                                inside = false;
                            }
                        }
                        if (inside)
                        {
                            double r = Math.Sqrt(px * px + py * py + pz * pz);
                            if (r > best)
                            {
                                best = r;
                            }
                            if (r > 1e-9)
                            {
                                AddCorner(corners, px / r, py / r, pz / r);
                            }
                        }
                    }
                }
            }
            return best;
        }
    }

    /// <summary>A shape together with the values derived from the current smoothing settings.</summary>
    internal sealed class PolyhedronModel
    {
        public readonly IShape Shape;

        /// <summary>Exponent used for the mesh (0 = sharp).</summary>
        public readonly double GeometryExponent;

        /// <summary>Exponent used for the gravity direction (0 = sharp).</summary>
        public readonly double GravityExponent;

        /// <summary>Distance to the corners / face centers of the smoothed solid (faces of the sharp solid = 1).</summary>
        public readonly double MaxRadius;
        public readonly double MinRadius;

        /// <summary>A distance the smoothed solid never exceeds (in the same units), for quick "far away" checks.</summary>
        public readonly double Bound;

        public PolyhedronModel(IShape shape, double geometryExponent, double gravityExponent)
        {
            Shape = shape;
            GeometryExponent = geometryExponent;
            GravityExponent = gravityExponent;
            MaxRadius = shape.MaxRadius(geometryExponent);
            MinRadius = shape.MinRadius(geometryExponent);
            Bound = Math.Max(shape.Bound(geometryExponent), MaxRadius);
        }

        /// <summary>True when the shape produced usable numbers.</summary>
        public bool IsValid
        {
            get
            {
                return MaxRadius > 0.0 && MinRadius > 0.0 && Bound > 0.0
                       && !double.IsNaN(MaxRadius) && !double.IsInfinity(MaxRadius)
                       && !double.IsNaN(MinRadius) && !double.IsInfinity(MinRadius)
                       && !double.IsNaN(Bound) && !double.IsInfinity(Bound);
            }
        }
    }

    /// <summary>The PQS mod itself. Additive, like VertexHeightOblate, so heightmaps and noise stack on top.</summary>
    public class PQSMod_VertexHeightPolyhedron : PQSMod
    {
        /// <summary>Ready-made irregular shape (cylinder, hemisphere, macaroni). None = use "faces" instead.</summary>
        public GeometricPreset geometricPreset = GeometricPreset.None;

        // settings for each preset; only the one that matches geometricPreset is used
        public CylinderSettings cylinder = new CylinderSettings();
        public HemisphereSettings hemisphere = new HemisphereSettings();
        public MacaroniSettings macaroni = new MacaroniSettings();
        public EllipsoidSettings ellipsoid = new EllipsoidSettings();

        // Text copy of the preset sub-node values ("node|name|value" lines). A plain string always
        // survives Unity copying the planet, so Build() re-applies it in case the settings objects didn't.
        [SerializeField]
        public string presetText = "";

        /// <summary>Forget the built shape so the next use rebuilds it from the current settings.</summary>
        public void Invalidate()
        {
            built = false;
            model = null;
        }

        private void ReapplyPresetText()
        {
            if (string.IsNullOrEmpty(presetText))
            {
                return;
            }
            foreach (string line in presetText.Split('\n'))
            {
                string[] parts = line.Split(new[] { '|' }, 3);
                if (parts.Length != 3)
                {
                    continue;
                }
                IPresetSettings target = null;
                switch (parts[0])
                {
                    case "ellipsoid": target = ellipsoid = ellipsoid ?? new EllipsoidSettings(); break;
                    case "cylinder": target = cylinder = cylinder ?? new CylinderSettings(); break;
                    case "hemisphere": target = hemisphere = hemisphere ?? new HemisphereSettings(); break;
                    case "macaroni": target = macaroni = macaroni ?? new MacaroniSettings(); break;
                }
                if (target != null)
                {
                    PresetText.Apply(target, parts[1], parts[2]);
                }
            }
        }

        /// <summary>
        /// 0 to 5: how much of the shape is applied to the body. 5 = all of it, 2.5 = every point of the
        /// surface halfway between the plain sphere and the shape, 0 = none (the mod does nothing).
        /// </summary>
        public double uniformity = MaxUniformity;

        public const double MaxUniformity = 5.0;

        /// <summary>Number of faces (a regular polyhedron). 0 = off (plain sphere). Ignored when geometricPreset is set.</summary>
        public int faces;
        public PolyhedronLayout layout = PolyhedronLayout.Auto;
        public PolyhedronFit fit = PolyhedronFit.Circumradius;

        /// <summary>Euler angles in degrees to orient the solid.</summary>
        public double rotationX;
        public double rotationY;
        public double rotationZ;

        /// <summary>0 = sharp edges. Up to 0.5. About 0.05 slightly softens corners, 0.15 is clearly rounded.</summary>
        public double smoothing;

        /// <summary>Gravity follows the surface instead of pulling toward the center.</summary>
        public bool surfaceGravity;

        /// <summary>How gradually the gravity direction turns across an edge (0 = abrupt, default 0.1).</summary>
        public double gravitySmoothing = 0.1;

        /// <summary>
        /// How far above the surface the surface gravity fades out into normal gravity,
        /// as a fraction of the distance from the center to the surface.
        /// </summary>
        public double gravityFadeHeight = 0.25;

        /// <summary>Multiplier on the body's surface gravity (GeeASL) used for surface gravity.</summary>
        public double surfaceGravityScale = 1.0;

        /// <summary>With surface gravity on, the free camera's "up" follows the gravity direction.</summary>
        public bool alignCamera = true;

        // null = behave as a normal sphere
        private volatile PolyhedronModel model;
        private volatile bool built;

        private static readonly HashSet<string> warned = new HashSet<string>();

        public override void OnSetup()
        {
            Build();
        }

        public override void OnVertexBuildHeight(PQS.VertexBuildData data)
        {
            PolyhedronModel m = GetModel();
            if (m == null)
            {
                return;
            }

            double radius = sphere.radius;
            Vector3d dir = data.directionFromCenter;
            double r = m.Shape.UnitRadius(dir.x, dir.y, dir.z, m.GeometryExponent);
            data.vertHeight += r * Scale(m, radius) - radius;
        }

        // Heights are relative to the body radius; PQS uses these for its bounds.
        public override double GetVertexMaxHeight()
        {
            PolyhedronModel m = GetModel();
            if (m == null || sphere == null)
            {
                return 0.0;
            }
            double radius = sphere.radius;
            return Math.Max(0.0, m.MaxRadius * Scale(m, radius) - radius);
        }

        public override double GetVertexMinHeight()
        {
            PolyhedronModel m = GetModel();
            if (m == null || sphere == null)
            {
                return 0.0;
            }
            double radius = sphere.radius;
            return Math.Min(0.0, m.MinRadius * Scale(m, radius) - radius);
        }

        // Circumradius: the corners of the smoothed solid touch the radius.
        // Inradius: the face centers touch the radius.
        private double Scale(PolyhedronModel m, double radius)
        {
            return fit == PolyhedronFit.Circumradius ? radius / m.MaxRadius : radius / m.MinRadius;
        }

        private PolyhedronModel GetModel()
        {
            if (!built)
            {
                Build();
            }
            return model;
        }

        private void Build()
        {
            PolyhedronModel result = null;
            try
            {
                ReapplyPresetText();
                double amount = Uniformity();
                IShape shape = amount > 0.0
                    ? (geometricPreset != GeometricPreset.None ? BuildPreset() : BuildPolyhedron())
                    : null;
                if (shape == null)
                {
                    Debug.Log("[PolyhedronPQS] " + (sphere != null ? sphere.name : "unknown body") + ": no shape (preset "
                              + geometricPreset + ", faces " + faces + ", uniformity " + uniformity + "), staying a sphere");
                }
                if (shape != null)
                {
                    double pGeometry = PolyhedronShape.ExponentFor(smoothing);
                    double pGravity = PolyhedronShape.ExponentFor(gravitySmoothing);
                    PolyhedronModel candidate = new PolyhedronModel(shape, pGeometry, pGravity);
                    if (candidate.IsValid && amount < 1.0)
                    {
                        // Only part of the shape: move the surface that fraction of the way from the
                        // sphere the shape is fitted to. Measured from that sphere, so "fit" still holds:
                        // with Circumradius the highest point stays at the body radius.
                        double fitted = fit == PolyhedronFit.Circumradius ? candidate.MaxRadius : candidate.MinRadius;
                        IShape blended = new BlendedShape(shape, amount, fitted,
                            candidate.MaxRadius, candidate.MinRadius, candidate.Bound);
                        candidate = new PolyhedronModel(blended, pGeometry, pGravity);
                    }
                    if (candidate.IsValid)
                    {
                        result = candidate;
                        Debug.Log("[PolyhedronPQS] " + (sphere != null ? sphere.name : "unknown body") + ": built "
                                  + (geometricPreset != GeometricPreset.None ? geometricPreset.ToString() : faces + " faces")
                                  + (geometricPreset == GeometricPreset.Ellipsoid && ellipsoid != null
                                      ? " (radii " + ellipsoid.radiusX + " / " + ellipsoid.radiusY + " / " + ellipsoid.radiusZ
                                        + ", endNoise " + ellipsoid.endNoise + ")"
                                      : "")
                                  + ", unit radius range " + candidate.MinRadius.ToString("0.###") + " - " + candidate.MaxRadius.ToString("0.###")
                                  + ", fit " + fit + ", uniformity " + uniformity);
                    }
                    else
                    {
                        Warn("the shape could not be built from these settings. Using a normal sphere.");
                    }
                }
            }
            catch (Exception e)
            {
                Warn("building the shape failed, using a normal sphere: " + e.Message);
            }

            model = result;
            built = true;
        }

        // uniformity as a fraction 0..1 (warns about values outside 0..5)
        private double Uniformity()
        {
            double u = uniformity;
            if (double.IsNaN(u) || double.IsInfinity(u))
            {
                Warn("uniformity is not a number, using " + MaxUniformity);
                u = MaxUniformity;
            }
            if (u < 0.0)
            {
                Warn("uniformity = " + u + " is below 0, using 0 (no shape)");
                u = 0.0;
            }
            if (u > MaxUniformity)
            {
                Warn("uniformity = " + u + " is above " + MaxUniformity + ", using " + MaxUniformity);
                u = MaxUniformity;
            }
            return u / MaxUniformity;
        }

        // A regular polyhedron from "faces"; null (and a warning) when there is none to make.
        private IShape BuildPolyhedron()
        {
            if (faces == 0)
            {
                return null;
            }

            if (faces < PolyhedronShape.MinFaces)
            {
                Warn("faces = " + faces + " cannot form a closed polyhedron (minimum is "
                     + PolyhedronShape.MinFaces + "). Using a normal sphere.");
                return null;
            }
            if (faces > PolyhedronShape.MaxFaces)
            {
                Warn("faces = " + faces + " is above the limit of " + PolyhedronShape.MaxFaces
                     + ". Ignoring it and using a normal sphere.");
                return null;
            }

            PolyhedronShape shape = PolyhedronShape.Create(faces, layout, rotationX, rotationY, rotationZ);
            if (shape == null)
            {
                Warn("faces = " + faces + " with layout " + layout
                     + " does not form a closed polyhedron. Using a normal sphere.");
            }
            return shape;
        }

        // One of the ready-made shapes, from geometricPreset and its settings node.
        private IShape BuildPreset()
        {
            if (faces != 0)
            {
                Warn("GeometricPreset = " + geometricPreset + " is set, so faces = " + faces + " is ignored.");
            }

            string notes;
            IShape shape = PresetFactory.Create(geometricPreset, cylinder, hemisphere, macaroni, ellipsoid,
                rotationX, rotationY, rotationZ, out notes);
            if (notes != null)
            {
                Warn(geometricPreset + ": " + notes);
            }
            if (shape == null)
            {
                Warn("GeometricPreset = " + geometricPreset + " is not a shape this version can build. Using a normal sphere.");
            }
            return shape;
        }

        /// <summary>True when this mod actually changes the body's shape.</summary>
        internal bool HasShape
        {
            get { return GetModel() != null; }
        }

        // ------------------------------------------------------------- gravity

        /// <summary>True when the position is close enough to the body for surface gravity to matter.</summary>
        internal bool IsInGravityZone(Vector3d worldPos, CelestialBody body)
        {
            if (!surfaceGravity || body == null || sphere == null)
            {
                return false;
            }
            PolyhedronModel m = GetModel();
            if (m == null)
            {
                return false;
            }

            double dx = worldPos.x - body.position.x;
            double dy = worldPos.y - body.position.y;
            double dz = worldPos.z - body.position.z;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double fade = Math.Max(gravityFadeHeight, 0.001);
            return dist < (1.0 + fade) * Scale(m, sphere.radius) * m.Bound;
        }

        /// <summary>
        /// Gravity acceleration (m/s^2) at a world position when surface gravity applies there.
        /// Returns false when stock gravity should be used instead.
        /// </summary>
        internal bool TryGetSurfaceGravity(Vector3d worldPos, CelestialBody body, out Vector3d result)
        {
            result = new Vector3d(0.0, 0.0, 0.0);
            if (!surfaceGravity || body == null || sphere == null)
            {
                return false;
            }
            PolyhedronModel m = GetModel();
            if (m == null)
            {
                return false;
            }

            double radius = sphere.radius;
            double scale = Scale(m, radius);
            double fade = Math.Max(gravityFadeHeight, 0.001);

            double rx = worldPos.x - body.position.x;
            double ry = worldPos.y - body.position.y;
            double rz = worldPos.z - body.position.z;
            double dist = Math.Sqrt(rx * rx + ry * ry + rz * rz);
            if (dist < 1.0)
            {
                return false;
            }

            // Anything beyond the fade zone of the farthest corner is plain stock gravity.
            if (dist >= (1.0 + fade) * scale * m.Bound)
            {
                return false;
            }

            // Direction from the center, expressed in the planet-fixed frame the mesh was built in.
            Transform frame = sphere.transform;
            Vector3 local = frame.InverseTransformDirection(
                new Vector3((float)(rx / dist), (float)(ry / dist), (float)(rz / dist)));
            double ux = local.x;
            double uy = local.y;
            double uz = local.z;
            double ulen = Math.Sqrt(ux * ux + uy * uy + uz * uz);
            if (ulen < 1e-6)
            {
                return false;
            }
            ux /= ulen;
            uy /= ulen;
            uz /= ulen;

            // Where the (idealised) surface is along this direction, and how far above it we are.
            double surfaceRadius = m.Shape.UnitRadius(ux, uy, uz, m.GeometryExponent) * scale;
            double above = dist / surfaceRadius - 1.0;

            // 1 at and below the surface, easing to 0 at 'fade' above it.
            double w = 1.0;
            if (above > 0.0)
            {
                double t = above / fade;
                w = t >= 1.0 ? 0.0 : 1.0 - t * t * (3.0 - 2.0 * t);
            }

            // The pull toward the center that gravity fades into. It is the stock pull, except that
            // inside the body's nominal radius it is held at the strength it has there. Without that,
            // the pull would soar near anything that sits close to the center (the flat side of a
            // hemisphere, the inside of a macaroni's bend, the waist of a tall cylinder) and crush
            // anything that climbs a few kilometers off the ground.
            double gm = body.gMagnitudeAtCenter;
            double pullDist = Math.Max(dist, radius);
            double gPoint = gm / (pullDist * pullDist);
            double px = -rx / dist * gPoint;
            double py = -ry / dist * gPoint;
            double pz = -rz / dist * gPoint;

            if (w <= 0.0)
            {
                if (dist >= radius)
                {
                    return false;   // exactly the stock gravity
                }
                result = new Vector3d(px, py, pz);
                return true;
            }

            // Surface normal, blended across edges, back in world axes.
            double nx;
            double ny;
            double nz;
            m.Shape.Normal(ux, uy, uz, m.GravityExponent, out nx, out ny, out nz);
            Vector3 nWorld = frame.TransformDirection(new Vector3((float)nx, (float)ny, (float)nz));

            // Same strength everywhere on the surface; falls off as 1/d^2 above it.
            double g0 = gm / (radius * radius) * surfaceGravityScale;
            double ratio = surfaceRadius / Math.Max(dist, surfaceRadius);
            double gSurface = g0 * ratio * ratio;

            double sx = -nWorld.x * gSurface;
            double sy = -nWorld.y * gSurface;
            double sz = -nWorld.z * gSurface;

            if (w >= 1.0)
            {
                result = new Vector3d(sx, sy, sz);
                return true;
            }

            // Fade into the pull toward the center.
            result = new Vector3d(px + (sx - px) * w, py + (sy - py) * w, pz + (sz - pz) * w);
            return true;
        }

        private void Warn(string message)
        {
            string body = sphere != null ? sphere.name : "unknown body";
            string line = "[PolyhedronPQS] " + body + ": " + message;
            lock (warned)
            {
                if (!warned.Add(line))
                {
                    return;
                }
            }
            Debug.LogWarning(line);
        }
    }
}

namespace Kopernicus.Configuration.ModLoader
{
    using PolyhedronPQS;

    /// <summary>Config node: add VertexHeightPolyhedron { ... } to a body's PQS / Mods node.</summary>
    [RequireConfigType(ConfigType.Node)]
    public class VertexHeightPolyhedron : ModLoader<PQSMod_VertexHeightPolyhedron>, IParserEventSubscriber
    {
        void IParserEventSubscriber.Apply(ConfigNode node) { }

        // Read the preset sub-nodes ourselves (any letter case) after the normal parse, so their
        // values always reach the mod, and keep a text copy that survives KSP cloning the planet.
        void IParserEventSubscriber.PostApply(ConfigNode node)
        {
            if (node == null || Mod == null)
            {
                return;
            }
            List<string> lines = new List<string>();
            foreach (ConfigNode child in node.nodes)
            {
                string key = child.name.ToLowerInvariant();
                IPresetSettings target = null;
                if (key == "ellipsoid") { target = Mod.ellipsoid = Mod.ellipsoid ?? new EllipsoidSettings(); }
                else if (key == "cylinder") { target = Mod.cylinder = Mod.cylinder ?? new CylinderSettings(); }
                else if (key == "hemisphere") { target = Mod.hemisphere = Mod.hemisphere ?? new HemisphereSettings(); }
                else if (key == "macaroni") { target = Mod.macaroni = Mod.macaroni ?? new MacaroniSettings(); }
                if (target == null)
                {
                    continue;
                }
                foreach (ConfigNode.Value v in child.values)
                {
                    PresetText.Apply(target, v.name, v.value);
                    lines.Add(key + "|" + v.name + "|" + v.value);
                }
            }
            Mod.presetText = string.Join("\n", lines.ToArray());
            Mod.Invalidate();
            Debug.Log("[PolyhedronPQS] parsed preset " + Mod.geometricPreset + " with " + lines.Count + " setting(s)"
                      + (Mod.geometricPreset == GeometricPreset.Ellipsoid
                          ? " (radii " + Mod.ellipsoid.radiusX + " / " + Mod.ellipsoid.radiusY + " / " + Mod.ellipsoid.radiusZ + ")"
                          : ""));
        }

        // Ready-made irregular shape: None, Cylinder, Hemisphere or Macaroni (any letter case).
        // When set, "faces" and "layout" are ignored; "fit", the rotations, smoothing and the
        // gravity options all still apply. Read as text so a typo is a warning, not a failed body.
        [ParserTarget("GeometricPreset")]
        [ParserTarget("geometricPreset")]
        public string Preset
        {
            get { return Mod.geometricPreset.ToString(); }
            set
            {
                GeometricPreset preset;
                if (!PresetText.TryPreset(value, out preset))
                {
                    Debug.LogWarning("[PolyhedronPQS] GeometricPreset = \"" + value + "\" is not a shape. "
                                     + "Use Cylinder, Hemisphere, Macaroni, Ellipsoid or None. No preset will be used.");
                }
                Mod.geometricPreset = preset;
            }
        }

        // Settings for each preset (only the one that matches GeometricPreset is used).
        [ParserTarget("Cylinder")]
        [ParserTarget("cylinder")]
        public PolyhedronCylinderNode CylinderConfig
        {
            get { return new PolyhedronCylinderNode(Mod.cylinder); }
            set { Mod.cylinder = value.Settings; }
        }

        [ParserTarget("Hemisphere")]
        [ParserTarget("hemisphere")]
        public PolyhedronHemisphereNode HemisphereConfig
        {
            get { return new PolyhedronHemisphereNode(Mod.hemisphere); }
            set { Mod.hemisphere = value.Settings; }
        }

        [ParserTarget("Macaroni")]
        [ParserTarget("macaroni")]
        public PolyhedronMacaroniNode MacaroniConfig
        {
            get { return new PolyhedronMacaroniNode(Mod.macaroni); }
            set { Mod.macaroni = value.Settings; }
        }

        [ParserTarget("Ellipsoid")]
        [ParserTarget("ellipsoid")]
        public PolyhedronEllipsoidNode EllipsoidConfig
        {
            get { return new PolyhedronEllipsoidNode(Mod.ellipsoid); }
            set { Mod.ellipsoid = value.Settings; }
        }

        // 0 to 5: how much of the shape is applied (5 = all, 2.5 = half, 0 = none). Read as text so a
        // typo is a warning rather than a failed body.
        [ParserTarget("uniformity")]
        [ParserTarget("Uniformity")]
        public string Uniformity
        {
            get { return Mod.uniformity.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            set
            {
                double u = Mod.uniformity;
                string problem;
                if (PresetText.ToDouble("uniformity", value, ref u, out problem))
                {
                    Mod.uniformity = u;
                }
                else
                {
                    Debug.LogWarning("[PolyhedronPQS] " + problem + ", using " + PQSMod_VertexHeightPolyhedron.MaxUniformity);
                }
            }
        }

        // Number of faces (4 to 30). 0 / below 4 / above 30 = plain sphere.
        [ParserTarget("faces")]
        public NumericParser<Int32> Faces
        {
            get { return Mod.faces; }
            set { Mod.faces = value; }
        }

        // Auto, Prism or Spread
        [ParserTarget("layout")]
        public EnumParser<PolyhedronLayout> Layout
        {
            get { return Mod.layout; }
            set { Mod.layout = value.Value; }
        }

        // Circumradius (corners touch the radius) or Inradius (face centers touch the radius)
        [ParserTarget("fit")]
        public EnumParser<PolyhedronFit> Fit
        {
            get { return Mod.fit; }
            set { Mod.fit = value.Value; }
        }

        // Orientation, Euler angles in degrees
        [ParserTarget("rotationX")]
        public NumericParser<Double> RotationX
        {
            get { return Mod.rotationX; }
            set { Mod.rotationX = value; }
        }

        [ParserTarget("rotationY")]
        public NumericParser<Double> RotationY
        {
            get { return Mod.rotationY; }
            set { Mod.rotationY = value; }
        }

        [ParserTarget("rotationZ")]
        public NumericParser<Double> RotationZ
        {
            get { return Mod.rotationZ; }
            set { Mod.rotationZ = value; }
        }

        // Rounds the edges and corners of the mesh. 0 = sharp, up to 0.5.
        [ParserTarget("smoothing")]
        public NumericParser<Double> Smoothing
        {
            get { return Mod.smoothing; }
            set { Mod.smoothing = value; }
        }

        // Gravity follows the surface normal and is equally strong everywhere on the surface.
        [ParserTarget("surfaceGravity")]
        public NumericParser<Boolean> SurfaceGravity
        {
            get { return Mod.surfaceGravity; }
            set { Mod.surfaceGravity = value; }
        }

        // How gradually gravity turns across an edge. 0 = abrupt, larger = wider blend.
        [ParserTarget("gravitySmoothing")]
        public NumericParser<Double> GravitySmoothing
        {
            get { return Mod.gravitySmoothing; }
            set { Mod.gravitySmoothing = value; }
        }

        // Height above the surface (as a fraction of the surface radius) over which
        // surface gravity fades into normal gravity.
        [ParserTarget("gravityFadeHeight")]
        public NumericParser<Double> GravityFadeHeight
        {
            get { return Mod.gravityFadeHeight; }
            set { Mod.gravityFadeHeight = value; }
        }

        // Multiplier on the body's surface gravity for surface gravity mode.
        [ParserTarget("surfaceGravityScale")]
        public NumericParser<Double> SurfaceGravityScale
        {
            get { return Mod.surfaceGravityScale; }
            set { Mod.surfaceGravityScale = value; }
        }

        // With surfaceGravity on, the free camera's "up" follows the gravity direction (default true).
        [ParserTarget("alignCamera")]
        public NumericParser<Boolean> AlignCamera
        {
            get { return Mod.alignCamera; }
            set { Mod.alignCamera = value; }
        }
    }
    // ------------------------------------------------------------------ settings sub-nodes
    // Each one is a node inside VertexHeightPolyhedron { }, named after its shape. The values live in
    // the plain settings classes in GeometricPresets.cs, so that file needs nothing from Kopernicus.
    //
    // The nodes read their values themselves instead of declaring one parser target per value. That way
    // the names are not case sensitive, "name = value" pairs sharing a line are all read, and a typo or a
    // word where a number should be is a warning in KSP.log (search "PolyhedronPQS") instead of an
    // exception that stops the whole body from loading.

    [RequireConfigType(ConfigType.Node)]
    public abstract class PolyhedronPresetNode<T> : IParserEventSubscriber where T : class, IPresetSettings, new()
    {
        public readonly T Settings;

        protected PolyhedronPresetNode()
        {
            Settings = new T();
        }

        protected PolyhedronPresetNode(T settings)
        {
            Settings = settings ?? new T();
        }

        protected abstract string NodeName { get; }

        public void Apply(ConfigNode node)
        {
            if (node == null)
            {
                return;
            }
            foreach (ConfigNode.Value value in node.values)
            {
                foreach (string problem in PresetText.Apply(Settings, value.name, value.value))
                {
                    Debug.LogWarning("[PolyhedronPQS] " + NodeName + " { }: " + problem);
                }
            }
        }

        public void PostApply(ConfigNode node)
        {
        }
    }

    /// <summary>Cylinder { verticalRadius, steps }</summary>
    [RequireConfigType(ConfigType.Node)]
    public class PolyhedronCylinderNode : PolyhedronPresetNode<CylinderSettings>
    {
        public PolyhedronCylinderNode() { }
        public PolyhedronCylinderNode(CylinderSettings settings) : base(settings) { }
        protected override string NodeName { get { return "Cylinder"; } }
    }

    /// <summary>Hemisphere { verticalRadius, baseDepth, steps }</summary>
    [RequireConfigType(ConfigType.Node)]
    public class PolyhedronHemisphereNode : PolyhedronPresetNode<HemisphereSettings>
    {
        public PolyhedronHemisphereNode() { }
        public PolyhedronHemisphereNode(HemisphereSettings settings) : base(settings) { }
        protected override string NodeName { get { return "Hemisphere"; } }
    }

    /// <summary>Macaroni { angle, length, verticalRadius, steps, roundEnds }</summary>
    [RequireConfigType(ConfigType.Node)]
    public class PolyhedronMacaroniNode : PolyhedronPresetNode<MacaroniSettings>
    {
        public PolyhedronMacaroniNode() { }
        public PolyhedronMacaroniNode(MacaroniSettings settings) : base(settings) { }
        protected override string NodeName { get { return "Macaroni"; } }
    }

    /// <summary>Ellipsoid { radiusX, radiusY, radiusZ, endNoise, endNoiseFrequency, endNoiseStart, seed }</summary>
    [RequireConfigType(ConfigType.Node)]
    public class PolyhedronEllipsoidNode : PolyhedronPresetNode<EllipsoidSettings>
    {
        public PolyhedronEllipsoidNode() { }
        public PolyhedronEllipsoidNode(EllipsoidSettings settings) : base(settings) { }
        protected override string NodeName { get { return "Ellipsoid"; } }
    }
}
