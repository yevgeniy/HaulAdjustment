using System.Linq;

namespace PickUpAndHaul;
public static class ModCompatibilityCheck
{
	public static bool CombatExtendedIsActive = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Combat Extended");
	public static bool VehicleIsActive = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Vehicle Framework");

    public static bool AllowToolIsActive = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Allow Tool");

	public static bool ExtendedStorageIsActive { get; } = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "ExtendedStorageFluffyHarmonised")
		|| ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Extended Storage")
		|| ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Core SK");

	public static bool HCSKIsActive = ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Core SK");
}