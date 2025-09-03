using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Drawing;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace ShowGroundEffects;

public class ShowGroundEffects : BaseSettingsPlugin<ShowGroundEffectsSettings>
{
    private Camera Camera => GameController.Game.IngameState.Camera;
    List<string> list = new List<string>();
    private readonly HashSet<string> _debugBuffNames = new();
    private long _lastDebugLogTicks;
    private static readonly long DebugLogIntervalTicks = TimeSpan.FromMilliseconds(500).Ticks;

    public override void DrawSettings()
    {
        base.DrawSettings();

        foreach (var str in _debugBuffNames)
        {
            ImGui.Text(str);
        }
    }

    public override void Render()
    {
        try
        {
            if (!Settings.Enable
            || GameController.Area.CurrentArea == null
            || GameController.Area.CurrentArea.IsTown
            || GameController.Area.CurrentArea.IsHideout
            || GameController.IsLoading
            || !GameController.InGame
            || GameController.Game.IngameState.IngameUi.StashElement?.IsVisibleLocal == true
            )
            {
                return;
            }

            // Cache screen rectangle once per frame for culling
            var windowRect = GameController.Window.GetWindowRectangleTimeCache;
            var screenRect = new RectangleF
            {
                X = 0,
                Y = 0,
                Width = windowRect.Size.X,
                Height = windowRect.Size.Y
            };

            if (GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Daemon, out var daemons) && daemons is not null)
            {
                foreach (var daemon in daemons)
                {
                    if (daemon.Path == null || !daemon.IsHostile) continue;
                    if (daemon.DistancePlayer > Settings.RenderDistance) continue;
                    if (daemon.Path.Contains("UberMapExarchDaemon"))
                    {
                        var positioned = daemon.GetComponent<Positioned>();
                        if (positioned == null) continue;
                        DrawFilledCircleInWorldPosition(GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition), positioned.Size, 1, Settings.FireColor);
                    }
                }
            }

            if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Effect, out var effects) || effects is null)
                return;
            foreach (var e in effects.ToArray())
            {
                if (e.Path == null || !e.IsHostile) continue;
                if (e.DistancePlayer > Settings.RenderDistance) continue;
                if (!e.Path.Contains("ground_effects", StringComparison.OrdinalIgnoreCase)) continue;

                var buffs = e.Buffs;
                if (buffs == null) continue;

                // Choose draw color once per effect based on its buffs
                Color? drawColor = null;
                string? chosenBuffName = null;
                foreach (var bf in buffs)
                {
                    var desc = bf.Description;
                    if (string.IsNullOrEmpty(desc)) continue;

                    if (desc.Contains("fire", StringComparison.OrdinalIgnoreCase) || desc.Contains("burning", StringComparison.OrdinalIgnoreCase))
                    { drawColor = Settings.FireColor; chosenBuffName = bf.Name; break; }
                    if (desc.Contains("cold", StringComparison.OrdinalIgnoreCase))
                    { drawColor = Settings.ColdColor; chosenBuffName = bf.Name; break; }
                    if (desc.Contains("lightning", StringComparison.OrdinalIgnoreCase))
                    { drawColor = Settings.LightningColor; chosenBuffName = bf.Name; break; }
                    if (desc.Contains("chaos", StringComparison.OrdinalIgnoreCase))
                    { drawColor = Settings.ChaosColor; chosenBuffName = bf.Name; break; }
                    if (desc.Contains("physical", StringComparison.OrdinalIgnoreCase))
                    { drawColor = Settings.PhysicalColor; chosenBuffName = bf.Name; break; }
                }
                drawColor ??= Color.HotPink;

                // Optional debug visuals, throttled and with reduced allocations
                if (Settings.DebugMode)
                {
                    if (!string.IsNullOrEmpty(chosenBuffName))
                    {
                        _debugBuffNames.Add(chosenBuffName);
                    }

                    var screen = Camera.WorldToScreen(e.Pos);
                    Graphics.DrawText(chosenBuffName ?? e.Path, screen);
                    var background = new ExileCore2.Shared.RectangleF(screen.X, screen.Y, 150, 20);
                    Graphics.DrawBox(background, Color.Black);

                    var nowTicks = DateTime.UtcNow.Ticks;
                    if (nowTicks - _lastDebugLogTicks >= DebugLogIntervalTicks)
                    {
                        var positionedComponent = e.GetComponent<Positioned>();
                        if (positionedComponent != null)
                        {
                            DebugWindow.LogMsg(positionedComponent.GridPosition.X + " , " + positionedComponent.GridPosition.Y + " Size: " + positionedComponent.Size);
                        }
                        _lastDebugLogTicks = nowTicks;
                    }
                }

                var rComp = e.GetComponent<Render>();
                if (rComp is not null)
                {
                    var radius = Math.Max(5f, rComp.Bounds.X * 4f);
                    DrawCircleInWorldPos(false, e.Pos, radius, 5, drawColor.Value, screenRect);
                }
            }
        }
        catch (Exception ex)
        {
            if (Settings.DebugMode)
                DebugWindow.LogMsg($"ShowGroundEffects error: {ex.Message}");
        }
    }
    /// <summary>
    /// Draws a circle at the specified world position with the given radius and color (optionally filled).
    /// </summary>
    private void DrawCircleInWorldPos(bool drawFilledCircle, Vector3 position, float radius, int thickness, Color color, RectangleF screensize)
    {
        var entityPos = RemoteMemoryObject.TheGame.IngameState.Camera.WorldToScreen(position);
        if (IsEntityWithinScreen(entityPos, screensize, 50))
        {
            if (drawFilledCircle)
            {
                Graphics.DrawFilledCircleInWorld(position, radius, color);
            }
            else
            {
                Graphics.DrawCircleInWorld(position, radius, color, thickness);
            }
        }
    }
    private bool IsEntityWithinScreen(Vector2 entityPos, RectangleF screensize, float allowancePX)
    {
        // Check if the entity position is within the screen bounds with allowance
        var leftBound = screensize.Left - allowancePX;
        var rightBound = screensize.Right + allowancePX;
        var topBound = screensize.Top - allowancePX;
        var bottomBound = screensize.Bottom + allowancePX;

        return entityPos.X >= leftBound && entityPos.X <= rightBound && entityPos.Y >= topBound && entityPos.Y <= bottomBound;
    }
    private void DrawFilledCircleInWorldPosition(Vector3 position, float radius, int thickness, Color color)
    {
        Graphics.DrawFilledCircleInWorld(position, radius, color);
    }
}
