using Dalamud.Interface;
using Dalamud.Interface.Components;
using SubmarineTracker.Data;
using SubmarineTracker.Resources;
using static SubmarineTracker.Utils;

namespace SubmarineTracker.Windows.Builder;

public partial class BuilderWindow
{
    public static List<Build.SubmarineBuild> AllBuilds = [];
    public int SelectedRank;
    public bool FuturePrediction;
    private Build.SubRank Rank;
    private const int PartsCount = 10;
    private const int PartTypes = 4;
    private static readonly string[] ShipPartSets = ["Shark", "Unkiu", "Whale", "Coelacanth", "Syldra", "MShark", "MUnkiu", "MWhale", "MCoelacanth", "MSyldra"];
    private static readonly string[] ShipPartNames = ["Hull", "Stern", "Bow", "Bridge"];
    private TargetValues Target;
    private TargetValues LockedTarget;

    private bool IgnoreBreakpoints;

    public void InitializeShip()
    {
        EnsureShipSolverParts();
        RebuildShipSolverBuilds();
        Target = new TargetValues(LockedTarget);
    }

    private void EnsureShipSolverParts()
    {
        var parts = Plugin.Configuration.ShipSolverParts;
        if (parts == null || parts.Length != PartsCount * PartTypes)
            Plugin.Configuration.ShipSolverParts = Enumerable.Repeat(true, PartsCount * PartTypes).ToArray();
    }

    private bool IsShipSolverPartEnabled(int set, int type)
    {
        EnsureShipSolverParts();
        return Plugin.Configuration.ShipSolverParts[(set * PartTypes) + type];
    }

    private void SetShipSolverPartEnabled(int set, int type, bool enabled)
    {
        EnsureShipSolverParts();
        Plugin.Configuration.ShipSolverParts[(set * PartTypes) + type] = enabled;
    }

    private int GetShipPartId(int set, int type)
    {
        // Sheet IDs are ordered Bow, Bridge, Hull, Stern, while the UI is Hull, Stern, Bow, Bridge.
        var baseId = (set % 5) * 4 + 1 + (set >= 5 ? 20 : 0);
        return type switch
        {
            0 => baseId + 2, // Hull
            1 => baseId + 3, // Stern
            2 => baseId,     // Bow
            3 => baseId + 1, // Bridge
            _ => baseId
        };
    }

    private IEnumerable<int> EnabledShipPartIds(int type)
    {
        for (var set = 0; set < PartsCount; set++)
            if (IsShipSolverPartEnabled(set, type))
                yield return GetShipPartId(set, type);
    }

    private void RebuildShipSolverBuilds()
    {
        EnsureShipSolverParts();

        var hulls = EnabledShipPartIds(0).ToArray();
        var sterns = EnabledShipPartIds(1).ToArray();
        var bows = EnabledShipPartIds(2).ToArray();
        var bridges = EnabledShipPartIds(3).ToArray();

        AllBuilds.Clear();
        if (hulls.Length == 0 || sterns.Length == 0 || bows.Length == 0 || bridges.Length == 0)
        {
            LockedTarget = new TargetValues();
            return;
        }

        foreach (var hull in hulls)
        foreach (var stern in sterns)
        foreach (var bow in bows)
        foreach (var bridge in bridges)
            AllBuilds.Add(new Build.SubmarineBuild(SelectedRank, hull, stern, bow, bridge));

        LockedTarget = new TargetValues(AllBuilds);
    }

    public void RefreshList()
    {
        RebuildShipSolverBuilds();
        if (AllBuilds.Count == 0)
            return;

        Target.MinSurveillance = Math.Clamp(Target.MinSurveillance, LockedTarget.MinSurveillance, LockedTarget.MaxSurveillance);
        Target.MaxSurveillance = Math.Clamp(Target.MaxSurveillance, LockedTarget.MinSurveillance, LockedTarget.MaxSurveillance);
        Target.MinRetrieval = Math.Clamp(Target.MinRetrieval, LockedTarget.MinRetrieval, LockedTarget.MaxRetrieval);
        Target.MaxRetrieval = Math.Clamp(Target.MaxRetrieval, LockedTarget.MinRetrieval, LockedTarget.MaxRetrieval);
        Target.MinSpeed = Math.Clamp(Target.MinSpeed, LockedTarget.MinSpeed, LockedTarget.MaxSpeed);
        Target.MaxSpeed = Math.Clamp(Target.MaxSpeed, LockedTarget.MinSpeed, LockedTarget.MaxSpeed);
        Target.MinRange = Math.Clamp(Target.MinRange, LockedTarget.MinRange, LockedTarget.MaxRange);
        Target.MaxRange = Math.Clamp(Target.MaxRange, LockedTarget.MinRange, LockedTarget.MaxRange);
        Target.MinFavor = Math.Clamp(Target.MinFavor, LockedTarget.MinFavor, LockedTarget.MaxFavor);
        Target.MaxFavor = Math.Clamp(Target.MaxFavor, LockedTarget.MinFavor, LockedTarget.MaxFavor);
    }

