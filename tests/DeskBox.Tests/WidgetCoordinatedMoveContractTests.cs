namespace DeskBox.Tests;

public sealed class WidgetCoordinatedMoveContractTests
{
    [Fact]
    public void CtrlTitleDrag_UsesAtomicSameMonitorBatchWithoutZOrderMutation()
    {
        string interaction = Read("src/DeskBox/Views/WidgetWindowBase.Interaction.cs");
        string manager = Read("src/DeskBox/Services/WidgetManager.CoordinatedMove.cs");
        string contentInteraction = Read(
            "src/DeskBox/Views/ContentWidgetWindow.WindowInteraction.cs");

        Assert.Contains("VirtualKey.Control", interaction, StringComparison.Ordinal);
        Assert.Contains("TryBeginCoordinatedMove(HWnd)", interaction, StringComparison.Ordinal);
        Assert.Contains("UpdateCoordinatedMove(", interaction, StringComparison.Ordinal);
        Assert.Contains("frame.DeltaX", interaction, StringComparison.Ordinal);
        Assert.Contains("frame.DeltaY", interaction, StringComparison.Ordinal);
        Assert.Contains("CompleteCoordinatedMove(HWnd, hasMoved)", interaction, StringComparison.Ordinal);
        Assert.Contains("if (!_isCoordinatedMoveDrag)", interaction, StringComparison.Ordinal);

        Assert.Contains("window.Visible", manager, StringComparison.Ordinal);
        Assert.Contains("window.CanParticipateInCoordinatedMove", manager, StringComparison.Ordinal);
        Assert.Contains("MonitorFromWindow", manager, StringComparison.Ordinal);
        Assert.Contains("BeginDeferWindowPos", manager, StringComparison.Ordinal);
        Assert.Contains("DeferWindowPos", manager, StringComparison.Ordinal);
        Assert.Contains("EndDeferWindowPos", manager, StringComparison.Ordinal);
        Assert.Contains("SWP_NOZORDER", manager, StringComparison.Ordinal);
        Assert.Contains("SWP_NOACTIVATE", manager, StringComparison.Ordinal);
        Assert.Contains("UpdateWidgetsBatch", manager, StringComparison.Ordinal);
        Assert.Contains(
            "!Win32Helper.IsKeyPressed(Windows.System.VirtualKey.Control)",
            contentInteraction,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetSpacingSetting_UsesSliderInsteadOfNumberBox()
    {
        string settingsXaml = Read("src/DeskBox/Views/SettingsWindow.xaml");
        int start = settingsXaml.IndexOf(
            "Settings.WidgetSnap.Spacing.Title",
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        int end = settingsXaml.IndexOf(
            "</toolkit:SettingsCard>",
            start,
            StringComparison.Ordinal);
        Assert.True(end > start);
        string card = settingsXaml[start..end];

        Assert.Contains("<Slider", card, StringComparison.Ordinal);
        Assert.Contains("StepFrequency=\"1\"", card, StringComparison.Ordinal);
        Assert.Contains("SnapSpacingText", card, StringComparison.Ordinal);
        Assert.DoesNotContain("<NumberBox", card, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetSnapGridSizeSetting_UsesSliderInsteadOfNumberBox()
    {
        string settingsXaml = Read("src/DeskBox/Views/SettingsWindow.xaml");
        int start = settingsXaml.IndexOf(
            "Settings.WidgetSnap.GridSize.Title",
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        int end = settingsXaml.IndexOf(
            "</toolkit:SettingsCard>",
            start,
            StringComparison.Ordinal);
        Assert.True(end > start);
        string card = settingsXaml[start..end];

        // Merge note: the wired settings section editors drop the legacy
        // Widget/Music prefixes (the section-level DataContext switch makes
        // them redundant), so the merged card binds SnapGridSizeText /
        // SnapGridSize instead of the fork's WidgetSnapGridSize* names.
        Assert.Contains("<Slider", card, StringComparison.Ordinal);
        Assert.Contains("StepFrequency=\"1\"", card, StringComparison.Ordinal);
        Assert.Contains("SnapGridSizeText", card, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding SnapGridSize, Mode=TwoWay}\"", card, StringComparison.Ordinal);
        Assert.DoesNotContain("<NumberBox", card, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetSnapGridSizeSetting_LivesInTheWidgetSnappingExpanderAndReachesTheLiveOverlay()
    {
        string settingsXaml = Read("src/DeskBox/Views/SettingsWindow.xaml");
        int snapExpander = settingsXaml.IndexOf(
            "svc:Localized.HeaderKey=\"Settings.ResizeSnap.Title\"",
            StringComparison.Ordinal);
        int spacingCard = settingsXaml.IndexOf(
            "Settings.WidgetSnap.Spacing.Title",
            StringComparison.Ordinal);
        int gridCard = settingsXaml.IndexOf(
            "Settings.WidgetSnap.GridSize.Title",
            StringComparison.Ordinal);
        int doubleClickCard = settingsXaml.IndexOf(
            "Settings.DesktopDoubleClick.Title",
            StringComparison.Ordinal);

        Assert.True(snapExpander >= 0);
        Assert.True(snapExpander < spacingCard);
        Assert.True(spacingCard < gridCard);
        Assert.True(gridCard < doubleClickCard);

        string overlay = Read("src/DeskBox/Services/ResizeGuideOverlayService.cs");
        Assert.Contains(
            "public double SnapGridSizeDips { get; set; }",
            overlay,
            StringComparison.Ordinal);
        Assert.Contains("SnapResizeToGrid(", overlay, StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeWidgetSnapGridSize(SnapGridSizeDips)",
            overlay,
            StringComparison.Ordinal);

        Assert.Contains(
            "ResizeGuideOverlay.SnapGridSizeDips = SettingsService.Settings.WidgetSnapGridSize;",
            Read("src/DeskBox/App.xaml.cs"),
            StringComparison.Ordinal);
        // Merge note: upstream's shell hosts the interaction editor as a
        // section VM, so the slider change arrives as the VM's
        // SnapGridSizeUserChanged event and the shell handler assigns the
        // overlay directly; normalization lives in the coordinator and again
        // at the overlay's own use site.
        Assert.Contains(
            "app.ResizeGuideOverlay.SnapGridSizeDips = value;",
            Read("src/DeskBox/ViewModels/SettingsViewModel.PreferenceCallbacks.cs"),
            StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(TestPaths.FromRepository(relativePath));
}
