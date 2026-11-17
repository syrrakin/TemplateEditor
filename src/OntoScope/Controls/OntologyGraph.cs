using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OntoScope.Models;

namespace OntoScope.Controls;

public sealed class OntologyGraph : FrameworkElement
{
    private const double NodeWidth = 178;
    private const double NodeHeight = 66;
    private readonly Dictionary<string, Rect> _nodeRects = new(StringComparer.OrdinalIgnoreCase);
    private OntologyDocument? _ontology;
    private OntologyNode? _selectedNode;
    private double _scale = 1;
    private Vector _offset;
    private Point _lastMousePosition;
    private bool _isDragging;
    private bool _hasMoved;
    private bool _hasFit;

    public static readonly DependencyProperty ZoomPercentProperty = DependencyProperty.Register(
        nameof(ZoomPercent), typeof(int), typeof(OntologyGraph), new PropertyMetadata(100));

    public int ZoomPercent
    {
        get => (int)GetValue(ZoomPercentProperty);
        private set => SetValue(ZoomPercentProperty, value);
    }

    public event Action<OntologyNode>? NodeSelected;

    public OntologyDocument? Ontology
    {
        get => _ontology;
        set
        {
            _ontology = value;
            _hasFit = false;
            RebuildLayout();
            Dispatcher.BeginInvoke(FitToView, System.Windows.Threading.DispatcherPriority.Loaded);
            InvalidateVisual();
        }
    }

