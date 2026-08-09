using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.Core
{
    public class TickRunner : MonoBehaviour, ITickProvider
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
                ProcessBufferedCommands();
                GameSimulation.SimulateTick(simState);

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