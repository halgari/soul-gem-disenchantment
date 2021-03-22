using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using static Mutagen.Bethesda.FormKeys.SkyrimSE.Skyrim.Keyword;
using static Mutagen.Bethesda.FormKeys.SkyrimSE.Skyrim.SoulGem;

namespace Halgari.SoulGemDisenchantment
{
    class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetTypicalOpen(GameRelease.SkyrimSE, "HalgariSoulGemDisenchantment.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args);
        }

        public static List<(IFormLinkGetter<ISoulGemGetter> Filled, IFormLinkGetter<ISoulGemGetter> Empty)> SoulGems = new()
        {
            (SoulGemPettyFilled, SoulGemPetty),
            (SoulGemLesserFilled, SoulGemLesser),
            (SoulGemCommonFilled, SoulGemCommon),
            (SoulGemGreaterFilled, SoulGemGreater),
            (SoulGemGrandFilled, SoulGemGrand)
        };

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var allGems = state.LoadOrder.PriorityOrder.SoulGem().WinningOverrides()
                .ToDictionary(d => d.AsLinkGetter());

            var matchedGems = SoulGems.Select(g => (g.Filled, g.Empty, allGems[g.Filled])).ToArray();
            Console.WriteLine($"Matched {matchedGems.Length} gems in ESPs");
            foreach (var gem in matchedGems)
            {
                Console.WriteLine($" - {gem.Filled} - {gem.Item3.Value} septims");
            }

            var armors = state.LoadOrder.PriorityOrder.Armor().WinningOverrides()
                .Where(armor => !armor.ObjectEffect.IsNull)
                .Select(armor => (armor.AsLink<IItemGetter>(), armor.Value, armor.EditorID))
                .ToArray();

            Console.WriteLine($"Found {armors.Length} enchanted armors");

            var weapons = state.LoadOrder.PriorityOrder.Weapon().WinningOverrides()
                .Where(armor => !armor.ObjectEffect.IsNull)
                .Select(weapon => (weapon.AsLink<IItemGetter>(), weapon.BasicStats!.Value, weapon.EditorID))
                .ToArray();

            Console.WriteLine($"Found {weapons.Length} enchanted weapons");

            var baseRecipe = Mutagen.Bethesda.FormKeys.SkyrimSE.Skyrim.ConstructibleObject.RecipeIngotIron;

            var sortedGems = matchedGems.OrderByDescending(g => g.Item3.Value).ToArray();

            foreach (var (link, value, editorId) in armors.Concat(weapons))
            {
                var (filled, empty, _) = sortedGems.FirstOrDefault(gem => gem.Item3.Value <= value * 0.75);
                if (filled.IsNull)
                    continue;
                var key = state.PatchMod.GetNextFormKey();
                var cpy = state.PatchMod.ConstructibleObjects.AddNew(key);
                cpy.WorkbenchKeyword.SetTo(CraftingSmelter);
                cpy.CreatedObjectCount = 1;
                cpy.EditorID = editorId + "_ToGem";
                cpy.Conditions.Add(new ConditionFloat
                {
                    Data = new FunctionConditionData
                    {
                        Function = Condition.Function.GetItemCount,
                        ParameterOneRecord = link
                    },
                    CompareOperator = CompareOperator.GreaterThanOrEqualTo,
                    ComparisonValue = 1.0f
                });
                cpy.Items = new ExtendedList<ContainerEntry> {
                    new()
                    {
                        Item = new ContainerItem
                        {
                            Item = link,
                            Count = 1
                        }
                    },
                    new()
                    {
                        Item = new ContainerItem
                        {
                            Item = empty.AsSetter(),
                            Count = 1
                        }
                    }
                };
                cpy.CreatedObject.SetTo(filled);

            }

        }
    }
}