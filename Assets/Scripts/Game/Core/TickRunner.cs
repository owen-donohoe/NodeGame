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
        private NodeWar.Input.BotPlayer botPlayer;

        private bool paused = true;

        // One log for the life of the runner, cleared before every tick.
        private readonly TickEventLog tickEvents = new TickEventLog();

        public event System.Action<TickEventLog> TickSimulated;

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

        /// <summary>
        /// Fraction of the current tick interval already elapsed, for View interpolation.
        /// Not clamped: Update drains whole ticks, so it reads in [0, 1) once Initialize has
        /// run. Before Initialize the interval is zero and the value is not meaningful.
        /// </summary>
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

                tickEvents.Clear();
                ProcessBufferedCommands();
                GameSimulation.SimulateTick(simState, tickEvents);

                if (simState.tickCount % 50 == 0)
                {
                    int hash = SimulationStateHasher.ComputeHash(simState);
                    UnityEngine.Debug.Log("[HASH] Tick " + simState.tickCount + " Hash: " + hash);
                }

                accumulator -= tickInterval;

                TickSimulated?.Invoke(tickEvents);
            }
        }

        private void ProcessBufferedCommands()
        {
            if (inputBuffer == null) return;

            GameCommand[] commands = inputBuffer.DrainCommands();
            for (int i = 0; i < commands.Length; i++)
            {
                CommandProcessor.ProcessCommand(simState, commands[i], tickEvents);
            }
        }
    }
}