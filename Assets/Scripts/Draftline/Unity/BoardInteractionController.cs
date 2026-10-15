using System;
using System.Collections.Generic;
using System.Linq;
using Draftline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Draftline.Unity
{
    [DisallowMultipleComponent]
    public sealed class BoardInteractionController : MonoBehaviour
    {
        public DraftlineGameController Controller;
        public BoardInteractionStyle Style;
        public Transform VisualRoot;
        private readonly List<BoardTarget> targets = new List<BoardTarget>();
        private readonly List<GameObject> paths = new List<GameObject>();
        private readonly List<GameObject> consequences = new List<GameObject>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private GameObject selection;
        private GameObject contextMenu;
        private int? previewPiece;
        private SlideDirection? stopDirection;
        private BoardTarget hovered;
        private int lastRevision = -1;
        public ItemType? PendingItem { get; private set; }
        public int? SelectedPieceId => Controller.Rules?.State.SelectedPieceId ?? previewPiece;
        public bool HasSelection => SelectedPieceId.HasValue;
        public string InspectionText { get; private set; }
        public string Instruction => Controller.Rules == null ? "" : Controller.Rules.State.Phase == MatchPhase.WaitRoll
            ? "点击桌面骰子，决定本回合滑行次数。"
            : !HasSelection ? "点击己方棋子查看路径 · 点出生格 + 出子"
            : (PendingItem == ItemType.EmergencyStop || Controller.Rules.State.EmergencyStopActive)
                ? "点击路径上的金色停点 · 右键取消预览"
                : "悬停查看路线，点击绿色终点或棋子旁箭头滑行。";

        public bool IsPointerOverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        private void Update()
        {
            if (Controller == null || Style == null) return;
            if (ThemeSceneryInteraction.ProcessPointer(this)) return;
            var state = Controller.Rules?.State;
            if (state == null) { if (targets.Count > 0 || selection != null) ClearVisuals(); return; }
            if (state.Revision != lastRevision) { lastRevision = state.Revision; StateChanged(); }
            if (!Controller.CanControl || Controller.IsAnimating || Controller.IsPaused || state.Phase == MatchPhase.GameOver)
            { if (targets.Count > 0) ClearVisuals(); return; }
            if (targets.Count == 0 && (HasSelection || state.Phase == MatchPhase.WaitSelect)) Rebuild();
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame && !Controller.CanvasUI.GuideClosedThisFrame) CancelPreview();
                if (keyboard.spaceKey.wasPressedThisFrame) Controller.RollFromUI();
                if (keyboard.upArrowKey.wasPressedThisFrame) ChooseDirection(SlideDirection.Up);
                if (keyboard.downArrowKey.wasPressedThisFrame) ChooseDirection(SlideDirection.Down);
                if (keyboard.leftArrowKey.wasPressedThisFrame) ChooseDirection(SlideDirection.Left);
                if (keyboard.rightArrowKey.wasPressedThisFrame) ChooseDirection(SlideDirection.Right);
            }
            var mouse = Mouse.current;
            if (mouse == null || Camera.main == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { CancelPreview(); return; }
            var position = mouse.position.ReadValue();
            if (IsPointerOverUI(position)) { Hover(null); return; }
            var target = TargetUnderPointer(position);
            Hover(target);
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (target != null) { ClickTarget(target); return; }
            var ray = Camera.main.ScreenPointToRay(position);
            var hits = Physics.RaycastAll(ray).OrderBy(hit => hit.distance);
            foreach (var hit in hits)
            {
                var piece = hit.collider.GetComponentInParent<DraftlinePieceView>();
                if (piece != null) { Select(piece.PieceId); return; }
                var dice = hit.collider.GetComponentInParent<DraftlineRollDice>();
                if (dice != null) { dice.TryPress(); return; }
                var prop = hit.collider.GetComponentInParent<DraftlineToyProp>();
                if (prop != null) { prop.Play(); return; }
            }
            if (state.Phase == MatchPhase.WaitSelect) CancelPreview();
        }

        private BoardTarget TargetUnderPointer(Vector2 screen)
        {
            BoardTarget closest = null;
            var center = Camera.main.WorldToScreenPoint(Vector3.zero);
            var neighbor = Camera.main.WorldToScreenPoint(Vector3.forward);
            var distance = Mathf.Max(12f * Screen.height / 1080f,Vector2.Distance(center,neighbor)*Style.PointerHitRadius);
            foreach (var target in targets)
            {
                if (target == null) continue;
                var point = Camera.main.WorldToScreenPoint(target.transform.position);
                if (point.z <= 0) continue;
                var d = Vector2.Distance(screen, new Vector2(point.x, point.y));
                if (d >= distance) continue;
                distance = d;
                closest = target;
            }
            return closest;
        }

        public void Select(int id)
        {
            if (!Controller.CanControl || Controller.IsAnimating || Controller.Rules == null) return;
            var state = Controller.Rules.State;
            if (state.Phase != MatchPhase.WaitSelect) return;
            var piece = Controller.Rules.PieceById(id);
            if (piece == null || piece.Owner != state.CurrentPlayer || !Controller.Rules.HasLegalSlide(piece))
            { Controller.ShowError("请选择可以滑行的己方棋子。"); return; }
            previewPiece = id;
            stopDirection = null;
            PendingItem = null;
            Controller.ShowError(null);
            Rebuild();
        }

        public void ToggleItem(ItemType item)
        {
            if (!HasSelection || !Controller.CanControl || Controller.IsAnimating) return;
            PendingItem = PendingItem == item ? (ItemType?)null : item;
            stopDirection = null;
            if (PreviewRules() == null) { PendingItem = null; Controller.ShowError("此道具已生效或暂不可使用。"); }
            else Controller.ShowError(null);
            Rebuild();
        }

        public void CancelPreview()
        {
            previewPiece = null;
            PendingItem = null;
            stopDirection = null;
            Controller.ShowError(null);
            Rebuild();
        }

        public void StateChanged()
        {
            lastRevision = Controller.Rules?.State.Revision ?? -1;
            previewPiece = null;
            PendingItem = null;
            stopDirection = null;
            Rebuild();
        }

        public MatchRules PreviewRules()
        {
            if (Controller.Rules == null || !SelectedPieceId.HasValue) return null;
            var preview = Controller.Rules.CloneForSimulation();
            if (preview.State.Phase == MatchPhase.WaitSelect && !preview.TrySelectPiece(SelectedPieceId.Value, out _)) return null;
            if (PendingItem.HasValue && !preview.TryActivateItem(PendingItem.Value, out _)) return null;
            return preview;
        }

        public void ChooseDirection(SlideDirection direction)
        {
            var preview = PreviewRules();
            if (preview == null || !preview.PlanSlide(preview.PieceById(SelectedPieceId.Value), direction).IsLegal) return;
            if (preview.State.EmergencyStopActive) { stopDirection = direction; Rebuild(); return; }
            Submit(direction, -1);
        }

        private void Submit(SlideDirection direction, int stopIndex)
        {
            if (!SelectedPieceId.HasValue) return;
            Controller.SubmitCommand(SlideCommand(direction,stopIndex));
        }

        private MatchCommand SlideCommand(SlideDirection direction,int stopIndex) => new MatchCommand { Kind = MatchCommandKind.Slide,
                ExpectedRevision = Controller.Rules.State.Revision, PieceId = SelectedPieceId.Value,
                Direction = direction, StopIndex = stopIndex, Item = PendingItem.HasValue ? (int)PendingItem.Value : 0 };

        private void ClickTarget(BoardTarget target)
        {
            if (target.Revision != Controller.Rules.State.Revision) { StateChanged(); return; }
            if (target.Deploy) { Controller.DeployFromUI(); return; }
            if (target.DirectionChoice) { ChooseDirection(target.Direction); return; }
            var matches = targets.Where(t => t != null && !t.DirectionChoice && t.Cell == target.Cell && t.Direction != target.Direction).ToArray();
            if (!stopDirection.HasValue && matches.Length > 0)
            {
                Controller.ShowError("多条路径到达这里，请点棋子旁的方向箭头选择路径。");
                Hover(null);
                return;
            }
            Submit(target.Direction, target.StopIndex);
        }

        public void Rebuild()
        {
            ClearVisuals();
            if (Style == null || Controller == null || Controller.Rules == null || !Controller.CanControl || Controller.IsAnimating || Controller.IsPaused) return;
            var state = Controller.Rules.State;
            if (state.Phase == MatchPhase.GameOver) return;
            if (state.Phase == MatchPhase.WaitSelect && Controller.Rules.CanDeploy(state.CurrentPlayer))
                AddTarget(Controller.Rules.Board.SpawnOf(state.CurrentPlayer), SlideDirection.Up, -1, "+", true, false);
            var preview = PreviewRules();
            if (preview == null) return;
            var piece = preview.PieceById(SelectedPieceId.Value);
            selection = Instantiate(Style.SelectionPrefab, VisualRoot);
            selection.name = "Selected Piece";
            selection.transform.position = BoardCoordinates.ToWorld(piece.Position, Style.TargetHeight - .08f);
            var selectionTint = new MaterialPropertyBlock();
            selectionTint.SetColor("_BaseColor",Style.Selected);selectionTint.SetColor("_Color",Style.Selected);
            selection.GetComponent<Renderer>().SetPropertyBlock(selectionTint);
            if (Style.ContextMenuPrefab != null && state.Players[(int)state.CurrentPlayer].Items.Count > 0)
            {
                contextMenu = Instantiate(Style.ContextMenuPrefab, VisualRoot);
                contextMenu.transform.position = BoardCoordinates.ToWorld(piece.Position) + Style.ContextOffset;
                contextMenu.GetComponent<BoardContextMenu>().Initialize(Controller);
            }
            foreach (SlideDirection direction in Enum.GetValues(typeof(SlideDirection)))
            {
                var plan = preview.PlanSlide(piece, direction);
                if (!plan.IsLegal) continue;
                var vector = DirectionVector(direction);
                var arrow = AddTarget(piece.Position, direction, -1, Arrow(direction), false, true);
                arrow.transform.position += vector * .88f;
                arrow.transform.localScale *= .6f;
                if (stopDirection.HasValue && stopDirection != direction) continue;
                if (preview.State.EmergencyStopActive)
                    for (var i = 0; i < plan.Steps.Count; i++) AddTarget(plan.Steps[i].Position, direction, i, "·", false, false);
                else AddTarget(plan.Destination, direction, -1, Arrow(direction), false, false);
            }
        }

        private BoardTarget AddTarget(GridPosition cell, SlideDirection direction, int stopIndex, string label, bool deploy, bool choice)
        {
            var go = Instantiate(Style.TargetPrefab, VisualRoot);
            go.name = deploy ? "Deploy +" : direction + " target " + cell;
            go.transform.position = BoardCoordinates.ToWorld(cell, Style.TargetHeight);
            go.transform.localScale = Style.TargetScale;
            var target = go.GetComponent<BoardTarget>();
            target.PieceId = SelectedPieceId ?? -1;
            target.Revision = Controller.Rules.State.Revision;
            target.Direction = direction; target.StopIndex = stopIndex; target.Cell = cell;
            target.Deploy = deploy; target.DirectionChoice = choice;
            var mesh = go.GetComponentInChildren<TextMesh>();
            if (mesh != null) mesh.text = label;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", stopIndex >= 0 ? Style.Stop : Style.Normal);
            block.SetColor("_Color", stopIndex >= 0 ? Style.Stop : Style.Normal);
            go.GetComponent<Renderer>().SetPropertyBlock(block);
            targets.Add(target);
            return target;
        }

        private void Hover(BoardTarget target)
        {
            if (target == hovered) return;
            hovered = target;
            InspectionText = null;
            foreach (var go in paths) if (go != null) Destroy(go);
            paths.Clear();
            foreach (var go in consequences) if (go != null) Destroy(go);
            consequences.Clear();
            var preview = PreviewRules();
            if (target == null) return;
            if (target.Deploy)
            {
                InspectionText = MatchExplanation.Describe(ActionInspection.Evaluate(Controller.Rules,
                    new MatchCommand { Kind=MatchCommandKind.Deploy, ExpectedRevision=Controller.Rules.State.Revision }));
                return;
            }
            if (preview == null) return;
            if (target.DirectionChoice && preview.State.EmergencyStopActive)
                InspectionText = "急停 · 这个箭头只选择路线\n再点金色停点，查看结果并确认。";
            else
            {
                var inspection = ActionInspection.Evaluate(Controller.Rules,SlideCommand(target.Direction,target.StopIndex));
                InspectionText = MatchExplanation.Describe(inspection);
                if (!target.DirectionChoice && !stopDirection.HasValue && targets.Any(t=>t!=null&&!t.DirectionChoice&&t.Cell==target.Cell&&t.Direction!=target.Direction))
                    InspectionText = "此格有多条路线 · 先点方向箭头\n" + InspectionText;
                foreach (var capture in inspection.Events.Where(e=>e.Type==MatchEventType.PieceCaptured))
                {
                    var ring=Instantiate(Style.SelectionPrefab,VisualRoot);
                    ring.name="Preview capture: "+capture.Cause;
                    ring.transform.position=BoardCoordinates.ToWorld(capture.Position,Style.TargetHeight);
                    var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",Style.Danger);tint.SetColor("_Color",Style.Danger);
                    ring.GetComponent<Renderer>().SetPropertyBlock(tint);consequences.Add(ring);
                }
            }
            var plan = preview.PlanSlide(preview.PieceById(SelectedPieceId.Value), target.Direction);
            var points = new List<Vector3> { BoardCoordinates.ToWorld(preview.PieceById(SelectedPieceId.Value).Position, Style.PathHeight) };
            for (var i = 0; i < plan.Steps.Count; i++)
            {
                if (target.StopIndex >= 0 && i > target.StopIndex) break;
                var step = plan.Steps[i];
                if (step.PortalExit) { DrawPath(points); points.Clear(); }
                points.Add(BoardCoordinates.ToWorld(step.Position, Style.PathHeight));
            }
            DrawPath(points);
        }

        private void DrawPath(List<Vector3> points)
        {
            if (points.Count < 2) return;
            var go = new GameObject("Slide Preview Path");
            go.transform.SetParent(VisualRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Style.PathMaterial;
            var tint = new MaterialPropertyBlock();tint.SetColor("_BaseColor",Style.Hover);tint.SetColor("_Color",Style.Hover);line.SetPropertyBlock(tint);
            line.startWidth = line.endWidth = Style.PathWidth;
            line.startColor = line.endColor = Style.Hover;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            paths.Add(go);
        }

        public void ClearVisuals()
        {
            hovered = null;
            InspectionText = null;
            foreach (var go in consequences) if (go != null) Destroy(go);
            consequences.Clear();
            foreach (var target in targets) if (target != null) Destroy(target.gameObject);
            targets.Clear();
            foreach (var go in paths) if (go != null) Destroy(go);
            paths.Clear();
            if (selection != null) Destroy(selection);
            selection = null;
            if (contextMenu != null) Destroy(contextMenu);
            contextMenu = null;
        }
        private void OnDisable() => ClearVisuals();
        private static string Arrow(SlideDirection d) => d == SlideDirection.Up ? "↑" : d == SlideDirection.Down ? "↓" : d == SlideDirection.Left ? "←" : "→";
        private static Vector3 DirectionVector(SlideDirection d) => d == SlideDirection.Up ? Vector3.left : d == SlideDirection.Down ? Vector3.right : d == SlideDirection.Left ? Vector3.back : Vector3.forward;
    }
}
