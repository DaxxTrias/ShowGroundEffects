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
    private static readonly Dictionary<string, Color> AbyssMineMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Metadata/Monsters/LeagueAbyss/Fodder/PaleWalker3/AbyssCrystalMine", Color.Lime }
    };
    private static readonly Dictionary<string, Color> OtherHostileEffectDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Metadata/Monsters/BloodFeverKarui/BloodFeverBloater/Objects/BloodFeverPustule", Color.Red }, // A4 giants
        { "Metadata/Monsters/KaruiChieftain/objects/KaruiCaptainBoss3Pustule", Color.Red }, // A4 tavakai
        { "Metadata/Monsters/KaruiChieftain/objects/KaruiCaptainBossJadeStabSpike", Color.Red }, // A4 tavakai
        { "Metadata/Effects/Spells/monsters_effects/Act1_FOUR/CarrionCrone/IceSpike", Color.Blue }, //Ice Hag boss A5-Ogham
        { "Metadata/Monsters/Strongbox/Daemon/IceSpike", Color.Blue }, // generic ice hag spikes
    };
    private readonly Dictionary<string, Color> _otherHostileEffectMetaColors = new(StringComparer.OrdinalIgnoreCase);
    private string _otherHostileEffectRaw = string.Empty;
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
                //Metadata/Monsters/LeagueAbyss/Fodder/PaleWalker3/AbyssCrystalMine
                
				foreach (var daemon in daemons)
                {
                    if (daemon.Path == null) continue;
                    var path = daemon.Path;
                    bool isExarchDaemon = path.Contains("UberMapExarchDaemon", StringComparison.OrdinalIgnoreCase);
                    bool isCurseDaemon = path.Contains("Metadata/Monsters/Daemon/MapMod", StringComparison.OrdinalIgnoreCase) && path.EndsWith("Daemon", StringComparison.OrdinalIgnoreCase);
                    if (!isExarchDaemon && !isCurseDaemon && !daemon.IsHostile) continue;
                    if (daemon.DistancePlayer > Settings.RenderDistance) continue;
                    if (isExarchDaemon)
                    {
                        var positioned = daemon.GetComponent<Positioned>();
                        if (positioned == null) continue;
                        var world = GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition);
                        // Use the culling-aware helper and cached screen rect
                        DrawCircleInWorldPos(true, world, Math.Max(5f, positioned.Size), 1, Settings.FireColor, screenRect);
                    }
                }
			}

            if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Effect, out var effects) || effects is null)
                return;

            // Pass 0: metadata-driven hostiles that live under EntityType.Effect (e.g., ice spikes)
            if (Settings.ShowOtherHostileEffects)
            {
                var extraTargets = GetOtherHostileEffectMetadataSet();
                if (extraTargets.Count > 0)
                {
                    DrawMetadataMatches(effects, extraTargets, 1f, Settings.OtherHostileEffectsColor.Value, "OtherHostileEffect", screenRect, requireHostile: true);
                }
            }

            foreach (var e in effects)
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

			// Additional pass: Curse zones are unique under Metadata/Monsters/CurseZones and are EntityType.None
			{
				var noneEntities = (IReadOnlyList<Entity>)null;
				if (GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.None, out var noneList) && noneList is not null)
				{
					noneEntities = noneList;
				}
				else
				{
					noneEntities = GameController?.EntityListWrapper?.OnlyValidEntities;
				}

				if (noneEntities is not null)
				{
					// 1) Curse zones (keep expanded radius)
					if (Settings.ShowCurseZones)
					{
						foreach (var ent in noneEntities)
						{
							var p = ent.Path;
							if (string.IsNullOrEmpty(p)) continue;
							if (!p.Contains("CurseZones", StringComparison.OrdinalIgnoreCase)) continue;
							if (ent.DistancePlayer > Settings.RenderDistance) continue;

							var positioned = ent.GetComponent<Positioned>();
							var renderComp = ent.GetComponent<Render>();

							float baseRadius = 0f;
							if (renderComp is not null)
							{
								baseRadius = Math.Max(5f, renderComp.Bounds.X * 4f);
							}
							else if (positioned is not null)
							{
								baseRadius = Math.Max(5f, positioned.Size);
							}
							else
							{
								continue;
							}

							var radius = baseRadius * 2.3f;
							Vector3 worldPos = positioned is not null
								? GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition)
								: ent.Pos;

							DrawCircleInWorldPos(false, worldPos, radius, 5, Color.Red, screenRect);

							if (Settings.DebugMode)
							{
								var screen = Camera.WorldToScreen(worldPos);
								Graphics.DrawText($"CurseZone: {p}", screen);
							}
						}
					}

					// 2) Abyss Crystal Mine metadata (standard radius, no expansion)
					if (Settings.ShowAbyssCrystalMines)
					{
						DrawMetadataMatches(noneEntities, AbyssMineMetadata, 1f, Color.Lime, "AbyssCrystalMine", screenRect);
					}

					// 3) Additional hostile effects (configurable metadata list)
					if (Settings.ShowOtherHostileEffects)
					{
						var extraTargets = GetOtherHostileEffectMetadataSet();
						if (extraTargets.Count > 0)
						{
							DrawMetadataMatches(noneEntities, extraTargets, 1f, Settings.OtherHostileEffectsColor.Value, "OtherHostileEffect", screenRect);
						}
					}
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

	private void DrawMetadataMatches(IEnumerable<Entity> entities, IReadOnlyDictionary<string, Color> metadataTargets, float radiusMultiplier, Color fallbackColor, string debugLabel, RectangleF screenRect, bool requireHostile = false)
	{
		if (metadataTargets.Count == 0) return;

		foreach (var ent in entities)
		{
			var path = ent.Path;
			if (string.IsNullOrEmpty(path)) continue;
			var hasColor = metadataTargets.TryGetValue(path, out var drawColor);
			if (!hasColor) continue;
			if (requireHostile && !ent.IsHostile) continue;
			if (ent.DistancePlayer > Settings.RenderDistance) continue;

			var positioned = ent.GetComponent<Positioned>();
			var renderComp = ent.GetComponent<Render>();

			float radius;
			if (renderComp is not null)
			{
				radius = Math.Max(5f, renderComp.Bounds.X * radiusMultiplier);
			}
			else if (positioned is not null)
			{
				radius = Math.Max(5f, positioned.Size * radiusMultiplier);
			}
			else
			{
				continue;
			}

			Vector3 worldPos = positioned is not null
				? GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition)
				: ent.Pos;

			DrawCircleInWorldPos(false, worldPos, radius, 5, hasColor ? drawColor : fallbackColor, screenRect);

			if (Settings.DebugMode)
			{
				var screen = Camera.WorldToScreen(worldPos);
				Graphics.DrawText($"{debugLabel}: {path}", screen);
			}
		}
	}

	private IReadOnlyDictionary<string, Color> GetOtherHostileEffectMetadataSet()
	{
		var raw = Settings.OtherHostileEffectMetadata?.Value ?? string.Empty;
		if (string.Equals(raw, _otherHostileEffectRaw, StringComparison.Ordinal))
			return _otherHostileEffectMetaColors;

		_otherHostileEffectRaw = raw;
		_otherHostileEffectMetaColors.Clear();

		// Seed with built-in defaults so they always render even if user config is empty/malformed.
		foreach (var kvp in OtherHostileEffectDefaults)
		{
			_otherHostileEffectMetaColors[kvp.Key] = kvp.Value;
		}

		var segments = raw.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (var segment in segments)
		{
			var cleaned = segment.Trim();
			if (cleaned.Length == 0) continue;

			var color = Settings.OtherHostileEffectsColor.Value; // default per-line fallback
			var meta = cleaned;

			var pipeIdx = cleaned.IndexOf('|');
			if (pipeIdx > 0 && pipeIdx < cleaned.Length - 1)
			{
				meta = cleaned[..pipeIdx].Trim();
				var colorText = cleaned[(pipeIdx + 1)..].Trim();
				if (!TryParseColor(colorText, out color))
				{
					color = Settings.OtherHostileEffectsColor;
				}
			}

			if (meta.Length == 0) continue;
			_otherHostileEffectMetaColors[meta] = color;
		}

		return _otherHostileEffectMetaColors;
	}

	private static bool TryParseColor(string text, out Color color)
	{
		// Try HTML/hex via ColorTranslator; supports #RRGGBB or named colors
		try
		{
			color = ColorTranslator.FromHtml(text);
			return true;
		}
		catch
		{
			color = default;
			return false;
		}
	}
}
