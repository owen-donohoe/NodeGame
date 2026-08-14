using UnityEngine;
using NodeWar.Simulation;
using System.Collections.Generic;

namespace NodeWar.View
{
    /// <summary>
    /// Manages positional slots for villagers on a node.
    /// Finds child transforms named "WorkSlot_X", "IdleCenter", "ClaimCenter".
    /// If children aren't found, generates fallback positions based on node scale.
    /// </summary>
    public class NodeSlotManager : MonoBehaviour
    {
        [Header("Spacing")]
        [SerializeField] private float idleRadius = 1.5f;
        [SerializeField] private float claimRadius = 1.0f;
        [SerializeField] private float backupWorkSpacing = 1.0f;
        //[SerializeField] private float villagerY = 0.1f;
        private float villagerY = 0.0f;//this is set in the villager node 3 times. 

        private Transform[] workSlots;
        private Transform idleCenter;
        private Transform claimCenter;

        private int nodeID;
        private bool initialized = false;

        public void Initialize(int id, float nodeScale)
        {
            nodeID = id;
            initialized = true;

            // Scale spacing relative to node size
            //idleRadius *= nodeScale;
            //claimRadius *= nodeScale;
            //backupWorkSpacing *= nodeScale;

            // Find slot transforms anywhere in hierarchy (supports nested WORKPOINTS parent)
            List<Transform> slots = new List<Transform>();
            Transform[] allChildren = GetComponentsInChildren<Transform>();

            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i] == transform) continue; // skip self

                if (allChildren[i].name.StartsWith("WorkSlot"))
                    slots.Add(allChildren[i]);

                else if (allChildren[i].name == "IdleCenter")
                    idleCenter = allChildren[i];

                else if (allChildren[i].name == "ClaimCenter")
                    claimCenter = allChildren[i];
            }
            workSlots = slots.ToArray();

            // Fallback: create default positions if not found in prefab
            if (idleCenter == null)
            {
                Debug.LogWarning("No Idle Center, creating...");
                GameObject go = new GameObject("IdleCenter");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(nodeScale * 0.25f, 0f, 0f);
                idleCenter = go.transform;
            }

            if (claimCenter == null)
            {
                Debug.LogWarning("No Claim Center, creating...");

                GameObject go = new GameObject("ClaimCenter");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(0f, 0f, nodeScale * -0.25f);
                claimCenter = go.transform;
            }

            if (workSlots.Length == 0)
            {
                Debug.LogWarning("No Work Slots, creating...");

                // Create 2 default work slots on left side
                List<Transform> defaultSlots = new List<Transform>();
                for (int i = 0; i < 2; i++)
                {
                    GameObject go = new GameObject("WorkSlot_" + i);
                    go.transform.SetParent(transform);
                    go.transform.localPosition = new Vector3(
                        nodeScale * -0.25f,
                        0f,
                        (i - 0.5f) * backupWorkSpacing
                    );
                    defaultSlots.Add(go.transform);
                }
                workSlots = defaultSlots.ToArray();
            }
        }

        /// <summary>
        /// Returns world position for a villager in Working state.
        /// slotIndex: 0 or 1 (which work slot they occupy).
        /// </summary>
        public Vector3 GetWorkPosition(int slotIndex)
        {
            if (workSlots != null && slotIndex < workSlots.Length)
                return workSlots[slotIndex].position + new Vector3(0f, villagerY, 0f);
            return transform.position + new Vector3(0f, villagerY, 0f);
        }

        /// <summary>
        /// Returns world position for a villager in Idle state.
        /// localIndex: this villager's index among all idle villagers on this node.
        /// totalIdle: total number of idle villagers on this node.
        /// </summary>
        public Vector3 GetIdlePosition(int localIndex, int totalIdle)
        {
            Vector3 center = idleCenter.position;
            if (totalIdle <= 1)
                return center + new Vector3(0f, villagerY, 0f);

            float angle = (float)localIndex / (float)totalIdle * Mathf.PI * 2f;
            float radius = idleRadius * Mathf.Min(1f, totalIdle * 0.2f);
            return center + new Vector3(
                Mathf.Cos(angle) * radius,
                villagerY,
                Mathf.Sin(angle) * radius
            );
        }

        /// <summary>
        /// Returns world position for a villager in Claiming state.
        /// </summary>
        public Vector3 GetClaimPosition(int localIndex, int totalClaiming)
        {
            Vector3 center = claimCenter.position;
            if (totalClaiming <= 1)
                return center + new Vector3(0f, villagerY, 0f);

            float angle = (float)localIndex / (float)totalClaiming * Mathf.PI * 2f;
            return center + new Vector3(
                Mathf.Cos(angle) * claimRadius,
                villagerY,
                Mathf.Sin(angle) * claimRadius
            );
        }

        /// <summary>
        /// Returns world position for a villager in Fighting state.
        /// Spread around node center.
        /// </summary>
        public Vector3 GetFightPosition(int localIndex, int totalFighting)
        {
            Vector3 center = transform.position;
            if (totalFighting <= 1)
                return center + new Vector3(0f, villagerY, 0f);

            float angle = (float)localIndex / (float)totalFighting * Mathf.PI * 2f;
            float radius = idleRadius * 0.5f;
            return center + new Vector3(
                Mathf.Cos(angle) * radius,
                villagerY,
                Mathf.Sin(angle) * radius
            );
        }

        public int WorkSlotCount => workSlots != null ? workSlots.Length : 0;
        public int NodeID => nodeID;
    }
}