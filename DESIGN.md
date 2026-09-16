# FlowNote repair UI contract

This maintenance release keeps the existing layout and visual identity.

- Capsule: 520 x 52 DIP, one input, at most one auxiliary panel.
- Main surface: existing paper #EDE8E1 and glass-card brushes.
- Text: #1C1C1E; secondary text: #636366; accent: #8A6A3D.
- Use the existing Malgun Gothic / Segoe UI font settings.
- Keep 4/8/12/16/24 spacing and current component radii.
- No new themes, gradients, characters, dashboards, or decorative animation.
- Normal startup opens the capsule only. The day window remains user-invoked.
- Search must be visible from panorama, task, and settings pages.
- Show the assembly version and build identity in Settings.
- State text is never editable note content.
- Preserve keyboard focus indicators, IME handling, and existing note data.
- Generated images are not evidence of runtime rendering.

## 0.5.4 quick-note update
- Notes are one body, not title plus excerpt. Preserve newlines and original storage.
- Sticky paper surface #FFF9E8, edge #E6DDC6, corner 3 DIP; use existing ink.
- Panorama keeps automatic work grouping and request markers; group labels are small context, not editable note titles.
- Existing records open as visible note bodies by default. Explicit collapse remains respected during refresh.
- Image click or keyboard Enter opens an in-app zoom view. No external app or upload.
- Explicit quick-note shortcut focuses the visible single-line/multiline editor and preserves the draft.
