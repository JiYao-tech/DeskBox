using Windows.Graphics;

namespace DeskBox.Services;

internal enum WidgetSnapEdge
{
    Left,
    Right,
    Top,
    Bottom
}

internal readonly record struct WidgetSnapTarget(
    RectInt32 Bounds,
    IntPtr WindowHandle);

internal readonly record struct WidgetSnapMatch(
    WidgetSnapEdge SourceEdge,
    WidgetSnapEdge TargetEdge,
    int Coordinate,
    int ResolvedOrigin,
    IntPtr TargetWindowHandle,
    bool UsesSpacing,
    int Delta);

internal readonly record struct WidgetMoveSnapResult(
    RectInt32 Bounds,
    WidgetSnapMatch? HorizontalMatch,
    WidgetSnapMatch? VerticalMatch);

/// <summary>
/// Pure physical-pixel snap solver shared by widget move and resize sessions.
/// The caller converts user-facing effective pixels to physical pixels once
/// per interaction, so every candidate in a session uses one coordinate space.
/// </summary>
internal static class WidgetSnapCalculator
{
    public static WidgetMoveSnapResult ResolveMove(
        RectInt32 proposedBounds,
        IReadOnlyList<WidgetSnapTarget> targets,
        RectInt32? workArea,
        int spacing,
        int engageThreshold,
        int releaseThreshold,
        WidgetSnapMatch? stickyHorizontal = null,
        WidgetSnapMatch? stickyVertical = null)
    {
        spacing = Math.Max(0, spacing);
        engageThreshold = Math.Max(0, engageThreshold);
        releaseThreshold = Math.Max(engageThreshold, releaseThreshold);

        WidgetSnapMatch? horizontal = ResolveStickyMoveMatch(
            proposedBounds.X,
            releaseThreshold,
            stickyHorizontal,
            horizontal: true) ??
            ResolveBestMoveMatch(
                proposedBounds,
                targets,
                workArea,
                spacing,
                engageThreshold,
                horizontal: true);
        WidgetSnapMatch? vertical = ResolveStickyMoveMatch(
            proposedBounds.Y,
            releaseThreshold,
            stickyVertical,
            horizontal: false) ??
            ResolveBestMoveMatch(
                proposedBounds,
                targets,
                workArea,
                spacing,
                engageThreshold,
                horizontal: false);

        return new WidgetMoveSnapResult(
            new RectInt32(
                horizontal?.ResolvedOrigin ?? proposedBounds.X,
                vertical?.ResolvedOrigin ?? proposedBounds.Y,
                proposedBounds.Width,
                proposedBounds.Height),
            horizontal,
            vertical);
    }

    /// <summary>
    /// Rounds the dimension that <paramref name="resizeDirection"/> is changing
    /// to the nearest multiple of <paramref name="gridSize"/>, so repeated
    /// manual resizes produce cells of a uniform size.  The edge opposite to
    /// the dragged one stays pinned, exactly like a plain resize.  A
    /// <paramref name="gridSize"/> of zero or less disables the grid and
    /// returns the proposed bounds unchanged.
    /// </summary>
    public static RectInt32 SnapResizeToGrid(
        RectInt32 proposedBounds,
        string resizeDirection,
        int gridSize)
    {
        if (gridSize <= 0)
        {
            return proposedBounds;
        }

        int width = proposedBounds.Width;
        int height = proposedBounds.Height;
        bool resizesHorizontally =
            resizeDirection.Contains("Left", StringComparison.Ordinal) ||
            resizeDirection.Contains("Right", StringComparison.Ordinal);
        bool resizesVertically =
            resizeDirection.Contains("Top", StringComparison.Ordinal) ||
            resizeDirection.Contains("Bottom", StringComparison.Ordinal);

        if (resizesHorizontally)
        {
            width = RoundToGrid(width, gridSize);
        }

        if (resizesVertically)
        {
            height = RoundToGrid(height, gridSize);
        }

        int x = resizeDirection.Contains("Left", StringComparison.Ordinal)
            ? proposedBounds.X + proposedBounds.Width - width
            : proposedBounds.X;
        int y = resizeDirection.Contains("Top", StringComparison.Ordinal)
            ? proposedBounds.Y + proposedBounds.Height - height
            : proposedBounds.Y;

        return new RectInt32(x, y, width, height);
    }

    private static int RoundToGrid(int value, int gridSize)
    {
        int steps = (int)Math.Round(value / (double)gridSize, MidpointRounding.AwayFromZero);
        return Math.Max(gridSize, steps * gridSize);
    }

    public static WidgetSnapMatch? ResolveResizeEdge(
        RectInt32 proposedBounds,
        WidgetSnapEdge sourceEdge,
        IReadOnlyList<WidgetSnapTarget> targets,
        RectInt32? workArea,
        int spacing,
        int threshold)
    {
        spacing = Math.Max(0, spacing);
        threshold = Math.Max(0, threshold);
        SnapCandidate? best = null;
        EvaluateEdgeCandidates(
            proposedBounds,
            targets,
            workArea,
            spacing,
            sourceEdge,
            threshold,
            ref best);
        return best?.Match;
    }

