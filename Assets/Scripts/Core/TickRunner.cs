using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.Core
{
    public class TickRunner : MonoBehaviour
    {
        [Header("Tick Settings")]
        public int ticksPerSecond = 10;

        private float tickInterval;
        private float accumulator;

        private SimulationState simState;
        private InputBuffer inputBuffer;

        public void Initialize(SimulationState state, InputBuffer buffer)
        {
            simState = state;
            inputBuffer = buffer;
            tickInterval = 1f / ticksPerSecond;
            accumulator = 0f;
        }

        /// <summary>
        /// Returns normalized progress (0-1) between last tick and next tick.
        /// Used by View layer for interpolation.
        /// </summary>
        public float TickAlpha
        {
            get { return accumulator / tickInterval; }
        }

        private void Update()
        {
            if (simState == null) return;
            if (simState.gameOver) return;

            accumulator += Time.deltaTime;

            while (accumulator >= tickInterval)
            {
                // Process all buffered commands before simulating
                ProcessBufferedCommands();

                // Simulate one tick
                GameSimulation.SimulateTick(simState);

                // TEMP: verify hasher produces consistent results
                if (simState.tickCount % 50 == 0)
                {
                    int hash = SimulationStateHasher.ComputeHash(simState);
                    UnityEngine.Debug.Log("[HASH] Tick " + simState.tickCount + " Hash: " + hash);
                }

                accumulator -= tickInterval;
            }
        }

        private void ProcessBufferedCommands()
        {
            if (inputBuffer == null) return;

            GameCommand[] commands = inputBuffer.DrainCommands();
            for (int i = 0; i < commands.Length; i++)
            {
                CommandProcessor.ProcessCommand(simState, commands[i]);
            }
        }
    }
}