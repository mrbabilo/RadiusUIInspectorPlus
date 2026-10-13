using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public class InspectorSettings : ModSettings
{
	public class LayoutPreset
	{
		public readonly string Id;

		public readonly string[] Rows;

		public string Label => ("RUI.Inspector.Preset" + Id).Translate();

		public string Blurb => ("RUI.Inspector.PresetDesc" + Id).Translate();

		public LayoutPreset(string id, string[] rows)
		{
			Id = id;
			Rows = rows;
		}
	}

	public bool enabled = true;

	public bool autoMinimize = true;

	public float minimizeDelay = 12f;

	public bool showVanillaTabs;

	public int paneWidth = 540;

	public bool widePanel = true;

	public int settingsVersion;

	private const int CurrentSettingsVersion = 2;

	public bool nativeStorageTab = true;

	public bool nativeContentsTab = true;

	public int storagePanelHeight = 430;

	public List<StorageBookmark> storageBookmarks = new List<StorageBookmark>();

	public List<string> layoutRows = new List<string>();

	public List<string> animalRows = new List<string>();

	public List<string> mechRows = new List<string>();

	public List<string> prisonerRows = new List<string>();

	public List<string> thingRows = new List<string>();

	public int paneHeight = 232;

	public int portraitSize;

	public float portraitZoom = 1f;

	public float portraitPanX;

	public float portraitPanY;

	public List<string> railRows = new List<string>();

	public List<string> animalRail = new List<string>();

	public List<string> mechRail = new List<string>();

	public List<string> prisonerRail = new List<string>();

	public List<string> hiddenTabs = new List<string>();

	public List<string> tabOrder = new List<string>();

	public bool moodTicks;

	public bool healthBands = true;

	public int healthRows = 3;

	public bool skillsHideZero;

	public int LayoutStamp;

	public static readonly string[] DefaultLayout = new string[18]
	{
		"header:Needs", "need:Joy|need:Comfort", "need:Beauty|need:Outdoors", "header:Health", "health", "header:Produce", "production", "header:Skills", "skill:Shooting|skill:Melee", "skill:Construction|skill:Mining",
		"skill:Social|skill:Intellectual", "header:Assignment", "area|timetable", "policies", "header:Equipment", "equip|armor", "mechenergy", "inspect"
	};

	public static readonly LayoutPreset[] Presets = new LayoutPreset[5]
	{
		new LayoutPreset("Glance", new string[4] { "header:Vitals", "mood", "need:Food|need:Rest", "need:Joy|need:Comfort" }),
		new LayoutPreset("Care", new string[13]
		{
			"header:Mood", "mood", "header:Needs", "need:Food|need:Rest", "need:Joy|need:Comfort", "need:Beauty|need:Outdoors", "header:Health", "health", "production", "header:Assignment",
			"area|timetable", "policies", "inspect"
		}),
		new LayoutPreset("Oversee", DefaultLayout),
		new LayoutPreset("Medic", new string[8] { "header:Health", "health", "medical", "header:Needs", "need:Food|need:Rest", "header:Protection", "equip|armor", "inspect" }),
		new LayoutPreset("Full", new string[19]
		{
			"header:Mood", "mood", "header:Needs", "need:Food|need:Rest", "need:Joy|need:Comfort", "need:Beauty|need:Outdoors", "header:Health", "health", "medical", "production",
			"header:Skills", "skills", "header:Assignment", "area|timetable", "policies", "header:Equipment", "equip|armor", "mechenergy", "inspect"
		})
	};

	public static readonly string[] DefaultRail = new string[8] { "portrait|name", "faction", "activity", "equip", "healthbar", "mood", "need:Food", "need:Rest" };

	public static readonly string[] DefaultAnimalLayout = new string[5] { "header:Health", "health", "header:Produce", "production", "inspect" };

	public static readonly string[] DefaultAnimalRail = new string[5] { "portrait|name", "faction", "activity", "healthbar", "need:Food" };

	public static readonly string[] DefaultMechLayout = new string[4] { "header:Health", "health", "workmode", "inspect" };

	public static readonly string[] DefaultMechRail = new string[5] { "portrait|name", "faction", "activity", "healthbar", "mechenergy" };

	public static readonly string[] DefaultPrisonerLayout = new string[5] { "resistance", "header:Health", "health", "medical", "inspect" };

	public static readonly string[] DefaultPrisonerRail = new string[7] { "portrait|name", "faction", "activity", "healthbar", "mood", "need:Food", "need:Rest" };

	public static readonly string[] DefaultThingLayout = new string[9] { "hp", "freshness", "fuel", "growth", "battery", "powergrid", "pipenet", "chips", "inspect" };

	public static string[] DefaultLayoutFor(PawnProfileKind kind)
	{
		return kind switch
		{
			PawnProfileKind.Animal => DefaultAnimalLayout, 
			PawnProfileKind.Mech => DefaultMechLayout, 
			PawnProfileKind.Prisoner => DefaultPrisonerLayout, 
			_ => DefaultLayout, 
		};
	}

	public static string[] DefaultRailFor(PawnProfileKind kind)
	{
		return kind switch
		{
			PawnProfileKind.Animal => DefaultAnimalRail, 
			PawnProfileKind.Mech => DefaultMechRail, 
			PawnProfileKind.Prisoner => DefaultPrisonerRail, 
			_ => DefaultRail, 
		};
	}

	public List<string> RowsFor(PawnProfileKind kind)
	{
		return kind switch
		{
			PawnProfileKind.Animal => animalRows, 
			PawnProfileKind.Mech => mechRows, 
			PawnProfileKind.Prisoner => prisonerRows, 
			_ => layoutRows, 
		};
	}

	public List<string> RailFor(PawnProfileKind kind)
	{
		return kind switch
		{
			PawnProfileKind.Animal => animalRail, 
			PawnProfileKind.Mech => mechRail, 
			PawnProfileKind.Prisoner => prisonerRail, 
			_ => railRows, 
		};
	}

	public List<string> EffectiveLayoutFor(PawnProfileKind kind)
	{
		List<string> list = RowsFor(kind);
		if (list.Count > 0)
		{
			return list;
		}
		list.AddRange(DefaultLayoutFor(kind));
		LayoutStamp++;
		return list;
	}

	public List<string> EffectiveRailFor(PawnProfileKind kind)
	{
		List<string> list = RailFor(kind);
		if (list.Count > 0)
		{
			return list;
		}
		list.AddRange(DefaultRailFor(kind));
		LayoutStamp++;
		return list;
	}

	public List<string> EffectiveRail()
	{
		return EffectiveRailFor(PawnProfileKind.Colonist);
	}

	public void ApplyPreset(LayoutPreset p)
	{
		ApplyPreset(p, PawnProfileKind.Colonist);
	}

	public void ApplyPreset(LayoutPreset p, PawnProfileKind kind)
	{
		List<string> list = RowsFor(kind);
		list.Clear();
		list.AddRange(p.Rows);
		LayoutStamp++;
	}

	public void ResetProfile(PawnProfileKind kind)
	{
		List<string> list = RowsFor(kind);
		list.Clear();
		list.AddRange(DefaultLayoutFor(kind));
		List<string> list2 = RailFor(kind);
		list2.Clear();
		list2.AddRange(DefaultRailFor(kind));
		LayoutStamp++;
	}

	public void CopyLane(PawnProfileKind from, PawnProfileKind to, bool rail)
	{
		List<string> collection = (rail ? EffectiveRailFor(from) : EffectiveLayoutFor(from));
		List<string> obj = (rail ? RailFor(to) : RowsFor(to));
		obj.Clear();
		obj.AddRange(collection);
		LayoutStamp++;
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref enabled, "enabled", defaultValue: true);
		Scribe_Values.Look(ref autoMinimize, "autoMinimize", defaultValue: true);
		Scribe_Values.Look(ref minimizeDelay, "minimizeDelay", 12f);
		Scribe_Values.Look(ref showVanillaTabs, "showVanillaTabs", defaultValue: false);
		Scribe_Values.Look(ref widePanel, "widePanel", defaultValue: true);
		Scribe_Values.Look(ref paneWidth, "paneWidth", -1);
		Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);
		Scribe_Values.Look(ref paneHeight, "paneHeight", 232);
		Scribe_Values.Look(ref portraitSize, "portraitSize", 0);
		Scribe_Values.Look(ref portraitZoom, "portraitZoom", 1f);
		Scribe_Values.Look(ref portraitPanX, "portraitPanX", 0f);
		Scribe_Values.Look(ref portraitPanY, "portraitPanY", 0f);
		Scribe_Collections.Look(ref railRows, "railRows", LookMode.Value);
		Scribe_Collections.Look(ref hiddenTabs, "hiddenTabs", LookMode.Value);
		Scribe_Collections.Look(ref tabOrder, "tabOrder", LookMode.Value);
		Scribe_Collections.Look(ref layoutRows, "layoutRows", LookMode.Value);
		Scribe_Collections.Look(ref animalRows, "animalRows", LookMode.Value);
		Scribe_Collections.Look(ref animalRail, "animalRail", LookMode.Value);
		Scribe_Collections.Look(ref mechRows, "mechRows", LookMode.Value);
		Scribe_Collections.Look(ref mechRail, "mechRail", LookMode.Value);
		Scribe_Collections.Look(ref prisonerRows, "prisonerRows", LookMode.Value);
		Scribe_Collections.Look(ref prisonerRail, "prisonerRail", LookMode.Value);
		Scribe_Collections.Look(ref thingRows, "thingRows", LookMode.Value);
		Scribe_Values.Look(ref moodTicks, "moodTicks", defaultValue: false);
		Scribe_Values.Look(ref healthBands, "healthBands", defaultValue: true);
		Scribe_Values.Look(ref healthRows, "healthRows", 3);
		Scribe_Values.Look(ref skillsHideZero, "skillsHideZero", defaultValue: false);
		Scribe_Values.Look(ref storagePanelHeight, "storagePanelHeight", 430);
		Scribe_Collections.Look(ref storageBookmarks, "storageBookmarks", LookMode.Deep);
		if (storageBookmarks == null)
		{
			storageBookmarks = new List<StorageBookmark>();
		}
		storageBookmarks.RemoveAll((StorageBookmark b) => b == null);
		if (layoutRows == null)
		{
			layoutRows = new List<string>();
		}
		if (railRows == null)
		{
			railRows = new List<string>();
		}
		if (hiddenTabs == null)
		{
			hiddenTabs = new List<string>();
		}
		if (tabOrder == null)
		{
			tabOrder = new List<string>();
		}
		if (animalRows == null)
		{
			animalRows = new List<string>();
		}
		if (animalRail == null)
		{
			animalRail = new List<string>();
		}
		if (mechRows == null)
		{
			mechRows = new List<string>();
		}
		if (mechRail == null)
		{
			mechRail = new List<string>();
		}
		if (prisonerRows == null)
		{
			prisonerRows = new List<string>();
		}
		if (prisonerRail == null)
		{
			prisonerRail = new List<string>();
		}
		if (thingRows == null)
		{
			thingRows = new List<string>();
		}
		portraitSize = Mathf.Clamp(portraitSize, 0, 2);
		if (float.IsNaN(portraitZoom) || float.IsInfinity(portraitZoom))
		{
			portraitZoom = 1f;
		}
		portraitZoom = Mathf.Clamp(portraitZoom, 0.6f, 1.8f);
		if (float.IsNaN(portraitPanX))
		{
			portraitPanX = 0f;
		}
		if (float.IsNaN(portraitPanY))
		{
			portraitPanY = 0f;
		}
		portraitPanX = Mathf.Clamp(portraitPanX, -0.6f, 0.6f);
		portraitPanY = Mathf.Clamp(portraitPanY, -0.6f, 0.6f);
		if (float.IsNaN(minimizeDelay) || float.IsInfinity(minimizeDelay))
		{
			minimizeDelay = 12f;
		}
		minimizeDelay = Mathf.Clamp(minimizeDelay, 3f, 60f);
		paneHeight = Mathf.Clamp(paneHeight, 165, 800);
		storagePanelHeight = Mathf.Clamp(storagePanelHeight, 250, 900);
		if (paneWidth < 0)
		{
			paneWidth = (widePanel ? 540 : 460);
		}
		paneWidth = Mathf.Clamp(paneWidth, 420, 720);
		healthRows = Mathf.Clamp(healthRows, 1, 8);
		if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
		{
			RunMigrations();
		}
		LayoutStamp++;
	}

	private void RunMigrations()
	{
		if (settingsVersion < 2)
		{
			if (settingsVersion < 2)
			{
				InsertFactionAfterName(layoutRows);
				InsertFactionAfterName(railRows);
				InsertFactionAfterName(animalRows);
				InsertFactionAfterName(animalRail);
				InsertFactionAfterName(mechRows);
				InsertFactionAfterName(mechRail);
				InsertFactionAfterName(prisonerRows);
				InsertFactionAfterName(prisonerRail);
			}
			settingsVersion = 2;
		}
	}

	private static void InsertFactionAfterName(List<string>? rows)
	{
		if (rows == null || rows.Count == 0)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < rows.Count; i++)
		{
			string[] array = rows[i].Split(new char[1] { '|' });
			for (int j = 0; j < array.Length; j++)
			{
				if (array[j] == "faction")
				{
					return;
				}
				if (array[j] == "name" && num < 0)
				{
					num = i;
				}
			}
		}
		if (num >= 0)
		{
			rows.Insert(num + 1, "faction");
		}
	}

	public List<string> EffectiveLayout()
	{
		if (layoutRows.Count > 0)
		{
			return layoutRows;
		}
		layoutRows.AddRange(DefaultLayout);
		LayoutStamp++;
		return layoutRows;
	}

	public List<string> EffectiveThingLayout()
	{
		if (thingRows.Count > 0)
		{
			return thingRows;
		}
		thingRows.AddRange(DefaultThingLayout);
		LayoutStamp++;
		return thingRows;
	}

	public void ResetThingLayout()
	{
		thingRows.Clear();
		thingRows.AddRange(DefaultThingLayout);
		LayoutStamp++;
	}

	public void ResetToDefaults()
	{
		enabled = true;
		autoMinimize = true;
		minimizeDelay = 12f;
		showVanillaTabs = false;
		widePanel = true;
		paneWidth = 540;
		paneHeight = 232;
		storagePanelHeight = 430;
		portraitSize = 0;
		portraitZoom = 1f;
		portraitPanX = 0f;
		portraitPanY = 0f;
		railRows.Clear();
		railRows.AddRange(DefaultRail);
		hiddenTabs.Clear();
		tabOrder.Clear();
		layoutRows.Clear();
		layoutRows.AddRange(DefaultLayout);
		animalRows.Clear();
		animalRail.Clear();
		mechRows.Clear();
		mechRail.Clear();
		prisonerRows.Clear();
		prisonerRail.Clear();
		thingRows.Clear();
		moodTicks = false;
		healthBands = true;
		healthRows = 3;
		skillsHideZero = false;
		LayoutStamp++;
	}
}
