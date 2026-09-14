using SubmarineTracker.Resources;

namespace SubmarineTracker.Windows.Builder;

public partial class BuilderWindow
{
    private const int AvailablePartsCount = 10;
    private const int AvailablePartTypes = 4;
    private static readonly string[] AvailablePartSets = ["Shark", "Unkiu", "Whale", "Coelacanth", "Syldra", "MShark", "MUnkiu", "MWhale", "MCoelacanth", "MSyldra"];
    private static readonly string[] AvailablePartNames = ["Hull", "Stern", "Bow", "Bridge"];

    private void EnsureAvailableShipParts()
    {
        var config = Plugin.Configuration;
        var parts = config.AvailableShipParts;
        if (parts == null || parts.Length != AvailablePartsCount * AvailablePartTypes)
        {
            config.AvailableShipParts = Enumerable.Repeat(true, AvailablePartsCount * AvailablePartTypes).ToArray();
            parts = config.AvailableShipParts;
        }

        // Migrate selections made by the previous version, which kept separate pools for
        // the Ship and Leveling solvers. Prefer the Leveling selection when it was customized,
        // otherwise use the Ship selection.
        if (config.Version < 3)
        {
            var leveling = config.LevelingSolverParts;
            var ship = config.ShipSolverParts;
            if (leveling is { Length: AvailablePartsCount * AvailablePartTypes } && leveling.Any(x => !x))
                config.AvailableShipParts = leveling.ToArray();
            else if (ship is { Length: AvailablePartsCount * AvailablePartTypes } && ship.Any(x => !x))
                config.AvailableShipParts = ship.ToArray();

            config.Version = 3;
            config.Save();
        }
    }

    private bool IsAvailableShipPartEnabled(int set, int type)
    {
        EnsureAvailableShipParts();
        return Plugin.Configuration.AvailableShipParts[(set * AvailablePartTypes) + type];
    }

    private void SetAvailableShipPartEnabled(int set, int type, bool enabled)
    {
        EnsureAvailableShipParts();
        Plugin.Configuration.AvailableShipParts[(set * AvailablePartTypes) + type] = enabled;
    }

    private int GetAvailableShipPartId(int set, int type)
    {
        // Sheet IDs are ordered Bow, Bridge, Hull, Stern, while the UI is Hull, Stern, Bow, Bridge.
        var baseId = (set % 5) * 4 + 1 + (set >= 5 ? 20 : 0);
        return type switch
        {
            0 => baseId + 2,
            1 => baseId + 3,
            2 => baseId,
            3 => baseId + 1,
            _ => baseId
        };
    }

    private bool IsAvailableShipPartIdEnabled(int partId)
    {
        for (var set = 0; set < AvailablePartsCount; set++)
        for (var type = 0; type < AvailablePartTypes; type++)
            if (GetAvailableShipPartId(set, type) == partId)
                return IsAvailableShipPartEnabled(set, type);

        return false;
    }

    private IEnumerable<int> GetAvailableShipPartIds(int type)
    {
        for (var set = 0; set < AvailablePartsCount; set++)
            if (IsAvailableShipPartEnabled(set, type))
                yield return GetAvailableShipPartId(set, type);
    }

    private bool AvailableShipPartsTab()
    {
        using var tabItem = ImRaii.TabItem("Available Ship Parts##AvailableShipParts");
        if (!tabItem.Success)
            return false;

        EnsureAvailableShipParts();

        ImGui.TextUnformatted("Select the individual submarine parts that are available to the solvers.");
        ImGuiHelpers.ScaledDummy(3.0f);

        if (ImGui.Button("Enable All##availableParts"))
        {
            Array.Fill(Plugin.Configuration.AvailableShipParts, true);
            Plugin.Configuration.Save();
            if (Plugin.Configuration.RestrictShipSolverPartsPool)
                RefreshList();
        }

        ImGui.SameLine();
        if (ImGui.Button("Disable All##availableParts"))
        {
            // Keep at least one part in each component slot so a restricted solver can still form builds.
            Array.Fill(Plugin.Configuration.AvailableShipParts, false);
            for (var type = 0; type < AvailablePartTypes; type++)
                Plugin.Configuration.AvailableShipParts[type] = true;
            Plugin.Configuration.Save();
            if (Plugin.Configuration.RestrictShipSolverPartsPool)
                RefreshList();
        }

        ImGui.SameLine();
        if (ImGui.Button("Disable Improved##availableParts"))
        {
            for (var set = 5; set < AvailablePartsCount; set++)
                for (var type = 0; type < AvailablePartTypes; type++)
                    SetAvailableShipPartEnabled(set, type, false);
            Plugin.Configuration.Save();
            if (Plugin.Configuration.RestrictShipSolverPartsPool)
                RefreshList();
        }

        using var table = ImRaii.Table("##availableShipParts", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp);
        if (!table.Success)
            return true;

        ImGui.TableSetupColumn("Set");
        foreach (var name in AvailablePartNames)
            ImGui.TableSetupColumn(name);
        ImGui.TableHeadersRow();

        for (var set = 0; set < AvailablePartsCount; set++)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(AvailablePartSets[set]);

            for (var type = 0; type < AvailablePartTypes; type++)
            {
                ImGui.TableNextColumn();
                var enabled = IsAvailableShipPartEnabled(set, type);
                if (ImGui.Checkbox($"##availablePart_{set}_{type}", ref enabled))
                {
                    // Do not permit a component type to have no available parts.
                    if (!enabled && GetAvailableShipPartIds(type).Count() <= 1)
                        enabled = true;
                    else
                    {
                        SetAvailableShipPartEnabled(set, type, enabled);
                        Plugin.Configuration.Save();
                        if (Plugin.Configuration.RestrictShipSolverPartsPool)
                            RefreshList();
                    }
                }
            }
        }

        return true;
    }
}
