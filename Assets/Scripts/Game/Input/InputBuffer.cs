using System.Collections.Generic;

namespace NodeWar.Simulation
{
    public class InputBuffer
    {
        private List<GameCommand> pendingCommands;

        public InputBuffer()
        {
            pendingCommands = new List<GameCommand>();
        }

        public void EnqueueCommand(GameCommand command)
        {
            pendingCommands.Add(command);
        }

        /// <summary>
        /// Returns all pending commands and clears the buffer.
        /// Called once per tick by TickRunner.
        /// </summary>
        public GameCommand[] DrainCommands()
        {
            if (pendingCommands.Count == 0)
                return new GameCommand[0];

            GameCommand[] result = pendingCommands.ToArray();
            pendingCommands.Clear();
            return result;
        }
    }
}