const fs = require("fs");
const pptxgen = require("pptxgenjs");

const BG = "1E1A16";
const CARD = "26211B";
const CARD_LINE = "3D342A";
const BRASS = "C9A227";
const CREAM = "F5EFE0";
const MUTED = "A89880";
const CAPTION = "BFAE97";
const RED = "D0534A";
const BLUE = "5B8DD9";
const ORANGE = "E07B39";
const FH = "Georgia";
const FB = "Calibri";

const dims = JSON.parse(fs.readFileSync("assets/dims.json", "utf8"));

const p = new pptxgen();
p.layout = "LAYOUT_WIDE"; // 13.333 x 7.5
p.title = "Driftline — Portfolio";

function header(s, kicker, title) {
  s.addText(kicker.toUpperCase(), { x: 0.7, y: 0.42, w: 9, h: 0.3, fontFace: FB, fontSize: 11, bold: true, color: BRASS, charSpacing: 3, margin: 0 });
  s.addText(title, { x: 0.68, y: 0.68, w: 11, h: 0.75, fontFace: FH, fontSize: 34, bold: true, color: CREAM, margin: 0 });
}

function footer(s, n) {
  s.addText("DRIFTLINE · PORTFOLIO", { x: 0.7, y: 7.12, w: 3, h: 0.3, fontFace: FB, fontSize: 9, color: MUTED, charSpacing: 2, margin: 0 });
  s.addText("0" + n + " / 09", { x: 11.9, y: 7.12, w: 0.95, h: 0.3, fontFace: FB, fontSize: 9, color: MUTED, align: "right", margin: 0 });
}

// Places an image at its native aspect ratio (contain, never stretched), with a thin frame.
function frameImage(s, path, x, y, maxW, maxH, opts = {}) {
  const d = dims[path];
  const ar = d.w / d.h;
  let w = maxW, h = w / ar;
  if (h > maxH) { h = maxH; w = h * ar; }
  const ix = opts.left ? x : x + (maxW - w) / 2;
  const iy = opts.top ? y : y + (maxH - h) / 2;
  s.addShape(p.shapes.RECTANGLE, { x: ix - 0.035, y: iy - 0.035, w: w + 0.07, h: h + 0.07, fill: { color: CARD_LINE } });
  s.addImage({ path, x: ix, y: iy, w, h });
  return { x: ix, y: iy, w, h };
}

function card(s, x, y, w, h) {
  s.addShape(p.shapes.ROUNDED_RECTANGLE, { x, y, w, h, fill: { color: CARD }, line: { color: CARD_LINE, width: 1 }, rectRadius: 0.07 });
}

// Straight horizontal/vertical connector; arrowEnd puts a triangle at (x2, y2).
function seg(s, x1, y1, x2, y2, arrowEnd = false, dash = null) {
  s.addShape(p.shapes.LINE, {
    x: Math.min(x1, x2), y: Math.min(y1, y2), w: Math.abs(x2 - x1), h: Math.abs(y2 - y1),
    flipH: x2 < x1, flipV: y2 < y1,
    line: { color: CAPTION, width: 1.5, endArrowType: arrowEnd ? "triangle" : "none", dashType: dash || "solid" },
  });
}

function flowNode(s, x, y, w, h, text, opts = {}) {
  s.addShape(p.shapes.ROUNDED_RECTANGLE, { x, y, w, h, fill: { color: opts.fill || CARD }, line: { color: opts.line || CARD_LINE, width: 1.25 }, rectRadius: 0.09 });
  s.addText(text, { x: x + 0.06, y, w: w - 0.12, h, fontFace: FB, fontSize: opts.size || 11.5, bold: true, color: opts.color || CREAM, align: "center", valign: "middle", lineSpacingMultiple: 1.05, margin: 0 });
}

function flowDiamond(s, x, y, w, h, text) {
  s.addShape(p.shapes.DIAMOND, { x, y, w, h, fill: { color: CARD }, line: { color: BRASS, width: 1.5 } });
  s.addText(text, { x: x + w * 0.16, y: y + h * 0.26, w: w * 0.68, h: h * 0.48, fontFace: FB, fontSize: 9.5, bold: true, color: CREAM, align: "center", valign: "middle", margin: 0 });
}

