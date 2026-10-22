using UnityEditor;
using UnityEngine;

namespace NodeWar.Lobby
{
    public static class LobbyStagePreview
    {
        private const string Menu = "Tools/Node War/Lobby Stage/";
        private static LobbyStage preview;
        private static int[] districtTypes;
        private static readonly int[] SuitTypes = { 1, 2, 3 };
        private static int nextType;
        private static int shot;
        private static int selectedSlot;
        private static int pickedSlot;
        private static bool visible;

        [MenuItem(Menu + "Spawn Preview")]
        public static void Spawn()
        {
            if (!EditorApplication.isPlaying) return;
            if (preview != null)
            {
                preview.SetVisible(false);
                Object.Destroy(preview.gameObject);
            }
            preview = LobbyStage.Create();
            districtTypes = new[] { 1, 2, 3, 4 };
            nextType = 5;
            shot = (int)StageShot.Home;
            selectedSlot = 0;
            pickedSlot = 0;
            visible = true;
            preview.SetLoadout(districtTypes, SuitTypes);
            preview.DistrictUsed += ApplyDistrict;
            Selection.activeGameObject = preview.gameObject;
        }

        [MenuItem(Menu + "Spawn Preview", true)]
        private static bool CanSpawn() => EditorApplication.isPlaying;

        [MenuItem(Menu + "Cycle Shot")]
        public static void CycleShot()
        {
            shot = (shot + 1) % 5;
            preview.ShowShot((StageShot)shot);
            Debug.Log("Lobby stage shot: " + (StageShot)shot);
        }

        [MenuItem(Menu + "Select Next District")]
        public static void SelectNext()
        {
            preview.SelectDistrict(selectedSlot);
            selectedSlot = (selectedSlot + 1) % districtTypes.Length;
        }

        [MenuItem(Menu + "Deselect")]
        public static void Deselect() => preview.Deselect();

        [MenuItem(Menu + "Use District")]
        public static void Use()
        {
            bool replacing = preview.BeginReplace();
            Debug.Log(replacing ? "Pick a replacement slot or cancel." : "Filled the empty slot directly.");
        }

        [MenuItem(Menu + "Pick Next Replacement")]
        public static void Pick()
        {
            preview.PickReplace(pickedSlot);
            pickedSlot = (pickedSlot + 1) % StageSlots.Count;
        }

        [MenuItem(Menu + "Cancel Replacement")]
        public static void Cancel() => preview.CancelReplace();

        [MenuItem(Menu + "Focus Middle Suit")]
        public static void Focus() => preview.FocusSuit(1);

        [MenuItem(Menu + "Clear Suit Focus")]
        public static void ClearFocus() => preview.ClearFocus();

        [MenuItem(Menu + "Toggle Visible")]
        public static void ToggleVisible()
        {
            visible = !visible;
            preview.SetVisible(visible);
        }

        [MenuItem(Menu + "Cycle Shot", true)]
        [MenuItem(Menu + "Select Next District", true)]
        [MenuItem(Menu + "Deselect", true)]
        [MenuItem(Menu + "Use District", true)]
        [MenuItem(Menu + "Pick Next Replacement", true)]
        [MenuItem(Menu + "Cancel Replacement", true)]
        [MenuItem(Menu + "Focus Middle Suit", true)]
        [MenuItem(Menu + "Clear Suit Focus", true)]
        [MenuItem(Menu + "Toggle Visible", true)]
        private static bool HasPreview() => EditorApplication.isPlaying && preview != null;

        private static void ApplyDistrict(DistrictClusterResult result)
        {
            if (result.Action == DistrictClusterAction.FillEmpty)
            {
                var filled = new int[districtTypes.Length + 1];
                System.Array.Copy(districtTypes, filled, districtTypes.Length);
                districtTypes = filled;
            }
            districtTypes[result.Slot] = nextType++;
            preview.SetLoadout(districtTypes, SuitTypes);
        }
    }
}
