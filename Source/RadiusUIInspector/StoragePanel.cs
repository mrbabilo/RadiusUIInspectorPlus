using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RadiusUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUIInspector;

public static class StoragePanel
{
	public enum Source
	{
		None,
		Thing,
		Comp,
		Group,
		Zone
	}

	private const float RowH = 24f;

	private const float Gap = 4f;

	private const int HpRangeId = 5309601;

	private const int QualityRangeId = 5309602;

	private static Vector2 scroll;

	private static string query = "";

	private static readonly HashSet<ThingCategoryDef> expanded = new HashSet<ThingCategoryDef>();

	private static float lastContentH = 10f;

	private static float lastViewH = 10f;

	private static readonly Dictionary<ThingCategoryDef, int> allowedMemo = new Dictionary<ThingCategoryDef, int>();

	private static readonly Dictionary<ThingCategoryDef, int> totalMemo = new Dictionary<ThingCategoryDef, int>();

	private static int memoKey = -1;

	private static bool memoDirty = true;

	private static readonly List<SpecialThingFilterDef> specialCache = new List<SpecialThingFilterDef>();

	private static ThingCategoryDef? specialRoot;

	private static ThingFilter? specialParent;

	private static readonly List<ThingDef> visibleDefScratch = new List<ThingDef>(512);

	private static IStoreSettingsParent? lastParent;

	// Search text and badge mask the match sets were last built for; null text forces a rebuild.
	private static string? matchText;

	private static Trait matchMask;

	private static readonly HashSet<ThingCategoryDef> matchCats = new HashSet<ThingCategoryDef>();

	private static readonly HashSet<ThingCategoryDef> nameMatched = new HashSet<ThingCategoryDef>();

	private static bool? stackGapActive;

	// Drag-paint across the filter tree (merged from RimKeeper): the row pressed first picks allow or disallow,
	// every row the pointer then crosses takes that state.
	private static bool painting;

	private static bool paintAllow;

	private static bool treeHovered;

	private const float Indent = 16f;

	private const float BookmarkRowH = 26f;

	private const float BookmarkActionW = 64f;

	private const float BookmarkSaveW = 120f;

	private const int MaxBookmarkRows = 5;

	// Search row, badge filter chips, both range sliders and six tree rows.
	private const float TreeReserveH = 28f + 26f + 64f + 144f;

	private const string BookmarkNameControl = "RUI_StorageBookmarkName";

	private const string SearchControl = "RUI_StorageSearch";

	private static bool specialOpen;

	private static bool bookmarksOpen;

	private static Vector2 bookmarkScroll;

	private static string newBookmarkName = "";

	private static StorageBookmark? pendingDelete;

	private const int MaxContentRows = 6;

	private const float ItemRowH = 28f;

	private const float ContentRowH = 30f;

	private static StorageBookmark? editing;

	private static bool evictWatching;

	private static readonly HashSet<Thing> evictBaseline = new HashSet<Thing>();

	private static readonly List<Thing> rejectedScratch = new List<Thing>();

	private static readonly Dictionary<SpecialThingFilterDef, int> specialEffect = new Dictionary<SpecialThingFilterDef, int>();

	private static readonly Dictionary<ThingCategoryDef, Color> categoryHue = new Dictionary<ThingCategoryDef, Color>();

	private static readonly Dictionary<ThingDef, SpecialThingFilterDef> partlyRefused = new Dictionary<ThingDef, SpecialThingFilterDef>();

	private static readonly ConditionalWeakTable<ThingFilter, Dictionary<SpecialThingFilterDef, List<ThingDef>>> syncedOff = new ConditionalWeakTable<ThingFilter, Dictionary<SpecialThingFilterDef, List<ThingDef>>>();

	private const int ContentsRefreshFrames = 60;

	private static bool contentsOpen;

	private static Vector2 contentsScroll;

	private static int contentsFrame = -1;

	private static int contentsTotal;

	private static readonly List<KeyValuePair<ThingDef, int>> contents = new List<KeyValuePair<ThingDef, int>>();

	private static readonly Dictionary<ThingDef, int> contentsScratch = new Dictionary<ThingDef, int>();

	// Per def: how many are stored here, and how many of those the current settings turn away. The tree reads
	// them so a row says what unticking it would move out.
	private static readonly Dictionary<ThingDef, int> storedCount = new Dictionary<ThingDef, int>();

	private static readonly Dictionary<ThingDef, int> leavingCount = new Dictionary<ThingDef, int>();

	// A click on a stored def opens its branch in the tree, scrolls to the row and outlines it for a moment.
	private static ThingDef? revealDef;

	private static bool revealScroll;

	private static float revealUntil;

	private const float RevealSeconds = 2f;

	// Weapons and apparel each carry their own "burnable", "smeltable"... filter under the same label: those
	// rows get their category appended so they stop looking like duplicates.
	private static readonly Dictionary<SpecialThingFilterDef, string> specialLabel = new Dictionary<SpecialThingFilterDef, string>();

	public static Source LastSource { get; private set; }

	public static float PreferredHeight => Mathf.Min(InspectorMod.Settings?.storagePanelHeight ?? 430, (float)UI.screenHeight * 0.75f);

	public static bool Owns(IInspectPane pane)
	{
		if (pane == null || InspectorMod.Settings == null)
		{
			return false;
		}
		if (!InspectorMod.Settings.nativeStorageTab)
		{
			return false;
		}
		if (!stackGapActive.HasValue)
		{
			stackGapActive = ModsConfig.IsActive("Andromeda.StackGap");
		}
		if (stackGapActive.Value)
		{
			return false;
		}
		return pane.OpenTabType == typeof(ITab_Storage);
	}

	public static IStoreSettingsParent? Resolve()
	{
		Selector selector = Find.Selector;
		if (selector == null)
		{
			return null;
		}
		List<object> selectedObjects = selector.SelectedObjects;
		if (selectedObjects == null || selectedObjects.Count == 0)
		{
			return null;
		}
		if (selectedObjects.Count > 1)
		{
			StorageGroup storageGroup = (selectedObjects[0] as IStorageGroupMember)?.Group;
			if (storageGroup == null)
			{
				return null;
			}
			for (int i = 0; i < selectedObjects.Count; i++)
			{
				if (!(selectedObjects[i] is IStorageGroupMember storageGroupMember) || storageGroupMember.Group != storageGroup)
				{
					return null;
				}
			}
			LastSource = Source.Group;
			return storageGroup;
		}
		object obj = selectedObjects[0];
		if (obj is Thing thing)
		{
			if (thing is IStoreSettingsParent result)
			{
				LastSource = Source.Thing;
				return result;
			}
			if (thing is ThingWithComps { AllComps: var allComps })
			{
				for (int j = 0; j < allComps.Count; j++)
				{
					if (allComps[j] is IStoreSettingsParent result2)
					{
						LastSource = Source.Comp;
						return result2;
					}
				}
			}
			return null;
		}
		if (obj is Zone_Stockpile result3)
		{
			LastSource = Source.Zone;
			return result3;
		}
		if (obj is IStoreSettingsParent result4)
		{
			LastSource = Source.Thing;
			return result4;
		}
		return null;
	}

	private static string SourceLabel()
	{
		return LastSource switch
		{
			Source.Comp => "RUI.Inspector.StorageViaComp".Translate(), 
			Source.Group => "RUI.Inspector.StorageViaGroup".Translate(), 
			Source.Zone => "RUI.Inspector.StorageViaZone".Translate(), 
			_ => "RUI.Inspector.StorageViaThing".Translate(), 
		};
	}

	public static void Notify_SelectionChanged()
	{
		scroll = Vector2.zero;
		query = "";
		badgeMask = Trait.None;
		presentMask = Trait.None;
		matchText = null;
		matchCats.Clear();
		nameMatched.Clear();
		memoDirty = true;
		specialRoot = null;
		specialParent = null;
		painting = false;
		pendingDelete = null;
		contentsScroll = Vector2.zero;
		contentsFrame = -1;
		storedCount.Clear();
		leavingCount.Clear();
		revealDef = null;
		revealScroll = false;
		evictWatching = false;
		evictBaseline.Clear();
		if (editing != null)
		{
			CancelEdit();
		}
	}

