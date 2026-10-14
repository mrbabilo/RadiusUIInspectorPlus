using System;
using System.Reflection;
using RadiusUI.Framework;
using RimWorld;
using Verse;

namespace RadiusUIInspector;

// Storage Sorting tunes one item or category of a storage (hit points, quality, freshness, stack cap,
// ingredients, priority) through a gear it draws on the vanilla tree rows. The storage panel draws the gear on
// its own rows and opens the same window.
internal static class StorageSortingCompat
{
	internal const string PackageId = "Ghosty.SortingMod";

	private const string WindowType = "SortingMod.UI.ItemFilterWindow";

	private const string TabType = "SortingMod.UI.FilterTab";

	private static bool? active;

	private static Type? windowType;

	private static object? generalTab;

	private static MethodInfo? storeTryGet;

	private static MethodInfo? dataTryGet;

	private static FieldInfo? enabledField;

	private static readonly object?[] storeArgs = new object?[2];

	private static readonly object?[] dataArgs = new object?[2];

	internal static bool Active
	{
		get
		{
			if (!active.HasValue)
			{
				active = false;
				if (ModCompat.IsActive(PackageId))
				{
					windowType = ModCompat.FindType(WindowType);
					Type? tabType = ModCompat.FindType(TabType);
					if (windowType == null || tabType == null)
					{
						Log.Warning("[Radius UI - Inspector] Storage Sorting is active but " + WindowType + " did not resolve (version drift?) - its per-row gear is left out.");
					}
					else
					{
						generalTab = Enum.ToObject(tabType, 0);
						active = true;
						// Only to light the gear of tuned rows: a missing member leaves every gear dim.
						storeTryGet = ModCompat.FindType("SortingMod.Data.StockpileFilterDataStore")?.GetMethod("TryGet", BindingFlags.Static | BindingFlags.Public);
						dataTryGet = ModCompat.FindType("SortingMod.Data.StockpileFilterData")?.GetMethod("TryGet", BindingFlags.Instance | BindingFlags.Public);
						enabledField = ModCompat.FindType("SortingMod.Data.ItemFilterSettings")?.GetField("Enabled", BindingFlags.Instance | BindingFlags.Public);
					}
				}
			}
			return active.Value;
		}
	}

	// Whether the storage has an enabled tuning for this def or category.
	internal static bool IsTuned(IStoreSettingsParent parent, string defName)
	{
		if (!Active || storeTryGet == null || dataTryGet == null || enabledField == null)
		{
			return false;
		}
		try
		{
			storeArgs[0] = parent;
			storeArgs[1] = null;
			if (!(bool)storeTryGet.Invoke(null, storeArgs))
			{
				return false;
			}
			dataArgs[0] = defName;
			dataArgs[1] = null;
			if (!(bool)dataTryGet.Invoke(storeArgs[1], dataArgs))
			{
				return false;
			}
			return (bool)enabledField.GetValue(dataArgs[1]);
		}
		catch (Exception)
		{
			storeTryGet = null;
			return false;
		}
	}

	internal static void Open(IStoreSettingsParent parent, string defName, string label)
	{
		if (!Active)
		{
			return;
		}
		try
		{
			Find.WindowStack.Add((Window)Activator.CreateInstance(windowType, parent, defName, label, generalTab));
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] Storage Sorting window failed to open: " + ex.Message, 5351411);
		}
	}
}
