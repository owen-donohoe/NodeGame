using UnityEngine;
using NodeWar.Simulation;
using Unity.VisualScripting.Antlr3.Runtime;

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
        private NodeWar.Input.BotPlayer botPlayer;

        private bool paused = true;

        public void Unpause()
        {
            paused = false;
        }

        public void Initialize(SimulationState state, InputBuffer buffer)
        {
            simState = state;
            inputBuffer = buffer;
            tickInterval = 1f / ticksPerSecond;
            accumulator = 0f;
        }

        public void SetBot(NodeWar.Input.BotPlayer bot)
        {
            botPlayer = bot;
        }

        public float TickAlpha
        {
            get { return accumulator / tickInterval; }
        }

        private void Update()
        {
            if (simState == null) return;
            if (paused) return;
            if (simState.gameOver) return;

            accumulator += Time.deltaTime;

            while (accumulator >= tickInterval)
            {
                if (botPlayer != null)
                    botPlayer.Evaluate();

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