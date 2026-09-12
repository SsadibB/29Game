using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game29;

namespace Game29.Editor
{
    /// <summary>
    /// Editor utility that builds the complete 29 Game scene hierarchy.
    ///
    /// Usage:  Menu ▶  "29 Game / Setup Scene"   (or press Alt+Shift+G)
    ///
    /// What it creates:
    ///
    ///   [29Game Root]
    ///   ├── [GameManager]          ← GameManager + GameBootstrap + DebugGameDisplay
    ///   └── [Camera]               ← Orthographic top-down camera (if none exists)
    ///
    ///   [GameTable]
    ///   ├── [TrickArea]            ← Center of table — cards are played here
    ///   ├── [BiddingArea]          ← Bidding display anchor
    ///   └── [ScoreArea]            ← Score display anchor
    ///
    ///   [Players]
    ///   ├── [Player_South]         ← Human player (bottom, z=0)
    ///   ├── [Player_North]         ← AI partner   (top)
    ///   ├── [Player_East]          ← AI opponent  (right)
    ///   └── [Player_West]          ← AI opponent  (left)
    ///
    /// All positions are laid out for a 2D top-down card table.
    /// Distances are in Unity units and are designed to work with
    /// an Orthographic camera size of 6.
    /// </summary>
    public static class GameSetupEditor
    {
        // Player seat world positions (top-down 2D layout)
        private static readonly Vector3 PosSouth  = new Vector3( 0f, -4.5f, 0f);
        private static readonly Vector3 PosNorth  = new Vector3( 0f,  4.5f, 0f);
        private static readonly Vector3 PosEast   = new Vector3( 7f,  0f,   0f);
        private static readonly Vector3 PosWest   = new Vector3(-7f,  0f,   0f);
        private static readonly Vector3 PosCenter = Vector3.zero;
        private static readonly Vector3 PosBid    = new Vector3( 0f,  1.5f, 0f);
        private static readonly Vector3 PosScore  = new Vector3( 0f,  2.8f, 0f);

        // ── Menu Items ────────────────────────────────────────────────────────
        [MenuItem("29 Game/Reimport Card Assets as Sprites")]
        public static void EnsureResourcesSprites()
        {
            string[] texturePaths = {
                "Assets/Resources/TableFelt.png",
                "Assets/Resources/CardFront.png",
                "Assets/Resources/CardBack.png",
                "Assets/Resources/SuitIcons.png"
            };

            foreach (string path in texturePaths)
            {
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null)
                {
                    bool dirty = false;
                    if (ti.textureType != TextureImporterType.Sprite)
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        dirty = true;
                    }
                    if (dirty)
                    {
                        EditorUtility.SetDirty(ti);
                        ti.SaveAndReimport();
                        Debug.Log($"[29 Setup] Reimported {path} as Sprite.");
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        [MenuItem("29 Game/Setup Trump Panels")]
        public static void SetupTrumpPanels()
        {
            GameObject canvasGO = GameObject.Find("[Canvas_GameTable]");
            if (canvasGO == null)
            {
                GameObject root = GameObject.Find("[29Game Root]");
                if (root != null)
                {
                    Transform t = root.transform.Find("[Canvas_GameTable]");
                    if (t != null) canvasGO = t.gameObject;
                }
            }

            if (canvasGO != null)
            {
                var tableUI = canvasGO.GetComponent<GameTableUI>();
                if (tableUI != null)
                {
                    tableUI.BuildUIIfMissing();
                    EditorUtility.SetDirty(tableUI);
                    EditorUtility.SetDirty(canvasGO);
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                    Debug.Log("[29 Setup] Trump Selection Modal and Trump Card Slot created & scene saved!");
                }
            }
        }

        [MenuItem("29 Game/Setup Scene %#g")]   // Ctrl+Shift+G
        public static void SetupScene()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Setup 29 Game Scene",
                "This will set up the complete 29 card game with visual cards, table felt, HUD and player layout.\n\nProceed?",
                "Setup", "Cancel");

            if (!proceed) return;

            Undo.SetCurrentGroupName("Setup 29 Game Scene");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureResourcesSprites();

            // ── Root containers ────────────────────────────────────────────
            GameObject root      = GetOrCreate("[29Game Root]",  null,            Vector3.zero);
            GameObject gmGO      = GetOrCreate("[GameManager]",  root.transform,  Vector3.zero);
            GameObject tablePar  = GetOrCreate("[GameTable]",    null,            Vector3.zero);
            GameObject playerPar = GetOrCreate("[Players]",      null,            Vector3.zero);

            // ── GameManager Components ─────────────────────────────────────
            AddComponentIfMissing<GameManager>(gmGO);
            AddComponentIfMissing<GameBootstrap>(gmGO);
            var dbg = AddComponentIfMissing<DebugGameDisplay>(gmGO);
            if (dbg != null) dbg.ShowDebugOverlay = false;

            // ── Canvas Game Table UI ────────────────────────────────────────
            GameObject canvasGO = GetOrCreate("[Canvas_GameTable]", root.transform, Vector3.zero);
            var tableUI = AddComponentIfMissing<GameTableUI>(canvasGO);
            if (tableUI != null)
                tableUI.BuildUIIfMissing();

            EnsureEventSystemInputModule();

            // ── Camera ────────────────────────────────────────────────────
            SetupCamera(root);

            // ── Table areas ───────────────────────────────────────────────
            GameObject trickArea   = GetOrCreate("[TrickArea]",   tablePar.transform, PosCenter);
            GameObject bidArea     = GetOrCreate("[BiddingArea]", tablePar.transform, PosBid);
            GameObject scoreArea   = GetOrCreate("[ScoreArea]",   tablePar.transform, PosScore);

            // ── Player anchors ────────────────────────────────────────────
            GameObject pSouth = GetOrCreate("[Player_South]", playerPar.transform, PosSouth);
            GameObject pNorth = GetOrCreate("[Player_North]", playerPar.transform, PosNorth);
            GameObject pEast  = GetOrCreate("[Player_East]",  playerPar.transform, PosEast);
            GameObject pWest  = GetOrCreate("[Player_West]",  playerPar.transform, PosWest);

            AddSeatMarker(pSouth, PlayerSeat.South);
            AddSeatMarker(pNorth, PlayerSeat.North);
            AddSeatMarker(pEast,  PlayerSeat.East);
            AddSeatMarker(pWest,  PlayerSeat.West);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = gmGO;

            Debug.Log("[29 Setup] Full visual scene hierarchy created successfully!\n" +
                      "  • Press ▶ Play to start the game (auto-start is ON).\n" +
                      "  • High quality card images, table felt, trick slots, and HUD are ready.");

            EditorUtility.DisplayDialog(
                "Setup Complete ✔",
                "Full Visual 29 Card Game is ready!\n\n" +
                "• Casino felt table background\n" +
                "• Visual player cards with rank, suit, point values\n" +
                "• Interactive bidding modal & trick center\n" +
                "• Top scoreboard & trump indicator\n\n" +
                "Press ▶ PLAY to start playing!",
                "Let's Play!");
        }

