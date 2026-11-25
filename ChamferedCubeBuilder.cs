using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace ViewCubeOverlay
{
  /// <summary>
  /// Фасочный навигационный куб: семейство усечённого кубооктаэдра.
  /// 6 восьмиугольников (грани), 12 прямоугольников (рёбра), 8 шестиугольников (углы).
  /// Все 48 вершин — знаковые перестановки (±p, ±q, ±r); грани плоские, общая топология.
  /// </summary>
  internal sealed class ChamferedFacet
  {
    public Point3D[] Positions;
    public Vector3D Outward;
    public ViewCubeControl.CubeFace? Face;
    public ViewCubeControl.CubeEdge? Edge;
    public ViewCubeControl.CubeCorner? Corner;
    public string Label;
  }

  internal static class ChamferedCubeBuilder
  {
    /// <summary>
    /// Глубина равномерной фаски относительно половины куба.
    /// q = r·(1 − t) — вылет октагона к ребру, ширина 12 фасок = t·r.
    /// p = r·(1 − 2t) — срез угла той же глубины; шестиугольники компактные.
    /// Правильный октагон p = q(√2 − 1) давал огромные угловые «носы».
    /// </summary>
    public const double ChamferFraction = 0.16;

    public static List<ChamferedFacet> Build(double s)
    {
      if (s < 1e-6)
        s = 0.78;

      // r — плоскость подписи (грань куба).
      // q — вылет октагона к ребру; r − q = одинаковая ширина всех 12 фасок.
      // p — ближе к q, чем у правильного октагона: короткие 45° срезы, крупные плоские грани.
      double r = s;
      double t = ChamferFraction;
      if (t < 0.12)
        t = 0.12;
      if (t > 0.32)
        t = 0.32;
      double q = r * (1.0 - t);
      double p = r * (1.0 - 2.0 * t);
      if (p < r * 0.08)
        p = r * 0.08;
      if (p >= q)
        p = q * 0.85;

      var faces = BuildOctagons(r, p, q);
      var edges = BuildRectangles(p, q, r);
      var corners = BuildHexagons(p, q, r);

      var all = new List<ChamferedFacet>(26);
      all.AddRange(faces);
      all.AddRange(edges);
      all.AddRange(corners);
      for (int i = 0; i < all.Count; i++)
        OrientOutward(all[i]);

      return all;
    }

    private static ChamferedFacet[] BuildOctagons(double r, double p, double q)
    {
      return new[]
      {
        MakeOctagon(ViewCubeControl.CubeFace.Top, new Vector3D(0, 0, 1), "Сверху", r, p, q),
        MakeOctagon(ViewCubeControl.CubeFace.Bottom, new Vector3D(0, 0, -1), "Снизу", r, p, q),
        MakeOctagon(ViewCubeControl.CubeFace.Front, new Vector3D(0, -1, 0), "Спереди", r, p, q),
        MakeOctagon(ViewCubeControl.CubeFace.Back, new Vector3D(0, 1, 0), "Сзади", r, p, q),
        MakeOctagon(ViewCubeControl.CubeFace.Right, new Vector3D(1, 0, 0), "Справа", r, p, q),
        MakeOctagon(ViewCubeControl.CubeFace.Left, new Vector3D(-1, 0, 0), "Слева", r, p, q)
      };
    }

    private static ChamferedFacet[] BuildRectangles(double p, double q, double r)
    {
      // Оси: 0=X, 1=Y, 2=Z. Знак — внешняя сторона грани.
      return new[]
      {
        MakeRectangle(ViewCubeControl.CubeEdge.TopFront, 2, 1, 1, -1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.TopBack, 2, 1, 1, 1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.TopRight, 2, 0, 1, 1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.TopLeft, 2, 0, 1, -1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.BottomFront, 2, 1, -1, -1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.BottomBack, 2, 1, -1, 1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.BottomRight, 2, 0, -1, 1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.BottomLeft, 2, 0, -1, -1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.FrontRight, 1, 0, -1, 1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.FrontLeft, 1, 0, -1, -1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.BackRight, 1, 0, 1, 1, p, q, r),
        MakeRectangle(ViewCubeControl.CubeEdge.BackLeft, 1, 0, 1, -1, p, q, r)
      };
    }

    private static ChamferedFacet[] BuildHexagons(double p, double q, double r)
    {
      return new[]
      {
        MakeCornerHex(ViewCubeControl.CubeCorner.TopFrontRight, 1, -1, 1, p, q, r),
        MakeCornerHex(ViewCubeControl.CubeCorner.TopFrontLeft, -1, -1, 1, p, q, r),
        MakeCornerHex(ViewCubeControl.CubeCorner.TopBackRight, 1, 1, 1, p, q, r),
        MakeCornerHex(ViewCubeControl.CubeCorner.TopBackLeft, -1, 1, 1, p, q, r),
        MakeCornerHex(ViewCubeControl.CubeCorner.BottomFrontRight, 1, -1, -1, p, q, r),
        MakeCornerHex(ViewCubeControl.CubeCorner.BottomFrontLeft, -1, -1, -1, p, q, r),
        MakeCornerHex(ViewCubeControl.CubeCorner.BottomBackRight, 1, 1, -1, p, q, r),
        MakeCornerHex(ViewCubeControl.CubeCorner.BottomBackLeft, -1, 1, -1, p, q, r)
      };
    }

    private static ChamferedFacet MakeOctagon(
      ViewCubeControl.CubeFace face,
      Vector3D normal,
      string label,
      double r,
      double p,
      double q)
    {
      normal.Normalize();
      GetFaceBasis(normal, out Vector3D right, out Vector3D up);
      var center = new Point3D(normal.X * r, normal.Y * r, normal.Z * r);
      var local = new[]
      {
        (q, p),
        (p, q),
        (-p, q),
        (-q, p),
        (-q, -p),
        (-p, -q),
        (p, -q),
        (q, -p)
      };

      var pts = new Point3D[8];
      for (int i = 0; i < 8; i++)
        pts[i] = center + right * local[i].Item1 + up * local[i].Item2;

      return new ChamferedFacet
      {
        Positions = pts,
        Outward = normal,
        Face = face,
        Label = label
      };
    }

    private static ChamferedFacet MakeRectangle(
      ViewCubeControl.CubeEdge edge,
      int axisA,
      int axisB,
      int signA,
      int signB,
      double p,
      double q,
      double r)
    {
      int axisT = 3 - axisA - axisB;
      var pts = new[]
      {
        AxisPoint(axisA, signA * r, axisB, signB * q, axisT, p),
        AxisPoint(axisA, signA * r, axisB, signB * q, axisT, -p),
        AxisPoint(axisA, signA * q, axisB, signB * r, axisT, -p),
        AxisPoint(axisA, signA * q, axisB, signB * r, axisT, p)
      };
      var outward = AxisVector(axisA, signA) + AxisVector(axisB, signB);
      pts = OrderAround(pts, outward);
      if (outward.LengthSquared > 1e-12)
        outward.Normalize();

      return new ChamferedFacet
      {
        Positions = pts,
        Outward = outward,
        Edge = edge
      };
    }

    private static ChamferedFacet MakeCornerHex(
      ViewCubeControl.CubeCorner corner,
      int sx,
      int sy,
      int sz,
      double p,
      double q,
      double r)
    {
      // Шесть перестановок (p,q,r) с фиксированными знаками октанта.
      // Компланарны: sx·x + sy·y + sz·z = p + q + r.
      var pts = new[]
      {
        new Point3D(sx * p, sy * q, sz * r),
        new Point3D(sx * p, sy * r, sz * q),
        new Point3D(sx * q, sy * p, sz * r),
        new Point3D(sx * q, sy * r, sz * p),
        new Point3D(sx * r, sy * p, sz * q),
        new Point3D(sx * r, sy * q, sz * p)
      };
      var outward = new Vector3D(sx, sy, sz);
      pts = OrderAround(pts, outward);
      outward.Normalize();

      return new ChamferedFacet
      {
        Positions = pts,
        Outward = outward,
        Corner = corner
      };
    }

    private static void GetFaceBasis(Vector3D normal, out Vector3D right, out Vector3D up)
    {
      up = Math.Abs(normal.Z) > 0.5 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
      right = Vector3D.CrossProduct(up, normal);
      right.Normalize();
      up = Vector3D.CrossProduct(normal, right);
      up.Normalize();
    }

    private static Point3D AxisPoint(int axisA, double a, int axisB, double b, int axisT, double t)
    {
      var p = new Point3D();
      SetAxis(ref p, axisA, a);
      SetAxis(ref p, axisB, b);
      SetAxis(ref p, axisT, t);
      return p;
    }

    private static void SetAxis(ref Point3D p, int axis, double value)
    {
      if (axis == 0)
        p.X = value;
      else if (axis == 1)
        p.Y = value;
      else
        p.Z = value;
    }

    private static Vector3D AxisVector(int axis, int sign)
    {
      if (axis == 0)
        return new Vector3D(sign, 0, 0);
      if (axis == 1)
        return new Vector3D(0, sign, 0);
      return new Vector3D(0, 0, sign);
    }

    private static Point3D[] OrderAround(Point3D[] pts, Vector3D axis)
    {
      if (pts == null || pts.Length < 3)
        return pts;

      if (axis.LengthSquared < 1e-16)
        axis = Newell(pts);
      else
        axis.Normalize();

      var c = new Point3D();
      for (int i = 0; i < pts.Length; i++)
      {
        c.X += pts[i].X;
        c.Y += pts[i].Y;
        c.Z += pts[i].Z;
      }

      c.X /= pts.Length;
      c.Y /= pts.Length;
      c.Z /= pts.Length;

      Vector3D refV = pts[0] - c;
      refV = refV - axis * Vector3D.DotProduct(refV, axis);
      if (refV.LengthSquared < 1e-16)
        refV = Vector3D.CrossProduct(axis, Math.Abs(axis.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0));
      if (refV.LengthSquared < 1e-16)
        return pts;
      refV.Normalize();
      var bitan = Vector3D.CrossProduct(axis, refV);
      if (bitan.LengthSquared < 1e-16)
        return pts;
      bitan.Normalize();

      var order = new int[pts.Length];
      var ang = new double[pts.Length];
      for (int i = 0; i < pts.Length; i++)
      {
        order[i] = i;
        Vector3D v = pts[i] - c;
        ang[i] = Math.Atan2(Vector3D.DotProduct(v, bitan), Vector3D.DotProduct(v, refV));
      }

      Array.Sort(ang, order);
      var result = new Point3D[pts.Length];
      for (int i = 0; i < pts.Length; i++)
        result[i] = pts[order[i]];
      return result;
    }

    private static void OrientOutward(ChamferedFacet facet)
    {
      if (facet?.Positions == null || facet.Positions.Length < 3)
        return;

      var n = Newell(facet.Positions);
      if (Vector3D.DotProduct(n, facet.Outward) < 0)
        Array.Reverse(facet.Positions);
    }

    private static Vector3D Newell(Point3D[] p)
    {
      double nx = 0;
      double ny = 0;
      double nz = 0;
      for (int i = 0; i < p.Length; i++)
      {
        var a = p[i];
        var b = p[(i + 1) % p.Length];
        nx += (a.Y - b.Y) * (a.Z + b.Z);
        ny += (a.Z - b.Z) * (a.X + b.X);
        nz += (a.X - b.X) * (a.Y + b.Y);
      }

      var n = new Vector3D(nx, ny, nz);
      if (n.LengthSquared > 1e-18)
        n.Normalize();
      return n;
    }
  }
}
