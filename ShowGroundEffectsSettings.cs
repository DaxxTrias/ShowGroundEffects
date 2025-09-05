using System.Collections.Generic;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using System.Drawing;

namespace ShowGroundEffects;

public class ShowGroundEffectsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Render distance")]
    public RangeNode<int> RenderDistance { get; set; } = new RangeNode<int>(80, 0, 100);
    [Menu("If you want to hide an element, set the color transparency to max", 100)]
    public EmptyNode Description { get; set; }
    [Menu("Fire damage", parentIndex = 100)]
    public ColorNode FireColor { get; set; } = new ColorNode(Color.Red);
    [Menu("Cold damage", parentIndex = 100)]
    public ColorNode ColdColor { get; set; } = new ColorNode(Color.Blue);
    [Menu("Lightning damage", parentIndex = 100)]
    public ColorNode LightningColor { get; set; } = new ColorNode(Color.Yellow);
    [Menu("Chaos damage", parentIndex = 100)]
    public ColorNode ChaosColor { get; set; } = new ColorNode(Color.Purple);
    [Menu("Physical damage", parentIndex = 100)]
    public ColorNode PhysicalColor { get; set; } = new ColorNode(Color.Brown);
    [Menu("Debug mode (shows encountered ground effects)")]
    public ToggleNode DebugMode { get; set; } = new ToggleNode(false);

    [Menu("Show curse zones in maps")]
    public ToggleNode ShowCurseZones { get; set; } = new ToggleNode(true);
}
