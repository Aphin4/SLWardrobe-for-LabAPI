using HarmonyLib;
using LabApi.Features.Wrappers;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using PlayerRoles.Visibility;
using System.Collections.Generic;
using System.Reflection.Emit;
using static HarmonyLib.AccessTools;

[HarmonyPatch(typeof(FpcServerPositionDistributor), nameof(FpcServerPositionDistributor.WriteAll))]
internal class GhostModePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator)
    {
        List<CodeInstruction> newInstructions = NorthwoodLib.Pools.ListPool<CodeInstruction>.Shared.Rent(instructions);

        const int offset = 6;
        int index = newInstructions.FindIndex(
            instruction => instruction.Calls(
                Method(typeof(VisibilityController),
                       nameof(VisibilityController.ValidateVisibility)))) + offset;

        newInstructions.InsertRange(index, new CodeInstruction[]
        {
            // receiver
            new(OpCodes.Ldarg_0),
            // referenceHub
            new(OpCodes.Ldloc_S, 5),
            // flag2
            new(OpCodes.Ldloca_S, 7),
            // HandleGhostMode(ReferenceHub, ReferenceHub, ref bool)
            new(OpCodes.Call,
                Method(typeof(GhostModePatch), nameof(HandleGhostMode),
                       new[] { typeof(ReferenceHub), typeof(ReferenceHub), typeof(bool).MakeByRefType() })),
        });

        for (int z = 0; z < newInstructions.Count; z++)
            yield return newInstructions[z];

        NorthwoodLib.Pools.ListPool<CodeInstruction>.Shared.Return(newInstructions);
    }

    private static void HandleGhostMode(
        ReferenceHub hubReceiver,
        ReferenceHub hubTarget,
        ref bool isInvisible)
    {
        // Already invisible - pass
        if (isInvisible)
            return;

        // Check invisiblity
        if (GhostModeManager.IsInvisible(hubTarget) ||
            GhostModeManager.IsInvisibleFor(hubTarget, hubReceiver))
        {
            isInvisible = true;
        }
    }
}

public static class GhostModeManager
{
    // Global invisiblity
    private static readonly HashSet<ReferenceHub> InvisiblePlayers = new();

    // Invisiblity for specific player
    private static readonly Dictionary<ReferenceHub, HashSet<ReferenceHub>> InvisibleFor = new();

    public static void SetInvisible(Player player, bool invisible)
    {
        if (invisible)
            InvisiblePlayers.Add(player.ReferenceHub);
        else
            InvisiblePlayers.Remove(player.ReferenceHub);
    }

    public static bool IsInvisible(ReferenceHub hub)
    {
        return InvisiblePlayers.Contains(hub);
    }

    public static void SetInvisibleFor(Player target, Player observer, bool invisible)
    {
        if (!InvisibleFor.ContainsKey(target.ReferenceHub))
            InvisibleFor[target.ReferenceHub] = new HashSet<ReferenceHub>();

        if (invisible)
            InvisibleFor[target.ReferenceHub].Add(observer.ReferenceHub);
        else
            InvisibleFor[target.ReferenceHub].Remove(observer.ReferenceHub);
    }

    public static bool IsInvisibleFor(ReferenceHub target, ReferenceHub observer)
    {
        return InvisibleFor.TryGetValue(target, out var set) && set.Contains(observer);
    }

    public static void Clear()
    {
        InvisiblePlayers.Clear();
        InvisibleFor.Clear();
    }

    public static void RemovePlayer(ReferenceHub hub)
    {
        InvisiblePlayers.Remove(hub);
        InvisibleFor.Remove(hub);
        foreach (var kvp in InvisibleFor)
            kvp.Value.Remove(hub);
    }
}