using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Content for Forge node panel.
    /// Same production disks as Farm/Mine, plus the allocation wheel.
    /// </summary>
    public class ForgePanelContent : MonoBehaviour
    {
        [Header("Disk References")]
        [SerializeField] private Image diskFill0;
        [SerializeField] private TextMeshProUGUI diskCenterText0;
        [SerializeField] private GameObject diskRoot0;

        [SerializeField] private Image diskFill1;
        [SerializeField] private TextMeshProUGUI diskCenterText1;
        [SerializeField] private GameObject diskRoot1;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI workerCountLabel;
        [SerializeField] private GameObject notYoursLabel;
        [SerializeField] private GameObject allocationSection;

        [Header("Allocation Wheel")]
        [SerializeField] private AllocationWheel allocationWheel;

        private SimulationState simState;
        private NodeWar.Core.TickRunner tickRunner;
        private InputBuffer inputBuffer;
        private int nodeID;
        private int controlledPID;
        private bool isOwned;

        public void Initialize(SimulationState state, NodeWar.Core.TickRunner runner,
            InputBuffer buffer, int node, int pid, bool owned)
        {
            simState = state;
            tickRunner = runner;
            inputBuffer = buffer;
            nodeID = node;
            controlledPID = pid;
            isOwned = owned;

            if (!owned)
            {
                notYoursLabel.SetActive(true);
                workerCountLabel.gameObject.SetActive(false);
                allocationSection.SetActive(false);
            }
            else
            {
                notYoursLabel.SetActive(false);
                int currentAlloc = state.nodes[nodeID].materialAllocation;
                allocationWheel.Initialize(currentAlloc, state, buffer, pid, nodeID);
            }
        }

        private void Update()
        {
            if (simState == null) return;

            int workerCount = 0;
            int[] workerIDs = new int[2];
            int nodeOwner = simState.nodes[nodeID].ownerID;

            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;
                if (v.ownerID != nodeOwner) continue;

                if (workerCount < 2)
                    workerIDs[workerCount] = i;
                workerCount++;
            }

            UpdateDisk(0, diskFill0, diskCenterText0, diskRoot0, workerCount, workerIDs);
            UpdateDisk(1, diskFill1, diskCenterText1, diskRoot1, workerCount, workerIDs);

            if (isOwned)
            {
                workerCountLabel.text = "Workers: " + Mathf.Min(workerCount, 2) + " / 2";
            }

            if (workerCount >= 1) diskCenterText0.text = "+1";
            if (workerCount >= 2) diskCenterText1.text = "+1";
        }

        private void UpdateDisk(int index, Image fill, TextMeshProUGUI center,
            GameObject root, int workerCount, int[] workerIDs)
        {
            if (index >= workerCount) { root.SetActive(false); return; }
            root.SetActive(true);

            VillagerData v = simState.villagers[workerIDs[index]];
            if (v.productionTicksMax <= 0) { fill.fillAmount = 0f; return; }

            float rawFill = 1f - ((float)v.productionTicksRemaining / v.productionTicksMax);
            float subTickBonus = (1f / v.productionTicksMax) * tickRunner.TickAlpha;
            fill.fillAmount = Mathf.Clamp01(rawFill + subTickBonus);
        }
    }
}