    private static WidgetSnapMatch? ResolveBestMoveMatch(
        RectInt32 source,
        IReadOnlyList<WidgetSnapTarget> targets,
        RectInt32? workArea,
        int spacing,
        int threshold,
        bool horizontal)
    {
        SnapCandidate? best = null;
        if (horizontal)
        {
            EvaluateEdgeCandidates(
                source,
                targets,
                workArea,
                spacing,
                WidgetSnapEdge.Left,
                threshold,
                ref best);
            EvaluateEdgeCandidates(
                source,
                targets,
                workArea,
                spacing,
                WidgetSnapEdge.Right,
                threshold,
                ref best);
        }
        else
        {
            EvaluateEdgeCandidates(
                source,
                targets,
                workArea,
                spacing,
                WidgetSnapEdge.Top,
                threshold,
                ref best);
            EvaluateEdgeCandidates(
                source,
                targets,
                workArea,
                spacing,
                WidgetSnapEdge.Bottom,
                threshold,
                ref best);
        }

        return best?.Match;
    }

    private static WidgetSnapMatch? ResolveStickyMoveMatch(
        int proposedOrigin,
        int releaseThreshold,
        WidgetSnapMatch? sticky,
        bool horizontal)
    {
        if (sticky is not { } match ||
            horizontal != (match.SourceEdge is WidgetSnapEdge.Left or WidgetSnapEdge.Right))
        {
            return null;
        }

        int delta = Math.Abs(proposedOrigin - match.ResolvedOrigin);
        return delta <= releaseThreshold
            ? match with { Delta = delta }
            : null;
    }

    private static void EvaluateEdgeCandidates(
        RectInt32 source,
        IReadOnlyList<WidgetSnapTarget> targets,
        RectInt32? workArea,
        int spacing,
        WidgetSnapEdge sourceEdge,
        int threshold,
        ref SnapCandidate? best)
    {
        foreach (WidgetSnapTarget target in targets)
        {
            EvaluateWidgetCandidates(
                source,
                target,
                spacing,
                sourceEdge,
                threshold,
                ref best);
        }

        if (workArea is { } area)
        {
            ConsiderCandidate(
                CreateWorkAreaCandidate(source, area, sourceEdge),
                threshold,
                ref best);
        }
    }

    private static void EvaluateWidgetCandidates(
        RectInt32 source,
        WidgetSnapTarget target,
        int spacing,
        WidgetSnapEdge sourceEdge,
        int threshold,
        ref SnapCandidate? best)
    {
        RectInt32 other = target.Bounds;
        int perpendicularGap = sourceEdge is WidgetSnapEdge.Left or WidgetSnapEdge.Right
            ? IntervalGap(source.Y, source.Y + source.Height, other.Y, other.Y + other.Height)
            : IntervalGap(source.X, source.X + source.Width, other.X, other.X + other.Width);
        int perpendicularCenterDistance = sourceEdge is WidgetSnapEdge.Left or WidgetSnapEdge.Right
            ? Math.Abs(
                source.Y + source.Height / 2 -
                (other.Y + other.Height / 2))
            : Math.Abs(
                source.X + source.Width / 2 -
                (other.X + other.Width / 2));

        switch (sourceEdge)
        {
            case WidgetSnapEdge.Left:
                ConsiderCandidate(CreateCandidate(
                    source,
                    sourceEdge,
                    WidgetSnapEdge.Left,
                    other.X,
                    target.WindowHandle,
                    usesSpacing: false,
                    priority: 2,
                    perpendicularGap,
                    perpendicularCenterDistance), threshold, ref best);
                ConsiderCandidate(CreateCandidate(
                    source,
                    sourceEdge,
                    WidgetSnapEdge.Right,
                    other.X + other.Width + spacing,
                    target.WindowHandle,
                    usesSpacing: true,
                    priority: 1,
                    perpendicularGap,
                    perpendicularCenterDistance), threshold, ref best);
                break;

            case WidgetSnapEdge.Right:
                ConsiderCandidate(CreateCandidate(
                    source,
                    sourceEdge,
                    WidgetSnapEdge.Right,
                    other.X + other.Width,
                    target.WindowHandle,
                    usesSpacing: false,
                    priority: 2,
                    perpendicularGap,
                    perpendicularCenterDistance), threshold, ref best);
                ConsiderCandidate(CreateCandidate(
                    source,
                    sourceEdge,
                    WidgetSnapEdge.Left,
                    other.X - spacing,
                    target.WindowHandle,
                    usesSpacing: true,
                    priority: 1,
                    perpendicularGap,
                    perpendicularCenterDistance), threshold, ref best);
                break;

            case WidgetSnapEdge.Top:
                ConsiderCandidate(CreateCandidate(
                    source,
                    sourceEdge,
                    WidgetSnapEdge.Top,
                    other.Y,
                    target.WindowHandle,
                    usesSpacing: false,
                    priority: 2,
                    perpendicularGap,
                    perpendicularCenterDistance), threshold, ref best);
                ConsiderCandidate(CreateCandidate(
                    source,
                    sourceEdge,
                    WidgetSnapEdge.Bottom,
                    other.Y + other.Height + spacing,
                    target.WindowHandle,
                    usesSpacing: true,
                    priority: 1,
                    perpendicularGap,
                    perpendicularCenterDistance), threshold, ref best);
                break;

            case WidgetSnapEdge.Bottom:
                ConsiderCandidate(CreateCandidate(
                    source,
                    sourceEdge,
                    WidgetSnapEdge.Bottom,
                    other.Y + other.Height,
                    target.WindowHandle,
                    usesSpacing: false,
                    priority: 2,
                    perpendicularGap,
                    perpendicularCenterDistance), threshold, ref best);
                ConsiderCandidate(CreateCandidate(
                    source,
                    sourceEdge,
                    WidgetSnapEdge.Top,
                    other.Y - spacing,
                    target.WindowHandle,
                    usesSpacing: true,
                    priority: 1,
                    perpendicularGap,
                    perpendicularCenterDistance), threshold, ref best);
                break;
        }
    }

