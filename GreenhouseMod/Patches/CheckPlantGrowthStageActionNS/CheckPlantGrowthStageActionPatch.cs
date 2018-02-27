using Harmony;
using Harmony.ILCopying;
using Staxel.EntityActions;
using Staxel.Logic;
using System.Collections.Generic;
using System.Reflection;

namespace GreenhouseMod.Patches.CheckPlantGrowthStageActionNS
{
	[HarmonyPatch(typeof(CheckPlantGrowthStageAction), "Start")]
	class CheckPlantGrowthStageActionPatch
	{
		/// <summary>
		/// Replaces CheckPlantGrowthAction.Start method IL with our own
		/// </summary>
		/// <param name="instructions"></param>
		/// <returns></returns>
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> TranspileStart(IEnumerable<CodeInstruction> instructions)
		{
			MethodBase ReplacementMethod = typeof(CheckPlantGrowthStageActionPatch).GetMethod("Start");
			List<ILInstruction> MethodILInstructions = MethodBodyReader.GetInstructions(ReplacementMethod);
			List<CodeInstruction> ReplacementMethodInstructions = new List<CodeInstruction>();

			foreach (ILInstruction MethodILInstruction in MethodILInstructions)
			{
				ReplacementMethodInstructions.Add(MethodILInstruction.GetCodeInstruction());
			}

			return ReplacementMethodInstructions;
		}

		/// <summary>
		/// Intermediate method, otherwise the MethodBodyReader throws exceptions @todo research this to do it without intermediate method
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="facade"></param>
		public void Start(Entity entity, EntityUniverseFacade facade)
		{
			GreenhouseModManager.Instance.PlantLogic.CheckPlantGrowthStage(entity, facade);
		}
	}
}