    private void DrawShipSolverPartOptions()
    {
        EnsureShipSolverParts();

        if (!ImGui.CollapsingHeader("Available Ship Parts"))
            return;

        ImGui.TextUnformatted("Select individual parts to include in the solver:");
        ImGuiHelpers.ScaledDummy(3.0f);

        if (ImGui.Button("Enable All"))
        {
            Array.Fill(Plugin.Configuration.ShipSolverParts, true);
            Plugin.Configuration.Save();
            RefreshList();
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Improved"))
        {
            for (var set = 5; set < PartsCount; set++)
                for (var type = 0; type < PartTypes; type++)
                    SetShipSolverPartEnabled(set, type, false);
            Plugin.Configuration.Save();
            RefreshList();
        }

        using var table = ImRaii.Table("##shipSolverParts", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("Set");
        foreach (var name in ShipPartNames)
            ImGui.TableSetupColumn(name);
        ImGui.TableHeadersRow();

        for (var set = 0; set < PartsCount; set++)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(ShipPartSets[set]);

            for (var type = 0; type < PartTypes; type++)
            {
                ImGui.TableNextColumn();
                var enabled = IsShipSolverPartEnabled(set, type);
                if (ImGui.Checkbox($"##solverPart_{set}_{type}", ref enabled))
                {
                    // Never allow an entire component slot to have zero possible parts.
                    // This keeps the solver's Cartesian product valid.
                    if (!enabled && EnabledShipPartIds(type).Count() <= 1)
                        enabled = true;
                    else
                    {
                        SetShipSolverPartEnabled(set, type, enabled);
                        Plugin.Configuration.Save();
                        RefreshList();
                    }
                }
            }
        }
    }

    public IEnumerable<Tuple<Build.SubmarineBuild, TimeSpan>> FilterBuilds()
    {
        uint distance = 0;
        var hasRoute = CurrentBuild.Sectors.Count > 0;
        if (hasRoute)
            distance = CurrentBuild.OptimizedDistance;

        var builds = AllBuilds
            .Where(b => SelectedRank >= b.HighestRankPart() && b.Range >= distance && b.BuildCost <= Rank.Capacity)
            .Where(hasRoute && !IgnoreBreakpoints ? Target.GetSectorFilter(CurrentBuild.Sectors) : Target.GetFilter())
            .Select(t => new Tuple<Build.SubmarineBuild, TimeSpan>(t, new TimeSpan(12, 0, 0)));
        if (hasRoute)
        {
            builds = builds.Select(tuple =>
            {
                var (build, _) = tuple;
                return new Tuple<Build.SubmarineBuild, TimeSpan>(build, TimeSpan.FromSeconds(Voyage.CalculateDuration(CurrentBuild.OptimizedRoute, build.Speed)));
            });
        }

        return builds;
    }

    public IEnumerable<Tuple<Build.SubmarineBuild, TimeSpan>> SortBuilds(ImGuiTableColumnSortSpecsPtr sortSpecsPtr)
    {
        Func<Tuple<Build.SubmarineBuild, TimeSpan>, int> sortFunc = sortSpecsPtr.ColumnIndex switch
        {
            0 => x => x.Item1.BuildCost,
            1 => x => x.Item1.RepairCosts,
            2 => x => (int)x.Item1.Hull.RowId,
            3 => x => (int)x.Item1.Stern.RowId,
            4 => x => (int)x.Item1.Bow.RowId,
            5 => x => (int)x.Item1.Bridge.RowId,
            6 => x => x.Item1.Surveillance,
            7 => x => x.Item1.Retrieval,
            8 => x => x.Item1.Favor,
            9 => x => x.Item1.Speed,
            10 => x => x.Item1.Range,
            _ => _ => 0
        };

        return sortSpecsPtr.SortDirection switch
        {
            ImGuiSortDirection.Ascending => FilterBuilds().OrderBy(sortFunc),
            ImGuiSortDirection.Descending => FilterBuilds().OrderByDescending(sortFunc),
            _ => FilterBuilds()
        };
    }

    public bool ShipTab()
    {
        using var tabItem = ImRaii.TabItem($"{Language.BuilderTabShip}##Ship");
        if (!tabItem.Success)
            return false;

        if (!FuturePrediction && SelectedRank > Sheets.LastRank)
            SelectedRank = (int)Sheets.LastRank;

        if (ImGui.SliderInt("##shipSliderRank", ref SelectedRank, 1, (int)Sheets.LastRank + (!FuturePrediction ? 0 : 50), $"{Language.TermsRank} %d"))
        {
            Rank = Build.SubRank.From((uint)SelectedRank);
            RefreshList();
        }

        ImGui.SameLine();
        ImGui.Checkbox(Language.BuilderShipCheckboxIgnoreBreakpoints, ref IgnoreBreakpoints);

        ImGui.SameLine();
        ImGui.Checkbox("Predict Future", ref FuturePrediction);

        DrawShipSolverPartOptions();
        ImGuiHelpers.ScaledDummy(5.0f);

        Helper.TextColored(ImGuiColors.DalamudViolet, Language.BuilderShipHeaderRoute);
        SelectedRoute();

        ImGuiHelpers.ScaledDummy(5.0f);

        var hasRoute = CurrentBuild.Sectors.Count > 0;
        if (!hasRoute || IgnoreBreakpoints)
        {
            if (ImGui.CollapsingHeader(Language.TermsStats))
            {
                var textWidth = ImGui.CalcTextSize("Surveillance:").X + (15.0f * ImGuiHelpers.GlobalScale);
                var sliderWidth = ImGui.GetWindowWidth() / 3;

                ImGui.TextUnformatted($"{Language.TermsSurveillance}:");
                ImGui.SameLine(textWidth);
                using (ImRaii.ItemWidth(sliderWidth))
                {
                    if (ImGui.SliderInt("##shipSliderMinSurveillance", ref Target.MinSurveillance, LockedTarget.MinSurveillance, LockedTarget.MaxSurveillance, "Min %d"))
                        Target.MaxSurveillance = Math.Max(Target.MinSurveillance, Target.MaxSurveillance);

                    ImGui.SameLine();

                    if (ImGui.SliderInt("##shipSliderMaxSurveillance", ref Target.MaxSurveillance, LockedTarget.MinSurveillance, LockedTarget.MaxSurveillance, "Max %d"))
                        Target.MinSurveillance = Math.Min(Target.MinSurveillance, Target.MaxSurveillance);
                }

                ImGui.TextUnformatted($"{Language.TermsRetrieval}:");
                ImGui.SameLine(textWidth);
                using (ImRaii.ItemWidth(sliderWidth))
                {
                    if (ImGui.SliderInt("##shipSliderMinRetrieval", ref Target.MinRetrieval, LockedTarget.MinRetrieval, LockedTarget.MaxRetrieval, "Min %d"))
                        Target.MaxRetrieval = Math.Max(Target.MinRetrieval, Target.MaxRetrieval);

                    ImGui.SameLine();

                    if (ImGui.SliderInt("##shipSliderMaxRetrieval", ref Target.MaxRetrieval, LockedTarget.MinRetrieval, LockedTarget.MaxRetrieval, "Max %d"))
                        Target.MinRetrieval = Math.Min(Target.MinRetrieval, Target.MaxRetrieval);
                }

                ImGui.TextUnformatted($"{Language.TermsFavor}:");
                ImGui.SameLine(textWidth);
                using (ImRaii.ItemWidth(sliderWidth))
                {
                    if (ImGui.SliderInt("##shipSliderMinFavor", ref Target.MinFavor, LockedTarget.MinFavor, LockedTarget.MaxFavor, "Min %d"))
                        Target.MaxFavor = Math.Max(Target.MinFavor, Target.MaxFavor);

                    ImGui.SameLine();

                    if (ImGui.SliderInt("##shipSliderMaxFavor", ref Target.MaxFavor, LockedTarget.MinFavor, LockedTarget.MaxFavor, "Max %d"))
                        Target.MinFavor = Math.Min(Target.MinFavor, Target.MaxFavor);
                }

                ImGui.TextUnformatted($"{Language.TermsSpeed}:");
                ImGui.SameLine(textWidth);
                using (ImRaii.ItemWidth(sliderWidth))
                {
                    if (ImGui.SliderInt("##shipSliderMinSpeed", ref Target.MinSpeed, LockedTarget.MinSpeed, LockedTarget.MaxSpeed, "Min %d"))
                        Target.MaxSpeed = Math.Max(Target.MinSpeed, Target.MaxSpeed);

                    ImGui.SameLine();

                    if (ImGui.SliderInt("##shipSliderMaxSpeed", ref Target.MaxSpeed, LockedTarget.MinSpeed, LockedTarget.MaxSpeed, "Max %d"))
                        Target.MinSpeed = Math.Min(Target.MinSpeed, Target.MaxSpeed);
                }
            }

            ImGuiHelpers.ScaledDummy(10.0f);
        }
        else
        {
            var secondRow = ImGui.GetWindowWidth() / 5.1f;

            var breakpoints = Sectors.CalculateBreakpoint(CurrentBuild.Sectors);

            Helper.TextColored(ImGuiColors.DalamudViolet, $"{Language.TermsBreakpoints}:");
            Helper.TextColored(ImGuiColors.HealerGreen, Language.TermsSurveillance);
            ImGui.SameLine(secondRow);
            ImGui.TextUnformatted($"T2: {breakpoints.T2} | T3: {breakpoints.T3}");

            Helper.TextColored(ImGuiColors.HealerGreen, Language.TermsRetrieval);
            ImGui.SameLine(secondRow);
            ImGui.TextUnformatted($"{Language.TermsNormal}: {breakpoints.Normal} | {Language.TermsOptimal}: {breakpoints.Optimal}");

            Helper.TextColored(ImGuiColors.HealerGreen, Language.TermsFavor);
            ImGui.SameLine(secondRow);
            ImGui.TextUnformatted($"{Language.TermsFavor}: {breakpoints.Favor}");

            Helper.TextColored(ImGuiColors.DalamudViolet, $"{Language.TermsOptions}:");

            if (ImGui.Checkbox(Language.BuilderShipCheckboxT1, ref Target.UseT1))
                Target.UseT2 = false;
            ImGui.SameLine();
            if (ImGui.Checkbox(Language.BuilderShipCheckboxT2, ref Target.UseT2))
                Target.UseT1 = false;

            if (ImGui.Checkbox(Language.BuilderShipCheckboxPoor, ref Target.UsePoor))
                Target.UseNormal = false;
            ImGui.SameLine();
            if (ImGui.Checkbox(Language.BuilderShipCheckboxNormal, ref Target.UseNormal))
                Target.UsePoor = false;

            ImGui.Checkbox(Language.BuilderShipCheckboxFavor, ref Target.IgnoreFavor);
            ImGui.SameLine();
            ImGui.Checkbox(Language.BuilderShipCheckboxModded, ref Target.NoModded);

            ImGuiHelpers.ScaledDummy(10.0f);
        }

        if (!FilterBuilds().Any())
        {
            ImGuiHelpers.ScaledDummy(20.0f);

            var text = Language.BuilderShipCalculationNothingFound;

            ImGui.SetCursorPosX((ImGui.GetWindowSize().X - ImGui.CalcTextSize(text).X) * 0.5f);
            Helper.TextColored(ImGuiColors.DalamudOrange, text);
            return true;
        }

        using var table = ImRaii.Table("##shipTable", hasRoute ? 13 : 12, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable);
        if (!table.Success)
            return true;

        ImGui.TableSetupColumn(Language.TermsCost);
        ImGui.TableSetupColumn(Language.TermsRepair);
        ImGui.TableSetupColumn(Language.TermsHull, ImGuiTableColumnFlags.NoSort);
        ImGui.TableSetupColumn(Language.TermsStern, ImGuiTableColumnFlags.NoSort);
        ImGui.TableSetupColumn(Language.TermsBow, ImGuiTableColumnFlags.NoSort);
        ImGui.TableSetupColumn(Language.TermsBridge, ImGuiTableColumnFlags.NoSort);
        ImGui.TableSetupColumn(Language.TermsSurveillance, ImGuiTableColumnFlags.PreferSortDescending);
        ImGui.TableSetupColumn(Language.TermsRetrieval, ImGuiTableColumnFlags.PreferSortDescending);
        ImGui.TableSetupColumn(Language.TermsFavor, ImGuiTableColumnFlags.PreferSortDescending);
        ImGui.TableSetupColumn(Language.TermsSpeed, ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending);
        ImGui.TableSetupColumn(Language.TermsRange, ImGuiTableColumnFlags.PreferSortDescending);
        if (hasRoute)
            ImGui.TableSetupColumn(Language.TermsDuration, ImGuiTableColumnFlags.NoSort);
        ImGui.TableSetupColumn("##Import", ImGuiTableColumnFlags.NoSort);

        ImGui.TableHeadersRow();
        var tableContent = SortBuilds(ImGui.TableGetSortSpecs().Specs).ToArray();

        using var clipper = new ListClipper(tableContent.Length, itemHeight: ImGui.GetTextLineHeight() * 1.1f);
        foreach (var i in clipper.Rows)
        {
            var (build, time) = tableContent[i];
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.BuildCost}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.RepairCosts}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.HullIdentifier}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.SternIdentifier}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.BowIdentifier}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.BridgeIdentifier}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.Surveillance}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.Retrieval}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.Favor}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.Speed}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{build.Range}");

            if (hasRoute)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(ToTime(time));
            }

            ImGui.TableNextColumn();
            if (ImGuiComponents.IconButton(i, FontAwesomeIcon.ArrowRightFromBracket))
            {
                CurrentBuild.UpdateBuild(build, SelectedRank);
                CurrentBuild.OriginalSub = 0;
            }

            if (ImGui.IsItemHovered())
                Helper.Tooltip(Language.BuilderShipTableSelect);

            ImGui.TableNextRow();
        }

        return true;
    }

    private struct TargetValues
    {
        public int MinSurveillance;
        public int MinRetrieval;
        public int MinSpeed;
        public int MinRange;
        public int MinFavor;
        public int MaxSurveillance;
        public int MaxRetrieval;
        public int MaxSpeed;
        public int MaxRange;
        public int MaxFavor;

        public bool UseT1 = false;
        public bool UseT2 = false;
        public bool UsePoor = false;
        public bool UseNormal = false;
        public bool IgnoreFavor = false;
        public bool NoModded = false;

        public TargetValues(List<Build.SubmarineBuild> allBuilds) : this()
        {
            MinSurveillance = allBuilds.Min(x => x.Surveillance);
            MinRetrieval = allBuilds.Min(x => x.Retrieval);
            MinSpeed = allBuilds.Min(x => x.Speed);
            MinRange = allBuilds.Min(x => x.Range);
            MinFavor = allBuilds.Min(x => x.Favor);
            MaxSurveillance = allBuilds.Max(x => x.Surveillance);
            MaxRetrieval = allBuilds.Max(x => x.Retrieval);
            MaxSpeed = allBuilds.Max(x => x.Speed);
            MaxRange = allBuilds.Max(x => x.Range);
            MaxFavor = allBuilds.Max(x => x.Favor);
        }

        public TargetValues(TargetValues lockedTarget)
        {
            MinSurveillance = lockedTarget.MinSurveillance;
            MinRetrieval = lockedTarget.MinRetrieval;
            MinSpeed = lockedTarget.MinSpeed;
            MinRange = lockedTarget.MinRange;
            MinFavor = lockedTarget.MinFavor;
            MaxSurveillance = lockedTarget.MaxSurveillance;
            MaxRetrieval = lockedTarget.MaxRetrieval;
            MaxSpeed = lockedTarget.MaxSpeed;
            MaxRange = lockedTarget.MaxRange;
            MaxFavor = lockedTarget.MaxFavor;
        }

        public Func<Build.SubmarineBuild, bool> GetFilter()
        {
            var tmpThis = this;
            return build =>
                build.Surveillance >= tmpThis.MinSurveillance &&
                build.Retrieval >= tmpThis.MinRetrieval &&
                build.Speed >= tmpThis.MinSpeed &&
                build.Range >= tmpThis.MinRange &&
                build.Favor >= tmpThis.MinFavor &&
                build.Surveillance <= tmpThis.MaxSurveillance &&
                build.Retrieval <= tmpThis.MaxRetrieval &&
                build.Speed <= tmpThis.MaxSpeed &&
                build.Range <= tmpThis.MaxRange &&
                build.Favor <= tmpThis.MaxFavor;
        }

        public Func<Build.SubmarineBuild, bool> GetSectorFilter(List<uint> path)
        {
            var breakpoints = Sectors.CalculateBreakpoint(path);
            var useT1 = UseT1;
            var useT2 = UseT2;
            var usePoor = UsePoor;
            var useNormal = UseNormal;
            var ignoreFavor = IgnoreFavor;
            var noModded = NoModded;

            return build =>
                build.Surveillance >= (useT1 ? 0 : useT2 ? breakpoints.T2 : breakpoints.T3) &&
                build.Retrieval >= (usePoor ? 0 : useNormal ? breakpoints.Normal : breakpoints.Optimal) &&
                (ignoreFavor || build.Favor >= breakpoints.Favor) &&
                (!noModded || build.HighestRankPart() < 50);
        }
    }
}