// Mini board built from tiles — the same visual language as the Capture & Surround diagram.
function miniBoard(s, x, y, cols, rows, cell, marks) {
  for (let r = 0; r < rows; r++)
    for (let c = 0; c < cols; c++)
      s.addShape(p.shapes.RECTANGLE, { x: x + c * cell, y: y + r * cell, w: cell, h: cell, fill: { color: "1B1712" }, line: { color: CARD_LINE, width: 1 } });
  for (const m of marks) {
    const cx = x + m.c * cell, cy = y + m.r * cell;
    if (m.t === "wall") s.addShape(p.shapes.RECTANGLE, { x: cx + 0.02, y: cy + 0.02, w: cell - 0.04, h: cell - 0.04, fill: { color: "5A4A33" }, line: { color: "8A7350", width: 1 } });
    if (m.t === "down") s.addShape(p.shapes.RECTANGLE, { x: cx + 0.02, y: cy + 0.02, w: cell - 0.04, h: cell - 0.04, fill: { color: "2E3D4A" }, line: { color: "4A6B80", width: 1 } });
    if (m.t === "portal") s.addShape(p.shapes.OVAL, { x: cx + 0.05, y: cy + 0.05, w: cell - 0.1, h: cell - 0.1, fill: { color: "1B1712" }, line: { color: ORANGE, width: 2 } });
    if (m.t === "red") s.addShape(p.shapes.OVAL, { x: cx + 0.05, y: cy + 0.05, w: cell - 0.1, h: cell - 0.1, fill: { color: RED } });
    if (m.t === "blue") s.addShape(p.shapes.OVAL, { x: cx + 0.05, y: cy + 0.05, w: cell - 0.1, h: cell - 0.1, fill: { color: BLUE } });
    if (m.t === "gold") s.addShape(p.shapes.RECTANGLE, { x: cx + 0.07, y: cy + 0.07, w: cell - 0.14, h: cell - 0.14, fill: { color: BRASS } });
  }
  return { cell };
}

const cc = (bx, by, cell, c, r) => [bx + c * cell + cell / 2, by + r * cell + cell / 2]; // cell center

// ============ SLIDE 1 — COVER ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  const img = frameImage(s, "assets/crop_board_en.png", 7.55, 0.55, 5.35, 6.1, { top: true });
  s.addText("Frontline Fortress board — live match, English UI", { x: 7.55, y: img.y + img.h + 0.12, w: 5.35, h: 0.28, fontFace: FB, fontSize: 9.5, color: MUTED, align: "center", margin: 0 });
  s.addShape(p.shapes.RECTANGLE, { x: 0.9, y: 1.55, w: 0.16, h: 0.16, fill: { color: BRASS } });
  s.addText("A CLOCKWORK TABLETOP TACTICS GAME", { x: 1.2, y: 1.47, w: 6, h: 0.3, fontFace: FB, fontSize: 11, bold: true, color: BRASS, charSpacing: 3, margin: 0 });
  s.addText("DRIFTLINE", { x: 0.9, y: 1.79, w: 6.6, h: 1.25, fontFace: FH, fontSize: 64, bold: true, color: CREAM, margin: 0 });
  s.addText("Roll. Slide. Surround. Outlast.", { x: 0.9, y: 3.35, w: 6.2, h: 0.45, fontFace: FH, fontSize: 17, italic: true, color: BRASS, margin: 0 });
  s.addText("Four expedition teams enter an ancient clockwork board. Dice decide how far you drift — walls decide when you stop. Connect five in a row, or be the last team standing.",
    { x: 0.9, y: 3.95, w: 5.9, h: 1.3, fontFace: FB, fontSize: 13, color: MUTED, lineSpacingMultiple: 1.3, margin: 0 });
}

// ============ SLIDE 2 — INSPIRATION & WORLD ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  header(s, "Why this game", "Inspiration & World");
  footer(s, 2);
  s.addText("Three games I grew up on", { x: 0.7, y: 1.6, w: 5.9, h: 0.36, fontFace: FB, fontSize: 15, bold: true, color: CREAM, margin: 0 });
  s.addText("I've played Go for years. Driftline fuses it with the other two boards on our family table: Go's shared-breath captures, Gomoku's five-in-a-row finish, and the dice, deployment and bumping of Aeroplane Chess.",
    { x: 0.7, y: 1.98, w: 5.85, h: 1.1, fontFace: FB, fontSize: 12, color: MUTED, lineSpacingMultiple: 1.25, margin: 0 });
  s.addText("Built in Minecraft first", { x: 0.7, y: 3.22, w: 5.9, h: 0.36, fontFace: FB, fontSize: 15, bold: true, color: CREAM, margin: 0 });
  s.addText("I love Minecraft, so before a single line of code I built the first board block by block — the four colored corners, the wall tiles and the portals were all there from day one. Every rule since is an iteration of that wooden prototype.",
    { x: 0.7, y: 3.6, w: 5.85, h: 1.25, fontFace: FB, fontSize: 12, color: MUTED, lineSpacingMultiple: 1.25, margin: 0 });
  card(s, 0.7, 5.2, 5.87, 1.5);
  s.addText("THE WORLD", { x: 0.95, y: 5.38, w: 3, h: 0.28, fontFace: FB, fontSize: 10.5, bold: true, color: BRASS, charSpacing: 2.5, margin: 0 });
  s.addText("The four corner colors survived the move to Unity — they became the four expeditions racing for the Realm Key.",
    { x: 0.95, y: 5.68, w: 5.35, h: 0.62, fontFace: FB, fontSize: 11.5, color: CREAM, lineSpacingMultiple: 1.15, margin: 0 });
  const mc = frameImage(s, "assets/mc_prototype.png", 7.0, 1.7, 5.6, 4.6, { top: true });
  s.addText("The first board — built in Minecraft before a single line of code", { x: 7.0, y: mc.y + mc.h + 0.08, w: 5.6, h: 0.26, fontFace: FB, fontSize: 9.5, color: MUTED, align: "center", margin: 0 });
}

