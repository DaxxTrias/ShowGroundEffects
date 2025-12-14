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

    [Menu("Show Curse Zones in maps", "Show Map-Modifier curses on the ground")]
    public ToggleNode ShowCurseZones { get; set; } = new ToggleNode(true);

    [Menu("Show Abyss Crystal Mines", "show Abyss Crystal Proximity Mines")]
    public ToggleNode ShowAbyssCrystalMines { get; set; } = new ToggleNode(true);

    [Menu("Other Hostile Effects", "Enable drawing for extra hostile metadata targets")]
    public ToggleNode ShowOtherHostileEffects { get; set; } = new ToggleNode(true);

    [Menu("Other Hostile Effects Color", parentIndex = 110)]
    public ColorNode OtherHostileEffectsColor { get; set; } = new ColorNode(Color.Red);

    [Menu("Other Hostile Effects Metadata", "Optional; one metadata path per line; add |#RRGGBB to override color; exact match; case-insensitive. Built-in defaults are applied automatically.", parentIndex = 110)]
    public TextNode OtherHostileEffectMetadata { get; set; } = new TextNode(string.Empty);
}
