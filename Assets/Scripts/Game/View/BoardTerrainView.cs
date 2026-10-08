using System.Collections.Generic;
using UnityEngine;
using NodeWar.Config;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// The board's terrain as plain runtime geometry, so the map reads before any art exists:
    /// ocean and lake as flat tiles, a faint frame on the one empty pier slot, a highlight on
    /// every cell the piece in hand may legally take, and a bridge where a Pier stands.
    ///
    /// Every decision comes from <see cref="TerrainPresentation"/>, which asks
    /// <see cref="PlacementLegality"/>; this class only draws what it says. A legal cell is
    /// marked twice - a tint AND an outline - so the cue never depends on colour alone.
    /// Ocean and open lake get no collider and no node: nothing can be tapped there.
    ///
    /// Code-only on purpose. Real terrain art and prefab wiring replace the fallback shapes
    /// later; this view stays the owner of what is highlighted.
    /// </summary>
    public sealed class BoardTerrainView : MonoBehaviour
    {
        private static readonly Color OceanColor = new Color(0.05f, 0.16f, 0.30f, 1f);
        private static readonly Color LakeColor = new Color(0.16f, 0.46f, 0.66f, 1f);
        private static readonly Color LandColor = new Color(0.38f, 0.43f, 0.29f, 1f);
        private static readonly Color SlotFrameColor = new Color(0.85f, 0.95f, 1f, 0.55f);
        private static readonly Color LegalTint = new Color(0.30f, 0.90f, 0.45f, 0.38f);
        private static readonly Color LegalOutline = new Color(1f, 0.95f, 0.35f, 1f);
        private static readonly Color PlankColor = new Color(0.55f, 0.38f, 0.20f, 1f);

        private const float GroundY = -0.12f;
        private const float HighlightY = 0.04f;
        private const float OutlineWidth = 0.28f;
        private const float PlankWidth = 1.4f;
        private const float PlankHeight = 0.18f;

        private float nodeScale = 6f;
        private BoardConfigData board;
        private Material material;
        private Transform tiles;
        private Transform bridges;
        private GameObject[] highlightByCell;
        private bool[] bridgeByCell;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();

        /// <summary>Builds the terrain for a board and returns the view that owns it.</summary>
        public static BoardTerrainView Create(BoardConfig config)
        {
            var go = new GameObject("BoardTerrainView");
            BoardTerrainView view = go.AddComponent<BoardTerrainView>();
            view.Build(config.Data, config.nodeScale);
            return view;
        }

        /// <summary>A tappable bridge deck when no Pier art is assigned. Never uses land art.</summary>
        public static GameObject CreatePierNode(BoardTerrainView terrain, int x, int z, Transform parent)
        {
            var root = new GameObject("PierFallback");
            root.transform.SetParent(parent, false);
            int nodeLayer = LayerMask.NameToLayer("Nodes");
            if (nodeLayer >= 0) root.layer = nodeLayer;
            // Align the deck with its land neighbours; the hourglass crosses north/south.
            bool alongX = (x > 0 && terrain.board.terrain[z * terrain.board.gridCols + x - 1] == TerrainType.Land) ||
                (x + 1 < terrain.board.gridCols && terrain.board.terrain[z * terrain.board.gridCols + x + 1] == TerrainType.Land);
            float width = terrain.nodeScale * 0.28f;
            float length = terrain.nodeScale * 0.96f;
            Vector3 deckSize = alongX ? new Vector3(length, PlankHeight, width) : new Vector3(width, PlankHeight, length);
            terrain.MakePierPart(root.transform, "Deck", new Vector3(0f, 0.22f, 0f), deckSize);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 offset = alongX ? new Vector3(0f, 0.6f, side * width * 0.45f)
                    : new Vector3(side * width * 0.45f, 0.6f, 0f);
                Vector3 railSize = alongX ? new Vector3(length, 0.12f, 0.12f) : new Vector3(0.12f, 0.12f, length);
                terrain.MakePierPart(root.transform, "Rail", offset, railSize);
            }
            BoxCollider target = root.AddComponent<BoxCollider>();
            target.center = new Vector3(0f, 0.25f, 0f);
            target.size = new Vector3(width, 0.5f, width);
            root.AddComponent<NodeView>();
            return root;
        }

        private void MakePierPart(Transform parent, string name, Vector3 offset, Vector3 size)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = offset;
            part.transform.localScale = size;
            RemoveCollider(part);
            Tint(part.GetComponent<Renderer>(), PlankColor);
        }

        public void Build(BoardConfigData data, float scale)
        {
            board = data;
            nodeScale = scale;
            material = new Material(Shader.Find("Sprites/Default"));
            tiles = NewChild("Tiles");
            bridges = NewChild("Bridges");

            int cells = board.gridCols * board.gridRows;
            highlightByCell = new GameObject[cells];
            bridgeByCell = new bool[cells];
            for (int cell = 0; cell < cells; cell++)
            {
                int x = cell % board.gridCols;
                int z = cell / board.gridCols;
                TerrainType terrain = board.terrain[cell];
                Color color = terrain == TerrainType.Ocean ? OceanColor : terrain == TerrainType.Lake ? LakeColor : LandColor;
                MakeQuad(terrain + "_" + x + "_" + z, tiles, Center(x, z, GroundY), nodeScale, color);

                // The empty pier slot keeps a faint frame, so the way across is visible
                // before anyone drafts a bridge.
                if (terrain == TerrainType.Lake && board.districtSlots[cell])
                    MakeOutline("PierSlot_" + x + "_" + z, tiles, Center(x, z, GroundY + 0.02f), SlotFrameColor, 0.12f);
            }
        }

        /// <summary>
        /// Shows what the draft looks like now: the placements so far (a Pier becomes a
        /// bridge), and, with a piece in hand, every cell it may legally take.
        /// </summary>
        public void ShowDraft(DraftPlacement[] placed, bool hasPick, DistrictType pick)
        {
            if (highlightByCell == null) return;

            CellDescriptor[] cells = TerrainPresentation.Describe(board, placed, hasPick, pick);
            for (int i = 0; i < cells.Length; i++)
                SetHighlight(cells[i]);
            bool changed = false;
            for (int i = 0; i < cells.Length; i++)
            {
                if (bridgeByCell[i] != cells[i].bridge) changed = true;
                bridgeByCell[i] = cells[i].bridge;
            }
            if (changed) RebuildBridges(cells);
        }

        /// <summary>The bridges for a built match: the same shapes, with no piece in hand.</summary>
        public void ShowMatch(DraftPlacement[] placed)
        {
            ShowDraft(placed, false, DistrictType.None);
        }

        private void SetHighlight(CellDescriptor cell)
        {
            GameObject existing = highlightByCell[cell.cell];
            if (!cell.legalForPick)
            {
                if (existing != null) existing.SetActive(false);
                return;
            }

            if (existing == null)
            {
                // Both cues on one object: a fill and a frame around it.
                var holder = new GameObject("Legal_" + cell.x + "_" + cell.z);
                holder.transform.SetParent(transform, false);
                MakeQuad("Tint", holder.transform, Center(cell.x, cell.z, HighlightY), nodeScale * 0.92f, LegalTint);
                MakeOutline("Outline", holder.transform, Center(cell.x, cell.z, HighlightY + 0.02f), LegalOutline, OutlineWidth);
                highlightByCell[cell.cell] = holder;
                existing = holder;
            }
            existing.SetActive(true);
        }

        private void RebuildBridges(CellDescriptor[] cells)
        {
            for (int i = bridges.childCount - 1; i >= 0; i--)
                DisposeObject(bridges.GetChild(i).gameObject);

            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].bridge) continue;
                Vector3 centre = Center(cells[i].x, cells[i].z, GroundY + PlankHeight * 0.5f + 0.1f);
                MakePlank("PierDeck_" + cells[i].x + "_" + cells[i].z, centre, PlankWidth * 1.6f, PlankWidth * 1.6f, 0f);

                // A walkway to each neighbouring node, left, right, down, up.
                TryPlank(cells, cells[i], -1, 0, centre);
                TryPlank(cells, cells[i], 1, 0, centre);
                TryPlank(cells, cells[i], 0, -1, centre);
                TryPlank(cells, cells[i], 0, 1, centre);
            }
        }

        private void TryPlank(CellDescriptor[] cells, CellDescriptor from, int dx, int dz, Vector3 centre)
        {
            int nx = from.x + dx;
            int nz = from.z + dz;
            if (nx < 0 || nx >= board.gridCols || nz < 0 || nz >= board.gridRows) return;
            if (!cells[nz * board.gridCols + nx].hasNodeTarget) return;

            Vector3 away = new Vector3(dx, 0f, dz) * nodeScale * 0.5f;
            float length = nodeScale;
            float width = PlankWidth;
            MakePlank("Walkway_" + from.x + "_" + from.z + "_" + nx + "_" + nz, centre + away,
                dx != 0 ? length : width, dz != 0 ? length : width, 0f);
        }

        // ===== SHAPES =====

        private Vector3 Center(int x, int z, float y)
        {
            return new Vector3(x * nodeScale, y, z * nodeScale);
        }

        private Transform NewChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private GameObject MakeQuad(string name, Transform parent, Vector3 position, float size, Color color)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            // Nothing on the water can be tapped or hit.
            RemoveCollider(quad);
            quad.transform.SetParent(parent, false);
            quad.transform.position = position;
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size, size, 1f);
            Tint(quad.GetComponent<Renderer>(), color);
            return quad;
        }

        private void MakePlank(string name, Vector3 position, float sizeX, float sizeZ, float yaw)
        {
            GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = name;
            RemoveCollider(plank);
            plank.transform.SetParent(bridges, false);
            plank.transform.position = position;
            plank.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            plank.transform.localScale = new Vector3(sizeX, PlankHeight, sizeZ);
            Tint(plank.GetComponent<Renderer>(), PlankColor);
        }

        private void MakeOutline(string name, Transform parent, Vector3 centre, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = width;
            line.numCapVertices = 0;
            line.sharedMaterial = material;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            float h = nodeScale * 0.46f;
            line.SetPosition(0, centre + new Vector3(-h, 0f, -h));
            line.SetPosition(1, centre + new Vector3(-h, 0f, h));
            line.SetPosition(2, centre + new Vector3(h, 0f, h));
            line.SetPosition(3, centre + new Vector3(h, 0f, -h));
        }

        private void Tint(Renderer renderer, Color color)
        {
            if (renderer == null) return;

            // The sprite shader multiplies by the vertex colour. A primitive mesh carries none,
            // so give it white explicitly rather than rely on what a missing stream reads as.
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                // An explicit copy rather than filter.mesh, which copies implicitly and
                // logs a leak error in edit mode. The copy is ours, disposed in OnDestroy.
                Mesh mesh = Instantiate(filter.sharedMesh);
                filter.sharedMesh = mesh;
                ownedMeshes.Add(mesh);
                var white = new Color32[mesh.vertexCount];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                mesh.colors32 = white;
            }

            renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ownedMeshes.Count; i++) DisposeObject(ownedMeshes[i]);
            if (material != null) DisposeObject(material);
        }

        private static void RemoveCollider(GameObject shape)
        {
            Collider collider = shape.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false;
            DisposeObject(collider);
        }

        private static void DisposeObject(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