// ============ SLIDE 3 — FROM BLOCKS TO BOARD ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  header(s, "Prototype → Unity", "From Blocks to Board");
  footer(s, 3);
  const stages = [
    ["01", "The Minecraft board", "Wood, wool and colored clay — corners, walls and portals already in place.", "assets/mc_prototype.png"],
    ["02", "First Unity build", "A grey-box grid with real rules — every core mechanic already playable.", "assets/early_board.png"],
    ["03", "Today", "Five arenas, LAN multiplayer, and an AI that actually plans.", "assets/today_board.png"],
  ];
  stages.forEach(([num, name, desc, img], i) => {
    const x = 0.7 + i * 4.05;
    s.addText(num, { x, y: 1.58, w: 0.6, h: 0.4, fontFace: FH, fontSize: 20, bold: true, color: BRASS, margin: 0 });
    s.addText(name, { x: x + 0.62, y: 1.64, w: 3.3, h: 0.34, fontFace: FB, fontSize: 14, bold: true, color: CREAM, margin: 0 });
    frameImage(s, img, x, 2.14, 3.9, 2.3, { top: true });
    s.addText(desc, { x, y: 4.62, w: 3.9, h: 0.66, fontFace: FB, fontSize: 10.5, color: MUTED, lineSpacingMultiple: 1.15, margin: 0 });
  });
  card(s, 0.7, 5.62, 11.93, 1.28);
  s.addText("Same four corners, same walls, same portals — the wooden sketch and the shipped game still agree with each other.",
    { x: 0.95, y: 5.9, w: 11.4, h: 0.66, fontFace: FB, fontSize: 12.5, italic: true, color: CREAM, lineSpacingMultiple: 1.2, margin: 0 });
}

