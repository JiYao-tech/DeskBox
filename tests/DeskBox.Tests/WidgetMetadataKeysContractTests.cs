namespace DeskBox.Tests;

/// <summary>
/// Freezes the complete WidgetConfig.Metadata key inventory through the
/// central WidgetMetadataKeys registry (pluginization roadmap section 7,
/// stage 2 data hygiene). Every key a family owns must be aliased here;
/// new keys are added together with their family constant, and the
/// QuickCaptureMasterPaneWidth literal exists exactly once (it used to be
/// declared privately in two files).
/// </summary>
public sealed class WidgetMetadataKeysContractTests
{
    [Fact]
    public void Registry_AliasesEveryKnownMetadataKey()
    {
        string registry = File.ReadAllText(TestPaths.SourceFile(
            "src/DeskBox/Services/WidgetMetadataKeys.cs"));

        string[] requiredAliases =
        [
            "ChromeMode = WidgetChromeModeNames.MetadataKey",
            "CollapseBehavior = WidgetCollapseBehaviorNames.MetadataKey",
            "FolderOpenBehavior = FileWidgetFolderOpenBehaviorNames.MetadataKey",
            "WidgetForegroundMode = WidgetForegroundSettings.ModeOverrideMetadataKey",
            "WidgetForegroundColor = WidgetForegroundSettings.ColorOverrideMetadataKey",
            // WidgetTextEdgeMode is intentionally absent: upstream 1.5.x
            // retired the per-widget text-edge override in favour of the
            // dual-layer text shadow, so the fork's alias no longer has a
            // source constant (WidgetForegroundContractTests pins the
            // retirement on the foreground side).
            "FileStacksEnabled = WidgetFileStackSettings.EnabledOverrideMetadataKey",
            "FileStackGroupBy = WidgetFileStackSettings.GroupByOverrideMetadataKey",
            "FileStackThreshold = WidgetFileStackSettings.ThresholdOverrideMetadataKey",
            "FileStackOrderBy = WidgetFileStackSettings.OrderByOverrideMetadataKey",
            "FileStackOpenMode = WidgetFileStackSettings.OpenModeOverrideMetadataKey",
            "FileStackDisabledGroups = WidgetFileStackSettings.DisabledStacksMetadataKey",
            "FileStackNameOverrides = WidgetFileStackSettings.StackNameOverridesMetadataKey",
            "FileStackGroupOrder = WidgetFileStackSettings.StackOrderMetadataKey",
            "FileStackMemberOverrides = WidgetFileStackSettings.StackMemberOverridesMetadataKey",
            "WeatherViewMode = WeatherWidgetViewModeSettings.MetadataKey",
            "TodoMasterPaneWidth = \"Todo.MasterPaneWidth\"",
            "TodoTitleEditorHeight = \"Todo.TitleEditorHeight\"",
            "QuickCaptureMasterPaneWidth = \"QuickCaptureMasterPaneWidth\"",
        ];

        foreach (string alias in requiredAliases)
        {
            Assert.Contains(alias, registry, StringComparison.Ordinal);
        }

        Assert.Equal(18, requiredAliases.Length);
    }

    [Fact]
    public void QuickCaptureMasterPaneWidth_DeclaredExactlyOnce()
    {
        // The literal used to be declared privately in both surfaces; the
        // only remaining consumer is the live surface (the dead
        // QuickCaptureWidgetWindow family was deleted upstream in 1.5.6).
        string[] consumers =
        [
            "src/DeskBox/Controls/WidgetContents/QuickCaptureSurfaceContent.xaml.cs",
        ];

        foreach (string consumer in consumers)
        {
            string source = File.ReadAllText(TestPaths.SourceFile(consumer));
            Assert.DoesNotContain(
                "const string MasterPaneWidthMetadataKey",
                source,
                StringComparison.Ordinal);
            Assert.Contains(
                "WidgetMetadataKeys.QuickCaptureMasterPaneWidth",
                source,
                StringComparison.Ordinal);
        }

        Assert.False(
            File.Exists(TestPaths.FromRepository(
                "src/DeskBox/Views/QuickCaptureWidgetWindow.ResponsiveDetail.cs")),
            "The dead QuickCaptureWidgetWindow family must stay deleted.");
    }
}