        // ── Validation menu ──────────────────────────────────────────────────

        [MenuItem("29 Game/Setup Scene %#g", true)]
        private static bool ValidateSetupScene()
        {
            // Only available when a scene is open.
            return !string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().name);
        }

        [MenuItem("29 Game/Clear Scene Objects")]
        public static void ClearScene()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Clear 29 Game Objects",
                "Remove all 29 game root objects ([29Game Root], [GameTable], [Players]) from the scene?\n\nThis cannot be undone.",
                "Clear", "Cancel");

            if (!proceed) return;

            string[] rootNames = { "[29Game Root]", "[GameTable]", "[Players]" };
            foreach (string name in rootNames)
            {
                GameObject go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[29 Setup] Scene objects cleared.");
        }

        // ════════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════════

        private static GameObject GetOrCreate(string name, Transform parent, Vector3 localPos)
        {
            // Try to find in full hierarchy first
            GameObject existing = GameObject.Find(name);
            if (existing != null) return existing;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            if (parent != null)
                go.transform.SetParent(parent, false);

            go.transform.localPosition = localPos;
            return go;
        }

        private static T AddComponentIfMissing<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            if (existing != null) return existing;

            return Undo.AddComponent<T>(go);
        }



        private static void AddSeatMarker(GameObject go, PlayerSeat seat)
        {
            var marker = AddComponentIfMissing<SeatMarker>(go);
            if (marker != null) marker.Seat = seat;
        }

        private static void SetupCamera(GameObject root)
        {
            // Configure any existing Main Camera instead of creating a new one.
            Camera existing = Camera.main;
            if (existing != null)
            {
                existing.orthographic     = true;
                existing.orthographicSize = 6f;
                existing.clearFlags       = CameraClearFlags.SolidColor;
                existing.backgroundColor  = new Color(0.08f, 0.35f, 0.15f); // card-table green
                EditorUtility.SetDirty(existing);
                Debug.Log("[29 Setup] Existing Main Camera updated (green background, orthographic size 6).");
                return;
            }

            // No camera found — create one.
            GameObject camGO = GetOrCreate("[Camera]", root.transform, new Vector3(0f, 0f, -12f));
            Camera cam = AddComponentIfMissing<Camera>(camGO);
            cam.orthographic     = true;
            cam.orthographicSize = 6f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.08f, 0.35f, 0.15f);
            cam.tag              = "MainCamera";
        }

        private static void EnsureEventSystemInputModule()
        {
            var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
                es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            var legacy = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy);
            }

            var inputModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = Undo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(es.gameObject);
            }
            if (inputModule != null)
            {
                inputModule.AssignDefaultActions();
                EditorUtility.SetDirty(inputModule);
            }
        }
    }
}