// ============ SLIDE 4 — CORE LOOP (flowchart) ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  header(s, "How a turn works", "Core Loop");
  footer(s, 4);
  // Row A (left → right): roll → deploy? → choose → item? → route → commit
  const NA = 1.62, NB = 3.75, NW = 1.6, NH = 0.75;
  flowNode(s, 0.7, NA, NW, NH, "Roll the die", { line: BRASS });
  seg(s, 2.3, 2.0, 2.55, 2.0, true);
  flowDiamond(s, 2.55, 1.35, NW, 1.3, "Bench piece to deploy?");
  s.addText("no", { x: 4.02, y: 1.68, w: 0.4, h: 0.22, fontFace: FB, fontSize: 9, italic: true, color: MUTED, margin: 0 });
  seg(s, 4.15, 2.0, 4.4, 2.0, true);
  flowNode(s, 4.4, NA, NW, NH, "Choose a piece");
  seg(s, 6.0, 2.0, 6.25, 2.0, true);
  flowDiamond(s, 6.25, 1.35, NW, 1.3, "Spend an item?");
  s.addText("no", { x: 7.72, y: 1.68, w: 0.4, h: 0.22, fontFace: FB, fontSize: 9, italic: true, color: MUTED, margin: 0 });
  seg(s, 7.85, 2.0, 8.1, 2.0, true);
  flowNode(s, 8.1, NA, NW, NH, "Pick a route\n& a stop", { size: 10.5 });
  seg(s, 9.7, 2.0, 9.95, 2.0, true);
  flowNode(s, 9.95, NA, NW, NH, "Commit — red circles preview every capture", { size: 9.5 });
  seg(s, 10.75, 2.37, 10.75, 3.75, true);
  // deploy branch (row A under-lane)
  seg(s, 3.35, 2.65, 3.35, 2.95, true);
  s.addText("yes", { x: 3.44, y: 2.68, w: 0.4, h: 0.22, fontFace: FB, fontSize: 9, italic: true, color: MUTED, margin: 0 });
  flowNode(s, 2.55, 2.95, NW, 0.6, "Deploy at spawn — free, once before sliding", { size: 9 });
  seg(s, 4.15, 3.25, 5.2, 3.25, false);
  seg(s, 5.2, 3.25, 5.2, 2.37, true);
  // item branch
  seg(s, 7.05, 2.65, 7.05, 2.95, true);
  s.addText("yes", { x: 7.14, y: 2.68, w: 0.4, h: 0.22, fontFace: FB, fontSize: 9, italic: true, color: MUTED, margin: 0 });
  flowNode(s, 6.25, 2.95, NW, 0.6, "Brake · Phase\nRam · Breach", { size: 9.5 });
  seg(s, 7.85, 3.25, 8.9, 3.25, false);
  // Row B (right → left): drift → slides left? → settle → win check → won
  flowNode(s, 9.95, NB, NW, NH, "The piece drifts — walls stop it, portals jump, rams bite", { size: 9.5 });
  seg(s, 9.95, 4.12, 9.7, 4.12, true);
  flowDiamond(s, 8.1, 3.48, NW, 1.3, "Slides left?");
  seg(s, 8.9, 3.48, 8.9, 2.37, true);
  s.addText("yes — same piece keeps going", { x: 9.0, y: 2.82, w: 1.75, h: 0.4, fontFace: FB, fontSize: 8.5, italic: true, color: MUTED, lineSpacingMultiple: 1.1, margin: 0 });
  s.addText("no", { x: 7.92, y: 3.84, w: 0.4, h: 0.22, fontFace: FB, fontSize: 9, italic: true, color: MUTED, margin: 0 });
  seg(s, 8.1, 4.12, 7.85, 4.12, true);
  flowNode(s, 6.25, NB, NW, NH, "Turn settles — surrounds, walls cycle, pickups top up", { size: 9.5 });
  seg(s, 6.25, 4.12, 6.0, 4.12, true);
  flowDiamond(s, 4.4, 3.48, NW, 1.3, "Five in a row? One team left?");
  s.addText("yes", { x: 3.95, y: 3.84, w: 0.4, h: 0.22, fontFace: FB, fontSize: 9, italic: true, color: MUTED, margin: 0 });
  seg(s, 4.4, 4.12, 4.15, 4.12, true);
  flowNode(s, 2.55, NB, NW, NH, "Match won", { fill: BRASS, color: BG });
  // loop back: no -> next turn
  seg(s, 5.2, 4.78, 5.2, 5.1, false);
  seg(s, 5.2, 5.1, 1.5, 5.1, false);
  seg(s, 1.5, 5.1, 1.5, 2.37, true);
  s.addText("no — next turn", { x: 1.68, y: 5.12, w: 1.4, h: 0.24, fontFace: FB, fontSize: 9, italic: true, color: MUTED, margin: 0 });
  // items band
  card(s, 0.7, 5.56, 11.93, 1.44);
  s.addText("ITEMS — SPAWN ON THE BOARD AT WEIGHTED RANDOM", { x: 0.95, y: 5.7, w: 8, h: 0.26, fontFace: FB, fontSize: 10, bold: true, color: BRASS, charSpacing: 2, margin: 0 });
  const items = [
    ["PASS WALL", "Slide straight through one wall."],
    ["EMERGENCY STOP", "Brake on golden stop tiles."],
    ["COLLISION CAPTURE", "Ram a piece — instant removal."],
    ["BREAK WALL", "Tear down a wall, open a lane."],
  ];
  items.forEach(([name, desc], i) => {
    const x = 0.95 + i * 2.92;
    s.addText(name, { x, y: 6.0, w: 2.8, h: 0.3, fontFace: FB, fontSize: 12, bold: true, color: CREAM, margin: 0 });
    s.addText(desc, { x, y: 6.3, w: 2.75, h: 0.5, fontFace: FB, fontSize: 10, color: MUTED, lineSpacingMultiple: 1.1, margin: 0, wrap: false });
  });
}

