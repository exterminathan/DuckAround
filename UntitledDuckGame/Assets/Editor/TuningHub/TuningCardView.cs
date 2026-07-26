// TuningCardView.cs — one collapsible card: header (arrow · title · type pill ·
// warning chip) + a body of TuningFieldRows. Handles search-filter roll-up and
// re-striping (USS has no :nth-child, so alternating rows are a C# class toggle).
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class TuningCardView : VisualElement {
	readonly VisualElement body;
	readonly VisualElement arrow;
	readonly List<TuningFieldRow> rows = new();
	bool collapsed;

	public IReadOnlyList<TuningFieldRow> Rows => rows;

	public TuningCardView(string title, string pillText, string warnText = null) {
		AddToClassList("th-card");

		var header = new VisualElement();
		header.AddToClassList("th-card-header");
		header.RegisterCallback<ClickEvent>(_ => ToggleCollapsed());

		arrow = new VisualElement();
		arrow.AddToClassList("th-card-arrow");
		header.Add(arrow);

		var titleEl = new Label(title);
		titleEl.AddToClassList("th-card-title");
		header.Add(titleEl);

		if (!string.IsNullOrEmpty(pillText)) {
			var pill = new Label(pillText);
			pill.AddToClassList("th-card-pill");
			header.Add(pill);
		}

		if (!string.IsNullOrEmpty(warnText)) {
			var warn = new Label(warnText);
			warn.AddToClassList("th-card-warn");
			header.Add(warn);
		}

		Add(header);

		body = new VisualElement();
		body.AddToClassList("th-card-body");
		Add(body);
	}

	void ToggleCollapsed() {
		collapsed = !collapsed;
		body.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
		arrow.EnableInClassList("is-collapsed", collapsed);
	}

	public void AddRow(TuningFieldRow row) {
		rows.Add(row);
		body.Add(row);
	}

	public void AddNote(string text) {
		var note = new Label(text);
		note.AddToClassList("th-note");
		body.Add(note);
	}

	public void AddWarning(string text) {
		var warn = new Label("⚠ " + text);
		warn.AddToClassList("th-missing");
		body.Add(warn);
	}

	public void AddSubHeader(string text) {
		var sub = new Label(text);
		sub.AddToClassList("th-subheader");
		body.Add(sub);
	}

	// Returns true when at least one row survives the filter; hides itself otherwise.
	public bool ApplyFilter(Func<TuningFieldRow, bool> predicate) {
		int visible = 0;
		foreach (var row in rows) {
			bool show = predicate == null || predicate(row);
			row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
			if (show) {
				row.EnableInClassList("th-row--alt", visible % 2 == 1);
				visible++;
			}
		}
		bool any = visible > 0 || rows.Count == 0; // keep empty-state/notes-only cards visible when unfiltered
		if (rows.Count == 0 && predicate != null) any = false;
		style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
		return any;
	}
}