	public static void Draw(Rect area, IStoreSettingsParent parent)
	{
		StorageSettings storeSettings = parent.GetStoreSettings();
		if (storeSettings == null || storeSettings.filter == null)
		{
			return;
		}
		ThingFilter filter = storeSettings.filter;
		ThingFilter parentFilter = parent.GetParentStoreSettings()?.filter;
		if (parent != lastParent)
		{
			lastParent = parent;
			Notify_SelectionChanged();
		}
		int num = parent.GetHashCode() ^ filter.AllowedDefCount;
		if (num != memoKey)
		{
			memoKey = num;
			memoDirty = true;
		}
		// A slider drag opens the eviction watch on its first change and closes it here once the button is up.
		if (evictWatching && !painting && !Input.GetMouseButton(0))
		{
			EndEvictWatch();
		}
		float y = area.y;
		float width = area.width;
		Rect rect = new Rect(area.x, y, width, 20f);
		CardChrome.Pill(rect, (LastSource == Source.Comp) ? Palette.Surface3 : Palette.Surface2);
		RadiusFont.Label(new Rect(rect.x + 9f, rect.y, rect.width - 18f, rect.height), SourceLabel(), GameFont.Tiny, heading: false, (LastSource == Source.Comp) ? RadiusTheme.Accent : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		y += 24f;
		if (parent.StorageTabVisible)
		{
			y = DrawPriority(area.x, y, width, storeSettings);
		}
		Rect r = new Rect(area.x, y, width * 0.5f - 2f, 24f);
		Rect r2 = new Rect(area.x + width * 0.5f + 2f, y, width * 0.5f - 2f, 24f);
		if (SmallButton(r, "RUI.Inspector.AllowAll".Translate()))
		{
			filter.SetAllowAll(parentFilter);
			memoDirty = true;
		}
		if (SmallButton(r2, "RUI.Inspector.ClearAll".Translate()))
		{
			BeginEvictWatch();
			filter.SetDisallowAll();
			memoDirty = true;
			EndEvictWatch();
		}
		y += 28f;
		y = DrawBookmarks(area.x, y, width, area.yMax, filter, parentFilter);
		y = DrawContents(area.x, y, width, area.yMax, parent);
		y = DrawSearchRow(area.x, y, width, RootFor(filter, parentFilter)?.catDef);
		y = DrawBadgeFilters(area.x, y, width);
		if (filter.allowedHitPointsConfigurable)
		{
			FloatRange range = filter.AllowedHitPointsPercents;
			Widgets.FloatRange(new Rect(area.x, y, width, 28f), 5309601, ref range, 0f, 1f, "HitPoints", ToStringStyle.PercentZero);
			if (range.min != filter.AllowedHitPointsPercents.min || range.max != filter.AllowedHitPointsPercents.max)
			{
				BeginEvictWatch();
				filter.AllowedHitPointsPercents = range;
				contentsFrame = -1;
			}
			y += 32f;
		}
		if (filter.allowedQualitiesConfigurable && ModsConfig.RoyaltyActive)
		{
			QualityRange range2 = filter.AllowedQualityLevels;
			Widgets.QualityRange(new Rect(area.x, y, width, 28f), 5309602, ref range2);
			if (range2.min != filter.AllowedQualityLevels.min || range2.max != filter.AllowedQualityLevels.max)
			{
				BeginEvictWatch();
				filter.AllowedQualityLevels = range2;
				contentsFrame = -1;
			}
			y += 32f;
		}
		Rect area2 = new Rect(area.x, y + 2f, width, area.yMax - y - 4f);
		if (!(area2.height < 40f))
		{
			DrawTree(area2, filter, parentFilter);
		}
	}

	private static float DrawPriority(float x, float y, float w, StorageSettings settings)
	{
		Rect rect = new Rect(x, y, w, 26f);
		CardChrome.Rounded(rect, Palette.Surface3, 8f);
		int num = 5;
		float num2 = (rect.width - 4f) / (float)num;
		for (int i = 0; i < num; i++)
		{
			StoragePriority storagePriority = (StoragePriority)(1 + i);
			Rect rect2 = new Rect(rect.x + 2f + (float)i * num2, rect.y + 2f, num2, 22f);
			bool flag = settings.Priority == storagePriority;
			bool flag2 = Mouse.IsOver(rect2);
			if (flag)
			{
				CardChrome.Rounded(rect2, RadiusTheme.Accent, 8f);
			}
			else if (flag2)
			{
				CardChrome.Hover(rect2, 8f);
			}
			RadiusFont.Label(rect2, storagePriority.Label().CapitalizeFirst(), GameFont.Tiny, flag, flag ? Palette.InkOnAccent : (flag2 ? Palette.Ink : Palette.TextDim), TextAnchor.MiddleCenter, wrap: false);
			if (Widgets.ButtonInvisible(rect2))
			{
				settings.Priority = storagePriority;
				InspectorCore.Poke();
			}
		}
		return y + 30f;
	}

	// True when this row must take paintAllow now: on the press that starts a stroke, or when a running stroke
	// crosses a row not yet in that state. Hit tests inside the scroll view ignore its clipping, so rows only
	// react while the pointer is over the visible tree.
	private static bool PaintHit(Rect r, bool fullyAllowed, bool anyAllowed)
	{
		if (!treeHovered || !Mouse.IsOver(r))
		{
			return false;
		}
		Event current = Event.current;
		if (current.type == EventType.MouseDown && current.button == 0)
		{
			painting = true;
			paintAllow = !fullyAllowed;
			BeginEvictWatch();
			current.Use();
			return true;
		}
		if (!painting || current.type == EventType.Layout)
		{
			return false;
		}
		return paintAllow ? !fullyAllowed : anyAllowed;
	}

	// Saved filters live inside the panel: a foldable list with one row of actions per filter, then a name field
	// to save the current one. Nothing opens over the panel.
	private static float DrawBookmarks(float x, float y, float w, float yMax, ThingFilter filter, ThingFilter? parentFilter)
	{
		List<StorageBookmark> bookmarks = InspectorMod.Settings.storageBookmarks;
		Rect header = new Rect(x, y, w, 24f);
		bool over = Mouse.IsOver(header);
		CardChrome.Rounded(header, over ? Palette.Surface3 : Palette.Surface2, 8f);
		IconSet.Get(bookmarksOpen ? "Action/ChevronDown" : "Action/Next").Draw(new Rect(header.x + 8f, header.y + 6f, 12f, 12f), Palette.TextDim);
		RadiusFont.Label(new Rect(header.x + 26f, header.y, header.width - 34f, header.height), "RUI.Inspector.Bookmarks".Translate(bookmarks.Count), GameFont.Tiny, heading: false, over ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		TooltipHandler.TipRegion(header, "RUI.Inspector.BookmarksTip".Translate());
		if (Widgets.ButtonInvisible(header))
		{
			bookmarksOpen = !bookmarksOpen;
			pendingDelete = null;
			InspectorCore.Poke();
		}
		y += 28f;
		if (!bookmarksOpen)
		{
			return y;
		}
		if (bookmarks.Count == 0)
		{
			RadiusFont.Label(new Rect(x + 8f, y, w - 16f, 22f), "RUI.Inspector.BookmarkNone".Translate(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			y += 24f;
		}
		else
		{
			// The list never takes the room the tree needs below it (search, ranges, a few rows): past that it scrolls.
			float room = yMax - y - 30f - TreeReserveH;
			int maxRows = Mathf.Clamp(Mathf.FloorToInt(room / BookmarkRowH), 1, MaxBookmarkRows);
			bool scrolls = bookmarks.Count > maxRows;
			Rect outRect = new Rect(x, y, w, (float)Mathf.Min(bookmarks.Count, maxRows) * BookmarkRowH);
			float vw = scrolls ? Mathf.Floor(w - 20f) : w;
			// Clicks inside a scroll view ignore its clipping: rows scrolled out of sight must not take a click
			// meant for the save row below.
			bool hot = !scrolls || Mouse.IsOver(outRect);
			int depth = FlatScroll.Depth;
			if (scrolls)
			{
				FlatScroll.Begin(outRect, ref bookmarkScroll, new Rect(0f, 0f, vw, (float)bookmarks.Count * BookmarkRowH));
			}
			try
			{
				float rx = scrolls ? 0f : x;
				float ry = scrolls ? 0f : y;
				for (int i = 0; i < bookmarks.Count; i++)
				{
					if (DrawBookmarkRow(bookmarks[i], rx, ry, vw, hot, filter, parentFilter))
					{
						break;
					}
					ry += BookmarkRowH;
				}
			}
			finally
			{
				if (scrolls)
				{
					FlatScroll.EndOrUnwind(depth);
				}
			}
			y += outRect.height + 2f;
		}
		return DrawBookmarkSave(x, y, w, filter);
	}

	// Returns true when the list changed under the loop (a delete), so the caller stops iterating.
	private static bool DrawBookmarkRow(StorageBookmark bookmark, float x, float y, float w, bool hot, ThingFilter filter, ThingFilter? parentFilter)
	{
		Rect row = new Rect(x, y, w, BookmarkRowH - 2f);
		if (editing == bookmark)
		{
			CardChrome.Outline(row, RadiusTheme.Accent, 1f, 8f);
		}
		else if (Mouse.IsOver(row))
		{
			CardChrome.Hover(row, 8f);
		}
		Rect trash = new Rect(row.xMax - 24f, row.y, 24f, row.height);
		Rect edit = new Rect(trash.x - 28f, row.y, 24f, row.height);
		Rect remove = new Rect(edit.x - 4f - BookmarkActionW, row.y, BookmarkActionW, row.height);
		Rect add = new Rect(remove.x - 4f - BookmarkActionW, row.y, BookmarkActionW, row.height);
		Rect replace = new Rect(add.x - 4f - BookmarkActionW, row.y, BookmarkActionW, row.height);
		RadiusFont.Label(new Rect(row.x + 8f, row.y, replace.x - row.x - 12f, row.height), bookmark.name, GameFont.Small, heading: false, Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		if (Mouse.IsOver(row))
		{
			TooltipHandler.TipRegion(replace, "RUI.Inspector.BookmarkReplace".Translate());
			TooltipHandler.TipRegion(add, "RUI.Inspector.BookmarkAdd".Translate());
			TooltipHandler.TipRegion(remove, "RUI.Inspector.BookmarkRemove".Translate());
		}
		if (SmallButton(replace, "RUI.Inspector.BookmarkApplyReplace".Translate(), hot))
		{
			ApplyBookmark(bookmark, filter, parentFilter, StorageBookmark.Mode.Replace);
		}
		if (SmallButton(add, "RUI.Inspector.BookmarkApplyAdd".Translate(), hot))
		{
			ApplyBookmark(bookmark, filter, parentFilter, StorageBookmark.Mode.Add);
		}
		if (SmallButton(remove, "RUI.Inspector.BookmarkApplyRemove".Translate(), hot))
		{
			ApplyBookmark(bookmark, filter, parentFilter, StorageBookmark.Mode.Remove);
		}
		bool overEdit = Mouse.IsOver(edit);
		CardChrome.Rounded(edit, (editing == bookmark) ? RadiusTheme.Accent : (overEdit ? Palette.Surface3 : Palette.Surface2), 8f);
		IconSet.Get("Action/Rename").Draw(edit.ContractedBy(5f), (editing == bookmark) ? Palette.InkOnAccent : (overEdit ? Palette.Ink : Palette.TextDim));
		TooltipHandler.TipRegion(edit, "RUI.Inspector.BookmarkEdit".Translate());
		if (hot && Widgets.ButtonInvisible(edit))
		{
			pendingDelete = null;
			InspectorCore.Poke();
			if (editing == bookmark)
			{
				CancelEdit();
			}
			else
			{
				editing = bookmark;
				newBookmarkName = bookmark.name;
				GUI.FocusControl(BookmarkNameControl);
			}
		}
		// Deleting is permanent and shared by every save: the first click arms the button, the second deletes.
		bool armed = pendingDelete == bookmark;
		bool overTrash = Mouse.IsOver(trash);
		CardChrome.Rounded(trash, armed ? Palette.Bad : (overTrash ? Palette.Surface3 : Palette.Surface2), 8f);
		IconSet.Get("Action/DevTrash").Draw(trash.ContractedBy(5f), (armed || overTrash) ? Palette.Ink : Palette.TextDim);
		TooltipHandler.TipRegion(trash, (armed ? "RUI.Inspector.BookmarkDeleteConfirm" : "RUI.Inspector.BookmarkDelete").Translate());
		if (hot && Widgets.ButtonInvisible(trash))
		{
			InspectorCore.Poke();
			if (!armed)
			{
				pendingDelete = bookmark;
				return false;
			}
			pendingDelete = null;
			if (editing == bookmark)
			{
				CancelEdit();
			}
			InspectorMod.Settings.storageBookmarks.Remove(bookmark);
			InspectorMod.Instance?.WriteSettings();
			return true;
		}
		return false;
	}

	// In edit mode the same row renames the bookmark and overwrites it with this storage's filter; an empty
	// name keeps the old one.
	private static float DrawBookmarkSave(float x, float y, float w, ThingFilter filter)
	{
		if (editing != null && !InspectorMod.Settings.storageBookmarks.Contains(editing))
		{
			CancelEdit();
		}
		float cancelW = (editing != null) ? 28f : 0f;
		Rect box = new Rect(x, y, w - BookmarkSaveW - 4f - cancelW, 24f);
		CardChrome.Rounded(box, Palette.Surface2, 8f);
		// Read Enter before the field draws, in case the field consumes the key event.
		Event current = Event.current;
		bool enter = current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == BookmarkNameControl;
		GUI.SetNextControlName(BookmarkNameControl);
		newBookmarkName = Widgets.TextField(box.ContractedBy(4f), newBookmarkName);
		if (newBookmarkName.Length == 0)
		{
			RadiusFont.Label(new Rect(box.x + 8f, box.y, box.width - 16f, box.height), ((editing != null) ? "RUI.Inspector.BookmarkEditHint" : "RUI.Inspector.BookmarkNameHint").Translate(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		}
		string name = newBookmarkName.Trim();
		Rect button = new Rect(box.xMax + 4f, y, BookmarkSaveW, 24f);
		if (editing != null)
		{
			// Renaming keeps the saved content; overwriting with this storage's filter is its own button, so a
			// rename confirmed with Enter never loses what the bookmark held.
			StorageBookmark bookmark = editing;
			Rect rename = new Rect(button.x, y, BookmarkSaveW * 0.5f - 2f, 24f);
			Rect overwrite = new Rect(rename.xMax + 4f, y, BookmarkSaveW * 0.5f - 2f, 24f);
			TooltipHandler.TipRegion(rename, "RUI.Inspector.BookmarkRenameTip".Translate());
			TooltipHandler.TipRegion(overwrite, "RUI.Inspector.BookmarkOverwriteTip".Translate());
			bool done = false;
			if (SmallButton(rename, "RUI.Inspector.BookmarkRename".Translate()) || enter)
			{
				done = UpdateBookmark(bookmark, (name.Length > 0) ? name : bookmark.name, null);
				if (enter)
				{
					current.Use();
				}
			}
			else if (SmallButton(overwrite, "RUI.Inspector.BookmarkOverwrite".Translate()))
			{
				done = UpdateBookmark(bookmark, (name.Length > 0) ? name : bookmark.name, filter);
			}
			if (done)
			{
				CancelEdit();
			}
		}
		else if (SmallButton(button, ((FindBookmark(name) >= 0) ? "RUI.Inspector.BookmarkUpdate" : "RUI.Inspector.BookmarkSave").Translate()) || enter)
		{
			SaveBookmark((name.Length > 0) ? name : DefaultBookmarkName(), filter);
			newBookmarkName = "";
			GUI.FocusControl(null);
			if (enter)
			{
				current.Use();
			}
		}
		if (editing != null)
		{
			Rect cancel = new Rect(button.xMax + 4f, y, 24f, 24f);
			bool overCancel = Mouse.IsOver(cancel);
			CardChrome.Rounded(cancel, overCancel ? Palette.Surface3 : Palette.Surface2, 8f);
			IconSet.Get("Action/Decline").Draw(cancel.ContractedBy(6f), overCancel ? Palette.Ink : Palette.TextDim);
			TooltipHandler.TipRegion(cancel, "RUI.Inspector.BookmarkCancelEdit".Translate());
			if (Widgets.ButtonInvisible(cancel))
			{
				CancelEdit();
				InspectorCore.Poke();
			}
		}
		return y + 28f;
	}

	// What the storage holds, one row per def, largest stacks first (RimKeeper's stockpile list). Storage that
	// keeps its things outside map cells (a comp, a container) has no slot group: the section is left out.
	private static IEnumerable<Thing>? HeldThings(IStoreSettingsParent parent)
	{
		if (parent is ISlotGroup slotGroup)
		{
			return slotGroup.HeldThings;
		}
		return (parent as ISlotGroupParent)?.GetSlotGroup()?.HeldThings;
	}

	// Walking every cell each GUI event is wasteful on big stockpiles: the list refreshes once a second.
	private static void RefreshContents(IEnumerable<Thing> things, StorageSettings settings)
	{
		if (contentsFrame >= 0 && Time.frameCount - contentsFrame < ContentsRefreshFrames)
		{
			return;
		}
		contentsFrame = Time.frameCount;
		storedCount.Clear();
		leavingCount.Clear();
		contentsTotal = 0;
		foreach (Thing thing in things)
		{
			storedCount.TryGetValue(thing.def, out var value);
			storedCount[thing.def] = value + thing.stackCount;
			contentsTotal += thing.stackCount;
			if (!settings.AllowedToAccept(thing))
			{
				leavingCount.TryGetValue(thing.def, out var value2);
				leavingCount[thing.def] = value2 + thing.stackCount;
			}
		}
		contents.Clear();
		contents.AddRange(storedCount);
		contents.Sort((KeyValuePair<ThingDef, int> a, KeyValuePair<ThingDef, int> b) => (a.Value != b.Value) ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key.label, b.Key.label));
	}

	private static float DrawContents(float x, float y, float w, float yMax, IStoreSettingsParent parent)
	{
		IEnumerable<Thing>? things = HeldThings(parent);
		StorageSettings? settings = parent.GetStoreSettings();
		if (things == null || settings == null)
		{
			storedCount.Clear();
			leavingCount.Clear();
			return y;
		}
		RefreshContents(things, settings);
		Rect header = new Rect(x, y, w, 24f);
		bool over = Mouse.IsOver(header);
		CardChrome.Rounded(header, over ? Palette.Surface3 : Palette.Surface2, 8f);
		IconSet.Get(contentsOpen ? "Action/ChevronDown" : "Action/Next").Draw(new Rect(header.x + 8f, header.y + 6f, 12f, 12f), Palette.TextDim);
		RadiusFont.Label(new Rect(header.x + 26f, header.y, header.width - 34f, header.height), "RUI.Inspector.StorageContents".Translate(contents.Count, contentsTotal), GameFont.Tiny, heading: false, over ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
		if (Widgets.ButtonInvisible(header))
		{
			contentsOpen = !contentsOpen;
			pendingDelete = null;
			InspectorCore.Poke();
		}
		y += 28f;
		if (!contentsOpen)
		{
			return y;
		}
		if (contents.Count == 0)
		{
			RadiusFont.Label(new Rect(x + 8f, y, w - 16f, 22f), "RUI.Inspector.StorageContentsEmpty".Translate(), GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			return y + 24f;
		}
		// Same budget as the saved filters: the tree below keeps its room, past that the list scrolls.
		float room = yMax - y - TreeReserveH;
		int maxRows = Mathf.Clamp(Mathf.FloorToInt(room / ContentRowH), 1, MaxContentRows);
		bool scrolls = contents.Count > maxRows;
		Rect outRect = new Rect(x, y, w, (float)Mathf.Min(contents.Count, maxRows) * ContentRowH);
		float vw = scrolls ? Mathf.Floor(w - 20f) : w;
		int depth = FlatScroll.Depth;
		if (scrolls)
		{
			FlatScroll.Begin(outRect, ref contentsScroll, new Rect(0f, 0f, vw, (float)contents.Count * ContentRowH));
		}
		try
		{
			float rx = scrolls ? 0f : x;
			float ry = scrolls ? 0f : y;
			for (int i = 0; i < contents.Count; i++)
			{
				if (!scrolls || ViewCull.Visible(ry, ContentRowH, contentsScroll.y, outRect.height))
				{
					DrawContentRow(contents[i].Key, contents[i].Value, rx, ry, vw);
				}
				ry += ContentRowH;
			}
		}
		finally
		{
			if (scrolls)
			{
				FlatScroll.EndOrUnwind(depth);
			}
		}
		return y + outRect.height + 4f;
	}

	private static void DrawContentRow(ThingDef def, int count, float x, float y, float w)
	{
		Rect row = new Rect(x, y, w, ContentRowH - 2f);
		if (Mouse.IsOver(row))
		{
			CardChrome.Hover(row, 8f);
		}
		leavingCount.TryGetValue(def, out var leaving);
		ItemIcon(new Rect(row.x + 2f, row.y + 1f, 26f, 26f), def, allowed: leaving < count);
		RadiusFont.Label(new Rect(row.x + 34f, row.y, row.width - 94f, row.height), def.LabelCap, GameFont.Small, heading: false, (leaving > 0) ? Palette.Warn : Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		RadiusFont.Label(new Rect(row.xMax - 60f, row.y, 54f, row.height), count.ToString(), GameFont.Small, heading: false, (leaving > 0) ? Palette.Warn : Palette.TextDim, TextAnchor.MiddleRight, wrap: false);
		if (Mouse.IsOver(row))
		{
			TooltipHandler.TipRegion(row, ((leaving > 0) ? "RUI.Inspector.StorageLeaving".Translate(def.label, leaving).Resolve() : (def.LabelCap + ": " + count)) + "\n\n" + "RUI.Inspector.StorageContentsReveal".Translate().Resolve());
		}
		if (Widgets.ButtonInvisible(row))
		{
			Reveal(def);
		}
	}

	private static void Reveal(ThingDef def)
	{
		pendingDelete = null;
		query = "";
		badgeMask = Trait.None;
		GUI.FocusControl(null);
		if (def.thingCategories != null)
		{
			for (int i = 0; i < def.thingCategories.Count; i++)
			{
				for (ThingCategoryDef? cat = def.thingCategories[i]; cat != null; cat = cat.parent)
				{
					expanded.Add(cat);
				}
			}
		}
		revealDef = def;
		revealScroll = true;
		revealUntil = Time.realtimeSinceStartup + RevealSeconds;
		InspectorCore.Poke();
	}

	// Item art carries wide transparent margins, so at row size a stack of steel or silver shrinks to a few
	// pixels. Plain icons are cropped to their opaque part (IconNorm) on a tile that keeps dark items readable
	// against the card. Icons drawn through a material, rotated or cut from a linked atlas go through vanilla.
	// The choice of path and the stuff colour are fixed per def: worked out once, not on every row draw.
	private struct IconInfo
	{
		public Texture2D? icon;

		public bool vanilla;

		public Color color;
	}

	private static readonly Dictionary<ThingDef, IconInfo> iconMemo = new Dictionary<ThingDef, IconInfo>();

	private static void ItemIcon(Rect tile, ThingDef def, bool allowed)
	{
		CardChrome.Rounded(tile, allowed ? Palette.Surface3 : Palette.Surface2, 6f);
		if (!iconMemo.TryGetValue(def, out var info))
		{
			info = IconInfoOf(def);
			iconMemo[def] = info;
		}
		if (info.icon == null && !info.vanilla)
		{
			return;
		}
		Rect box = tile.ContractedBy(2f);
		Color color = info.color;
		if (!allowed)
		{
			color.a *= 0.45f;
		}
		try
		{
			if (info.vanilla)
			{
				Widgets.DefIcon(box, def, null, 1f, null, drawPlaceholder: false, null, null, null, color.a);
			}
			else
			{
				IconNorm.DrawTexture(info.icon, box, color);
			}
		}
		catch (Exception)
		{
		}
	}

	private static IconInfo IconInfoOf(ThingDef def)
	{
		IconInfo result = default(IconInfo);
		try
		{
			if (def.uiIcon == null || def.uiIcon == BaseContent.BadTex)
			{
				return result;
			}
			Texture2D icon = Widgets.GetIconFor(def, out Material material);
			bool linked = def.uiIconPath.NullOrEmpty() && def.graphicData != null && def.graphicData.linkFlags != LinkFlags.None;
			result.color = def.MadeFromStuff ? def.GetColorForStuff(GenStuff.DefaultStuffFor(def)) : def.uiIconColor;
			result.vanilla = icon == null || material != null || def.uiIconAngle != 0f || linked;
			result.icon = icon;
		}
		catch (Exception)
		{
			result = default(IconInfo);
		}
		return result;
	}

	private static void CancelEdit()
	{
		editing = null;
		newBookmarkName = "";
		GUI.FocusControl(null);
	}

	// Refuses a name already used by another bookmark: two bookmarks with one name could not be told apart.
	// A null filter renames only.
	private static bool UpdateBookmark(StorageBookmark bookmark, string name, ThingFilter? filter)
	{
		List<StorageBookmark> bookmarks = InspectorMod.Settings.storageBookmarks;
		int index = bookmarks.IndexOf(bookmark);
		int clash = FindBookmark(name);
		if (index < 0)
		{
			return true;
		}
		if (clash >= 0 && clash != index)
		{
			Messages.Message("RUI.Inspector.BookmarkNameTaken".Translate(name), MessageTypeDefOf.RejectInput, historical: false);
			return false;
		}
		if (filter == null)
		{
			bookmark.name = name;
		}
		else
		{
			bookmarks[index] = StorageBookmark.Capture(name, filter);
		}
		InspectorMod.Instance?.WriteSettings();
		return true;
	}

	private static int FindBookmark(string name)
	{
		List<StorageBookmark> bookmarks = InspectorMod.Settings.storageBookmarks;
		for (int i = 0; i < bookmarks.Count; i++)
		{
			if (bookmarks[i].name == name)
			{
				return i;
			}
		}
		return -1;
	}

	// An unnamed save never overwrites an existing filter.
	private static string DefaultBookmarkName()
	{
		int n = InspectorMod.Settings.storageBookmarks.Count + 1;
		string name;
		while (FindBookmark(name = "RUI.Inspector.BookmarkDefault".Translate(n)) >= 0)
		{
			n++;
		}
		return name;
	}

	private static void ApplyBookmark(StorageBookmark bookmark, ThingFilter filter, ThingFilter? parentFilter, StorageBookmark.Mode mode)
	{
		BeginEvictWatch();
		bookmark.ApplyTo(filter, parentFilter, mode);
		EndEvictWatch();
		pendingDelete = null;
		memoDirty = true;
		InspectorCore.Poke();
	}

	// Saving under an existing name updates that bookmark. Mod settings are only written when their window
	// closes, so write now or the bookmark is lost on restart.
	private static void SaveBookmark(string name, ThingFilter filter)
	{
		List<StorageBookmark> bookmarks = InspectorMod.Settings.storageBookmarks;
		StorageBookmark bookmark = StorageBookmark.Capture(name, filter);
		int num = FindBookmark(name);
		pendingDelete = null;
		if (num >= 0)
		{
			bookmarks[num] = bookmark;
		}
		else
		{
			bookmarks.Add(bookmark);
		}
		InspectorMod.Instance?.WriteSettings();
	}

	// The clear button sits outside the text field's rect, or the field would take its click. Dropping the
	// keyboard focus makes the field show the emptied query instead of its own edit buffer.
	// The field stands out from the plain buttons: magnifier and accent outline, solid once it has focus or a
	// query. Clicking the magnifier focuses the field.
	private static float DrawSearchRow(float x, float y, float w, ThingCategoryDef? root)
	{
		const float FoldW = 100f;
		Rect box = new Rect(x, y, w - FoldW - 4f, 24f);
		bool hasQuery = query.Length > 0;
		bool focused = GUI.GetNameOfFocusedControl() == SearchControl;
		bool active = focused || hasQuery;
		CardChrome.Rounded(box, active ? Palette.Surface3 : Palette.Surface2, 8f);
		if (Mouse.IsOver(box) && !focused)
		{
			CardChrome.Hover(box, 8f);
		}
		CardChrome.Outline(box, active ? RadiusTheme.Accent : Palette.WithAlpha(RadiusTheme.Accent, 0.45f), 1f, 8f);
		Rect lens = new Rect(box.x + 2f, box.y, 22f, 24f);
		IconSet.Get("Action/Search").Draw(new Rect(lens.x + 5f, lens.y + 6f, 12f, 12f), active ? RadiusTheme.Accent : Palette.TextDim);
		if (Widgets.ButtonInvisible(lens))
		{
			GUI.FocusControl(SearchControl);
		}
		Rect field = new Rect(lens.xMax, box.y + 4f, box.xMax - lens.xMax - (hasQuery ? 26f : 4f), 16f);
		GUI.SetNextControlName(SearchControl);
		string text = Widgets.TextField(field, query);
		if (text != query)
		{
			query = text;
		}
		if (!hasQuery)
		{
			RadiusFont.Label(new Rect(field.x + 4f, box.y, field.width - 4f, box.height), "RUI.Inspector.StorageSearchHint".Translate(), GameFont.Tiny, heading: false, focused ? Palette.TextDim : Palette.Ink, TextAnchor.MiddleLeft, wrap: false);
		}
		else
		{
			Rect clear = new Rect(box.xMax - 22f, box.y + 3f, 18f, 18f);
			IconSet.Get("Action/Decline").Draw(clear.ContractedBy(3f), Mouse.IsOver(clear) ? Palette.Ink : Palette.TextDim);
			TooltipHandler.TipRegion(clear, "RUI.Inspector.StorageClearSearch".Translate());
			if (Widgets.ButtonInvisible(clear))
			{
				query = "";
				GUI.FocusControl(null);
				InspectorCore.Poke();
			}
		}
		// One toggle: folds everything while any branch of this tree is open, unfolds every level otherwise.
		// A search or a badge filter shows every match unfolded, so the toggle rests until it is cleared. The chevron matches
		// the tree's: the state the button leads to.
		Rect fold = new Rect(box.xMax + 4f, y, FoldW, 24f);
		bool anyOpen = AnyBranchOpen(root);
		bool filtering = hasQuery || badgeMask != Trait.None;
		if (SmallButton(fold, (anyOpen ? "RUI.Inspector.StorageCollapseAll" : "RUI.Inspector.StorageExpandAll").Translate(), true, anyOpen ? "Action/Next" : "Action/ChevronDown", !filtering))
		{
			if (anyOpen)
			{
				expanded.Clear();
				specialOpen = false;
			}
			else if (root != null)
			{
				ExpandBranch(root);
				specialOpen = true;
			}
			scroll = Vector2.zero;
		}
		TooltipHandler.TipRegion(fold, (filtering ? "RUI.Inspector.StorageFoldSearchTip" : (anyOpen ? "RUI.Inspector.StorageCollapseAllTip" : "RUI.Inspector.StorageExpandAllTip")).Translate());
		return y + 28f;
	}

	// The expanded set outlives a storage switch, so only this tree's top-level rows say whether it shows open.
	private static bool AnyBranchOpen(ThingCategoryDef? root)
	{
		if (specialOpen)
		{
			return true;
		}
		if (root == null)
		{
			return false;
		}
		List<ThingCategoryDef> childCategories = root.childCategories;
		for (int i = 0; i < childCategories.Count; i++)
		{
			if (expanded.Contains(childCategories[i]) && totalMemo.TryGetValue(childCategories[i], out var total) && total > 0)
			{
				return true;
			}
		}
		return false;
	}

	// Empty categories are skipped: the tree never draws them.
	private static void ExpandBranch(ThingCategoryDef cat)
	{
		List<ThingCategoryDef> childCategories = cat.childCategories;
		for (int i = 0; i < childCategories.Count; i++)
		{
			ThingCategoryDef child = childCategories[i];
			if (totalMemo.TryGetValue(child, out var total) && total > 0)
			{
				expanded.Add(child);
				ExpandBranch(child);
			}
		}
	}

	// Any other button disarms a pending bookmark delete. hot: the pointer may click here (false outside a
	// scrolled list). enabled: false greys the button out. An icon, when given, leads the label.
	private static bool SmallButton(Rect r, string label, bool hot = true, string? icon = null, bool enabled = true)
	{
		hot = hot && enabled;
		bool flag = enabled && Mouse.IsOver(r);
		CardChrome.Rounded(r, flag ? Palette.Surface3 : Palette.Surface2, 8f);
		Color ink = flag ? Palette.Ink : (enabled ? Palette.TextDim : Palette.WithAlpha(Palette.TextDim, 0.4f));
		Rect labelRect = r;
		if (icon != null)
		{
			IconSet.Get(icon).Draw(new Rect(r.x + 8f, r.y + (r.height - 12f) * 0.5f, 12f, 12f), flag ? RadiusTheme.Accent : ink);
			labelRect = new Rect(r.x + 22f, r.y, r.width - 26f, r.height);
		}
		RadiusFont.Label(labelRect, label, GameFont.Tiny, heading: false, ink, TextAnchor.MiddleCenter, wrap: false);
		bool num = hot && Widgets.ButtonInvisible(r);
		if (num)
		{
			pendingDelete = null;
			InspectorCore.Poke();
		}
		return num;
	}

	private static TreeNode_ThingCategory? RootFor(ThingFilter filter, ThingFilter? parentFilter)
	{
		if (parentFilter == null)
		{
			return filter.RootNode;
		}
		return parentFilter.DisplayRootCategory;
	}

	private static float revealY = -1f;

	private static void DrawTree(Rect area, ThingFilter filter, ThingFilter? parentFilter)
	{
		TreeNode_ThingCategory treeNode_ThingCategory = RootFor(filter, parentFilter);
		if (treeNode_ThingCategory == null || treeNode_ThingCategory.catDef == null)
		{
			return;
		}
		if (memoDirty)
		{
			allowedMemo.Clear();
			totalMemo.Clear();
			visibleMemo.Clear();
			presentMask = Trait.None;
			RebuildCounts(treeNode_ThingCategory.catDef, filter, parentFilter);
			RebuildPartlyRefused(treeNode_ThingCategory.catDef, filter, parentFilter);
			memoDirty = false;
			// The filter changed: recount the stock on the next event so rows show at once what leaves.
			contentsFrame = -1;
		}
		if (specialRoot != treeNode_ThingCategory.catDef || specialParent != parentFilter)
		{
			specialRoot = treeNode_ThingCategory.catDef;
			specialParent = parentFilter;
			RebuildSpecials(treeNode_ThingCategory.catDef, parentFilter);
		}
		string text = query.Trim();
		if (text != matchText || badgeMask != matchMask)
		{
			matchText = text;
			matchMask = badgeMask;
			matchCats.Clear();
			nameMatched.Clear();
			if (text.Length > 0 || badgeMask != Trait.None)
			{
				RebuildMatches(treeNode_ThingCategory.catDef, parentFilter, text);
			}
		}
		lastViewH = area.height;
		float num = Mathf.Floor(area.width - 20f);
		Rect viewRect = new Rect(0f, 0f, num, Mathf.Max(lastContentH, area.height));
		int depth = FlatScroll.Depth;
		if (painting && !Input.GetMouseButton(0))
		{
			painting = false;
			EndEvictWatch();
		}
		treeHovered = Mouse.IsOver(area);
		FlatScroll.Begin(area, ref scroll, viewRect);
		try
		{
			float cy = 0f;
			string text2 = text;
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < specialCache.Count; i++)
			{
				if (text2.Length == 0 || SpecialMatches(specialCache[i], text2))
				{
					num2++;
					if (filter.Allows(specialCache[i]))
					{
						num3++;
					}
				}
			}
			if (num2 > 0)
			{
				// Folded by default: it sits above the whole tree. The header keeps the allowed count visible,
				// and a search shows matching special filters even while folded.
				// Set apart from the tree as an accent card: these filters act on every item below them. The count
				// turns to a warning when some are refused, so a folded header still tells it.
				bool flag2 = specialOpen || text2.Length > 0;
				Rect rect2 = new Rect(0f, cy, num, 26f);
				if (ViewCull.Visible(cy, 26f, scroll.y, lastViewH))
				{
					CardChrome.Rounded(rect2, Palette.Surface3, 8f);
					CardChrome.Outline(rect2, Palette.WithAlpha(RadiusTheme.Accent, 0.6f), 1f, 8f);
					if (Mouse.IsOver(rect2))
					{
						CardChrome.Hover(rect2, 8f);
					}
					IconSet.Get(flag2 ? "Action/ChevronDown" : "Action/Next").Draw(new Rect(6f, rect2.y + 7f, 12f, 12f), RadiusTheme.Accent);
					RadiusFont.Label(new Rect(24f, rect2.y, num - 84f, 26f), "RUI.Inspector.StorageSpecial".Translate(), GameFont.Small, heading: true, RadiusTheme.Accent, TextAnchor.MiddleLeft, wrap: false);
					RadiusFont.Label(new Rect(num - 60f, rect2.y, 52f, 26f), num3 + "/" + num2, GameFont.Tiny, heading: false, (num3 < num2) ? Palette.Warn : Palette.TextDim, TextAnchor.MiddleRight, wrap: false);
					TooltipHandler.TipRegion(rect2, "RUI.Inspector.StorageSpecialTip".Translate());
				}
				if (text2.Length == 0 && Widgets.ButtonInvisible(rect2))
				{
					specialOpen = !specialOpen;
					InspectorCore.Poke();
				}
				cy += 28f;
				for (int j = 0; j < specialCache.Count && flag2; j++)
				{
					SpecialThingFilterDef specialThingFilterDef = specialCache[j];
					if (text2.Length > 0 && !SpecialMatches(specialThingFilterDef, text2))
					{
						continue;
					}
					bool flag = filter.Allows(specialThingFilterDef);
					Rect rect = new Rect(Indent, cy, num - Indent, 24f);
					if (ViewCull.Visible(cy, 24f, scroll.y, lastViewH))
					{
						DrawGuides(1, cy, 24f, RadiusTheme.Accent);
						if (Mouse.IsOver(rect))
						{
							CardChrome.Hover(rect, 8f);
						}
						DrawTriState(new Rect(rect.x + 18f, rect.y + 5f, 14f, 14f), flag ? 1 : 0, 1);
						// Green admits sound things (fresh, clean), red admits spoiled or risky ones (rotten, tainted).
						int effect = SpecialEffect(specialThingFilterDef);
						Color tone = (effect > 0) ? Palette.Good : ((effect < 0) ? Palette.Bad : Palette.Ink);
						float labelX = rect.x + 38f;
						if (effect != 0)
						{
							CardChrome.Rounded(new Rect(labelX, rect.y + 8f, 8f, 8f), flag ? tone : Palette.WithAlpha(tone, 0.35f), 4f);
							labelX += 14f;
						}
						RadiusFont.Label(new Rect(labelX, rect.y, rect.xMax - labelX - 4f, 24f), SpecialLabel(specialThingFilterDef), GameFont.Small, heading: false, flag ? tone : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
						if (!specialThingFilterDef.description.NullOrEmpty())
						{
							TooltipHandler.TipRegion(rect, specialThingFilterDef.description);
						}
					}
					if (PaintHit(rect, flag, flag))
					{
						filter.SetAllow(specialThingFilterDef, paintAllow);
						SyncSpecialDefs(specialThingFilterDef, paintAllow, filter, parentFilter);
						memoDirty = true;
						InspectorCore.Poke();
					}
					cy += 24f;
				}
				cy += 6f;
				CardChrome.Fill(new Rect(0f, cy, num, 1f), Palette.Border);
				cy += 6f;
			}
			revealY = -1f;
			DrawNode(treeNode_ThingCategory.catDef, filter, parentFilter, 0, num, ref cy, text2, Color.clear);
			lastContentH = Mathf.Max(cy, 10f);
			if (revealScroll && Event.current.type == EventType.Repaint)
			{
				// Centre the revealed row; a def the tree hides (parent filter, search) just drops the request.
				revealScroll = false;
				if (revealY >= 0f)
				{
					scroll.y = Mathf.Clamp(revealY - (lastViewH - ItemRowH) * 0.5f, 0f, Mathf.Max(0f, lastContentH - lastViewH));
				}
			}
		}
		finally
		{
			FlatScroll.EndOrUnwind(depth);
		}
	}

	// hue: colour of the top-level category this node belongs to, carried down the branch on its guide line.
	private static void DrawNode(ThingCategoryDef cat, ThingFilter filter, ThingFilter? parentFilter, int depth, float vw, ref float cy, string q, Color hue)
	{
		if (depth == 1)
		{
			hue = CategoryHue(cat);
		}
		bool flag = q.Length > 0 || badgeMask != Trait.None;
		int value;
		int num = (allowedMemo.TryGetValue(cat, out value) ? value : 0);
		int value2;
		int num2 = (totalMemo.TryGetValue(cat, out value2) ? value2 : 0);
		if (num2 == 0 || (flag && !matchCats.Contains(cat)))
		{
			return;
		}
		bool flag2 = flag && nameMatched.Contains(cat);
		bool flag3 = flag || expanded.Contains(cat) || depth == 0;
		if (depth > 0)
		{
			// Row layout from the left: chevron, checkbox, label. Item rows leave the chevron slot empty, so every
			// checkbox of one level sits in the same column. Only the checkbox paints; chevron and label fold.
			float num4 = (float)(depth - 1) * Indent;
			Rect rect = new Rect(num4, cy, vw - num4, 24f);
			if (ViewCull.Visible(cy, 24f, scroll.y, lastViewH))
			{
				DrawGuides(depth - 1, cy, 24f, hue);
				if (Mouse.IsOver(rect))
				{
					CardChrome.Hover(rect, 8f);
				}
				IconSet.Get(flag3 ? "Action/ChevronDown" : "Action/Next").Draw(new Rect(rect.x + 1f, rect.y + 6f, 12f, 12f), (depth == 1) ? hue : Palette.TextDim);
				DrawTriState(new Rect(rect.x + 18f, rect.y + 5f, 14f, 14f), num, num2);
				float labelX = rect.x + 38f;
				if (depth == 1)
				{
					Rect tile = new Rect(labelX, rect.y + 2f, 20f, 20f);
					CardChrome.Rounded(tile, (num > 0) ? hue : Palette.WithAlpha(hue, 0.35f), 6f);
					if (cat.icon != null && cat.icon != BaseContent.BadTex)
					{
						IconNorm.DrawTexture(cat.icon, tile.ContractedBy(3f), Palette.InkOnAccent);
					}
					labelX = tile.xMax + 6f;
				}
				RadiusFont.Label(new Rect(labelX, rect.y, rect.xMax - 56f - labelX, 24f), cat.LabelCap, GameFont.Small, depth == 1, (num > 0) ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
				RadiusFont.Label(new Rect(rect.xMax - 52f, rect.y, 48f, 24f), num + "/" + num2, GameFont.Tiny, heading: false, Palette.TextDim, TextAnchor.MiddleRight, wrap: false);
			}
			if (PaintHit(new Rect(rect.x + 16f, rect.y, 20f, 24f), num >= num2, num > 0))
			{
				filter.SetAllow(cat, paintAllow);
				memoDirty = true;
				InspectorCore.Poke();
			}
			else if (Widgets.ButtonInvisible(new Rect(rect.x, rect.y, 16f, 24f)) || Widgets.ButtonInvisible(new Rect(rect.x + 36f, rect.y, rect.width - 36f, 24f)))
			{
				if (!expanded.Remove(cat))
				{
					expanded.Add(cat);
				}
				InspectorCore.Poke();
			}
			cy += 24f;
			if (!flag3)
			{
				return;
			}
		}
		List<ThingCategoryDef> childCategories = cat.childCategories;
		for (int i = 0; i < childCategories.Count; i++)
		{
			DrawNode(childCategories[i], filter, parentFilter, depth + 1, vw, ref cy, q, hue);
		}
		List<ThingDef> childThingDefs = cat.childThingDefs;
		for (int j = 0; j < childThingDefs.Count; j++)
		{
			ThingDef thingDef = childThingDefs[j];
			if (!Visible(thingDef, parentFilter) || (flag && !ItemMatches(thingDef, q, flag2)))
			{
				continue;
			}
			float num3 = (float)depth * Indent;
			Rect rect2 = new Rect(num3, cy, vw - num3, ItemRowH);
			bool flag4 = filter.Allows(thingDef);
			bool revealed = thingDef == revealDef;
			if (revealed && revealY < 0f)
			{
				revealY = cy;
			}
			if (ViewCull.Visible(cy, ItemRowH, scroll.y, lastViewH))
			{
				DrawGuides(depth, cy, ItemRowH, hue);
				if (revealed && Time.realtimeSinceStartup < revealUntil)
				{
					CardChrome.Rounded(rect2, Palette.WithAlpha(RadiusTheme.Accent, 0.18f), 8f);
					CardChrome.Outline(rect2, RadiusTheme.Accent, 1f, 8f);
				}
				if (Mouse.IsOver(rect2))
				{
					CardChrome.Hover(rect2, 8f);
				}
				DrawTriState(new Rect(rect2.x + 18f, rect2.y + 7f, 14f, 14f), flag4 ? 1 : 0, 1);
				ItemIcon(new Rect(rect2.x + 37f, rect2.y + 1f, 26f, 26f), thingDef, flag4);
				float labelW = rect2.width - 73f;
				bool hasStored = storedCount.TryGetValue(thingDef, out var stored);
				// The stock column stays on every row once the storage holds anything, so the badges keep one
				// column right of the names instead of shifting with each row's stock.
				if (storedCount.Count > 0)
				{
					labelW -= 48f;
				}
				// Marks and badges carry their own tooltip, so the row's stock tooltip stays off them.
				float badgesEnd = rect2.x + 69f + labelW;
				labelW -= DrawTraitBadges(thingDef, badgesEnd, rect2.y, flag4);
				Rect badgeZone = new Rect(rect2.x + 69f + labelW, rect2.y, badgesEnd - rect2.x - 69f - labelW, ItemRowH);
				Rect tipFree = default;
				if (flag4 && partlyRefused.TryGetValue(thingDef, out var refusedBy))
				{
					int effect = SpecialEffect(refusedBy);
					Rect mark = new Rect(rect2.x + 69f + labelW - 12f, rect2.y + 10f, 8f, 8f);
					CardChrome.Rounded(mark, (effect > 0) ? Palette.Good : ((effect < 0) ? Palette.Bad : Palette.Warn), 4f);
					tipFree = new Rect(mark.x - 6f, rect2.y, 20f, ItemRowH);
					if (Mouse.IsOver(tipFree))
					{
						TooltipHandler.TipRegion(tipFree, "RUI.Inspector.StoragePartlyRefused".Translate(refusedBy.LabelCap));
					}
					labelW -= 18f;
				}
				// Stock held here shows on its row; in warning colour once the settings turn it away.
				if (hasStored)
				{
					leavingCount.TryGetValue(thingDef, out var leaving);
					RadiusFont.Label(new Rect(rect2.xMax - 48f, rect2.y, 44f, ItemRowH), stored.ToString(), GameFont.Tiny, heading: false, (leaving > 0) ? Palette.Warn : Palette.TextDim, TextAnchor.MiddleRight, wrap: false);
					if (Mouse.IsOver(rect2) && !Mouse.IsOver(tipFree) && !Mouse.IsOver(badgeZone))
					{
						TooltipHandler.TipRegion(rect2, (leaving > 0) ? "RUI.Inspector.StorageLeaving".Translate(thingDef.label, leaving) : "RUI.Inspector.StorageUntickMoves".Translate(thingDef.label, stored));
					}
				}
				RadiusFont.Label(new Rect(rect2.x + 69f, rect2.y, labelW, ItemRowH), thingDef.LabelCap, GameFont.Small, heading: false, flag4 ? Palette.Ink : Palette.TextDim, TextAnchor.MiddleLeft, wrap: false);
			}
			if (PaintHit(rect2, flag4, flag4))
			{
				filter.SetAllow(thingDef, paintAllow);
				memoDirty = true;
				InspectorCore.Poke();
			}
			cy += ItemRowH;
		}
	}

	// Def-level properties behind the vanilla special filters, read once per def. Burnable and smeltable carry a
	// second bit when they depend on the material (as the filter workers' CanEverMatch / AlwaysMatches).
	// Food follows the Ideology diet filters; meals and corpses are left out, their kind depends on each thing.
	// Toxic gathers what harms whoever eats it or stands near it: drugs, toxic waste, tox gas, poisoning food.
	private enum ToxicBadge : byte
	{
		None,
		Food,
		Drug,
		AddictiveDrug,
		Waste,
		Gas
	}

	// One bit per badge: a def carries the set of its properties, and the filter chips select by the same bits.
	[Flags]
	private enum Trait
	{
		None = 0,
		Plant = 1,
		AnimalProduct = 2,
		Meat = 4,
		Insect = 8,
		Human = 0x10,
		Toxic = 0x20,
		Perishable = 0x40,
		Burnable = 0x80,
		Smeltable = 0x100,
		Child = 0x200,
		Male = 0x400,
		Female = 0x800,
		BurnableByStuff = 0x1000,
		SmeltableByStuff = 0x2000
	}

	private struct ItemTraits
	{
		public Trait flags;

		public float rotDays;

		public ToxicBadge toxic;
	}

	private struct BadgeStyle
	{
		public Trait trait;

		public string? icon;

		public Color color;

		public BadgeStyle(Trait trait, string? icon, Color color)
		{
			this.trait = trait;
			this.icon = icon;
			this.color = color;
		}
	}

	// Reading order of the badges on a row and of the filter chips. Food kinds never meet on one row, so they
	// may share hues with each other; the others stay apart. Gender draws the vanilla symbol instead of an icon.
	private static readonly BadgeStyle[] badgeStyles = new BadgeStyle[12]
	{
		new BadgeStyle(Trait.Plant, "Common/Leaf", Palette.Good),
		new BadgeStyle(Trait.AnimalProduct, "Stat/Droplet", Palette.Health.Eating),
		new BadgeStyle(Trait.Meat, "Common/Fang", Palette.Health.BloodPumping),
		new BadgeStyle(Trait.Insect, "Event/Bug", Palette.Health.Talking),
		new BadgeStyle(Trait.Human, "Common/Skull", Palette.Health.BloodFiltration),
		new BadgeStyle(Trait.Toxic, "Marker/Toxic", Palette.WithAlpha(Palette.Health.MarkerToxic, 1f)),
		new BadgeStyle(Trait.Perishable, "Common/Clock", Palette.Genes.Endogene),
		new BadgeStyle(Trait.Burnable, "Stat/Flame", Palette.WithAlpha(Palette.Health.MarkerBurn, 1f)),
		new BadgeStyle(Trait.Smeltable, "Action/Swap", Palette.Flat.InkMid),
		new BadgeStyle(Trait.Child, "Occasion/Growth", Palette.Health.Consciousness),
		new BadgeStyle(Trait.Male, null, Palette.Commands.Orders),
		new BadgeStyle(Trait.Female, null, Palette.Commands.Social)
	};

	private const float BadgeD = 20f;

	private const float BadgeStep = 22f;

	private const float ChipD = 22f;

	// Properties the chips filter the tree by (all of them must apply), and those found in the tree on screen.
	private static Trait badgeMask;

	private static Trait presentMask;

	private static readonly Dictionary<ThingDef, ItemTraits> traitsMemo = new Dictionary<ThingDef, ItemTraits>();

	private static ItemTraits TraitsOf(ThingDef def)
	{
		if (!traitsMemo.TryGetValue(def, out var value))
		{
			value.rotDays = def.GetCompProperties<CompProperties_Rottable>()?.daysToRotStart ?? 0f;
			value.toxic = ToxicOf(def);
			Trait trait = FoodOf(def);
			if (value.toxic != ToxicBadge.None)
			{
				trait |= Trait.Toxic;
			}
			if (value.rotDays > 0f)
			{
				trait |= Trait.Perishable;
			}
			if (def.burnableByRecipe)
			{
				trait |= def.MadeFromStuff ? (Trait.Burnable | Trait.BurnableByStuff) : Trait.Burnable;
			}
			// Raw metals are smeltable too, but as the material itself: no use flagging them.
			if (def.smeltable && !def.IsStuff)
			{
				if (!def.MadeFromStuff)
				{
					trait |= Trait.Smeltable;
				}
				else if (def.PotentiallySmeltable)
				{
					trait |= Trait.Smeltable | Trait.SmeltableByStuff;
				}
			}
			if (def.IsApparel)
			{
				if (!def.apparel.developmentalStageFilter.Has(DevelopmentalStage.Adult))
				{
					trait |= Trait.Child;
				}
				if (def.apparel.gender == Gender.Male)
				{
					trait |= Trait.Male;
				}
				else if (def.apparel.gender == Gender.Female)
				{
					trait |= Trait.Female;
				}
			}
			value.flags = trait;
			traitsMemo[def] = value;
		}
		return value;
	}

	// Search text and chips combine: a name hit (item or its category) and every selected property.
	private static bool ItemMatches(ThingDef def, string q, bool categoryNamed)
	{
		if (!categoryNamed && q.Length > 0 && !DefMatches(def, q))
		{
			return false;
		}
		return (TraitsOf(def).flags & badgeMask) == badgeMask;
	}

	private static ToxicBadge ToxicOf(ThingDef def)
	{
		if (def.IsDrug)
		{
			return (def.GetCompProperties<CompProperties_Drug>()?.Addictive ?? false) ? ToxicBadge.AddictiveDrug : ToxicBadge.Drug;
		}
		if (def.GetCompProperties<CompProperties_DissolutionEffectPollution>() != null)
		{
			return ToxicBadge.Waste;
		}
		CompProperties_Explosive explosive = def.GetCompProperties<CompProperties_Explosive>();
		if (explosive != null && (explosive.postExplosionGasType == GasType.ToxGas || IsToxGas(explosive.explosiveDamageType)))
		{
			return ToxicBadge.Gas;
		}
		if (def.Verbs != null)
		{
			for (int i = 0; i < def.Verbs.Count; i++)
			{
				ProjectileProperties projectile = def.Verbs[i].defaultProjectile?.projectile;
				if (projectile != null && (projectile.postExplosionGasType == GasType.ToxGas || IsToxGas(projectile.damageDef)))
				{
					return ToxicBadge.Gas;
				}
			}
		}
		List<IngestionOutcomeDoer> outcomeDoers = def.ingestible?.outcomeDoers;
		if (outcomeDoers != null)
		{
			for (int j = 0; j < outcomeDoers.Count; j++)
			{
				if (outcomeDoers[j] is IngestionOutcomeDoer_GiveHediff giveHediff && giveHediff.hediffDef != null && (giveHediff.hediffDef.defName == "ToxicBuildup" || giveHediff.hediffDef.defName == "FoodPoisoning"))
				{
					return ToxicBadge.Food;
				}
			}
		}
		return ToxicBadge.None;
	}

	private static bool IsToxGas(DamageDef? damage)
	{
		return damage != null && damage.defName == "ToxGas";
	}

	private static Trait FoodOf(ThingDef def)
	{
		if (!def.IsIngestible || def.IsDrug || def.IsCorpse || def.IsProcessedFood)
		{
			return Trait.None;
		}
		switch (FoodUtility.GetFoodKind(def))
		{
		case FoodKind.Meat:
			switch (FoodUtility.GetMeatSourceCategory(def))
			{
			case MeatSourceCategory.Humanlike:
				return Trait.Human;
			case MeatSourceCategory.Insect:
				return Trait.Insect;
			default:
				return Trait.Meat;
			}
		case FoodKind.NonMeat:
			return Trait.Plant;
		default:
			return def.IsAnimalProduct ? Trait.AnimalProduct : Trait.None;
		}
	}

	// Draws the badges right-aligned, ending at endX, and returns the width they take from the label. Read from
	// the left in badgeStyles order. Faded on a refused row, like its label; a property that depends on the
	// material is faded too.
	private static float DrawTraitBadges(ThingDef def, float endX, float y, bool allowed)
	{
		ItemTraits traits = TraitsOf(def);
		float x = endX;
		float fade = allowed ? 1f : 0.5f;
		for (int num = badgeStyles.Length - 1; num >= 0; num--)
		{
			BadgeStyle style = badgeStyles[num];
			if ((traits.flags & style.trait) == 0)
			{
				continue;
			}
			bool byStuff = (style.trait == Trait.Burnable && (traits.flags & Trait.BurnableByStuff) != 0) || (style.trait == Trait.Smeltable && (traits.flags & Trait.SmeltableByStuff) != 0);
			x -= BadgeStep;
			DrawPastille(new Rect(x + (BadgeStep - BadgeD) * 0.5f, y + (ItemRowH - BadgeD) * 0.5f, BadgeD, BadgeD), style, byStuff ? (fade * 0.45f) : fade, selected: false);
			Rect zone = new Rect(x, y, BadgeStep, ItemRowH);
			if (Mouse.IsOver(zone))
			{
				TooltipHandler.TipRegion(zone, TraitTip(traits, style.trait));
			}
		}
		return endX - x;
	}

	private static string TraitTip(ItemTraits traits, Trait trait)
	{
		return trait switch
		{
			Trait.Plant => "RUI.Inspector.StorageFoodPlant".Translate(), 
			Trait.AnimalProduct => "RUI.Inspector.StorageFoodAnimalProduct".Translate(), 
			Trait.Meat => "RUI.Inspector.StorageFoodMeat".Translate(), 
			Trait.Insect => "RUI.Inspector.StorageFoodInsect".Translate(), 
			Trait.Human => "RUI.Inspector.StorageFoodHuman".Translate(), 
			Trait.Toxic => ("RUI.Inspector.StorageToxic" + traits.toxic).Translate(), 
			Trait.Perishable => "RUI.Inspector.StoragePerishable".Translate(traits.rotDays.ToString("0.#")), 
			Trait.Burnable => (((traits.flags & Trait.BurnableByStuff) == 0) ? "RUI.Inspector.StorageBurnable" : "RUI.Inspector.StorageBurnableStuff").Translate(), 
			Trait.Smeltable => (((traits.flags & Trait.SmeltableByStuff) == 0) ? "RUI.Inspector.StorageSmeltable" : "RUI.Inspector.StorageSmeltableStuff").Translate(), 
			Trait.Child => "RUI.Inspector.StorageChildOnly".Translate(), 
			Trait.Male => "RUI.Inspector.StorageMaleOnly".Translate(), 
			_ => "RUI.Inspector.StorageFemaleOnly".Translate(), 
		};
	}

	// The glyph keeps the badge's colour on a faint disc of the same hue: a dark glyph on a filled disc was
	// unreadable at row size. A selected filter chip gets a stronger disc and a ring.
	private static void DrawPastille(Rect rect, BadgeStyle style, float alpha, bool selected)
	{
		float radius = rect.width * 0.5f;
		CardChrome.Rounded(rect, Palette.WithAlpha(style.color, (selected ? 0.35f : 0.16f) * alpha), radius);
		if (selected)
		{
			CardChrome.Outline(rect, style.color, 1.5f, radius);
		}
		Rect glyph = rect.ContractedBy(rect.width * 0.17f);
		Color ink = Palette.WithAlpha(style.color, alpha);
		if (style.icon != null)
		{
			IconSet.Get(style.icon).Draw(glyph, ink);
		}
		else
		{
			IconNorm.DrawTexture(((style.trait == Trait.Male) ? Gender.Male : Gender.Female).GetIcon(), glyph, ink);
		}
	}

	// One chip per property found in the tree on screen (or still selected). Selected chips fill with their colour;
	// several selected must all apply. Wraps onto extra lines when the panel is narrow.
	private static float DrawBadgeFilters(float x, float y, float w)
	{
		Trait shown = presentMask | badgeMask;
		if (shown == Trait.None)
		{
			return y;
		}
		float cx = x;
		float cy = y;
		for (int i = 0; i < badgeStyles.Length; i++)
		{
			BadgeStyle style = badgeStyles[i];
			if ((shown & style.trait) == 0)
			{
				continue;
			}
			if (cx + ChipD > x + w)
			{
				cx = x;
				cy += ChipD + 3f;
			}
			Rect rect = new Rect(cx, cy, ChipD, ChipD);
			bool on = (badgeMask & style.trait) != 0;
			if (!on)
			{
				CardChrome.Rounded(rect, Mouse.IsOver(rect) ? Palette.Surface3 : Palette.Surface2, ChipD * 0.5f);
			}
			DrawPastille(rect, style, (on || Mouse.IsOver(rect)) ? 1f : 0.7f, on);
			if (Mouse.IsOver(rect))
			{
				TooltipHandler.TipRegion(rect, ("RUI.Inspector.StorageTrait" + style.trait).Translate().Resolve() + "\n" + "RUI.Inspector.StorageTraitFilterTip".Translate().Resolve());
			}
			if (Widgets.ButtonInvisible(rect))
			{
				badgeMask ^= style.trait;
				pendingDelete = null;
				scroll = Vector2.zero;
				InspectorCore.Poke();
			}
			cx += ChipD + 3f;
		}
		return cy + ChipD + 4f;
	}

	// One faint vertical line per ancestor level, under its chevron, so deep rows still show where they belong.
	// The outermost line takes the branch colour (top-level category, or accent for special filters).
	private static void DrawGuides(int levels, float y, float h, Color hue)
	{
		for (int i = 0; i < levels; i++)
		{
			CardChrome.Fill(new Rect((float)i * Indent + 7f, y, (i == 0 && hue.a > 0f) ? 2f : 1f, h), (i == 0 && hue.a > 0f) ? Palette.WithAlpha(hue, 0.55f) : Palette.Border);
		}
	}

	// Vanilla top-level categories get fixed, distinct colours; anything a mod adds falls back on a stable hash.
	private static Color CategoryHue(ThingCategoryDef cat)
	{
		if (categoryHue.TryGetValue(cat, out var value))
		{
			return value;
		}
		value = cat.defName switch
		{
			"Foods" => new Color(0.56f, 0.79f, 0.42f), 
			"Manufactured" => new Color(0.44f, 0.83f, 0.77f), 
			"ResourcesRaw" => new Color(0.91f, 0.72f, 0.5f), 
			"Items" => new Color(0.66f, 0.72f, 0.91f), 
			"Weapons" => new Color(0.91f, 0.53f, 0.53f), 
			"Apparel" => new Color(0.5f, 0.66f, 0.91f), 
			"Buildings" => new Color(0.91f, 0.84f, 0.5f), 
			"Chunks" => new Color(0.7f, 0.73f, 0.77f), 
			"Animals" => new Color(0.91f, 0.62f, 0.4f), 
			"Plants" => new Color(0.72f, 0.86f, 0.45f), 
			"Corpses" => new Color(0.84f, 0.62f, 0.91f), 
			_ => Palette.Policies.Fallback[(StableHash(cat.defName) & int.MaxValue) % Palette.Policies.Fallback.Length], 
		};
		categoryHue[cat] = value;
		return value;
	}

	private static int StableHash(string text)
	{
		int num = 17;
		for (int i = 0; i < text.Length; i++)
		{
			num = num * 31 + text[i];
		}
		return num;
	}

	// +1 when the filter admits sound things, -1 when it admits spoiled or risky ones, 0 when neutral. Read
	// from the defName so modded filters named the vanilla way get the same colour. Things that can be burned
	// or smelted down, finished goods and friendly mechs are green; their counterparts (non-burnable,
	// unfinished, enemy) are red.
	private static int SpecialEffect(SpecialThingFilterDef sf)
	{
		if (specialEffect.TryGetValue(sf, out var value))
		{
			return value;
		}
		string n = sf.defName ?? "";
		if (n.Contains("NonBurnable") || n.Contains("NonSmeltable") || n.Contains("Unfinished") || n.Contains("Enemy") || n.Contains("Hostile"))
		{
			value = -1;
		}
		else if (n.Contains("Burnable") || n.Contains("Smeltable") || n.Contains("Finished") || n.Contains("Friendly"))
		{
			value = 1;
		}
		else if (n.Contains("Fresh") || n.Contains("Clean") || n.Contains("NonDeadmans") || n.Contains("NonBiocoded") || n.Contains("NonTainted"))
		{
			value = 1;
		}
		else if (n.Contains("Rotten") || n.Contains("Deadmans") || n.Contains("Tainted") || n.Contains("Biocoded") || n.Contains("Cannibal") || n.Contains("InsectMeat") || n.Contains("Unnatural") || n.Contains("Dessicated") || n.Contains("Desiccated"))
		{
			value = -1;
		}
		else
		{
			value = 0;
		}
		specialEffect[sf] = value;
		return value;
	}

	// A special filter that matches every thing of a def decides that def outright (no meal is ever rotten, so
	// refusing "fresh" refuses meals). Refusing the filter unticks those rows so the tree shows what the
	// storage really accepts. Allowing it again only reticks the rows it unticked in this session: reticking
	// every matching def would widen a storage that took a few of them. Filters that depend on each thing's
	// state (rotten, tainted) leave the rows alone.
	private static void SyncSpecialDefs(SpecialThingFilterDef sf, bool allow, ThingFilter filter, ThingFilter? parentFilter)
	{
		ThingCategoryDef? scope = sf.parentCategory ?? specialRoot;
		if (scope == null)
		{
			return;
		}
		try
		{
			Dictionary<SpecialThingFilterDef, List<ThingDef>> memo = syncedOff.GetOrCreateValue(filter);
			if (allow)
			{
				if (memo.TryGetValue(sf, out var list))
				{
					memo.Remove(sf);
					for (int i = 0; i < list.Count; i++)
					{
						if (Visible(list[i], parentFilter))
						{
							filter.SetAllow(list[i], allow: true);
						}
					}
				}
				return;
			}
			SpecialThingFilterWorker worker = sf.Worker;
			if (worker == null)
			{
				return;
			}
			List<ThingDef> unticked = new List<ThingDef>();
			foreach (ThingDef def in scope.DescendantThingDefs)
			{
				if (filter.Allows(def) && Visible(def, parentFilter) && worker.CanEverMatch(def) && worker.AlwaysMatches(def))
				{
					filter.SetAllow(def, allow: false);
					unticked.Add(def);
				}
			}
			if (unticked.Count > 0)
			{
				memo[sf] = unticked;
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[Radius UI - Inspector] special filter sync failed: " + ex.Message, 5351405);
		}
	}

	// Refused special filters that depend on each thing's state (rotten, tainted) cannot untick a row: the
	// allowed rows they can still turn away get a dot in the filter's colour instead.
	private static void RebuildPartlyRefused(ThingCategoryDef root, ThingFilter filter, ThingFilter? parentFilter)
	{
		partlyRefused.Clear();
		for (int i = 0; i < specialCache.Count; i++)
		{
			SpecialThingFilterDef sf = specialCache[i];
			if (filter.Allows(sf))
			{
				continue;
			}
			try
			{
				SpecialThingFilterWorker worker = sf.Worker;
				if (worker == null)
				{
					continue;
				}
				foreach (ThingDef def in (sf.parentCategory ?? root).DescendantThingDefs)
				{
					if (!partlyRefused.ContainsKey(def) && Visible(def, parentFilter) && worker.CanEverMatch(def) && !worker.AlwaysMatches(def))
					{
						partlyRefused[def] = sf;
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce("[Radius UI - Inspector] special filter scan failed: " + ex.Message, 5351406);
			}
		}
	}

	// Things already stored here that a change makes the storage refuse get hauled away: say so. A stroke or
	// a button opens the watch, its end compares with what was refused before.
	private static void BeginEvictWatch()
	{
		if (evictWatching)
		{
			return;
		}
		evictWatching = true;
		evictBaseline.Clear();
		rejectedScratch.Clear();
		CollectRejected(rejectedScratch);
		evictBaseline.UnionWith(rejectedScratch);
	}

	private static void EndEvictWatch()
	{
		if (!evictWatching)
		{
			return;
		}
		evictWatching = false;
		rejectedScratch.Clear();
		CollectRejected(rejectedScratch);
		contentsScratch.Clear();
		int total = 0;
		foreach (Thing thing in rejectedScratch)
		{
			if (!evictBaseline.Contains(thing))
			{
				contentsScratch.TryGetValue(thing.def, out var value);
				contentsScratch[thing.def] = value + thing.stackCount;
				total += thing.stackCount;
			}
		}
		evictBaseline.Clear();
		rejectedScratch.Clear();
		if (total == 0)
		{
			return;
		}
		List<KeyValuePair<ThingDef, int>> list = new List<KeyValuePair<ThingDef, int>>(contentsScratch);
		list.Sort((KeyValuePair<ThingDef, int> a, KeyValuePair<ThingDef, int> b) => b.Value.CompareTo(a.Value));
		List<string> names = new List<string>();
		for (int i = 0; i < list.Count && i < 3; i++)
		{
			names.Add(list[i].Key.label + " x" + list[i].Value);
		}
		if (list.Count > 3)
		{
			names.Add("…");
		}
		contentsFrame = -1;
		Messages.Message("RUI.Inspector.StorageEvicts".Translate(total, names.ToCommaList()), MessageTypeDefOf.CautionInput, historical: false);
	}

	private static void CollectRejected(List<Thing> into)
	{
		IStoreSettingsParent? parent = lastParent;
		StorageSettings? settings = parent?.GetStoreSettings();
		IEnumerable<Thing>? things = (parent == null) ? null : HeldThings(parent);
		if (settings == null || things == null)
		{
			return;
		}
		foreach (Thing thing in things)
		{
			if (!settings.AllowedToAccept(thing))
			{
				into.Add(thing);
			}
		}
	}

	private static void DrawTriState(Rect r, int allowed, int total)
	{
		if (allowed >= total && total > 0)
		{
			CardChrome.Rounded(r, RadiusTheme.Accent, 4f);
			IconSet.Get("Action/Check").Draw(r.ContractedBy(2f), Palette.InkOnAccent);
		}
		else if (allowed > 0)
		{
			CardChrome.Rounded(r, Palette.Surface3, 4f);
			CardChrome.Fill(new Rect(r.x + 3f, r.center.y - 1f, r.width - 6f, 2f), RadiusTheme.Accent);
		}
		else
		{
			CardChrome.Rounded(r, Palette.Surface3, 4f);
			CardChrome.Outline(r, Palette.Border, 1f, 4f);
		}
	}

	// The tree asks this for every def on every GUI event, and the parent filter's special-filter test walks all
	// its refused filters: answers are kept per def until the parent filter changes or the counts are rebuilt.
	// Parent filters (a building's fixed settings) are not edited from this panel, so nothing else can stale it.
	private static readonly Dictionary<ThingDef, bool> visibleMemo = new Dictionary<ThingDef, bool>();

	private static ThingFilter? visibleMemoParent;

	private static bool Visible(ThingDef d, ThingFilter? parentFilter)
	{
		if (d == null)
		{
			return false;
		}
		if (parentFilter != visibleMemoParent)
		{
			visibleMemo.Clear();
			visibleMemoParent = parentFilter;
		}
		if (!visibleMemo.TryGetValue(d, out var value))
		{
			value = d.PlayerAcquirable && d.virtualDefParent == null && (parentFilter == null || (parentFilter.Allows(d) && !parentFilter.IsAlwaysDisallowedDueToSpecialFilters(d)));
			visibleMemo[d] = value;
		}
		return value;
	}

	private static bool SpecialMatches(SpecialThingFilterDef sf, string q)
	{
		if (sf.label != null)
		{
			return SpecialLabel(sf).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return false;
	}

	private static string SpecialLabel(SpecialThingFilterDef sf)
	{
		if (specialLabel.TryGetValue(sf, out var value))
		{
			return value;
		}
		return sf.LabelCap;
	}

	private static void RebuildSpecials(ThingCategoryDef cat, ThingFilter? parentFilter)
	{
		specialCache.Clear();
		specialLabel.Clear();
		if (cat == null)
		{
			return;
		}
		visibleDefScratch.Clear();
		foreach (ThingDef descendantThingDef in cat.DescendantThingDefs)
		{
			if (Visible(descendantThingDef, parentFilter))
			{
				visibleDefScratch.Add(descendantThingDef);
			}
		}
		foreach (SpecialThingFilterDef parentsSpecialThingFilterDef in cat.ParentsSpecialThingFilterDefs)
		{
			TryAddSpecial(parentsSpecialThingFilterDef, parentFilter);
		}
		foreach (SpecialThingFilterDef descendantSpecialThingFilterDef in cat.DescendantSpecialThingFilterDefs)
		{
			TryAddSpecial(descendantSpecialThingFilterDef, parentFilter);
		}
		visibleDefScratch.Clear();
		for (int i = 0; i < specialCache.Count; i++)
		{
			SpecialThingFilterDef sf = specialCache[i];
			for (int j = 0; j < specialCache.Count; j++)
			{
				if (j != i && specialCache[j].label == sf.label && sf.parentCategory != null)
				{
					specialLabel[sf] = sf.LabelCap + " (" + sf.parentCategory.label + ")";
					break;
				}
			}
		}
	}

	private static void TryAddSpecial(SpecialThingFilterDef sf, ThingFilter? parentFilter)
	{
		if (sf != null && sf.configurable && !specialCache.Contains(sf) && (parentFilter == null || (parentFilter.Allows(sf) && (parentFilter.hiddenSpecialFilters == null || !parentFilter.hiddenSpecialFilters.Contains(sf)))) && !SuppressedInStorageTab(sf) && CanEverMatchSomethingVisible(sf))
		{
			specialCache.Add(sf);
		}
	}

	private static bool SuppressedInStorageTab(SpecialThingFilterDef sf)
	{
		if (!ModsConfig.IdeologyActive)
		{
			return false;
		}
		if (sf != SpecialThingFilterDefOf.AllowVegetarian && sf != SpecialThingFilterDefOf.AllowCarnivore && sf != SpecialThingFilterDefOf.AllowCannibal)
		{
			return sf == SpecialThingFilterDefOf.AllowInsectMeat;
		}
		return true;
	}

	private static bool CanEverMatchSomethingVisible(SpecialThingFilterDef sf)
	{
		try
		{
			SpecialThingFilterWorker worker = sf.Worker;
			if (worker == null)
			{
				return true;
			}
			for (int i = 0; i < visibleDefScratch.Count; i++)
			{
				if (worker.CanEverMatch(visibleDefScratch[i]))
				{
					return true;
				}
			}
		}
		catch (Exception)
		{
			return true;
		}
		return false;
	}

	private static bool DefMatches(ThingDef d, string q)
	{
		if (d.label != null)
		{
			return d.label.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return false;
	}

	private static bool RebuildMatches(ThingCategoryDef cat, ThingFilter? parentFilter, string q)
	{
		// A category named by the search shows all its items, unless badge filters still sort them.
		bool flag = false;
		bool named = q.Length > 0 && cat.label != null && cat.label.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
		if (named)
		{
			nameMatched.Add(cat);
			flag = badgeMask == Trait.None;
		}
		List<ThingCategoryDef> childCategories = cat.childCategories;
		for (int i = 0; i < childCategories.Count; i++)
		{
			if (RebuildMatches(childCategories[i], parentFilter, q))
			{
				flag = true;
			}
		}
		if (!flag)
		{
			List<ThingDef> childThingDefs = cat.childThingDefs;
			for (int j = 0; j < childThingDefs.Count; j++)
			{
				if (Visible(childThingDefs[j], parentFilter) && ItemMatches(childThingDefs[j], q, named))
				{
					flag = true;
					break;
				}
			}
		}
		if (flag)
		{
			matchCats.Add(cat);
		}
		return flag;
	}

	private static int RebuildCounts(ThingCategoryDef cat, ThingFilter filter, ThingFilter? parentFilter)
	{
		int num = 0;
		int num2 = 0;
		List<ThingCategoryDef> childCategories = cat.childCategories;
		for (int i = 0; i < childCategories.Count; i++)
		{
			RebuildCounts(childCategories[i], filter, parentFilter);
			num += (allowedMemo.TryGetValue(childCategories[i], out var value) ? value : 0);
			num2 += (totalMemo.TryGetValue(childCategories[i], out var value2) ? value2 : 0);
		}
		List<ThingDef> childThingDefs = cat.childThingDefs;
		for (int j = 0; j < childThingDefs.Count; j++)
		{
			if (Visible(childThingDefs[j], parentFilter))
			{
				num2++;
				presentMask |= TraitsOf(childThingDefs[j]).flags;
				if (filter.Allows(childThingDefs[j]))
				{
					num++;
				}
			}
		}
		allowedMemo[cat] = num;
		totalMemo[cat] = num2;
		return num2;
	}
}