// ============ SLIDE 5 — A LIVING BOARD (full-board schematic) ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  header(s, "The board fights back", "A Living Board");
  footer(s, 5);

  // The real 19×19 rule map, transcribed straight from BoardLayout.cs.
  const RAW = [
    ".....#########.....",
    ".S##..#######..##S.",
    ".####..#####..####.",
    ".##.GG.#####.GG.##.",
    "..#G##.O...C.##G#..",
    "#..G#####.#####G..#",
    "##...#w#####x#...##",
    "####M#####b###M####",
    "####.##a######.####",
    "####..###.###..####",
    "####.######c##.####",
    "####R###d#####R####",
    "##...#z#####y#...##",
    "#..G#####.#####G..#",
    "..#G##.O...C.##G#..",
    ".##.GG.#####.GG.##.",
    ".####..#####..####.",
    ".S##..#######..##S.",
    ".....#########.....",
  ];
  const FULLC = { Red: RED, Blue: BLUE, Green: "5FB96F", Yellow: "D9B84A" };
  const DIMC = { Red: "7A332E", Blue: "33517A", Green: "33704A", Yellow: "8A7433" };
  const ownerOf = (c, r) => c < 9 && r < 9 ? "Red" : c > 9 && r < 9 ? "Blue" : c > 9 && r > 9 ? "Green" : "Yellow";
  const DOWN = "2E3D4A", DOWN_LINE = "4A6B80";

  function realBoard(bx, by, cell, phase, pickups) {
    for (let r = 0; r < 19; r++) for (let c = 0; c < 19; c++) {
      const ch = RAW[r][c];
      const x = bx + c * cell, y = by + r * cell;
      let fill = ch === "." ? "171310" : "6E6353";
      if (ch === "S") fill = FULLC[ownerOf(c, r)];
      else if (ch === "G") fill = DIMC[ownerOf(c, r)];
      s.addShape(p.shapes.RECTANGLE, { x, y, w: cell, h: cell, fill: { color: fill }, line: { color: "0E0B09", width: 0.25 } });
      if ("OCMR".includes(ch))
        s.addShape(p.shapes.OVAL, { x: x + cell * 0.16, y: y + cell * 0.16, w: cell * 0.68, h: cell * 0.68, fill: { color: "6E6353" }, line: { color: ORANGE, width: 1 } });
      if ("abcdwxyz".includes(ch)) {
        const inner = ch <= "d";
        const idx = inner ? ch.charCodeAt(0) - 97 : ch.charCodeAt(0) - 119;
        const up = inner ? idx % 2 === phase : idx % 2 !== phase;
        s.addShape(p.shapes.RECTANGLE, { x: x + 0.012, y: y + 0.012, w: cell - 0.024, h: cell - 0.024, fill: { color: up ? BRASS : DOWN }, line: { color: up ? "E8C94A" : DOWN_LINE, width: 0.75 } });
      }
    }
    for (const [c, r] of pickups)
      s.addShape(p.shapes.DIAMOND, { x: bx + c * cell + cell * 0.22, y: by + r * cell + cell * 0.22, w: cell * 0.56, h: cell * 0.56, fill: { color: "F5EFE0" }, line: { color: "E8C94A", width: 1 } });
  }

  card(s, 0.7, 1.5, 8.55, 5.4);
  const cell = 0.155, by = 2.02, boardW = 19 * cell;
  s.addText("TURN N — one diagonal ring stands", { x: 1.05, y: 1.7, w: 3.4, h: 0.26, fontFace: FB, fontSize: 10.5, bold: true, color: BRASS, margin: 0, wrap: false });
  s.addText("TURN N+1 — the other ring stands", { x: 5.0, y: 1.7, w: 3.4, h: 0.26, fontFace: FB, fontSize: 10.5, bold: true, color: BRASS, margin: 0, wrap: false });
  realBoard(1.05, by, cell, 0, [[10, 7], [8, 11]]);
  realBoard(5.0, by, cell, 1, [[7, 8]]);
  seg(s, 4.12, by + boardW / 2, 4.88, by + boardW / 2, true);
  s.addText("the turn\npasses", { x: 4.02, y: by + boardW / 2 + 0.08, w: 1.0, h: 0.45, fontFace: FB, fontSize: 8.5, italic: true, color: MUTED, align: "center", lineSpacingMultiple: 1.1, margin: 0 });
  // legend
  const legend = [
    ["square", BRASS, "wall stands"],
    ["square", DOWN, "slot lowered"],
    ["diamond", "E8C94A", "pickup"],
    ["ring", ORANGE, "portal — 4 pairs"],
    ["square", RED, "spawn corner"],
  ];
  legend.forEach(([shape, color, label], i) => {
    const x = 1.05 + i * 1.58;
    if (shape === "square") s.addShape(p.shapes.RECTANGLE, { x, y: 5.28, w: 0.13, h: 0.13, fill: { color } });
    else if (shape === "diamond") s.addShape(p.shapes.DIAMOND, { x, y: 5.27, w: 0.15, h: 0.15, fill: { color: "F5EFE0" }, line: { color: "E8C94A", width: 1 } });
    else s.addShape(p.shapes.OVAL, { x, y: 5.28, w: 0.13, h: 0.13, fill: { color: "6E6353" }, line: { color, width: 1.25 } });
    s.addText(label, { x: x + 0.2, y: 5.19, w: 1.45, h: 0.26, fontFace: FB, fontSize: 8.5, color: MUTED, margin: 0, wrap: false });
  });
  s.addText("A rising wall blocks slides and shares no liberties — the toggle itself settles captures, and whatever it covers is buried.",
    { x: 1.05, y: 5.68, w: 7.6, h: 0.5, fontFace: FB, fontSize: 10.5, italic: true, color: CAPTION, lineSpacingMultiple: 1.15, margin: 0 });
  // right column — the logic, in plain words
  card(s, 9.45, 1.5, 3.18, 1.9);
  s.addText("The wall cycle", { x: 9.68, y: 1.66, w: 2.8, h: 0.3, fontFace: FB, fontSize: 13.5, bold: true, color: CREAM, margin: 0 });
  s.addText("Eight slots form two diagonal rings. Every time the turn passes they trade: one pair rises, the other drops. The toggle alone can capture a group.",
    { x: 9.68, y: 2.0, w: 2.75, h: 1.3, fontFace: FB, fontSize: 10.5, color: MUTED, lineSpacingMultiple: 1.18, margin: 0 });
  card(s, 9.45, 3.56, 3.18, 2.05);
  s.addText("Pickups live in the walls", { x: 9.68, y: 3.72, w: 2.8, h: 0.3, fontFace: FB, fontSize: 13.5, bold: true, color: CREAM, margin: 0 });
  s.addText("Pickups sprout only on lowered slots, and a rising wall buries them. Two stay visible at most, topped up as turns pass. Phase, Brake and Breach are common — Ram is one pull in ten.",
    { x: 9.68, y: 4.06, w: 2.75, h: 1.45, fontFace: FB, fontSize: 10.5, color: MUTED, lineSpacingMultiple: 1.18, margin: 0 });
  card(s, 9.45, 5.77, 3.18, 1.13);
  s.addText("Four portal pairs", { x: 9.68, y: 5.9, w: 2.8, h: 0.3, fontFace: FB, fontSize: 13.5, bold: true, color: CREAM, margin: 0 });
  s.addText("Two run vertical, two lateral. You land facing the center; a blocked exit cancels the jump.",
    { x: 9.68, y: 6.22, w: 2.75, h: 0.62, fontFace: FB, fontSize: 10.5, color: MUTED, lineSpacingMultiple: 1.18, margin: 0 });
}

