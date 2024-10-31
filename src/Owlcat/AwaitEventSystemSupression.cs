using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker.UI.Selection;

namespace CinematicUnityExplorer.Owlcat;
internal class AwaitEventSystemSupression
{
    [HarmonyPatch]
    internal static class KingmakerInputModule_CheckEventSystem
    {
        static MethodBase? TargetMethod() =>
            typeof(KingmakerInputModule)
                .GetNestedTypes(AccessTools.all)
                .Where(t => t.GetCustomAttributes<CompilerGeneratedAttribute>().Any())
                .Select(t => t.GetMethod("MoveNext", AccessTools.all))
                .FirstOrDefault();

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Main.Logger.Log($"{nameof(KingmakerInputModule_CheckEventSystem)}.{nameof(Transpiler)}");

            var match = new Func<CodeInstruction, bool>[]
            {
                ci => ci.opcode == OpCodes.Br_S,
                ci => ci.opcode == OpCodes.Ldstr && "Await event system".Equals(ci.operand),
                ci => ci.opcode == OpCodes.Call,
            };

            var iMatch = instructions.FindInstructionsIndexed(match);

            //foreach ((var index, var instruction) in iMatch)
            //{
            //    Main.Logger.Log($"{index}: {instruction}");
            //}

            if (!iMatch.Any())
            {
                Main.Logger.Log("No match found");
                return instructions;
            }

            var iList = instructions.ToList();

            foreach ((var index, var _) in iMatch)
            {
                var i = new CodeInstruction(OpCodes.Nop)
                {
                    labels = iList[index].labels,
                    blocks = iList[index].blocks
                };
                iList[index] = i;
            }

            return iList;
        }
    }
}
