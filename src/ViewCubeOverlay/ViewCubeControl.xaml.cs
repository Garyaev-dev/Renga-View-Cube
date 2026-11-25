using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ViewCubeOverlay
{
  public partial class ViewCubeControl : UserControl
  {
    private const double CubeHalfSize = 0.78;
    private const double CameraDistance = 8.4;
    private const double CameraFov = 28.0;
    private const double ClickSlopPx = 6.0;
    private const double CornerHalfScale = 0.10;
    private const double EdgeHalfThickness = 0.055;
    private const double EdgeHalfLength = 0.28;

    private static readonly Typeface LabelTypeface = new Typeface(
      new FontFamily("Segoe UI"),
      FontStyles.Normal,
      FontWeights.Bold,
      FontStretches.Normal);

    private readonly Dictionary<GeometryModel3D, CubeFace> _faceByModel = new Dictionary<GeometryModel3D, CubeFace>();
    private readonly Dictionary<CubeFace, GeometryModel3D> _modelByFace = new Dictionary<CubeFace, GeometryModel3D>();
    private readonly Dictionary<GeometryModel3D, CubeCorner> _cornerByModel = new Dictionary<GeometryModel3D, CubeCorner>();
    private readonly Dictionary<GeometryModel3D, CubeEdge> _edgeByModel = new Dictionary<GeometryModel3D, CubeEdge>();
    private readonly Dictionary<GeometryModel3D, Material> _baseMaterials = new Dictionary<GeometryModel3D, Material>();
    private readonly Dictionary<GeometryModel3D, Material> _activeMaterials = new Dictionary<GeometryModel3D, Material>();
    private readonly Dictionary<GeometryModel3D, Material> _hoverMaterials = new Dictionary<GeometryModel3D, Material>();

    private PerspectiveCamera _camera;
    private DirectionalLight _dirLight;

    private bool _pressed;
    private bool _orbiting;
    private Point _lastPos;
    private Point _downPos;
    private CubeFace? _hitFace;
    private CubeCorner? _hitCorner;
    private CubeEdge? _hitEdge;
    private double _yawRad = Math.PI / 4;
    private double _pitchRad = -Math.PI / 6;

    private GeometryModel3D _hoveredModel;
    private CubeFace? _activeFace;
    private Material _hotspotHoverMaterial;
    private ViewCubeSettings _appearance = new ViewCubeSettings();
    private Vector3D _lastLook = new Vector3D(-1, 1, -1);
    private Vector3D _lastUp = new Vector3D(0, 0, 1);

    public event EventHandler HomeClicked;
    public event EventHandler<int> ScaleStepRequested;

    public enum CubeFace
    {
      Top,
      Bottom,
      Front,
      Back,
      Right,
      Left
    }

    public enum CubeCorner
    {
      TopFrontRight,
      TopFrontLeft,
      TopBackRight,
      TopBackLeft,
      BottomFrontRight,
      BottomFrontLeft,
      BottomBackRight,
      BottomBackLeft
    }

    public enum CubeEdge
    {
      TopFront,
      TopBack,
      TopLeft,
      TopRight,
      BottomFront,
      BottomBack,
      BottomLeft,
      BottomRight,
      FrontLeft,
      FrontRight,
      BackLeft,
      BackRight
    }

    public class ViewRotationEventArgs : EventArgs
    {
      public Vector3D Direction { get; }

      public ViewRotationEventArgs(Vector3D direction)
      {
        Direction = direction;
      }
    }

    public ViewCubeControl()
    {
      InitializeComponent();
      BuildScene();
    }

    /// <summary>
    /// Прячет живой Viewport3D на время DragMove заголовка: в AllowsTransparency
    /// он рисует непрозрачный белый квад. Layout не меняется (Hidden, не Collapsed).
    /// </summary>
    public void SetViewportHidden(bool hidden)
    {
      if (Viewport == null)
        return;
      Viewport.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
    }

    public void ApplyAppearance(ViewCubeSettings settings)
    {
      _appearance = settings ?? new ViewCubeSettings();
      BuildScene();
      UpdateWpfCamera(_lastLook, _lastUp);
      SetActiveFace(CameraMath.PickDominantFace(_lastLook));
    }

    public event EventHandler<CubeFace> FaceClicked;
    public event EventHandler<CubeCorner> CornerClicked;
    public event EventHandler<CubeEdge> EdgeClicked;
    public event EventHandler<ViewRotationEventArgs> ViewRotated;

    public void UpdateCameraSync(Vector3D lookDirection, Vector3D upDirection)
    {
      UpdateWpfCamera(lookDirection, upDirection);

      _pitchRad = Math.Asin(Math.Min(1.0, Math.Max(-1.0, lookDirection.Z)));
      if (Math.Abs(lookDirection.Z) < 0.999)
        _yawRad = Math.Atan2(lookDirection.Y, lookDirection.X);

      SetActiveFace(CameraMath.PickDominantFace(lookDirection));
    }

    private void BuildScene()
    {
      Viewport.Children.Clear();
      _faceByModel.Clear();
      _modelByFace.Clear();
      _cornerByModel.Clear();
      _edgeByModel.Clear();
      _baseMaterials.Clear();
      _activeMaterials.Clear();
      _hoverMaterials.Clear();
      _activeFace = null;
      _hoveredModel = null;

      var look = _lastLook.LengthSquared > 1e-12 ? _lastLook : new Vector3D(-1, 1, -1);
      look.Normalize();
      var up = _lastUp.LengthSquared > 1e-12 ? _lastUp : new Vector3D(0, 0, 1);
      up.Normalize();
      _camera = new PerspectiveCamera
      {
        Position = new Point3D(-look.X * CameraDistance, -look.Y * CameraDistance, -look.Z * CameraDistance),
        LookDirection = look,
        UpDirection = up,
        FieldOfView = CameraFov
      };
      Viewport.Camera = _camera;

      var style = _appearance ?? new ViewCubeSettings();
      bool cad = style.IsChamferedShape();
      var ambient = new AmbientLight(cad ? Color.FromRgb(236, 237, 239) : Color.FromRgb(230, 230, 230));
      _dirLight = new DirectionalLight(cad ? Color.FromRgb(198, 200, 204) : Color.FromRgb(255, 255, 255), look);
      Viewport.Children.Add(new ModelVisual3D { Content = ambient });
      Viewport.Children.Add(new ModelVisual3D { Content = _dirLight });

      var group = new Model3DGroup();
      double s = CubeHalfSize;

      _hotspotHoverMaterial = new DiffuseMaterial(new SolidColorBrush(style.HoverColor))
      {
        AmbientColor = Colors.White
      };

      if (cad)
        BuildChamferedCube(group, s, style);
      else
        BuildSharpCube(group, s, style);

      Viewport.Children.Add(new ModelVisual3D { Content = group });
    }

    private void BuildSharpCube(Model3DGroup group, double s, ViewCubeSettings style)
    {
      AddFace(group, CubeFace.Top, "Сверху", new SolidColorBrush(style.FaceTop), s);
      AddFace(group, CubeFace.Bottom, "Снизу", new SolidColorBrush(style.FaceBottom), s);
      AddFace(group, CubeFace.Front, "Спереди", new SolidColorBrush(style.FaceFront), s);
      AddFace(group, CubeFace.Back, "Сзади", new SolidColorBrush(style.FaceBack), s);
      AddFace(group, CubeFace.Right, "Справа", new SolidColorBrush(style.FaceRight), s);
      AddFace(group, CubeFace.Left, "Слева", new SolidColorBrush(style.FaceLeft), s);

      var hotspot = CreateHotspotMaterial(style.HotspotColor);

      AddCorner(group, CubeCorner.TopFrontRight, new Point3D(s, -s, s), s, hotspot);
      AddCorner(group, CubeCorner.TopFrontLeft, new Point3D(-s, -s, s), s, hotspot);
      AddCorner(group, CubeCorner.TopBackRight, new Point3D(s, s, s), s, hotspot);
      AddCorner(group, CubeCorner.TopBackLeft, new Point3D(-s, s, s), s, hotspot);
      AddCorner(group, CubeCorner.BottomFrontRight, new Point3D(s, -s, -s), s, hotspot);
      AddCorner(group, CubeCorner.BottomFrontLeft, new Point3D(-s, -s, -s), s, hotspot);
      AddCorner(group, CubeCorner.BottomBackRight, new Point3D(s, s, -s), s, hotspot);
      AddCorner(group, CubeCorner.BottomBackLeft, new Point3D(-s, s, -s), s, hotspot);

      AddEdge(group, CubeEdge.TopFront, new Point3D(0, -s, s), EdgeHalfLength * s, EdgeHalfThickness * s, EdgeHalfThickness * s, hotspot);
      AddEdge(group, CubeEdge.TopBack, new Point3D(0, s, s), EdgeHalfLength * s, EdgeHalfThickness * s, EdgeHalfThickness * s, hotspot);
      AddEdge(group, CubeEdge.TopLeft, new Point3D(-s, 0, s), EdgeHalfThickness * s, EdgeHalfLength * s, EdgeHalfThickness * s, hotspot);
      AddEdge(group, CubeEdge.TopRight, new Point3D(s, 0, s), EdgeHalfThickness * s, EdgeHalfLength * s, EdgeHalfThickness * s, hotspot);
      AddEdge(group, CubeEdge.BottomFront, new Point3D(0, -s, -s), EdgeHalfLength * s, EdgeHalfThickness * s, EdgeHalfThickness * s, hotspot);
      AddEdge(group, CubeEdge.BottomBack, new Point3D(0, s, -s), EdgeHalfLength * s, EdgeHalfThickness * s, EdgeHalfThickness * s, hotspot);
      AddEdge(group, CubeEdge.BottomLeft, new Point3D(-s, 0, -s), EdgeHalfThickness * s, EdgeHalfLength * s, EdgeHalfThickness * s, hotspot);
      AddEdge(group, CubeEdge.BottomRight, new Point3D(s, 0, -s), EdgeHalfThickness * s, EdgeHalfLength * s, EdgeHalfThickness * s, hotspot);
      AddEdge(group, CubeEdge.FrontLeft, new Point3D(-s, -s, 0), EdgeHalfThickness * s, EdgeHalfThickness * s, EdgeHalfLength * s, hotspot);
      AddEdge(group, CubeEdge.FrontRight, new Point3D(s, -s, 0), EdgeHalfThickness * s, EdgeHalfThickness * s, EdgeHalfLength * s, hotspot);
      AddEdge(group, CubeEdge.BackLeft, new Point3D(-s, s, 0), EdgeHalfThickness * s, EdgeHalfThickness * s, EdgeHalfLength * s, hotspot);
      AddEdge(group, CubeEdge.BackRight, new Point3D(s, s, 0), EdgeHalfThickness * s, EdgeHalfThickness * s, EdgeHalfLength * s, hotspot);
    }

    private void BuildChamferedCube(Model3DGroup group, double s, ViewCubeSettings style)
    {
      var facets = ChamferedCubeBuilder.Build(s);
      foreach (var facet in facets)
      {
        if (facet.Face.HasValue)
          AddChamferedFace(group, facet, s, style);
        else
          AddChamferedHotspot(group, facet, style);
      }

      AddChamferedOutlines(group, facets, s);
    }

    private void AddChamferedFace(Model3DGroup group, ChamferedFacet facet, double s, ViewCubeSettings style)
    {
      var face = facet.Face.Value;
      GetFaceNormalAndTextBasis(face, out Vector3D normal, out Vector3D textRight, out Vector3D textUp);
      Point3D center = new Point3D(normal.X * s, normal.Y * s, normal.Z * s);

      var ring = facet.Positions;
      var uvs = new Point[ring.Length];
      for (int i = 0; i < ring.Length; i++)
        uvs[i] = ToFaceUv(ring[i], center, textRight, textUp, s);

      var mesh = CreateFanMesh(ring, uvs);
      var fill = FaceBrush(face, style);
      string label = facet.Label ?? "";
      if (style.IsChamferedShape() && !string.IsNullOrEmpty(label))
        label = label.ToUpper(CultureInfo.GetCultureInfo("ru-RU"));

      var textBrush = CreateTextBrush(label);
      var outlineBrush = CreateUvOutlineBrush(uvs);
      var baseMat = CreateFaceMaterial(fill, textBrush, outlineBrush);
      var activeMat = CreateFaceMaterial(new SolidColorBrush(style.ActiveFaceColor), textBrush, outlineBrush);
      var hoverMat = CreateFaceMaterial(new SolidColorBrush(style.HoverColor), textBrush, outlineBrush);

      var geom = new GeometryModel3D(mesh, baseMat);
      _faceByModel[geom] = face;
      _modelByFace[face] = geom;
      _baseMaterials[geom] = baseMat;
      _activeMaterials[geom] = activeMat;
      _hoverMaterials[geom] = hoverMat;
      group.Children.Add(geom);
    }

    private void AddChamferedHotspot(Model3DGroup group, ChamferedFacet facet, ViewCubeSettings style)
    {
      var uvs = ToPlanarUvs(facet.Positions, facet.Outward);
      var mesh = CreateFanMesh(facet.Positions, uvs);
      var outlineBrush = CreateUvOutlineBrush(uvs);
      var fill = CreateHotspotMaterial(style.HotspotColor);
      var material = new MaterialGroup();
      material.Children.Add(fill);
      material.Children.Add(new DiffuseMaterial(outlineBrush) { AmbientColor = Colors.White });
      var geom = new GeometryModel3D(mesh, material);
      _baseMaterials[geom] = material;

      if (facet.Edge.HasValue)
        _edgeByModel[geom] = facet.Edge.Value;
      else if (facet.Corner.HasValue)
        _cornerByModel[geom] = facet.Corner.Value;

      group.Children.Add(geom);
    }

    private static Brush FaceBrush(CubeFace face, ViewCubeSettings style)
    {
      switch (face)
      {
        case CubeFace.Top: return new SolidColorBrush(style.FaceTop);
        case CubeFace.Bottom: return new SolidColorBrush(style.FaceBottom);
        case CubeFace.Front: return new SolidColorBrush(style.FaceFront);
        case CubeFace.Back: return new SolidColorBrush(style.FaceBack);
        case CubeFace.Right: return new SolidColorBrush(style.FaceRight);
        case CubeFace.Left: return new SolidColorBrush(style.FaceLeft);
        default: return new SolidColorBrush(style.FaceFront);
      }
    }

    private static Point[] ToPlanarUvs(Point3D[] ring, Vector3D outward)
    {
      if (ring == null || ring.Length == 0)
        return Array.Empty<Point>();

      if (outward.LengthSquared < 1e-12)
        outward = new Vector3D(0, 0, 1);
      else
        outward.Normalize();

      var up = Math.Abs(outward.Z) > 0.5 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
      var right = Vector3D.CrossProduct(up, outward);
      if (right.LengthSquared < 1e-12)
        right = new Vector3D(1, 0, 0);
      right.Normalize();
      up = Vector3D.CrossProduct(outward, right);
      up.Normalize();

      var local = new Point[ring.Length];
      double minX = double.PositiveInfinity;
      double minY = double.PositiveInfinity;
      double maxX = double.NegativeInfinity;
      double maxY = double.NegativeInfinity;
      for (int i = 0; i < ring.Length; i++)
      {
        Vector3D v = ring[i] - new Point3D();
        double x = Vector3D.DotProduct(v, right);
        double y = Vector3D.DotProduct(v, up);
        local[i] = new Point(x, y);
        if (x < minX) minX = x;
        if (y < minY) minY = y;
        if (x > maxX) maxX = x;
        if (y > maxY) maxY = y;
      }

      double w = Math.Max(1e-8, maxX - minX);
      double h = Math.Max(1e-8, maxY - minY);
      var uvs = new Point[ring.Length];
      for (int i = 0; i < ring.Length; i++)
        uvs[i] = new Point((local[i].X - minX) / w, 1.0 - (local[i].Y - minY) / h);
      return uvs;
    }

    private static Brush CreateUvOutlineBrush(Point[] uvs)
    {
      const double canvas = 256.0;
      if (uvs == null || uvs.Length < 3)
      {
        var empty = new SolidColorBrush(Colors.Transparent);
        if (empty.CanFreeze)
          empty.Freeze();
        return empty;
      }

      var inset = InsetUvPolygon(uvs, 0.035);
      var geo = new StreamGeometry();
      using (var ctx = geo.Open())
      {
        ctx.BeginFigure(new Point(inset[0].X * canvas, inset[0].Y * canvas), true, true);
        for (int i = 1; i < inset.Length; i++)
          ctx.LineTo(new Point(inset[i].X * canvas, inset[i].Y * canvas), true, false);
      }

      if (geo.CanFreeze)
        geo.Freeze();

      var pen = new Pen(new SolidColorBrush(Color.FromRgb(72, 76, 82)), 5.5)
      {
        LineJoin = PenLineJoin.Miter,
        MiterLimit = 6,
        StartLineCap = PenLineCap.Flat,
        EndLineCap = PenLineCap.Flat
      };
      if (pen.CanFreeze)
        pen.Freeze();

      var drawing = new DrawingGroup();
      using (var dc = drawing.Open())
      {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, canvas, canvas));
        dc.DrawGeometry(null, pen, geo);
      }

      if (drawing.CanFreeze)
        drawing.Freeze();

      var brush = new DrawingBrush(drawing)
      {
        Stretch = Stretch.Fill,
        Viewport = new Rect(0, 0, 1, 1),
        ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
        TileMode = TileMode.None
      };
      RenderOptions.SetCachingHint(brush, CachingHint.Cache);
      if (brush.CanFreeze)
        brush.Freeze();
      return brush;
    }

    private static Point[] InsetUvPolygon(Point[] uvs, double amount)
    {
      double cx = 0;
      double cy = 0;
      for (int i = 0; i < uvs.Length; i++)
      {
        cx += uvs[i].X;
        cy += uvs[i].Y;
      }

      cx /= uvs.Length;
      cy /= uvs.Length;
      var result = new Point[uvs.Length];
      for (int i = 0; i < uvs.Length; i++)
      {
        double dx = uvs[i].X - cx;
        double dy = uvs[i].Y - cy;
        result[i] = new Point(cx + dx * (1.0 - amount), cy + dy * (1.0 - amount));
      }

      return result;
    }

    private static MeshGeometry3D CreateFanMesh(Point3D[] ring, Point[] uvs)
    {
      int n = ring.Length;
      var center = new Point3D();
      for (int i = 0; i < n; i++)
      {
        center.X += ring[i].X;
        center.Y += ring[i].Y;
        center.Z += ring[i].Z;
      }

      center.X /= n;
      center.Y /= n;
      center.Z /= n;

      var uvCenter = new Point(0.5, 0.5);
      if (uvs != null && uvs.Length == n)
      {
        uvCenter = new Point(0, 0);
        for (int i = 0; i < n; i++)
        {
          uvCenter.X += uvs[i].X;
          uvCenter.Y += uvs[i].Y;
        }

        uvCenter.X /= n;
        uvCenter.Y /= n;
      }

      var pos = new Point3DCollection { center };
      var tex = new PointCollection { uvCenter };
      var idx = new Int32Collection();
      for (int i = 0; i < n; i++)
      {
        pos.Add(ring[i]);
        tex.Add(uvs != null && i < uvs.Length ? uvs[i] : new Point(0, 0));
      }

      for (int i = 0; i < n; i++)
      {
        int a = i + 1;
        int b = (i + 1) % n + 1;
        idx.Add(0);
        idx.Add(a);
        idx.Add(b);
      }

      var mesh = new MeshGeometry3D
      {
        Positions = pos,
        TriangleIndices = idx,
        TextureCoordinates = tex
      };
      if (mesh.CanFreeze)
        mesh.Freeze();
      return mesh;
    }

    private static void AddChamferedOutlines(Model3DGroup group, List<ChamferedFacet> facets, double s)
    {
      var outline = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(72, 76, 82)))
      {
        AmbientColor = Colors.White
      };
      var emissive = new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(48, 50, 54)));
      var matGroup = new MaterialGroup();
      matGroup.Children.Add(outline);
      matGroup.Children.Add(emissive);
      if (matGroup.CanFreeze)
        matGroup.Freeze();

      double lift = 0.0028;
      double half = 0.0042 * (s / CubeHalfSize);

      var pos = new Point3DCollection();
      var idx = new Int32Collection();
      int baseIndex = 0;
      var seen = new HashSet<string>();

      foreach (var facet in facets)
      {
        var ring = facet.Positions;
        if (ring == null || ring.Length < 2)
          continue;

        var outward = facet.Outward;
        if (outward.LengthSquared < 1e-12)
          continue;
        outward.Normalize();

        for (int i = 0; i < ring.Length; i++)
        {
          var a = ring[i];
          var b = ring[(i + 1) % ring.Length];
          if (!seen.Add(CanonicalEdgeKey(a, b)))
            continue;

          var edge = b - a;
          if (edge.LengthSquared < 1e-12)
            continue;
          edge.Normalize();
          var perp = Vector3D.CrossProduct(outward, edge);
          if (perp.LengthSquared < 1e-12)
            continue;
          perp.Normalize();

          var liftV = outward * lift;
          var pw = perp * half;
          pos.Add(a + liftV - pw);
          pos.Add(a + liftV + pw);
          pos.Add(b + liftV + pw);
          pos.Add(b + liftV - pw);
          idx.Add(baseIndex);
          idx.Add(baseIndex + 1);
          idx.Add(baseIndex + 2);
          idx.Add(baseIndex);
          idx.Add(baseIndex + 2);
          idx.Add(baseIndex + 3);
          baseIndex += 4;
        }
      }

      var mesh = new MeshGeometry3D
      {
        Positions = pos,
        TriangleIndices = idx
      };
      if (mesh.CanFreeze)
        mesh.Freeze();

      group.Children.Add(new GeometryModel3D(mesh, matGroup));
    }

    private static string CanonicalEdgeKey(Point3D a, Point3D b)
    {
      int Quant(double v) => (int)Math.Round(v * 100000.0);
      int ax = Quant(a.X), ay = Quant(a.Y), az = Quant(a.Z);
      int bx = Quant(b.X), by = Quant(b.Y), bz = Quant(b.Z);
      bool swap = ax > bx || (ax == bx && (ay > by || (ay == by && az > bz)));
      if (swap)
        return bx + "," + by + "," + bz + ":" + ax + "," + ay + "," + az;
      return ax + "," + ay + "," + az + ":" + bx + "," + by + "," + bz;
    }

    private void AddFace(Model3DGroup group, CubeFace face, string text, Brush color, double size)
    {
      GetFaceNormalAndTextBasis(face, out Vector3D normal, out Vector3D textRight, out Vector3D textUp);

      // Геометрия всегда CCW снаружи (UV задаётся отдельно через textRight/textUp).
      Vector3D geomUp = Math.Abs(normal.Z) > 0.5 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
      Vector3D geomRight = Vector3D.CrossProduct(geomUp, normal);
      geomRight.Normalize();
      geomUp = Vector3D.CrossProduct(normal, geomRight);
      geomUp.Normalize();

      double h = size;
      Point3D center = new Point3D(normal.X * h, normal.Y * h, normal.Z * h);

      Point3D p0 = center - geomRight * h - geomUp * h;
      Point3D p1 = center + geomRight * h - geomUp * h;
      Point3D p2 = center + geomRight * h + geomUp * h;
      Point3D p3 = center - geomRight * h + geomUp * h;

      var uv0 = ToFaceUv(p0, center, textRight, textUp, h);
      var uv1 = ToFaceUv(p1, center, textRight, textUp, h);
      var uv2 = ToFaceUv(p2, center, textRight, textUp, h);
      var uv3 = ToFaceUv(p3, center, textRight, textUp, h);

      var mesh = new MeshGeometry3D
      {
        Positions = new Point3DCollection { p0, p1, p2, p3 },
        TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 },
        TextureCoordinates = new PointCollection { uv0, uv1, uv2, uv3 }
      };

      if (mesh.CanFreeze)
        mesh.Freeze();

      var textBrush = CreateTextBrush(text);
      var style = _appearance ?? new ViewCubeSettings();
      var baseMat = CreateFaceMaterial(color, textBrush);
      var activeMat = CreateFaceMaterial(new SolidColorBrush(style.ActiveFaceColor), textBrush);
      var hoverMat = CreateFaceMaterial(new SolidColorBrush(style.HoverColor), textBrush);

      var geom = new GeometryModel3D(mesh, baseMat);
      _faceByModel[geom] = face;
      _modelByFace[face] = geom;
      _baseMaterials[geom] = baseMat;
      _activeMaterials[geom] = activeMat;
      _hoverMaterials[geom] = hoverMat;
      group.Children.Add(geom);
    }

    private static Point ToFaceUv(Point3D p, Point3D center, Vector3D textRight, Vector3D textUp, double h)
    {
      Vector3D v = p - center;
      double u = Vector3D.DotProduct(v, textRight) / (2.0 * h) + 0.5;
      double tv = 0.5 - Vector3D.DotProduct(v, textUp) / (2.0 * h);
      return new Point(u, tv);
    }

    /// <summary>
    /// Базис UV грани: textRight = Cross(textUp, normal), чтобы UVs были
    /// правосторонними относительно внешней нормали (иначе WPF даёт зеркало).
    /// Для Top/Bottom «вверх» текста — мировой +Y (назад): в изометрии и в виде
    /// сверху (Up=(0,1,0)) верх букв смотрит к задней грани, без поворота UV.
    /// </summary>
    private static void GetFaceNormalAndTextBasis(
      CubeFace face,
      out Vector3D normal,
      out Vector3D textRight,
      out Vector3D textUp)
    {
      switch (face)
      {
        case CubeFace.Top:
          normal = new Vector3D(0, 0, 1);
          textUp = new Vector3D(0, 1, 0);
          break;
        case CubeFace.Bottom:
          normal = new Vector3D(0, 0, -1);
          textUp = new Vector3D(0, 1, 0);
          break;
        case CubeFace.Front:
          normal = new Vector3D(0, -1, 0);
          textUp = new Vector3D(0, 0, 1);
          break;
        case CubeFace.Back:
          normal = new Vector3D(0, 1, 0);
          textUp = new Vector3D(0, 0, 1);
          break;
        case CubeFace.Right:
          normal = new Vector3D(1, 0, 0);
          textUp = new Vector3D(0, 0, 1);
          break;
        case CubeFace.Left:
          normal = new Vector3D(-1, 0, 0);
          textUp = new Vector3D(0, 0, 1);
          break;
        default:
          normal = new Vector3D(0, -1, 0);
          textUp = new Vector3D(0, 0, 1);
          break;
      }

      normal.Normalize();
      textUp.Normalize();
      textRight = Vector3D.CrossProduct(textUp, normal);
      textRight.Normalize();
    }

    private static Material CreateFaceMaterial(Brush faceColor, Brush textBrush, Brush outlineBrush = null)
    {
      var group = new MaterialGroup();
      group.Children.Add(new DiffuseMaterial(faceColor) { AmbientColor = Colors.White });
      if (outlineBrush != null)
        group.Children.Add(new DiffuseMaterial(outlineBrush) { AmbientColor = Colors.White });
      group.Children.Add(new DiffuseMaterial(textBrush) { AmbientColor = Colors.White });
      group.Children.Add(new EmissiveMaterial(textBrush));
      return group;
    }

    private static Material CreateHotspotMaterial(Color color)
    {
      return new DiffuseMaterial(new SolidColorBrush(color))
      {
        AmbientColor = Colors.White
      };
    }

    private void AddCorner(Model3DGroup group, CubeCorner corner, Point3D center, double cubeSize, Material material)
    {
      double h = cubeSize * CornerHalfScale;
      var geom = CreateBoxModel(center, h, h, h, material);
      _cornerByModel[geom] = corner;
      _baseMaterials[geom] = material;
      group.Children.Add(geom);
    }

    private void AddEdge(Model3DGroup group, CubeEdge edge, Point3D center, double hx, double hy, double hz, Material material)
    {
      var geom = CreateBoxModel(center, hx, hy, hz, material);
      _edgeByModel[geom] = edge;
      _baseMaterials[geom] = material;
      group.Children.Add(geom);
    }

    private static GeometryModel3D CreateBoxModel(Point3D center, double hx, double hy, double hz, Material material)
    {
      var p000 = new Point3D(center.X - hx, center.Y - hy, center.Z - hz);
      var p001 = new Point3D(center.X - hx, center.Y - hy, center.Z + hz);
      var p010 = new Point3D(center.X - hx, center.Y + hy, center.Z - hz);
      var p011 = new Point3D(center.X - hx, center.Y + hy, center.Z + hz);
      var p100 = new Point3D(center.X + hx, center.Y - hy, center.Z - hz);
      var p101 = new Point3D(center.X + hx, center.Y - hy, center.Z + hz);
      var p110 = new Point3D(center.X + hx, center.Y + hy, center.Z - hz);
      var p111 = new Point3D(center.X + hx, center.Y + hy, center.Z + hz);

      var mesh = new MeshGeometry3D
      {
        Positions = new Point3DCollection { p000, p001, p010, p011, p100, p101, p110, p111 },
        TriangleIndices = new Int32Collection
        {
          2, 6, 7, 2, 7, 3,
          0, 1, 5, 0, 5, 4,
          0, 2, 3, 0, 3, 1,
          4, 5, 7, 4, 7, 6,
          1, 3, 7, 1, 7, 5,
          0, 4, 6, 0, 6, 2
        }
      };

      if (mesh.CanFreeze)
        mesh.Freeze();

      return new GeometryModel3D(mesh, material);
    }

    private Brush CreateTextBrush(string text)
    {
      var style = _appearance ?? new ViewCubeSettings();
      if (!style.ShowLabels || string.IsNullOrWhiteSpace(text))
      {
        var empty = new SolidColorBrush(Colors.Transparent);
        if (empty.CanFreeze)
          empty.Freeze();
        return empty;
      }

      bool cad = style.IsChamferedShape();
      bool drawPill = !cad && style.LabelPill.A >= 16;
      const double fontSize = 72;
      var ru = CultureInfo.GetCultureInfo("ru-RU");
      var textBrush = new SolidColorBrush(style.LabelText);
      var outlineBrush = new SolidColorBrush(ContrastOutline(style.LabelText));
      var white = new FormattedText(
        text,
        ru,
        FlowDirection.LeftToRight,
        LabelTypeface,
        fontSize,
        textBrush,
        1.0);

      FormattedText outline = null;
      if (!cad)
      {
        outline = new FormattedText(
          text,
          ru,
          FlowDirection.LeftToRight,
          LabelTypeface,
          fontSize,
          outlineBrush,
          1.0);
      }

      double padX = cad ? 16 : 40;
      double padY = cad ? 10 : 24;
      double w = Math.Max(8, white.Width + padX * 2);
      double h = Math.Max(8, white.Height + padY * 2);
      double x = (w - white.Width) / 2.0;
      double y = (h - white.Height) / 2.0;

      var drawing = new DrawingGroup();
      using (var dc = drawing.Open())
      {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        if (drawPill)
        {
          dc.DrawRoundedRectangle(
            new SolidColorBrush(style.LabelPill),
            null,
            new Rect(6, 6, w - 12, h - 12),
            20,
            20);
        }

        if (outline != null)
        {
          const double outlineWidth = 3.5;
          for (int ox = -1; ox <= 1; ox++)
          {
            for (int oy = -1; oy <= 1; oy++)
            {
              if (ox == 0 && oy == 0)
                continue;
              dc.DrawText(outline, new Point(x + ox * outlineWidth, y + oy * outlineWidth));
            }
          }
        }

        dc.DrawText(white, new Point(x, y));
      }

      if (drawing.CanFreeze)
        drawing.Freeze();

      var brush = new DrawingBrush(drawing)
      {
        Stretch = Stretch.Uniform,
        AlignmentX = AlignmentX.Center,
        AlignmentY = AlignmentY.Center,
        Viewport = cad ? new Rect(0.10, 0.30, 0.80, 0.40) : new Rect(0.04, 0.22, 0.92, 0.56),
        ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
        TileMode = TileMode.None
      };

      RenderOptions.SetCachingHint(brush, CachingHint.Cache);

      if (brush.CanFreeze)
        brush.Freeze();

      return brush;
    }

    private static Color ContrastOutline(Color text)
    {
      double luma = (0.299 * text.R + 0.587 * text.G + 0.114 * text.B) / 255.0;
      return luma > 0.55 ? Color.FromRgb(16, 16, 16) : Color.FromRgb(255, 255, 255);
    }

    private void UpdateWpfCamera(Vector3D dir, Vector3D up)
    {
      if (_camera == null)
        return;

      if (dir.LengthSquared < 1e-12)
        return;
      dir.Normalize();

      if (up.LengthSquared < 1e-12)
        up = new Vector3D(0, 0, 1);
      else
        up.Normalize();

      _lastLook = dir;
      _lastUp = up;
      _camera.Position = new Point3D(-dir.X * CameraDistance, -dir.Y * CameraDistance, -dir.Z * CameraDistance);
      _camera.LookDirection = dir;
      _camera.UpDirection = up;
      _camera.FieldOfView = CameraFov;

      if (_dirLight != null)
        _dirLight.Direction = dir;
    }

    private void SetActiveFace(CubeFace? face)
    {
      if (_activeFace == face)
        return;

      _activeFace = face;
      RefreshMaterials();
    }

    private void RefreshMaterials()
    {
      foreach (var pair in _faceByModel)
      {
        var model = pair.Key;
        var face = pair.Value;

        if (ReferenceEquals(model, _hoveredModel) && _hoverMaterials.TryGetValue(model, out var hoverMat))
          model.Material = hoverMat;
        else if (_activeFace.HasValue && face == _activeFace.Value && _activeMaterials.TryGetValue(model, out var activeMat))
          model.Material = activeMat;
        else if (_baseMaterials.TryGetValue(model, out var baseMat))
          model.Material = baseMat;
      }

      foreach (var pair in _cornerByModel)
        ApplyHotspotMaterial(pair.Key);

      foreach (var pair in _edgeByModel)
        ApplyHotspotMaterial(pair.Key);
    }

    private void ApplyHotspotMaterial(GeometryModel3D model)
    {
      if (ReferenceEquals(model, _hoveredModel))
        model.Material = _hotspotHoverMaterial;
      else if (_baseMaterials.TryGetValue(model, out var baseMat))
        model.Material = baseMat;
    }

    private void ClearHover()
    {
      if (_hoveredModel == null)
      {
        Cursor = Cursors.Arrow;
        return;
      }

      _hoveredModel = null;
      Cursor = Cursors.Arrow;
      RefreshMaterials();
    }

    private static bool IsFromButton(RoutedEventArgs e)
    {
      DependencyObject src = e.OriginalSource as DependencyObject;
      while (src != null)
      {
        if (src is Button)
          return true;
        src = VisualTreeHelper.GetParent(src) ?? LogicalTreeHelper.GetParent(src);
      }

      return false;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      if (IsFromButton(e))
        return;

      if (e.LeftButton != MouseButtonState.Pressed)
        return;

      var pt = e.GetPosition(Viewport);
      _pressed = true;
      _orbiting = false;
      _lastPos = e.GetPosition(Root);
      _downPos = _lastPos;
      _hitFace = null;
      _hitCorner = null;
      _hitEdge = null;

      Root.CaptureMouse();

      if (TryHitModel(pt, out var gm))
      {
        if (_faceByModel.TryGetValue(gm, out var face))
          _hitFace = face;
        else if (_cornerByModel.TryGetValue(gm, out var corner))
          _hitCorner = corner;
        else if (_edgeByModel.TryGetValue(gm, out var edge))
          _hitEdge = edge;
      }

      e.Handled = true;
    }

    private void Root_MouseMove(object sender, MouseEventArgs e)
    {
      if (_pressed && e.LeftButton == MouseButtonState.Pressed)
      {
        var pt = e.GetPosition(Root);
        double travel = Math.Max(Math.Abs(pt.X - _downPos.X), Math.Abs(pt.Y - _downPos.Y));

        if (!_orbiting && travel > ClickSlopPx)
        {
          _orbiting = true;
          _hitFace = null;
          _hitCorner = null;
          _hitEdge = null;
          ClearHover();
          Cursor = Cursors.SizeAll;
        }

        if (_orbiting)
        {
          double dx = pt.X - _lastPos.X;
          double dy = pt.Y - _lastPos.Y;
          _lastPos = pt;
          if (Math.Abs(dx) > 0.2 || Math.Abs(dy) > 0.2)
            Orbit(dx, dy);
        }

        e.Handled = true;
        return;
      }

      if (_pressed)
        return;

      if (!TryHitModel(e.GetPosition(Viewport), out var hoverModel))
      {
        ClearHover();
        return;
      }

      Cursor = Cursors.Hand;
      if (ReferenceEquals(hoverModel, _hoveredModel))
        return;

      _hoveredModel = hoverModel;
      RefreshMaterials();
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
      if (!_pressed)
        return;

      bool wasOrbiting = _orbiting;
      _pressed = false;
      _orbiting = false;
      Root.ReleaseMouseCapture();
      Cursor = Cursors.Arrow;

      if (!wasOrbiting)
      {
        if (_hitFace.HasValue)
          FaceClicked?.Invoke(this, _hitFace.Value);
        else if (_hitCorner.HasValue)
          CornerClicked?.Invoke(this, _hitCorner.Value);
        else if (_hitEdge.HasValue)
          EdgeClicked?.Invoke(this, _hitEdge.Value);
      }

      _hitFace = null;
      _hitCorner = null;
      _hitEdge = null;
    }

    private void Root_MouseLeave(object sender, MouseEventArgs e)
    {
      if (!_pressed)
        ClearHover();
    }

    private bool TryHitModel(Point viewportPoint, out GeometryModel3D model)
    {
      model = null;
      if (!TryCreatePickRay(viewportPoint, out Point3D origin, out Vector3D dir))
        return false;

      double bestT = double.PositiveInfinity;
      GeometryModel3D best = null;

      HitMeshes(_faceByModel.Keys, origin, dir, ref bestT, ref best);
      HitMeshes(_cornerByModel.Keys, origin, dir, ref bestT, ref best);
      HitMeshes(_edgeByModel.Keys, origin, dir, ref bestT, ref best);

      model = best;
      return best != null;
    }

    private static void HitMeshes(
      IEnumerable<GeometryModel3D> models,
      Point3D origin,
      Vector3D dir,
      ref double bestT,
      ref GeometryModel3D best)
    {
      foreach (var gm in models)
      {
        if (gm.Geometry is MeshGeometry3D mesh && TryIntersectMesh(origin, dir, mesh, out double t) && t < bestT)
        {
          bestT = t;
          best = gm;
        }
      }
    }

    /// <summary>
    /// Луч из PerspectiveCamera. FieldOfView в WPF — горизонтальный.
    /// Свой ray-pick нужен, потому что VisualTreeHelper.HitTest по Viewport3D
    /// часто возвращает null в окне с AllowsTransparency.
    /// </summary>
    private bool TryCreatePickRay(Point pt, out Point3D origin, out Vector3D direction)
    {
      origin = new Point3D();
      direction = new Vector3D();

      if (_camera == null)
        return false;

      double w = Viewport.ActualWidth;
      double h = Viewport.ActualHeight;
      if (w < 2 || h < 2)
        return false;

      var look = _camera.LookDirection;
      var up = _camera.UpDirection;
      if (look.LengthSquared < 1e-12 || up.LengthSquared < 1e-12)
        return false;

      look.Normalize();
      var right = Vector3D.CrossProduct(look, up);
      if (right.LengthSquared < 1e-12)
        return false;
      right.Normalize();
      up = Vector3D.CrossProduct(right, look);
      up.Normalize();

      double nx = (pt.X / w) * 2.0 - 1.0;
      double ny = 1.0 - (pt.Y / h) * 2.0;
      double aspect = w / h;
      double tanHalf = Math.Tan(_camera.FieldOfView * Math.PI / 360.0);

      direction = look + right * (nx * tanHalf) + up * (ny * tanHalf / aspect);
      if (direction.LengthSquared < 1e-12)
        return false;
      direction.Normalize();
      origin = _camera.Position;
      return true;
    }

    private static bool TryIntersectMesh(Point3D origin, Vector3D dir, MeshGeometry3D mesh, out double tHit)
    {
      tHit = double.PositiveInfinity;
      var pos = mesh.Positions;
      var idx = mesh.TriangleIndices;
      bool any = false;

      for (int i = 0; i + 2 < idx.Count; i += 3)
      {
        if (IntersectTriangle(origin, dir, pos[idx[i]], pos[idx[i + 1]], pos[idx[i + 2]], out double t)
            && t < tHit)
        {
          tHit = t;
          any = true;
        }
      }

      return any;
    }

    private static bool IntersectTriangle(Point3D orig, Vector3D dir, Point3D v0, Point3D v1, Point3D v2, out double t)
    {
      t = 0;
      const double eps = 1e-8;
      Vector3D e1 = v1 - v0;
      Vector3D e2 = v2 - v0;
      Vector3D pvec = Vector3D.CrossProduct(dir, e2);
      double det = Vector3D.DotProduct(e1, pvec);
      if (det > -eps && det < eps)
        return false;

      double invDet = 1.0 / det;
      Vector3D tvec = orig - v0;
      double u = Vector3D.DotProduct(tvec, pvec) * invDet;
      if (u < 0.0 || u > 1.0)
        return false;

      Vector3D qvec = Vector3D.CrossProduct(tvec, e1);
      double v = Vector3D.DotProduct(dir, qvec) * invDet;
      if (v < 0.0 || u + v > 1.0)
        return false;

      t = Vector3D.DotProduct(e2, qvec) * invDet;
      return t > 1e-4;
    }

    private void Orbit(double dx, double dy)
    {
      const double sensitivity = 0.005;
      _yawRad -= dx * sensitivity;
      _pitchRad -= dy * sensitivity;

      double limit = Math.PI / 2.0 - 0.01;
      if (_pitchRad > limit) _pitchRad = limit;
      if (_pitchRad < -limit) _pitchRad = -limit;

      double cp = Math.Cos(_pitchRad);
      var dir = new Vector3D(
        cp * Math.Cos(_yawRad),
        cp * Math.Sin(_yawRad),
        Math.Sin(_pitchRad));

      ViewRotated?.Invoke(this, new ViewRotationEventArgs(dir));
      UpdateWpfCamera(dir, new Vector3D(0, 0, 1));
      SetActiveFace(CameraMath.PickDominantFace(dir));
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
      HomeClicked?.Invoke(this, EventArgs.Empty);
    }

    private void ScaleUpButton_Click(object sender, RoutedEventArgs e)
    {
      ScaleStepRequested?.Invoke(this, 1);
    }

    private void ScaleDownButton_Click(object sender, RoutedEventArgs e)
    {
      ScaleStepRequested?.Invoke(this, -1);
    }

    private void Root_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
      int steps = e.Delta > 0 ? 1 : -1;
      ScaleStepRequested?.Invoke(this, steps);
      e.Handled = true;
    }
  }
}