    private static SnapCandidate CreateWorkAreaCandidate(
        RectInt32 source,
        RectInt32 workArea,
        WidgetSnapEdge sourceEdge)
    {
        int coordinate = sourceEdge switch
        {
            WidgetSnapEdge.Left => workArea.X,
            WidgetSnapEdge.Right => workArea.X + workArea.Width,
            WidgetSnapEdge.Top => workArea.Y,
            WidgetSnapEdge.Bottom => workArea.Y + workArea.Height,
            _ => 0
        };
        return CreateCandidate(
            source,
            sourceEdge,
            sourceEdge,
            coordinate,
            IntPtr.Zero,
            usesSpacing: false,
            priority: 0,
            perpendicularGap: 0,
            perpendicularCenterDistance: 0);
    }

    private static SnapCandidate CreateCandidate(
        RectInt32 source,
        WidgetSnapEdge sourceEdge,
        WidgetSnapEdge targetEdge,
        int coordinate,
        IntPtr targetWindowHandle,
        bool usesSpacing,
        int priority,
        int perpendicularGap,
        int perpendicularCenterDistance)
    {
        int currentCoordinate = GetEdgeCoordinate(source, sourceEdge);
        int resolvedOrigin = sourceEdge switch
        {
            WidgetSnapEdge.Left => coordinate,
            WidgetSnapEdge.Right => coordinate - source.Width,
            WidgetSnapEdge.Top => coordinate,
            WidgetSnapEdge.Bottom => coordinate - source.Height,
            _ => 0
        };
        int delta = Math.Abs(currentCoordinate - coordinate);
        return new SnapCandidate(
            new WidgetSnapMatch(
                sourceEdge,
                targetEdge,
                coordinate,
                resolvedOrigin,
                targetWindowHandle,
                usesSpacing,
                delta),
            priority,
            perpendicularGap,
            perpendicularCenterDistance);
    }

    private static void ConsiderCandidate(
        SnapCandidate candidate,
        int threshold,
        ref SnapCandidate? best)
    {
        if (candidate.Match.Delta > threshold ||
            best is { } current && !IsBetter(candidate, current))
        {
            return;
        }

        best = candidate;
    }

    private static bool IsBetter(SnapCandidate candidate, SnapCandidate current) =>
        candidate.Match.Delta < current.Match.Delta ||
        candidate.Match.Delta == current.Match.Delta &&
        (candidate.Priority < current.Priority ||
         candidate.Priority == current.Priority &&
         (candidate.PerpendicularGap < current.PerpendicularGap ||
          candidate.PerpendicularGap == current.PerpendicularGap &&
          candidate.PerpendicularCenterDistance < current.PerpendicularCenterDistance));

    private static int GetEdgeCoordinate(RectInt32 bounds, WidgetSnapEdge edge) =>
        edge switch
        {
            WidgetSnapEdge.Left => bounds.X,
            WidgetSnapEdge.Right => bounds.X + bounds.Width,
            WidgetSnapEdge.Top => bounds.Y,
            WidgetSnapEdge.Bottom => bounds.Y + bounds.Height,
            _ => 0
        };

    private static int IntervalGap(int firstStart, int firstEnd, int secondStart, int secondEnd)
    {
        if (firstEnd < secondStart)
        {
            return secondStart - firstEnd;
        }

        return secondEnd < firstStart
            ? firstStart - secondEnd
            : 0;
    }

    private readonly record struct SnapCandidate(
        WidgetSnapMatch Match,
        int Priority,
        int PerpendicularGap,
        int PerpendicularCenterDistance);
}
