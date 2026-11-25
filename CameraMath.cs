using System;
using System.Windows.Media.Media3D;

namespace ViewCubeOverlay
{
  /// <summary>
  /// Вспомогательная математика для позиционирования камеры Renga / WPF-куба.
  /// </summary>
  internal static class CameraMath
  {
    public static double Distance(Renga.FloatPoint3D a, Renga.FloatPoint3D b)
    {
      double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
      return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static double DistanceSquared(Renga.FloatPoint3D a, Renga.FloatPoint3D b)
    {
      double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
      return dx * dx + dy * dy + dz * dz;
    }

    public static double DistanceSquared(Renga.FloatVector3D a, Renga.FloatVector3D b)
    {
      double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
      return dx * dx + dy * dy + dz * dz;
    }

    public static double SmoothStep(double t)
    {
      t = Math.Min(1.0, Math.Max(0.0, t));
      return t * t * (3.0 - 2.0 * t);
    }

    public static Renga.FloatPoint3D Lerp(Renga.FloatPoint3D a, Renga.FloatPoint3D b, double t)
    {
      return new Renga.FloatPoint3D
      {
        X = (float)(a.X + (b.X - a.X) * t),
        Y = (float)(a.Y + (b.Y - a.Y) * t),
        Z = (float)(a.Z + (b.Z - a.Z) * t)
      };
    }

    public static Renga.FloatVector3D Lerp(Renga.FloatVector3D a, Renga.FloatVector3D b, double t)
    {
      return new Renga.FloatVector3D
      {
        X = (float)(a.X + (b.X - a.X) * t),
        Y = (float)(a.Y + (b.Y - a.Y) * t),
        Z = (float)(a.Z + (b.Z - a.Z) * t)
      };
    }

    /// <summary>
    /// Slerp единичных направлений; при почти противоположных векторах — fallback на nlerp.
    /// </summary>
    public static Vector3D Slerp(Vector3D a, Vector3D b, double t)
    {
      a.Normalize();
      b.Normalize();

      double dot = Vector3D.DotProduct(a, b);
      if (dot > 0.9995)
      {
        var n = a + (b - a) * t;
        n.Normalize();
        return n;
      }

      if (dot < -0.9995)
      {
        var axis = Vector3D.CrossProduct(a, Math.Abs(a.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0));
        axis.Normalize();
        var mid = RotateAroundAxis(a, axis, Math.PI * 0.5);
        if (t < 0.5)
          return Slerp(a, mid, t * 2.0);
        return Slerp(mid, b, (t - 0.5) * 2.0);
      }

      dot = Math.Min(1.0, Math.Max(-1.0, dot));
      double theta = Math.Acos(dot);
      double sinTheta = Math.Sin(theta);
      double w1 = Math.Sin((1.0 - t) * theta) / sinTheta;
      double w2 = Math.Sin(t * theta) / sinTheta;
      var result = a * w1 + b * w2;
      result.Normalize();
      return result;
    }

    public static Vector3D RotateAroundAxis(Vector3D vector, Vector3D axis, double angle)
    {
      axis.Normalize();
      double cos = Math.Cos(angle);
      double sin = Math.Sin(angle);

      return vector * cos +
             Vector3D.CrossProduct(axis, vector) * sin +
             axis * (Vector3D.DotProduct(axis, vector) * (1 - cos));
    }

    public static Renga.FloatPoint3D Offset(Renga.FloatPoint3D p, double dx, double dy, double dz)
    {
      return new Renga.FloatPoint3D
      {
        X = (float)(p.X + dx),
        Y = (float)(p.Y + dy),
        Z = (float)(p.Z + dz)
      };
    }

    public static Renga.FloatVector3D Vec(double x, double y, double z)
    {
      return new Renga.FloatVector3D { X = (float)x, Y = (float)y, Z = (float)z };
    }

    public static bool TryGetFaceCamera(
      ViewCubeControl.CubeFace face,
      Renga.FloatPoint3D focus,
      double distance,
      out Renga.FloatPoint3D position,
      out Renga.FloatVector3D up)
    {
      position = focus;
      up = Vec(0, 0, 1);

      switch (face)
      {
        case ViewCubeControl.CubeFace.Top:
          position = Offset(focus, 0, 0, distance);
          up = Vec(0, 1, 0);
          return true;
        case ViewCubeControl.CubeFace.Bottom:
          position = Offset(focus, 0, 0, -distance);
          up = Vec(0, 1, 0);
          return true;
        case ViewCubeControl.CubeFace.Front:
          position = Offset(focus, 0, -distance, 0);
          return true;
        case ViewCubeControl.CubeFace.Back:
          position = Offset(focus, 0, distance, 0);
          return true;
        case ViewCubeControl.CubeFace.Right:
          position = Offset(focus, distance, 0, 0);
          return true;
        case ViewCubeControl.CubeFace.Left:
          position = Offset(focus, -distance, 0, 0);
          return true;
        default:
          return false;
      }
    }

    public static bool TryGetCornerDirection(ViewCubeControl.CubeCorner corner, out Vector3D dir)
    {
      switch (corner)
      {
        case ViewCubeControl.CubeCorner.TopFrontRight: dir = new Vector3D(1, -1, 1); break;
        case ViewCubeControl.CubeCorner.TopFrontLeft: dir = new Vector3D(-1, -1, 1); break;
        case ViewCubeControl.CubeCorner.TopBackRight: dir = new Vector3D(1, 1, 1); break;
        case ViewCubeControl.CubeCorner.TopBackLeft: dir = new Vector3D(-1, 1, 1); break;
        case ViewCubeControl.CubeCorner.BottomFrontRight: dir = new Vector3D(1, -1, -1); break;
        case ViewCubeControl.CubeCorner.BottomFrontLeft: dir = new Vector3D(-1, -1, -1); break;
        case ViewCubeControl.CubeCorner.BottomBackRight: dir = new Vector3D(1, 1, -1); break;
        case ViewCubeControl.CubeCorner.BottomBackLeft: dir = new Vector3D(-1, 1, -1); break;
        default:
          dir = new Vector3D();
          return false;
      }

      dir.Normalize();
      return true;
    }

    public static bool TryGetEdgeDirection(ViewCubeControl.CubeEdge edge, out Vector3D dir)
    {
      switch (edge)
      {
        case ViewCubeControl.CubeEdge.TopFront: dir = new Vector3D(0, -1, 1); break;
        case ViewCubeControl.CubeEdge.TopBack: dir = new Vector3D(0, 1, 1); break;
        case ViewCubeControl.CubeEdge.TopLeft: dir = new Vector3D(-1, 0, 1); break;
        case ViewCubeControl.CubeEdge.TopRight: dir = new Vector3D(1, 0, 1); break;
        case ViewCubeControl.CubeEdge.BottomFront: dir = new Vector3D(0, -1, -1); break;
        case ViewCubeControl.CubeEdge.BottomBack: dir = new Vector3D(0, 1, -1); break;
        case ViewCubeControl.CubeEdge.BottomLeft: dir = new Vector3D(-1, 0, -1); break;
        case ViewCubeControl.CubeEdge.BottomRight: dir = new Vector3D(1, 0, -1); break;
        case ViewCubeControl.CubeEdge.FrontLeft: dir = new Vector3D(-1, -1, 0); break;
        case ViewCubeControl.CubeEdge.FrontRight: dir = new Vector3D(1, -1, 0); break;
        case ViewCubeControl.CubeEdge.BackLeft: dir = new Vector3D(-1, 1, 0); break;
        case ViewCubeControl.CubeEdge.BackRight: dir = new Vector3D(1, 1, 0); break;
        default:
          dir = new Vector3D();
          return false;
      }

      dir.Normalize();
      return true;
    }

    /// <summary>
    /// Грань, на которую смотрит камера «в лицо». Для изометрии (dot &lt; threshold) — null.
    /// lookDirection: от камеры к focus.
    /// </summary>
    public static ViewCubeControl.CubeFace? PickDominantFace(Vector3D lookDirection, double threshold = 0.75)
    {
      if (lookDirection.LengthSquared < 1e-12)
        return null;

      lookDirection.Normalize();
      // Нормаль грани, обращённая к камере, совпадает с -lookDirection при ортогональном виде.
      var towardCamera = -lookDirection;

      ViewCubeControl.CubeFace best = ViewCubeControl.CubeFace.Front;
      double bestDot = double.NegativeInfinity;

      TryFace(ViewCubeControl.CubeFace.Top, new Vector3D(0, 0, 1), towardCamera, ref best, ref bestDot);
      TryFace(ViewCubeControl.CubeFace.Bottom, new Vector3D(0, 0, -1), towardCamera, ref best, ref bestDot);
      TryFace(ViewCubeControl.CubeFace.Front, new Vector3D(0, -1, 0), towardCamera, ref best, ref bestDot);
      TryFace(ViewCubeControl.CubeFace.Back, new Vector3D(0, 1, 0), towardCamera, ref best, ref bestDot);
      TryFace(ViewCubeControl.CubeFace.Right, new Vector3D(1, 0, 0), towardCamera, ref best, ref bestDot);
      TryFace(ViewCubeControl.CubeFace.Left, new Vector3D(-1, 0, 0), towardCamera, ref best, ref bestDot);

      if (bestDot < threshold)
        return null;

      return best;
    }

    private static void TryFace(
      ViewCubeControl.CubeFace face,
      Vector3D normal,
      Vector3D towardCamera,
      ref ViewCubeControl.CubeFace best,
      ref double bestDot)
    {
      double dot = Vector3D.DotProduct(towardCamera, normal);
      if (dot > bestDot)
      {
        bestDot = dot;
        best = face;
      }
    }
  }
}