// ============ SLIDE 6 — FIVE BATTLEFIELDS ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  header(s, "One ruleset, five arenas", "Five Battlefields");
  footer(s, 6);
  const themes = [
    ["FRONTLINE FORTRESS", "Sandbags, watchtowers,\ndiesel trucks", "assets/theme_fortress.png"],
    ["RUNE DUNGEON", "Crystal light in\nsunken halls", "assets/theme_dungeon.png"],
    ["STARSHIP HANGAR", "Deck plates and\nengine glow", "assets/theme_hangar.png"],
    ["TWILIGHT GROVE", "Altars under dark\ncanopies", "assets/theme_forest.png"],
    ["TIDAL HARBOR", "Docks, cargo and\na moored ship", "assets/theme_harbor.png"],
  ];
  themes.forEach(([name, mood, img], i) => {
    const x = 0.7 + i * 2.43;
    s.addText(name, { x, y: 1.5, w: 2.3, h: 0.3, fontFace: FB, fontSize: 11.5, bold: true, color: BRASS, align: "center", margin: 0, wrap: false });
    frameImage(s, img, x, 1.92, 2.3, 2.2, { top: true });
    s.addText(mood, { x, y: 4.2, w: 2.3, h: 0.55, fontFace: FB, fontSize: 10, color: MUTED, align: "center", lineSpacingMultiple: 1.15, margin: 0 });
  });
  card(s, 0.7, 5.12, 11.93, 1.32);
  s.addText("Board, walls, props, backdrop and the light itself all swap between arenas — two environment moods apiece, and the host's choice follows everyone into the match.",
    { x: 0.95, y: 5.36, w: 11.4, h: 0.62, fontFace: FB, fontSize: 12, color: CREAM, lineSpacingMultiple: 1.2, margin: 0 });
  s.addText("Arena art: KayKit · polyperfect · Off Axis Studios", { x: 0.95, y: 6.04, w: 11.4, h: 0.26, fontFace: FB, fontSize: 9.5, color: MUTED, margin: 0 });
}