    public OntologyNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            _selectedNode = value;
            InvalidateVisual();
        }
    }

    public void FitToView()
    {
        if (_nodeRects.Count == 0 || ActualWidth < 10 || ActualHeight < 10)
        {
            return;
        }

        var bounds = _nodeRects.Values.Aggregate(Rect.Union);
        bounds.Inflate(48, 48);
        _scale = Math.Min(ActualWidth / bounds.Width, ActualHeight / bounds.Height);
        _scale = Math.Clamp(_scale, 0.28, 1.35);
        _offset = new Vector(
            (ActualWidth - bounds.Width * _scale) / 2 - bounds.X * _scale,
            (ActualHeight - bounds.Height * _scale) / 2 - bounds.Y * _scale);
        _hasFit = true;
        UpdateZoom();
        InvalidateVisual();
    }

    public void ZoomIn() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1.15);

    public void ZoomOut() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1 / 1.15);

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!_hasFit)
        {
            FitToView();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Brush("#101620"), null, new Rect(0, 0, ActualWidth, ActualHeight));
        DrawGrid(drawingContext);

        if (_ontology is null || _nodeRects.Count == 0)
        {
            return;
        }

        drawingContext.PushTransform(new MatrixTransform(_scale, 0, 0, _scale, _offset.X, _offset.Y));
        DrawRelations(drawingContext);
        DrawNodes(drawingContext);
        drawingContext.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        _lastMousePosition = e.GetPosition(this);
        _isDragging = true;
        _hasMoved = false;
        CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        var delta = position - _lastMousePosition;
        if (delta.Length > 2)
        {
            _hasMoved = true;
        }

        _offset += delta;
        _lastMousePosition = position;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;

        if (_hasMoved || _ontology is null)
        {
            return;
        }

        var worldPoint = ScreenToWorld(e.GetPosition(this));
        var hit = _nodeRects.FirstOrDefault(pair => pair.Value.Contains(worldPoint));
        if (hit.Key is not null)
        {
            var node = _ontology.Nodes.FirstOrDefault(item => item.Id.Equals(hit.Key, StringComparison.OrdinalIgnoreCase));
            if (node is not null)
            {
                NodeSelected?.Invoke(node);
            }
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.12 : 1 / 1.12);
        e.Handled = true;
    }

    private void RebuildLayout()
    {
        _nodeRects.Clear();
        if (_ontology is null)
        {
            return;
        }

        var nodes = _ontology.Nodes;
        var depths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            GetDepth(node, nodes, depths, []);
        }

        foreach (var level in nodes.GroupBy(node => depths.GetValueOrDefault(node.Id)).OrderBy(group => group.Key))
        {
            var ordered = level.OrderBy(node => node.Name, StringComparer.CurrentCulture).ToList();
            for (var index = 0; index < ordered.Count; index++)
            {
                _nodeRects[ordered[index].Id] = new Rect(level.Key * 248, index * 94, NodeWidth, NodeHeight);
            }
        }
    }

    private static int GetDepth(
        OntologyNode node,
        IReadOnlyList<OntologyNode> nodes,
        IDictionary<string, int> depths,
        HashSet<string> path)
    {
        if (depths.TryGetValue(node.Id, out var cached))
        {
            return cached;
        }

        if (!path.Add(node.Id) || node.ParentIds.Count == 0)
        {
            return 0;
        }

        var parents = nodes.Where(parent => node.ParentIds.Contains(parent.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        var depth = parents.Count == 0 ? 0 : parents.Max(parent => GetDepth(parent, nodes, depths, path)) + 1;
        path.Remove(node.Id);
        depths[node.Id] = depth;
        return depth;
    }

    private void ZoomAt(Point screenPoint, double factor)
    {
        var worldPoint = ScreenToWorld(screenPoint);
        _scale = Math.Clamp(_scale * factor, 0.25, 2.5);
        _offset = new Vector(screenPoint.X - worldPoint.X * _scale, screenPoint.Y - worldPoint.Y * _scale);
        UpdateZoom();
        InvalidateVisual();
    }

    private Point ScreenToWorld(Point point) => new(
        (point.X - _offset.X) / _scale,
        (point.Y - _offset.Y) / _scale);

    private void UpdateZoom() => ZoomPercent = (int)Math.Round(_scale * 100);

    private void DrawGrid(DrawingContext context)
    {
        var brush = Brush("#263043");
        const int spacing = 24;
        for (double x = 12; x < ActualWidth; x += spacing)
        {
            for (double y = 12; y < ActualHeight; y += spacing)
            {
                context.DrawEllipse(brush, null, new Point(x, y), 0.65, 0.65);
            }
        }
    }

    private void DrawRelations(DrawingContext context)
    {
        foreach (var relation in _ontology!.Relations)
        {
            if (!_nodeRects.TryGetValue(relation.SourceId, out var source) || !_nodeRects.TryGetValue(relation.TargetId, out var target))
            {
                continue;
            }

            var start = new Point(source.Right, source.Top + source.Height / 2);
            var end = new Point(target.Left, target.Top + target.Height / 2);
            if (target.Left < source.Right)
            {
                start = new Point(source.Left + source.Width / 2, source.Top);
                end = new Point(target.Left + target.Width / 2, target.Bottom);
            }

            var distance = Math.Max(48, Math.Abs(end.X - start.X) * 0.48);
            var control1 = new Point(start.X + distance, start.Y);
            var control2 = new Point(end.X - distance, end.Y);
            if (Math.Abs(start.X - end.X) < 2)
            {
                control1 = new Point(start.X - 60, start.Y - 38);
                control2 = new Point(end.X - 60, end.Y + 38);
            }

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new BezierSegment(control1, control2, end, true));
            geometry.Figures.Add(figure);
            var association = relation.Kind == OntologyRelationKind.Association;
            var pen = new Pen(Brush(association ? "#397F82" : "#48546C"), association ? 1.25 : 1.4);
            if (association)
            {
                pen.DashStyle = new DashStyle([4, 4], 0);
            }

            context.DrawGeometry(null, pen, geometry);
            if (!association)
            {
                DrawArrow(context, end);
                continue;
            }

            var middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2 - 7);
            var text = Text(relation.Label, 9.5, "#75B8B6", FontWeights.Normal);
            var labelRect = new Rect(middle.X - text.Width / 2 - 4, middle.Y - 2, text.Width + 8, text.Height + 4);
            context.DrawRoundedRectangle(Brush("#16232C"), null, labelRect, 3, 3);
            context.DrawText(text, new Point(middle.X - text.Width / 2, middle.Y));
        }
    }

    private static void DrawArrow(DrawingContext context, Point end)
    {
        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(end, true, true);
            geometryContext.LineTo(new Point(end.X - 8, end.Y - 4), true, false);
            geometryContext.LineTo(new Point(end.X - 8, end.Y + 4), true, false);
        }

        geometry.Freeze();
        context.DrawGeometry(Brush("#66738D"), null, geometry);
    }

    private void DrawNodes(DrawingContext context)
    {
        foreach (var node in _ontology!.Nodes)
        {
            if (!_nodeRects.TryGetValue(node.Id, out var rect))
            {
                continue;
            }

            var selected = node.Id.Equals(_selectedNode?.Id, StringComparison.OrdinalIgnoreCase);
            if (selected)
            {
                var glow = rect;
                glow.Inflate(4, 4);
                context.DrawRoundedRectangle(null, new Pen(Brush("#368B7CF6"), 1), glow, 13, 13);
            }

            context.DrawRoundedRectangle(
                Brush(selected ? "#282742" : "#1B2433"),
                new Pen(Brush(selected ? "#9B8AFB" : "#3B475C"), selected ? 1.6 : 1),
                rect,
                11,
                11);
            context.DrawRoundedRectangle(
                Brush(selected ? "#A896FF" : "#7667D9"),
                null,
                new Rect(rect.X, rect.Y + 13, 3, rect.Height - 26),
                1.5,
                1.5);

            var iconRect = new Rect(rect.X + 14, rect.Y + 17, 31, 31);
            context.DrawRoundedRectangle(Brush(selected ? "#3B345B" : "#293149"), null, iconRect, 8, 8);
            var initial = Text(node.Name.Length == 0 ? "?" : node.Name[..1].ToUpperInvariant(), 14, "#B8ACFF", FontWeights.SemiBold);
            context.DrawText(initial, new Point(iconRect.X + (iconRect.Width - initial.Width) / 2, iconRect.Y + 5));

            var title = Text(Trim(node.Name, 14, 112, FontWeights.SemiBold), 12.5, selected ? "#F4F1FF" : "#E5EAF3", FontWeights.SemiBold);
            context.DrawText(title, new Point(rect.X + 55, rect.Y + 14));
            var subtitle = Text(Trim(node.Id, 9.5, 112, FontWeights.Normal), 9.5, "#8390A6", FontWeights.Normal);
            context.DrawText(subtitle, new Point(rect.X + 55, rect.Y + 36));
        }
    }

    private static string Trim(string value, double size, double maxWidth, FontWeight weight)
    {
        if (Text(value, size, "#FFFFFF", weight).Width <= maxWidth || value.Length < 4)
        {
            return value;
        }

        var length = value.Length;
        while (length > 3 && Text(value[..length] + "…", size, "#FFFFFF", weight).Width > maxWidth)
        {
            length--;
        }

        return value[..length] + "…";
    }

    private static FormattedText Text(string value, double size, string color, FontWeight weight) => new(
        value,
        CultureInfo.GetCultureInfo("ru-RU"),
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
        size,
        Brush(color),
        1);

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
