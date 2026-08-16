using System.Collections.Generic;
using System.Text;
using RimWorld;
using Ustas.RimAI.Art.storage;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Art.manual
{
    public sealed class Dialog_ManualTextEditor : Window
    {
        private readonly ManualTextEditContext _context;
        private string _titleBuffer;
        private string _bodyBuffer;
        private Vector2 _historyScroll;

        public override Vector2 InitialSize => new Vector2(920f, 760f);

        public Dialog_ManualTextEditor(ManualTextEditContext context)
        {
            _context = context;
            _titleBuffer = context?.Title ?? string.Empty;
            _bodyBuffer = context?.Body ?? string.Empty;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (_context == null)
            {
                Close();
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f),
                "RimTalkLE_ManualEditor_Title".Translate(_context.KindLabelKey.Translate()));

            Text.Font = GameFont.Small;
            float cursorY = inRect.y + 42f;
            var infoRect = new Rect(inRect.x, cursorY, inRect.width, 52f);
            Widgets.Label(infoRect, BuildTargetSummary());
            cursorY = infoRect.yMax + 8f;

            Widgets.Label(new Rect(inRect.x, cursorY, inRect.width, Text.LineHeight), "RimTalkLE_ManualEditor_FieldTitle".Translate());
            cursorY += Text.LineHeight + 4f;
            _titleBuffer = Widgets.TextField(new Rect(inRect.x, cursorY, inRect.width, 32f), _titleBuffer ?? string.Empty);
            cursorY += 40f;

            Widgets.Label(new Rect(inRect.x, cursorY, inRect.width, Text.LineHeight), "RimTalkLE_ManualEditor_FieldBody".Translate());
            cursorY += Text.LineHeight + 4f;

            float buttonsHeight = 42f;
            float historyHeight = 170f;
            float bodyHeight = Mathf.Max(180f, inRect.yMax - cursorY - historyHeight - buttonsHeight - 26f);
            var bodyRect = new Rect(inRect.x, cursorY, inRect.width, bodyHeight);
            _bodyBuffer = Widgets.TextArea(bodyRect, _bodyBuffer ?? string.Empty);
            cursorY = bodyRect.yMax + 8f;

            Widgets.Label(new Rect(inRect.x, cursorY, inRect.width, Text.LineHeight), "RimTalkLE_ManualEditor_History".Translate());
            cursorY += Text.LineHeight + 4f;
            DrawHistory(new Rect(inRect.x, cursorY, inRect.width, historyHeight));

            var buttonRow = new Rect(inRect.x, inRect.yMax - buttonsHeight, inRect.width, buttonsHeight);
            DrawButtons(buttonRow);
        }

        private string BuildTargetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("RimTalkLE_ManualEditor_Target".Translate(_context.TargetLabel ?? string.Empty));
            sb.AppendLine("RimTalkLE_ManualEditor_DefName".Translate(_context.TargetDefName ?? string.Empty));
            sb.Append("RimTalkLE_ManualEditor_FutureNote".Translate());
            return sb.ToString().TrimEnd();
        }

        private void DrawHistory(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            var history = _context.History ?? new List<TextHistoryEntry>();
            if (history.Count == 0)
            {
                Widgets.Label(rect.ContractedBy(8f), "RimTalkLE_ManualEditor_HistoryEmpty".Translate());
                return;
            }

            float rowHeight = 56f;
            float viewHeight = history.Count * rowHeight;
            var outRect = rect.ContractedBy(4f);
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref _historyScroll, viewRect);

            float y = 0f;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var entry = history[i];
                if (entry == null) continue;

                var rowRect = new Rect(0f, y, viewRect.width, rowHeight - 4f);
                Widgets.DrawHighlightIfMouseover(rowRect);
                Widgets.Label(rowRect, BuildHistoryLine(entry));
                y += rowHeight;
            }

            Widgets.EndScrollView();
        }

        private string BuildHistoryLine(TextHistoryEntry entry)
        {
            string sourceKey = entry.Source == TextHistorySource.Manual
                ? "RimTalkLE_ManualEditor_SourceManual"
                : "RimTalkLE_ManualEditor_SourceGenerated";

            string title = string.IsNullOrWhiteSpace(entry.Title)
                ? "RimTalkLE_ManualEditor_EmptyValue".Translate().ToString()
                : entry.Title.Trim();
            string bodyPreview = string.IsNullOrWhiteSpace(entry.Body)
                ? "RimTalkLE_ManualEditor_EmptyValue".Translate().ToString()
                : TrimPreview(entry.Body, 100);

            return "RimTalkLE_ManualEditor_HistoryLine".Translate(
                sourceKey.Translate(),
                entry.Tick.ToString(),
                title,
                bodyPreview);
        }

        private static string TrimPreview(string value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            if (value.Length <= maxChars)
                return value;
            return value.Substring(0, maxChars).TrimEnd() + "...";
        }

        private void DrawButtons(Rect rect)
        {
            float gap = 8f;
            float width = (rect.width - gap * 2f) / 3f;
            var cancelRect = new Rect(rect.x, rect.y, width, rect.height);
            var restoreRect = new Rect(cancelRect.xMax + gap, rect.y, width, rect.height);
            var saveRect = new Rect(restoreRect.xMax + gap, rect.y, width, rect.height);

            if (Widgets.ButtonText(cancelRect, "Cancel".Translate().CapitalizeFirst()))
                Close();

            bool canRestore = _context.CanRestoreAutomatic;
            using (new GUIStateScope(enabled: canRestore))
            {
                if (Widgets.ButtonText(restoreRect, "RimTalkLE_ManualEditor_Restore".Translate()) && canRestore)
                {
                    if (ManualTextEditService.RestoreAutomatic(_context))
                    {
                        Messages.Message("RimTalkLE_ManualEditor_RestoreSuccess".Translate(), MessageTypeDefOf.PositiveEvent, false);
                        Close();
                    }
                    else
                    {
                        Messages.Message("RimTalkLE_ManualEditor_RestoreFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                }
            }

            if (Widgets.ButtonText(saveRect, "RimTalkLE_ManualEditor_Save".Translate()))
            {
                if (ManualTextEditService.Save(_context, _titleBuffer, _bodyBuffer))
                {
                    Messages.Message("RimTalkLE_ManualEditor_SaveSuccess".Translate(), MessageTypeDefOf.PositiveEvent, false);
                    Close();
                }
                else
                {
                    Messages.Message("RimTalkLE_ManualEditor_SaveFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }
        }

        private readonly struct GUIStateScope : System.IDisposable
        {
            private readonly bool _previous;

            public GUIStateScope(bool enabled)
            {
                _previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _previous;
            }
        }
    }
}