// ============ SLIDE 7 — CAPTURE & SURROUND ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  header(s, "Two ways to kill", "Capture & Surround");
  footer(s, 7);
  card(s, 0.7, 1.5, 5.92, 3.6);
  s.addText("Collision capture", { x: 0.95, y: 1.72, w: 5.4, h: 0.34, fontFace: FB, fontSize: 15, bold: true, color: CREAM, margin: 0 });
  s.addText("Slide into an enemy with movement to spare and it is removed on impact — no contest, no counter-play. The attacker ends its move on the victim's tile.",
    { x: 0.95, y: 2.1, w: 5.4, h: 0.9, fontFace: FB, fontSize: 11.5, color: MUTED, lineSpacingMultiple: 1.15, margin: 0 });
  const dy = 3.3;
  s.addShape(p.shapes.OVAL, { x: 1.15, y: dy, w: 0.52, h: 0.52, fill: { color: RED } });
  s.addShape(p.shapes.LINE, { x: 1.85, y: dy + 0.26, w: 1.15, h: 0, line: { color: CREAM, width: 2, endArrowType: "triangle" } });
  s.addShape(p.shapes.OVAL, { x: 3.15, y: dy, w: 0.52, h: 0.52, fill: { color: BLUE } });
  s.addShape(p.shapes.LINE, { x: 3.19, y: dy + 0.04, w: 0.44, h: 0.44, line: { color: RED, width: 3 } });
  s.addShape(p.shapes.LINE, { x: 3.19, y: dy + 0.04, w: 0.44, h: 0.44, flipH: true, line: { color: RED, width: 3 } });
  s.addText("removed on impact", { x: 3.9, y: dy + 0.08, w: 2.2, h: 0.34, fontFace: FB, fontSize: 10.5, italic: true, color: MUTED, margin: 0 });
  s.addText("Every removal: −1 HP for that team.", { x: 1.15, y: dy + 0.72, w: 4.5, h: 0.3, fontFace: FB, fontSize: 10.5, italic: true, color: CAPTION, margin: 0 });
  card(s, 6.72, 1.5, 5.92, 3.6);
  s.addText("Surround — Go-style liberties", { x: 6.97, y: 1.72, w: 5.4, h: 0.34, fontFace: FB, fontSize: 15, bold: true, color: CREAM, margin: 0 });
  s.addText("A connected block shares liberties from open tiles, spawn gates and lowered wall slots; walls and portals give none. Zero liberties means capture — and your own moves can never self-capture.",
    { x: 6.97, y: 2.1, w: 5.45, h: 1.0, fontFace: FB, fontSize: 11.5, color: MUTED, lineSpacingMultiple: 1.15, margin: 0 });
  const gx = 7.1, gy = 3.3, gcell = 0.4;
  for (let r = 0; r < 4; r++)
    for (let c = 0; c < 4; c++)
      s.addShape(p.shapes.RECTANGLE, { x: gx + c * gcell, y: gy + r * gcell, w: gcell, h: gcell, fill: { color: "1B1712" }, line: { color: CARD_LINE, width: 1 } });
  s.addShape(p.shapes.RECTANGLE, { x: gx + gcell, y: gy + 2 * gcell, w: gcell, h: gcell, fill: { color: "5A4A33" }, line: { color: CARD_LINE, width: 1 } });
  const piece = (c, r, col) => s.addShape(p.shapes.OVAL, { x: gx + c * gcell + 0.055, y: gy + r * gcell + 0.055, w: gcell - 0.11, h: gcell - 0.11, fill: { color: col } });
  piece(1, 1, BLUE);
  piece(1, 0, RED);
  piece(0, 1, RED);
  piece(2, 1, RED);
  s.addText("0 liberties → the whole block is captured", { x: 9.1, y: 3.42, w: 3.3, h: 0.6, fontFace: FB, fontSize: 10.5, italic: true, color: MUTED, lineSpacingMultiple: 1.15, margin: 0 });
  s.addText("shaded tile — mechanism wall", { x: 9.1, y: 4.0, w: 3.3, h: 0.3, fontFace: FB, fontSize: 10.5, italic: true, color: MUTED, margin: 0 });
  s.addText("Every cause — ram, surround, wall change — is spelled out in the action preview.", { x: 9.1, y: 4.42, w: 3.3, h: 0.6, fontFace: FB, fontSize: 10.5, italic: true, color: CAPTION, lineSpacingMultiple: 1.15, margin: 0 });
  card(s, 0.7, 5.42, 5.92, 1.5);
  s.addText("WIN — FIVE IN A ROW", { x: 0.95, y: 5.6, w: 5.4, h: 0.3, fontFace: FB, fontSize: 12.5, bold: true, color: BRASS, charSpacing: 1.5, margin: 0 });
  s.addText("Connect five pieces in any direction — horizontal, vertical or diagonal — for an instant victory.",
    { x: 0.95, y: 5.95, w: 5.45, h: 0.72, fontFace: FB, fontSize: 11.5, color: MUTED, lineSpacingMultiple: 1.15, margin: 0 });
  card(s, 6.72, 5.42, 5.92, 1.5);
  s.addText("WIN — LAST TEAM STANDING", { x: 6.97, y: 5.6, w: 5.4, h: 0.3, fontFace: FB, fontSize: 12.5, bold: true, color: BRASS, charSpacing: 1.5, margin: 0 });
  s.addText("Every team has 5 HP and loses one per captured piece. Bench pieces redeploy — outlive everyone.",
    { x: 6.97, y: 5.95, w: 5.45, h: 0.72, fontFace: FB, fontSize: 11.5, color: MUTED, lineSpacingMultiple: 1.15, margin: 0 });
}

// ============ SLIDE 8 — MULTIPLAYER & AI ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  header(s, "Play together — or against the machine", "Multiplayer & AI");
  footer(s, 8);
  const bullet = { code: "25A0", color: BRASS };
  card(s, 0.7, 1.5, 5.92, 3.42);
  s.addText("LAN — one host calls the shots", { x: 0.95, y: 1.68, w: 5.4, h: 0.34, fontFace: FB, fontSize: 15, bold: true, color: CREAM, margin: 0 });
  s.addText([
    { text: "The host simulates; everyone else just renders — no conflicts to argue about", options: { bullet, breakLine: true } },
    { text: "Dropped? Rejoin within 60 seconds — an AI pilots your seat meanwhile", options: { bullet, breakLine: true } },
    { text: "Theme, dice and every move stay in lockstep for all four seats", options: { bullet } },
  ], { x: 0.95, y: 2.14, w: 5.45, h: 2.7, fontFace: FB, fontSize: 12, color: CREAM, lineSpacingMultiple: 1.12, paraSpaceAfter: 9, margin: 0 });
  card(s, 6.72, 1.5, 5.92, 3.42);
  s.addText("AI — it plans, but never peeks", { x: 6.97, y: 1.68, w: 5.4, h: 0.34, fontFace: FB, fontSize: 15, bold: true, color: CREAM, margin: 0 });
  s.addText([
    { text: "Beam search over isolated copies of the rules — no Unity, no future knowledge", options: { bullet, breakLine: true } },
    { text: "Normal thinks for about a quarter of a second a turn; Easy plays loose and fast", options: { bullet, breakLine: true } },
    { text: "Sits in any empty chair, at home or online", options: { bullet } },
  ], { x: 6.97, y: 2.14, w: 5.45, h: 2.4, fontFace: FB, fontSize: 12, color: CREAM, lineSpacingMultiple: 1.12, paraSpaceAfter: 9, margin: 0 });
  const lan = frameImage(s, "assets/lan_full.png", 2.2, 5.24, 2.81, 1.58, { left: true, top: true });
  s.addText("LAN lobby — share an IP and play", { x: 2.2, y: lan.y + lan.h + 0.03, w: 3.2, h: 0.22, fontFace: FB, fontSize: 9, color: MUTED, margin: 0, wrap: false });
  const setup = frameImage(s, "assets/local_setup.png", 5.5, 5.24, 2.81, 1.58, { left: true, top: true });
  s.addText("Local setup — Player · Computer · Off per seat", { x: 5.5, y: setup.y + setup.h + 0.03, w: 3.4, h: 0.22, fontFace: FB, fontSize: 9, color: MUTED, margin: 0, wrap: false });
  s.addText("Humans, AI and remote clients all play through the same validated command route — one rules core judges every move.",
    { x: 9.2, y: 5.42, w: 3.4, h: 1.4, fontFace: FB, fontSize: 10.5, italic: true, color: CAPTION, lineSpacingMultiple: 1.2, margin: 0 });
}

// ============ SLIDE 9 — CODE ============
{
  const s = p.addSlide();
  s.background = { color: BG };
  header(s, "Under the hood", "Code");
  footer(s, 9);
  const hero = frameImage(s, "assets/code_rules.png", 0.7, 1.42, 7.9, 4.55, { left: true, top: true });
  s.addText("MatchRules.cs · FindDeadBlocks() — blocks with zero liberties are captured.",
    { x: 0.7, y: hero.y + hero.h + 0.12, w: 7.8, h: 0.4, fontFace: FB, fontSize: 10.5, color: MUTED, lineSpacingMultiple: 1.15, margin: 0 });
  const ai = frameImage(s, "assets/code_ai.png", 8.95, 1.42, 3.85, 1.15, { left: true, top: true });
  s.addText("ComputerPlanner.cs — the beam search, on isolated copies.",
    { x: 8.95, y: ai.y + ai.h + 0.09, w: 3.85, h: 0.62, fontFace: FB, fontSize: 10, color: CREAM, lineSpacingMultiple: 1.12, margin: 0 });
  const lan = frameImage(s, "assets/code_lan.png", 8.95, 3.5, 3.85, 1.04, { left: true, top: true });
  s.addText("DraftlineLanRoom.cs — sequenced snapshots, version-guarded.",
    { x: 8.95, y: lan.y + lan.h + 0.09, w: 3.85, h: 0.62, fontFace: FB, fontSize: 10, color: CREAM, lineSpacingMultiple: 1.12, margin: 0 });
  s.addText("Every rule lives in a plain C# core with no Unity inside — a click, an AI rollout and a network packet all face the same judge.",
    { x: 8.95, y: 5.5, w: 3.85, h: 1.3, fontFace: FB, fontSize: 11, italic: true, color: CAPTION, lineSpacingMultiple: 1.25, margin: 0 });
}

p.writeFile({ fileName: "Driftline-Portfolio.pptx" }).then(() => console.log("written: Driftline-Portfolio.pptx"));
